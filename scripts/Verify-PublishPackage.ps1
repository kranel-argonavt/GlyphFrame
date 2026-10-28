# Provisional packaging evidence, intentionally separate from xUnit execution.
[CmdletBinding()]
param(
    [ValidateRange(1, 30)][int]$StartupObservationSeconds = 3,
    [ValidatePattern('^[0-9a-f]{32}$')][string]$RunId = [Guid]::NewGuid().ToString('N')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This verifier requires PowerShell 7 on Windows.' }
. (Join-Path $PSScriptRoot 'PackageVerification.Session.ps1')
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = Join-Path $repositoryRoot "artifacts/package-verification/$runId"
$publishRoot = Join-Path $runRoot 'publish'
$dataRoot = $null
$pathPlan = $null
$launchCommand = $null
$outsideDirectory = Join-Path ([IO.Path]::GetTempPath()) "GlyphFrame.PackageVerification/$runId"
$checks = [Collections.Generic.List[string]]::new()
$application = $null
$failure = $null
$sessionObservation = $null
$sessionCleanup = $null

Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($runRoot))
if ([IO.Directory]::Exists($runRoot) -or [IO.File]::Exists($runRoot)) {
    throw 'Verification output already exists; refuse to reuse or overwrite a previous run.'
}

function Require-File([string]$RelativePath) {
    $file = Join-Path $publishRoot $RelativePath
    if (-not [IO.File]::Exists($file) -or (Get-Item -LiteralPath $file).Length -eq 0) {
        throw "Missing or empty package file: $RelativePath"
    }
    return $file
}

function Verify-Tool([string]$RelativePath) {
    $start = [Diagnostics.ProcessStartInfo]::new((Require-File $RelativePath))
    $start.WorkingDirectory = $outsideDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('-version')
    $tool = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $tool.StandardOutput.ReadToEndAsync()
        $stderr = $tool.StandardError.ReadToEndAsync()
        if (-not $tool.WaitForExit(15000)) {
            $tool.Kill($true)
            $tool.WaitForExit(5000) | Out-Null
            throw "Tool version check timed out: $RelativePath"
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        $expected = [IO.Path]::GetFileNameWithoutExtension($RelativePath)
        if ($tool.ExitCode -ne 0 -or $output -notmatch "(?m)^$expected version ") {
            throw "Tool version check failed: $RelativePath ($errorOutput)"
        }
        [IO.File]::WriteAllText((Join-Path $runRoot "$expected-version.txt"), $output)
        $checks.Add("$RelativePath executes from an outside-repository working directory")
    }
    finally { $tool.Dispose() }
}

