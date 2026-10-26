# Shared structural checks. No publishing, application launch, extraction or recursive deletion.
. (Join-Path $PSScriptRoot 'PackageLegal.Helpers.ps1')
function Get-RequiredPackagePaths {
    return @(
        'GlyphFrame.App.exe', 'GlyphFrame.App.dll', 'GlyphFrame.App.deps.json', 'GlyphFrame.App.runtimeconfig.json',
        'GlyphFrame.Core.dll', 'GlyphFrame.Infrastructure.dll', 'GlyphFrame.Rendering.dll',
        'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll',
        'PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll',
        'PresentationNative_cor3.dll', 'wpfgfx_cor3.dll', 'vcruntime140_cor3.dll',
        'D3DCompiler_47_cor3.dll', 'PenImc_cor3.dll', 'libSkiaSharp.dll', 'SkiaSharp.dll',
        'LICENSE', 'licenses/manifest.json', 'THIRD_PARTY_NOTICES.md', 'Tools/ffmpeg/ffmpeg.exe', 'Tools/ffmpeg/ffprobe.exe',
        'Tools/ffmpeg/SHA256SUMS.txt', 'Tools/ffmpeg/LICENSE.txt', 'Tools/ffmpeg/DISTRIBUTION_README.txt'
    )
}

function Assert-PackageRequiredFiles([string]$Root) {
    foreach ($relative in Get-RequiredPackagePaths) {
        $file = Join-Path $Root $relative
        if (-not [IO.File]::Exists($file) -or [IO.FileInfo]::new($file).Length -eq 0) {
            throw "Missing or empty package file: $relative"
        }
    }
}

