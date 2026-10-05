# Read-only package-verifier helpers. Call only with a run-owned or test-owned data root.
function Get-PackageArtifactPathEvidence([string]$SessionDirectory) {
    return @('mp4', 'pending.mp4') | ForEach-Object {
        $path = Join-Path $SessionDirectory "artifact-$('0' * 32).$_"
        $uri = ([Uri]::new($path, [UriKind]::Absolute)).AbsoluteUri
        [pscustomobject]@{ Path = $path; PathLength = $path.Length; FileUri = $uri; FileUriLength = $uri.Length }
    }
}

function Get-PackageVerificationPathPlan([string]$TemporaryBase, [string]$RunId) {
    if (-not [IO.Path]::IsPathFullyQualified($TemporaryBase) -or $RunId -cnotmatch '^[0-9a-f]{32}$') {
        throw 'Verification requires an absolute temporary base and a unique canonical run GUID.'
    }
    $data = [IO.Path]::GetFullPath((Join-Path $TemporaryBase "GFpkg/$RunId"))
    # Production naming, allowing the longest positive Int32 PID before the actual PID is known.
    $session = Join-Path (Get-PackageArtifactCacheRoot $data) "session-2147483647-$('0' * 32)"
    $paths = @(Get-PackageArtifactPathEvidence $session)
    # Harness headroom below the observed 266-character failing playback path, not a Windows/product limit.
    return [pscustomobject]@{
        DataRoot = $data
        DataRootLength = $data.Length
        DataRootFileUriLength = ([Uri]::new($data, [UriKind]::Absolute)).AbsoluteUri.Length
        MaximumPathLength = 220
        MaximumFileUriLength = 240
        ProspectiveArtifacts = @($paths)
        WithinBudget = @($paths | Where-Object { $_.PathLength -gt 220 -or $_.FileUriLength -gt 240 }).Count -eq 0
    }
}

function Assert-PackageVerificationPathBudget($Plan) {
    if (-not $Plan.WithinBudget) {
        $lengths = ($Plan.ProspectiveArtifacts | ForEach-Object { "path=$($_.PathLength), URI=$($_.FileUriLength)" }) -join '; '
        throw "Temporary verification base is unsuitable: $lengths exceed harness budgets of $($Plan.MaximumPathLength) path / $($Plan.MaximumFileUriLength) URI characters. Use a shorter temporary base; no nested artifacts-directory fallback is allowed."
    }
}

function Get-PackageArtifactCacheRoot([string]$DataRoot) {
    return [IO.Path]::GetFullPath((Join-Path $DataRoot 'GlyphFrame/Cache/VideoArtifacts'))
}

function Test-PackageLeaseSharingViolation([Exception]$Exception) {
    $cause = $Exception.GetBaseException()
    # HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION). No other I/O failure proves FileShare.None.
    return $cause -is [IO.IOException] -and $cause.HResult -eq -2147024864
}

function Test-PackageActiveLease([string]$LeasePath) {
    try {
        $attributes = [IO.File]::GetAttributes($LeasePath)
        if (($attributes -band ([IO.FileAttributes]::ReparsePoint -bor [IO.FileAttributes]::Directory)) -ne 0) {
            return $false
        }
    }
    catch [IO.IOException] { return $false }
    catch [UnauthorizedAccessException] { return $false }
    $probe = $null
    try {
        # Production holds ReadWrite/FileShare.None. Open, never OpenOrCreate: observation cannot create evidence.
        $probe = [IO.FileStream]::new($LeasePath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None, 1, [IO.FileOptions]::None)
        return $false
    }
    catch [IO.IOException] { return Test-PackageLeaseSharingViolation $_.Exception }
    catch [UnauthorizedAccessException] { return $false }
    finally { if ($null -ne $probe) { $probe.Dispose() } }
}

