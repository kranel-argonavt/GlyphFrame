# Deterministic synthetic packaging regressions. No publish, app launch, or external packages.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 on Windows is required.' }
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')
$parent = Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.PortablePackageTests'
$runId = [Guid]::NewGuid().ToString('N')
$testRoot = New-PortableRunDirectory $parent $runId
$passed = 0
function Assert-Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw "Assertion failed: $Message" } }
function Assert-Throws([scriptblock]$Action) {
    $thrown = $false
    try { & $Action | Out-Null } catch { $thrown = $true }
    Assert-Check $thrown 'Expected a failure, not successful validation'
}
function Run-Check([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:passed++
    Write-Host "PASS: $Name"
}
function Write-TestFile([string]$Root, [string]$Path, [string]$Content = 'synthetic payload') {
    $file = Join-Path $Root $Path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file)) | Out-Null
    [IO.File]::WriteAllText($file, $Content)
}
function New-CraftedArchive([string]$Name, [string[]]$Entries) {
    $path = Join-Path $testRoot "$Name.zip"
    $stream = [IO.FileStream]::new($path, [IO.FileMode]::CreateNew)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $Entries) {
            $content = $zip.CreateEntry($entry).Open()
            try { $bytes = [Text.Encoding]::UTF8.GetBytes('synthetic payload'); $content.Write($bytes) }
            finally { $content.Dispose() }
        }
    }
    finally { $zip.Dispose(); $stream.Dispose() }
    return $path
}
try {
    $required = Join-Path $testRoot 'required'
    foreach ($path in Get-RequiredPackagePaths) { Write-TestFile $required $path }
    Run-Check 'All required application/runtime/native/tool/attribution files are enforced' {
        Assert-PackageRequiredFiles $required
    }
    Run-Check 'Missing required file fails' {
        $file = Join-Path $required 'Tools/ffmpeg/ffprobe.exe'
        [IO.File]::Delete($file)
        Assert-Throws { Assert-PackageRequiredFiles $required }
        Write-TestFile $required 'Tools/ffmpeg/ffprobe.exe'
    }
    Run-Check 'Empty required file fails' {
        Write-TestFile $required 'libSkiaSharp.dll' ''
        Assert-Throws { Assert-PackageRequiredFiles $required }
        Write-TestFile $required 'libSkiaSharp.dll'
    }
    Run-Check 'Version uses the same assembly Major.Minor.Build as About' {
        Assert-Check ((Format-PortablePackageVersion ([Version]'1.2.3.4')) -ceq '1.2.3') 'no unrelated product version'
        Assert-Throws { Format-PortablePackageVersion ([Version]'1.2') }
        $assembly = [psobject].Assembly
        Assert-Check ((Get-PortablePackageVersion $assembly.Location) -ceq (Format-PortablePackageVersion $assembly.GetName().Version)) 'real assembly metadata read'
    }
    Run-Check 'Output is fresh, unique and marked; existing output cannot be reused' {
        $id = [Guid]::NewGuid().ToString('N')
        $root = New-PortableRunDirectory (Join-Path $testRoot 'runs') $id
        Assert-Check ([IO.File]::ReadAllText((Join-Path $root '.glyphframe-portable-package-run')) -ceq $id) 'positive ownership marker'
        Assert-Throws { New-PortableRunDirectory (Join-Path $testRoot 'runs') $id }
    }
    Run-Check 'Relative output and traversal/noncanonical run names are rejected' {
        Assert-Throws { New-PortableRunDirectory 'relative' $runId }
        Assert-Throws { New-PortableRunDirectory $testRoot '../outside' }
        Write-TestFile $testRoot 'not-a-directory'
        Assert-Throws { New-PortableRunDirectory (Join-Path $testRoot 'not-a-directory') $runId }
    }
    Run-Check 'Unsafe Windows/archive paths are rejected' {
        foreach ($path in @('/absolute', 'C:/absolute', '../outside', 'a/../b', 'a\b', 'a//b', 'a/./b', 'a:stream', 'a/NUL.txt', 'a/alias.', 'a/alias ')) {
            Assert-Throws { Assert-PortableRelativePath $path }
        }
    }
    Run-Check 'Development, media, user-data and verification evidence are forbidden' {
        foreach ($path in @('tests/a.dll', 'fixtures/input.mp4', '.git/config', '.vs/a', 'obj/a.dll', 'logs/a.json',
            'Cache/VideoArtifacts/a', 'settings.json', '.session.lease', '.glyphframe-package-verification',
            'smoke-report.md', 'photo.png', 'export.mp4', 'decoder.OpenCvSharp.dll', 'a.pdb', 'notes.log')) {
            Assert-Throws { Assert-PortableContentPath $path }
        }
    }
    Run-Check 'Symbol exclusion is explicit; runtime and attribution remain allowed' {
        Assert-PortableContentPath 'libSkiaSharp.pdb' -AllowSymbols
        Assert-PortableContentPath 'Tools/ffmpeg/LICENSE.txt'
        Assert-PortableContentPath 'THIRD_PARTY_NOTICES.md'
        Assert-PortableContentPath 'GlyphFrame.App.runtimeconfig.json'
    }
    $payloadRoot = Join-Path $testRoot 'payload'
    Write-TestFile $payloadRoot 'GlyphFrame.App.dll'
    Write-TestFile $payloadRoot 'Tools/ffmpeg/ffmpeg.exe'
    Write-TestFile $payloadRoot 'README.md'
    $payload = @(Get-PortablePayload $payloadRoot)
    $archive = Join-Path $testRoot 'valid.zip'
    Run-Check 'Real inventory rejects user data, unknown JSON and unsupported file types' {
        foreach ($path in @('settings.json', 'private-data.json', 'private-data.csv')) {
            Write-TestFile $payloadRoot $path
            Assert-Throws { Get-PortablePayload $payloadRoot }
            [IO.File]::Delete((Join-Path $payloadRoot $path))
        }
    }
    Run-Check 'Real ZIP has one application directory and matches every staged byte/hash' {
        New-PortableArchive $payloadRoot $archive 'GlyphFrame-1.0.0-win-x64' $payload
        $evidence = Test-PortableArchive $archive 'GlyphFrame-1.0.0-win-x64' $payload
        Assert-Check ($evidence.Success -and $evidence.FileCount -eq 3 -and $evidence.PayloadHashesVerified) 'complete archive validated'
    }
    Run-Check 'Archive creation refuses an existing output instead of truncating it' {
        $before = (Get-FileHash $archive).Hash
        Assert-Throws { New-PortableArchive $payloadRoot $archive 'GlyphFrame-1.0.0-win-x64' $payload }
        Assert-Check ((Get-FileHash $archive).Hash -ceq $before) 'existing archive unchanged'
    }
    Run-Check 'Changed staged bytes and missing archive payload are detected' {
        Write-TestFile $payloadRoot 'README.md' 'changed bytes'
        $changed = @(Get-PortablePayload $payloadRoot)
        Assert-Throws { Test-PortableArchive $archive 'GlyphFrame-1.0.0-win-x64' $changed }
        $missing = New-CraftedArchive 'missing' @('GlyphFrame-1.0.0-win-x64/GlyphFrame.App.dll')
        Assert-Throws { Test-PortableArchive $missing 'GlyphFrame-1.0.0-win-x64' $payload }
    }
    Run-Check 'Wrong top-level directory, absolute and traversal ZIP entries are rejected' {
        $index = 0
        foreach ($entry in @('Other/GlyphFrame.App.dll', '/GlyphFrame.App.dll', 'C:/GlyphFrame.App.dll', 'GlyphFrame-1.0.0-win-x64/../outside.dll')) {
            $bad = New-CraftedArchive "unsafe-$index" @($entry)
            Assert-Throws { Test-PortableArchive $bad 'GlyphFrame-1.0.0-win-x64' $payload }
            $index++
        }
    }
    Run-Check 'Duplicate and case-conflicting ZIP entries are rejected' {
        foreach ($second in @('GlyphFrame.App.dll', 'glyphframe.app.DLL')) {
            $bad = New-CraftedArchive ([Guid]::NewGuid().ToString('N')) @("GlyphFrame-1.0.0-win-x64/GlyphFrame.App.dll", "GlyphFrame-1.0.0-win-x64/$second")
            Assert-Throws { Test-PortableArchive $bad 'GlyphFrame-1.0.0-win-x64' $payload }
        }
    }
    Run-Check 'Unexpected and forbidden ZIP files cannot bypass staging policy' {
        foreach ($entry in @('settings.json', 'extra.dll', '.glyphframe-package-verification', 'logs/a.json')) {
            $bad = New-CraftedArchive ([Guid]::NewGuid().ToString('N')) @("GlyphFrame-1.0.0-win-x64/$entry")
            Assert-Throws { Test-PortableArchive $bad 'GlyphFrame-1.0.0-win-x64' $payload }
        }
    }
    Run-Check 'File/directory prefix conflicts are rejected even with matching hashes' {
        $file = $payload | Where-Object Path -CEQ 'GlyphFrame.App.dll'
        $conflicting = @($file, [pscustomobject]@{ Path = 'GlyphFrame.App.dll/other.dll'; Length = $file.Length; Sha256 = $file.Sha256 })
        $bad = New-CraftedArchive 'file-directory-conflict' @('GlyphFrame-1.0.0-win-x64/GlyphFrame.App.dll', 'GlyphFrame-1.0.0-win-x64/GlyphFrame.App.dll/other.dll')
        Assert-Throws { Test-PortableArchive $bad 'GlyphFrame-1.0.0-win-x64' $conflicting }
    }
    Run-Check 'Archived symlink metadata is rejected rather than treated as an ordinary file' {
        $linked = Join-Path $testRoot 'linked.zip'
        [IO.File]::Copy($archive, $linked, $false)
        $zip = [IO.Compression.ZipFile]::Open($linked, [IO.Compression.ZipArchiveMode]::Update)
        try { $zip.Entries[0].ExternalAttributes = [int](0xA1FF -shl 16) }
        finally { $zip.Dispose() }
        Assert-Throws { Test-PortableArchive $linked 'GlyphFrame-1.0.0-win-x64' $payload }
    }
    Run-Check 'Checksum is SHA-256 of the completed ZIP with its exact filename' {
        $checksum = $archive + '.sha256'
        $hash = Write-PortableChecksum $archive $checksum
        Assert-Check ($hash -ceq (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()) 'actual archive hash'
        Assert-Check ([IO.File]::ReadAllText($checksum) -ceq "$hash  valid.zip`n") 'stable checksum syntax'
        Assert-Throws { Write-PortableChecksum $archive $checksum }
    }
    Run-Check 'Failure and unfinished steps are never reported as package success' {
        $failed = New-PortablePackagingReport $false 'controlled verification failure' 'Archive verification' @('Publish passed')
        $roundtrip = $failed | ConvertTo-Json | ConvertFrom-Json
        Assert-Check (-not $roundtrip.Success -and $roundtrip.Failure -ceq 'controlled verification failure') 'truthful machine-readable failure'
        Assert-Check ($roundtrip.CompletedChecks.Count -eq 1 -and $roundtrip.FinalStep -ceq 'Archive verification') 'only completed steps retained'
        Assert-Check (-not (New-PortablePackagingReport $false '' 'Pending' @()).Success) 'unfinished is not success'
        Assert-Check (-not (New-PortablePackagingReport $true 'failure' 'Checksum' @()).Success) 'failure overrides completion'
        Assert-Check (New-PortablePackagingReport $true '' 'Completed' @('Verified')).Success 'verified completion succeeds'
    }
    Write-Host "Portable packaging helper checks passed: $passed; failed: 0. No publish or application launch performed."
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    Assert-PortableDirectoryChain $resolved
    if ([IO.Path]::GetDirectoryName($resolved) -cne [IO.Path]::GetFullPath($parent) -or
        [IO.Path]::GetFileName($resolved) -cne $runId -or
        [IO.File]::ReadAllText((Join-Path $resolved '.glyphframe-portable-package-run')) -cne $runId) {
        throw 'Refuse cleanup outside the marked test-owned root.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
