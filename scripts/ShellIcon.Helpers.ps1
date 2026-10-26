# Read-only ICO/PE validation; no shell/UI automation or icon-cache changes.
function Get-ShellIconFrames([string]$IconPath) {
    $bytes = [IO.File]::ReadAllBytes($IconPath)
    if ($bytes.Length -lt 6 -or [BitConverter]::ToUInt16($bytes, 0) -ne 0 -or [BitConverter]::ToUInt16($bytes, 2) -ne 1) { throw 'Invalid ICO header.' }
    $count = [BitConverter]::ToUInt16($bytes, 4)
    if ($count -ne 9 -or $bytes.Length -lt (6 + 16 * $count)) { throw 'Expected nine shell icon sizes.' }
    $expected = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    for ($i = 0; $i -lt $count; $i++) {
        $entry = 6 + 16 * $i
        $width = if ($bytes[$entry] -eq 0) { 256 } else { [int]$bytes[$entry] }
        $height = if ($bytes[$entry + 1] -eq 0) { 256 } else { [int]$bytes[$entry + 1] }
        $length = [BitConverter]::ToUInt32($bytes, $entry + 8)
        $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
        if ($width -ne $expected[$i] -or $height -ne $width -or [BitConverter]::ToUInt16($bytes, $entry + 6) -ne 32 -or
            $offset -lt (6 + 16 * $count) -or $length -lt 8 -or ([long]$offset + $length) -gt $bytes.Length) { throw 'Invalid shell icon frame.' }
        [pscustomobject]@{ Size=$width; Bytes=[byte[]]$bytes[$offset..($offset + $length - 1)] }
    }
}

function Assert-ExecutableShellIcon([string]$ExecutablePath, [string]$IconPath) {
    $stream = [IO.File]::OpenRead($ExecutablePath)
    $reader = $null
    try {
        $reader = [Reflection.PortableExecutable.PEReader]::new($stream)
        $directory = $reader.PEHeaders.PEHeader.ResourceTableDirectory
        if ($directory.Size -le 0) { throw 'Executable has no native resources.' }
        $section = $reader.GetSectionData($directory.RelativeVirtualAddress)
        $resources = [Text.Encoding]::Latin1.GetString([byte[]]$section.GetContent(0, $directory.Size))
        foreach ($frame in Get-ShellIconFrames $IconPath) {
            if (-not $resources.Contains([Text.Encoding]::Latin1.GetString($frame.Bytes), [StringComparison]::Ordinal)) {
                throw "Executable lacks the expected embedded $($frame.Size)-px shell icon: $ExecutablePath"
            }
        }
    }
    finally { if ($reader) { $reader.Dispose() }; $stream.Dispose() }
}