function Test-PackageCacheDirectoryChain([string]$DataRoot) {
    $directory = [IO.Path]::GetFullPath($DataRoot)
    foreach ($segment in @('', 'GlyphFrame', 'Cache', 'VideoArtifacts')) {
        if ($segment.Length -ne 0) { $directory = Join-Path $directory $segment }
        try {
            $attributes = [IO.File]::GetAttributes($directory)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                ($attributes -band [IO.FileAttributes]::Directory) -eq 0) { return $false }
        }
        catch [IO.IOException] { return $false }
        catch [UnauthorizedAccessException] { return $false }
    }
    return $true
}

function Get-PackageActiveSessionEvidence([string]$DataRoot, [int]$ApplicationProcessId) {
    if (-not (Test-PackageCacheDirectoryChain $DataRoot)) { return $null }
    $cache = Get-PackageArtifactCacheRoot $DataRoot
    $sessions = @([IO.Directory]::GetDirectories($cache))
    # A fresh run must contain exactly the one session initialized by its launched process.
    if ($sessions.Count -ne 1) { return $null }
    $session = $sessions[0]
    if ([IO.Path]::GetFileName($session) -cnotmatch "^session-$ApplicationProcessId-[0-9a-f]{32}$" -or
        ([IO.File]::GetAttributes($session) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $null }
    $lease = Join-Path $session '.session.lease'
    if (-not (Test-PackageActiveLease $lease)) { return $null }
    return [pscustomobject]@{
        CacheRoot = $cache
        SessionDirectory = $session
        LeasePath = $lease
        ApplicationProcessId = $ApplicationProcessId
        ObservedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        LeaseEvidence = 'ReadWrite/FileShare.None probe: ERROR_SHARING_VIOLATION (0x80070020)'
        ArtifactPathLengths = @(Get-PackageArtifactPathEvidence $session)
    }
}

function Wait-PackageActiveSession {
    param(
        [Parameter(Mandatory)][string]$DataRoot,
        [Parameter(Mandatory)][int]$ApplicationProcessId,
        [ValidateRange(0, 30000)][int]$TimeoutMilliseconds = 15000,
        [scriptblock]$IsApplicationRunning = { $true }
    )
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        if (-not (& $IsApplicationRunning)) { throw 'Published app exited before isolated session observation.' }
        $evidence = Get-PackageActiveSessionEvidence $DataRoot $ApplicationProcessId
        if ($null -ne $evidence) { return $evidence }
        $remaining = $TimeoutMilliseconds - $timer.ElapsedMilliseconds
        if ($remaining -le 0) { break }
        Start-Sleep -Milliseconds ([int][Math]::Min(100, $remaining))
    } while ($true)
    throw 'No actively leased production session was observed under the isolated data root within the timeout.'
}

function Get-PackageSessionCleanupEvidence([string]$DataRoot, [string]$ObservedSessionDirectory) {
    $cache = Get-PackageArtifactCacheRoot $DataRoot
    $observed = [IO.Path]::GetFullPath($ObservedSessionDirectory)
    if (-not [IO.Path]::GetDirectoryName($observed).Equals($cache, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($observed) -cnotmatch '^session-[0-9]+-[0-9a-f]{32}$') {
        throw 'Cleanup evidence must refer to the observed direct production session in this isolated cache.'
    }
    if (-not (Test-PackageCacheDirectoryChain $DataRoot)) {
        throw 'The observed isolated cache is missing, inaccessible, or unsafe; cleanup cannot be verified.'
    }
    # Enumerations throw on access errors rather than interpreting Exists(false) as successful deletion.
    $entries = @([IO.Directory]::GetFileSystemEntries($cache))
    $remaining = @([IO.Directory]::GetDirectories($cache))
    $removed = @($entries | Where-Object { $_.Equals($observed, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 0
    return [pscustomobject]@{
        SessionRemoved = [bool]$removed
        RemainingSessionDirectories = $remaining
        Success = [bool]$removed -and $remaining.Count -eq 0
    }
}
