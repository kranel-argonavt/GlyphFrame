# Inert archive acquisition helpers. Never execute downloaded tools or overwrite a cache.
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')

function Get-LocalToolPins([string]$ManifestPath) {
    $pins = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($line in Get-Content -LiteralPath $ManifestPath) {
        if ($line -match '^\s*(#|$)') { continue }
        if ($line -cnotmatch '^([0-9a-f]{64})  ([a-zA-Z0-9_.-]+)$' -or -not $pins.TryAdd($Matches[2], $Matches[1])) {
            throw 'Malformed or duplicate pinned tool checksum.'
        }
    }
    $names = @('GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip', 'ffmpeg.exe', 'ffprobe.exe', 'LICENSE.txt', 'DISTRIBUTION_README.txt')
    if ($pins.Count -ne $names.Count -or @($names | Where-Object { -not $pins.ContainsKey($_) }).Count) { throw 'Unexpected tool pin set; review version, provenance and acquisition together.' }
    return ,$pins
}

function Test-LocalTools([string]$Directory, $Pins) {
    Assert-PortableDirectoryChain $Directory
    foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
        $path = Join-Path $Directory $name
        if (-not [IO.File]::Exists($path)) { return $false }
        if (([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Pins[$name]) { throw "Local $name is linked or differs from its reviewed pin. No overwrite performed." }
    }
    return $true
}

function Install-LocalToolArchive([string]$Archive, [string]$Destination, [string]$RunRoot, $Pins) {
    Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Archive)))
    Assert-PortableDirectoryChain $Destination
    Assert-PortableDirectoryChain $RunRoot
    if (([IO.File]::GetAttributes($Archive) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Pins['GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip']) { throw 'Archive checksum mismatch; no extracted file will be accepted.' }
    if ([IO.Directory]::Exists($Destination) -or [IO.File]::Exists($Destination)) { throw 'Destination already exists; refusing partial-cache repair or overwrite.' }
    $stage = Join-Path $RunRoot 'staged-tools'
    if (Test-Path -LiteralPath $stage) { throw 'Staging must be fresh.' }
    [void][IO.Directory]::CreateDirectory($stage)
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        if ($zip.Entries.Count -ne 2) { throw 'The reviewed controlled archive contains exactly two tool files.' }
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $zip.Entries) {
            $relative = $entry.FullName.TrimEnd('/')
            Assert-PortableRelativePath $relative
            if ($entry.FullName -cnotin @('Tools/ffmpeg/ffmpeg.exe','Tools/ffmpeg/ffprobe.exe')) { throw 'Unexpected controlled tool archive entry.' }
            if (-not $seen.Add($relative) -or ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000)) { throw 'Conflicting or linked archive entry.' }
        }
        foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
            $entry = $zip.GetEntry("Tools/ffmpeg/$name")
            if ($null -eq $entry -or $entry.Length -le 0 -or $entry.Length -gt 268435456) { throw "Missing or oversized pinned $name archive entry." }
            $output = [IO.FileStream]::new((Join-Path $stage $name), [IO.FileMode]::CreateNew)
            $input = $entry.Open()
            try { $input.CopyTo($output) } finally { $input.Dispose(); $output.Dispose() }
        }
    }
    finally { $zip.Dispose() }
    if (-not (Test-LocalTools $stage $Pins)) { throw 'Extracted tool verification failed.' }
    # Move only this fresh, verified two-file directory, never a broad/computed user tree.
    $source = [IO.Path]::GetFullPath($stage)
    $target = [IO.Path]::GetFullPath($Destination)
    if ([IO.Path]::GetDirectoryName($source) -cne [IO.Path]::GetFullPath($RunRoot) -or
        [IO.Path]::GetFileName($source) -cne 'staged-tools' -or
        [IO.Path]::GetPathRoot($target) -ceq $target -or $source.Equals($target, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe tool move target.' }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    [IO.Directory]::Move($source, $target)
}