function Assert-PortableDirectoryChain([string]$Directory) {
    if (-not [IO.Path]::IsPathFullyQualified($Directory)) { throw 'An absolute directory is required.' }
    $current = [IO.Path]::GetFullPath($Directory)
    while ($current) {
        if ([IO.File]::Exists($current)) { throw "Directory conflicts with a file: $current" }
        if ([IO.Directory]::Exists($current) -and
            ([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Reparse-point directory is not allowed: $current"
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function New-PortableRunDirectory([string]$Parent, [string]$RunId) {
    Assert-PortableDirectoryChain $Parent
    if ($RunId -cnotmatch '^[0-9a-f]{32}$') { throw 'A canonical run GUID is required.' }
    $root = [IO.Path]::GetFullPath((Join-Path $Parent $RunId))
    if ([IO.Directory]::Exists($root) -or [IO.File]::Exists($root)) {
        throw 'Run output already exists; refuse to reuse or overwrite it.'
    }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $marker = [IO.FileStream]::new((Join-Path $root '.glyphframe-portable-package-run'),
        [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($RunId)
        $marker.Write($bytes)
    }
    finally { $marker.Dispose() }
    return $root
}

function Format-PortablePackageVersion([Version]$Version) {
    # Same assembly Major.Minor.Build contract as AboutDialogService.FormatVersion.
    if ($null -eq $Version -or $Version.Build -lt 0) { throw 'Application assembly version is unavailable.' }
    return "$($Version.Major).$($Version.Minor).$($Version.Build)"
}

function Get-PortablePackageVersion([string]$AssemblyPath) {
    return Format-PortablePackageVersion ([Reflection.AssemblyName]::GetAssemblyName($AssemblyPath).Version)
}

function Assert-PortableRelativePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.Contains('\') -or $Path.Contains(':') -or
        $Path.StartsWith('/') -or $Path.EndsWith('/')) { throw "Unsafe archive/payload path: $Path" }
    foreach ($segment in $Path.Split('/')) {
        if ($segment -in @('', '.', '..') -or $segment -match '[<>"|?*\x00-\x1f]' -or
            $segment.EndsWith('.') -or $segment.EndsWith(' ') -or
            $segment -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') {
            throw "Unsafe archive/payload path: $Path"
        }
    }
}

function Assert-PortableContentPath([string]$Path, [switch]$AllowSymbols) {
    Assert-PortableRelativePath $Path
    foreach ($segment in $Path.Split('/')) {
        if ($segment -match '^(?i:tests?|fixtures?|bin|obj|artifacts|logs|cache|temp|tmp|\.git|\.vs|\.idea|\.vscode)$' -or
            $segment -match '^(?i:smoke-|\.glyphframe-|session-)') {
            throw "Development or application-data content is forbidden: $Path"
        }
    }
    $name = $Path.Split('/')[-1]
    if ($name -match '(?i)opencv|opencvsharp' -or $name -match '^(?i:settings)(?:[.-]|$)' -or
        $name -eq '.session.lease' -or $name -match '\.(?i:cs|xaml|csproj|sln|suo|user|log|trx|tmp|partial|part|zip|png|jpe?g|bmp|webp|mp4|mov|mkv|avi)$' -or
        (-not $AllowSymbols -and $name -match '\.(?i:pdb)$')) {
        throw "Development or application-data file is forbidden: $Path"
    }
    # Explicit file-type policy is applied only to files by Get-PortablePayload.
}

function Assert-PortableFilePath([string]$Path, [switch]$AllowSymbols) {
    Assert-PortableContentPath $Path -AllowSymbols:$AllowSymbols
    if ($Path -cne 'LICENSE' -and $Path -notmatch '\.(?i:dll|exe|json|txt|dat|config|md)$' -and
        -not ($AllowSymbols -and $Path -match '\.(?i:pdb)$')) { throw "Unexpected distributed file type: $Path" }
    if ($Path -match '\.(?i:json)$' -and $Path -cnotin @('GlyphFrame.App.deps.json', 'GlyphFrame.App.runtimeconfig.json', 'licenses/manifest.json')) {
        throw "Unexpected JSON (potential user or verification data): $Path"
    }
}

function Get-PortablePayload([string]$Root, [switch]$AllowSymbols) {
    Assert-PortableDirectoryChain $Root
    if (-not [IO.Directory]::Exists($Root)) { throw 'Payload directory is missing.' }
    $rootPath = [IO.Path]::GetFullPath($Root)
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($rootPath)
    $files = [Collections.Generic.List[object]]::new()
    while ($pending.Count -gt 0) {
        foreach ($path in [IO.Directory]::GetFileSystemEntries($pending.Pop())) {
            $relative = [IO.Path]::GetRelativePath($rootPath, $path).Replace('\', '/')
            $attributes = [IO.File]::GetAttributes($path)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Linked payload is forbidden: $relative" }
            Assert-PortableContentPath $relative -AllowSymbols:$AllowSymbols
            if (($attributes -band [IO.FileAttributes]::Directory) -ne 0) { $pending.Push($path); continue }
            Assert-PortableFilePath $relative -AllowSymbols:$AllowSymbols
            $files.Add([pscustomobject]@{
                Path = $relative
                Length = [IO.FileInfo]::new($path).Length
                Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            })
        }
    }
    if ($files.Count -eq 0) { throw 'Payload must not be empty.' }
    return $files.ToArray() | Sort-Object Path -CaseSensitive
}

function New-PortableArchive([string]$PayloadRoot, [string]$ArchivePath, [string]$ApplicationDirectory, [object[]]$Payload) {
    Assert-PortableRelativePath $ApplicationDirectory
    if ($ApplicationDirectory.Contains('/')) { throw 'Exactly one application directory is required.' }
    Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ArchivePath)))
    $stream = [IO.FileStream]::new($ArchivePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $zip = $null
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        foreach ($file in $Payload) {
            Assert-PortableFilePath $file.Path
            $entry = $zip.CreateEntry("$ApplicationDirectory/$($file.Path)", [IO.Compression.CompressionLevel]::Optimal)
            $source = [IO.FileStream]::new((Join-Path $PayloadRoot $file.Path), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            $target = $entry.Open()
            try { $source.CopyTo($target, 131072) }
            finally { $target.Dispose(); $source.Dispose() }
        }
    }
    finally { if ($null -ne $zip) { $zip.Dispose() }; $stream.Dispose() }
}

function Test-PortableArchive([string]$ArchivePath, [string]$ApplicationDirectory, [object[]]$Payload) {
    Assert-PortableRelativePath $ApplicationDirectory
    if ($ApplicationDirectory.Contains('/')) { throw 'Exactly one application directory is required.' }
    $expected = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Payload) {
        Assert-PortableFilePath $file.Path
        if (-not $expected.TryAdd("$ApplicationDirectory/$($file.Path)", $file)) { throw 'Conflicting payload paths.' }
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $zip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        foreach ($entry in $zip.Entries) {
            Assert-PortableRelativePath $entry.FullName
            if (-not $entry.FullName.StartsWith($ApplicationDirectory + '/', [StringComparison]::Ordinal)) {
                throw "Unexpected top-level archive path: $($entry.FullName)"
            }
            Assert-PortableFilePath $entry.FullName.Substring($ApplicationDirectory.Length + 1)
            if (-not $seen.Add($entry.FullName)) { throw "Duplicate/conflicting archive path: $($entry.FullName)" }
            if (-not $expected.ContainsKey($entry.FullName)) { throw "Unexpected archive file: $($entry.FullName)" }
            $file = $expected[$entry.FullName]
            if ($entry.FullName -cne "$ApplicationDirectory/$($file.Path)" -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or
                ($entry.ExternalAttributes -band [int][IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Noncanonical or linked archive entry: $($entry.FullName)"
            }
            $content = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($content)).ToLowerInvariant() }
            finally { $content.Dispose() }
            if ($entry.Length -ne $file.Length -or $hash -cne $file.Sha256) { throw "Archive payload mismatch: $($entry.FullName)" }
        }
        if ($seen.Count -ne $expected.Count) { throw 'Archive is missing payload files.' }
        foreach ($path in $seen) {
            $parent = $path
            while ($parent.Contains('/')) {
                $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
                if ($seen.Contains($parent)) { throw "File/directory archive conflict: $parent" }
            }
        }
        return [pscustomobject]@{ Success = $true; FileCount = $seen.Count; PayloadHashesVerified = $true; TopLevelDirectory = $ApplicationDirectory }
    }
    finally { $zip.Dispose() }
}

function Write-PortableChecksum([string]$ArchivePath, [string]$ChecksumPath) {
    $hash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $stream = [IO.FileStream]::new($ChecksumPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes("$hash  $([IO.Path]::GetFileName($ArchivePath))`n")
        $stream.Write($bytes)
    }
    finally { $stream.Dispose() }
    return $hash
}

function New-PortablePackagingReport([bool]$Completed, [string]$Failure, [string]$Step, [string[]]$Checks) {
    return [ordered]@{
        Success = $Completed -and [string]::IsNullOrEmpty($Failure)
        Failure = if ([string]::IsNullOrEmpty($Failure)) { $null } else { $Failure }
        FinalStep = $Step
        CompletedChecks = @($Checks)
        ExtractedZipInteractiveValidation = 'NOT TESTED — Phase 15B'
        CleanWindowsWithoutInstalledDotnetOrFfmpeg = 'NOT TESTED — Phase 15C'
        PublicRelease = 'NOT CREATED — Phase 16; licensing review remains required'
    }
}