try {
    . (Join-Path $PSScriptRoot 'LocalTools.Helpers.ps1')
    $toolPins = Get-LocalToolPins (Join-Path $repositoryRoot 'Tools/ffmpeg/SHA256SUMS.txt')
    if (-not (Test-LocalTools (Join-Path $repositoryRoot 'artifacts/local-runtime/controlled-9.0-glyphframe.2/Tools/ffmpeg') $toolPins)) {
        throw 'Controlled local tools are required for package verification. Run scripts/Initialize-LocalTools.ps1 -ArchivePath with the exact reviewed local archive, then retry.'
    }
    [IO.Directory]::CreateDirectory($publishRoot) | Out-Null
    $pathPlan = Get-PackageVerificationPathPlan ([IO.Path]::GetTempPath()) $runId
    $dataRoot = $pathPlan.DataRoot
    Assert-PackageVerificationPathBudget $pathPlan
    $checks.Add('Short isolated data root: prospective completed/pending paths and absolute file URIs satisfy harness budgets')
    if ([IO.Directory]::Exists($dataRoot) -or [IO.File]::Exists($dataRoot)) {
        throw 'This unique verification data root already exists; refuse to reuse it.'
    }
    [IO.Directory]::CreateDirectory($dataRoot) | Out-Null
    [IO.Directory]::CreateDirectory($outsideDirectory) | Out-Null
    if ($outsideDirectory.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The launch working directory must be outside the repository.'
    }
    [IO.File]::WriteAllText((Join-Path $dataRoot '.glyphframe-package-verification'), $runId)

    & dotnet publish (Join-Path $repositoryRoot 'src/GlyphFrame.App/GlyphFrame.App.csproj') `
        --configuration Release --runtime win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false --output $publishRoot
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    $checks.Add('Actual application project published as self-contained Windows x64 Release')

    Assert-PackageRequiredFiles $publishRoot
    if ((Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'licenses/manifest.json')).Hash -ne
        (Get-FileHash -LiteralPath (Require-File 'licenses/manifest.json')).Hash) {
        throw 'Published legal manifest differs from repository manifest.'
    }
    $legal = Assert-LegalPackage $publishRoot
    $controlledLegal = Assert-ControlledToolLegalBinding $publishRoot $toolPins
    $checks.Add("Controlled tool bytes/version/source-material identity and $($controlledLegal.DocumentsVerified) required notices verified; source hosting/public approval remain open")
    $checks.Add("Legal text hashes and exact dependency versions verified; $($legal.Status)")
    $runtime = Get-Content -LiteralPath (Join-Path $publishRoot 'GlyphFrame.App.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (-not $runtime.runtimeOptions.PSObject.Properties['includedFrameworks']) {
        throw 'Runtime configuration does not describe included self-contained frameworks.'
    }
    foreach ($framework in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        if ($framework -notin $runtime.runtimeOptions.includedFrameworks.name) {
            throw "Missing included runtime framework: $framework"
        }
    }
    $checks.Add('Application, self-contained CLR/WPF runtime, Skia native dependency, tools, licenses and notices present')

    $manifest = Join-Path $repositoryRoot 'Tools/ffmpeg/SHA256SUMS.txt'
    if ((Get-FileHash -LiteralPath $manifest).Hash -ne (Get-FileHash -LiteralPath (Require-File 'Tools/ffmpeg/SHA256SUMS.txt')).Hash) {
        throw 'Published checksum manifest differs from committed manifest.'
    }
    $verified = @{}
    foreach ($line in Get-Content -LiteralPath $manifest) {
        if ($line -match '^([0-9a-fA-F]{64})\s+([\w.-]+)$' -and $Matches[2] -in @('ffmpeg.exe', 'ffprobe.exe', 'LICENSE.txt', 'DISTRIBUTION_README.txt')) {
            $name = $Matches[2]
            $expectedHash = $Matches[1]
            if ((Get-FileHash -LiteralPath (Require-File "Tools/ffmpeg/$name") -Algorithm SHA256).Hash -ne $expectedHash) {
                throw "Committed checksum mismatch: $name"
            }
            $verified[$name] = $true
        }
    }
    if ($verified.Count -ne 4) { throw 'Manifest did not verify every required distributed FFmpeg file.' }
    if ((Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY_NOTICES.md')).Hash -ne
        (Get-FileHash -LiteralPath (Require-File 'THIRD_PARTY_NOTICES.md')).Hash) {
        throw 'Published third-party notices differ from repository notices.'
    }
    $checks.Add('FFmpeg, ffprobe, license and distribution hashes match committed manifest; notices match repository')

    $obsolete = @(Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Where-Object { $_.Name -match '(?i)opencv|opencvsharp' })
    if ($obsolete.Count -ne 0) { throw 'Obsolete OpenCV decoder assets found in package.' }
    $checks.Add('No OpenCV/OpenCvSharp decoder assets')
    Verify-Tool 'Tools/ffmpeg/ffmpeg.exe'
    Verify-Tool 'Tools/ffmpeg/ffprobe.exe'

    $start = [Diagnostics.ProcessStartInfo]::new((Require-File 'GlyphFrame.App.exe'))
    $start.WorkingDirectory = $outsideDirectory
    $start.UseShellExecute = $false
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.ArgumentList.Add('--verification-data-root')
    $start.ArgumentList.Add($dataRoot)
    $quotedExe = $start.FileName.Replace("'", "''")
    $quotedRoot = $dataRoot.Replace("'", "''")
    $quotedWorking = ([IO.Path]::GetTempPath()).Replace("'", "''")
    $launchCommand = "Start-Process -FilePath '$quotedExe' -ArgumentList @('--verification-data-root', ('`"' + '$quotedRoot' + '`"')) -WorkingDirectory '$quotedWorking'"
    $application = [Diagnostics.Process]::Start($start)
    if (-not $application.WaitForInputIdle(15000)) { throw 'Published app did not become input-idle.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $application.Refresh()
        if ($application.HasExited) { throw "Published app exited during startup ($($application.ExitCode))." }
        if ($application.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($application.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Published app did not create a main window.' }
    if ($application.WaitForExit($StartupObservationSeconds * 1000)) {
        throw "Published app exited during startup observation ($($application.ExitCode))."
    }
    if (-not $application.Responding) { throw 'Published app is not responding.' }
    $checks.Add('Published app created a responsive window and survived bounded startup observation outside repository')
    $sessionObservation = Wait-PackageActiveSession -DataRoot $dataRoot -ApplicationProcessId $application.Id `
        -IsApplicationRunning { -not $application.HasExited }
    $checks.Add('Isolation verified: production session observed under this run data root with an actively held exclusive lease')
    if (-not $application.CloseMainWindow() -or -not $application.WaitForExit(15000)) {
        throw 'Published app did not shut down gracefully within 15 seconds.'
    }
    if ($application.ExitCode -ne 0) { throw "Published app shutdown failed ($($application.ExitCode))." }
    $checks.Add('Only the verifier-owned process closed gracefully; exit code zero')
    $sessionCleanup = Get-PackageSessionCleanupEvidence $dataRoot $sessionObservation.SessionDirectory
    if (-not $sessionCleanup.Success) {
        throw 'Observed isolated session or other session directories remain after shutdown.'
    }
    $checks.Add('Cleanup verified: observed session removed and zero session directories remain under isolated cache root')
}
catch { $failure = $_.Exception.Message }
finally {
    if ($null -ne $application) {
        if (-not $application.HasExited) {
            $application.CloseMainWindow() | Out-Null
            if (-not $application.WaitForExit(5000)) {
                $application.Kill($true)
                $application.WaitForExit(5000) | Out-Null
            }
        }
        $application.Dispose()
    }
    # Delete only this generated, verified empty working directory; never recurse into unexpected content.
    $expectedTemporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.PackageVerification'))
    $resolvedOutside = [IO.Path]::GetFullPath($outsideDirectory)
    if ($null -ne $sessionCleanup -and $sessionCleanup.Success -and
        $resolvedOutside -eq (Join-Path $expectedTemporaryRoot $runId) -and [IO.Directory]::Exists($resolvedOutside)) {
        try { [IO.Directory]::Delete($resolvedOutside, $false) }
        catch [IO.IOException] {
            if ($null -eq $failure) { $failure = 'Temporary working directory could not be removed; see retained evidence.' }
        }
    }
    $report = [ordered]@{
        Success = $null -eq $failure
        Failure = $failure
        PublishDirectory = $publishRoot
        IsolatedDataDirectory = $dataRoot
        ArtifactPathBudget = $pathPlan
        InteractiveLaunchCommand = $launchCommand
        DataRetention = 'Marked temporary data retained for inspection/interactive retesting; verify session cleanup before deleting this run-owned root. Failure working directory also retained.'
        SessionObservation = $sessionObservation
        SessionCleanup = $sessionCleanup
        Checks = $checks.ToArray()
        InteractiveStartupSettingsRenderExport = 'NOT TESTED by this automated script'
        CleanWindowsWithoutInstalledDotnet = 'PENDING Phase 15'
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'report.json') -Encoding utf8
}
foreach ($check in $checks) { Write-Host "PASS: $check" }
Write-Host "Evidence/package: $runRoot"
Write-Host "Isolated data: $dataRoot"
if ($null -ne $launchCommand) { Write-Host "Interactive retest: $launchCommand" }
if ($null -ne $failure) { throw $failure }
