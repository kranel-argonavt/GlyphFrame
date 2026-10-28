# Synthetic acquisition/export-policy checks. No network or downloaded-code execution.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTools.Helpers.ps1')
. (Join-Path $PSScriptRoot 'PortfolioSource.Helpers.ps1')
$parent = Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.SourcePreparationTests'
$id = [Guid]::NewGuid().ToString('N')
$root = New-PortableRunDirectory $parent $id
$passed = 0
function Assert-Check([bool]$Value,[string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Throws([scriptblock]$Action) { $caught=$false;try { & $Action | Out-Null } catch { $caught=$true };Assert-Check $caught 'Expected rejection.' }
function Run-Check([string]$Name,[scriptblock]$Action) { & $Action; $script:passed++; Write-Host "PASS: $Name" }
try {
    $pins = Get-LocalToolPins (Join-Path $PSScriptRoot '../Tools/ffmpeg/SHA256SUMS.txt')
    Run-Check 'Reviewed checksum set covers exact archive/tools/provenance' { Assert-Check ($pins.Count -eq 5) 'Wrong pin count.' }
    Run-Check 'Missing local pair is not available' { Assert-Check (-not (Test-LocalTools (Join-Path $root 'absent') $pins)) 'Missing pair accepted.' }
    $zipPath = Join-Path $root 'synthetic.zip'
    $zip = [IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in @('ffmpeg.exe','ffprobe.exe')) {
            $entry=$zip.CreateEntry("Tools/ffmpeg/$name")
            $stream=$entry.Open()
            try { $stream.Write([Text.Encoding]::UTF8.GetBytes("inert $name")) } finally { $stream.Dispose() }
            $bytes=[Text.Encoding]::UTF8.GetBytes("inert $name")
            $pins[$name]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        }
    } finally { $zip.Dispose() }
    $destination=Join-Path $root 'accepted-tools'
    $run=New-PortableRunDirectory $root ([Guid]::NewGuid().ToString('N'))
    Run-Check 'Wrong archive hash rejected before installation' { Assert-Throws {Install-LocalToolArchive $zipPath $destination $run $pins};Assert-Check (-not(Test-Path -LiteralPath $destination)) 'Destination created on bad input.' }
    $pins['GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip']=(Get-FileHash -LiteralPath $zipPath).Hash.ToLowerInvariant()
    Run-Check 'Safe archive pair extracted, hashed and atomically accepted without execution' {Install-LocalToolArchive $zipPath $destination $run $pins;Assert-Check (Test-LocalTools $destination $pins) 'Pair not verified.'}
    Run-Check 'Existing destination cannot be overwritten' {Assert-Throws {Install-LocalToolArchive $zipPath $destination $run $pins}}
    Run-Check 'Altered local tool is rejected rather than silently replaced' {[IO.File]::AppendAllText((Join-Path $destination 'ffmpeg.exe'),'changed');Assert-Throws {Test-LocalTools $destination $pins}}
    Run-Check 'Source policy accepts original code, tests, licenses and own assets' {
        foreach($path in @('LICENSE','README.md','src/GlyphFrame.App/App.xaml','scripts/Initialize-LocalTools.ps1','installer/GlyphFrame.iss','licenses/manifest.json','docs/screenshots/main.png','tests/GlyphFrame.Tests/Fixtures/sample.mp4','Tools/ffmpeg/SHA256SUMS.txt')) {Assert-PortfolioSourcePath $path}
    }
    Run-Check 'Source policy rejects executables, archives, Git history and private/generated paths' {
        foreach($path in @('Tools/ffmpeg/ffmpeg.exe','src/other.dll','src/bin/a.cs','artifacts/report.md','.git/config','secrets.txt','docs/screenshot.zip','docs/unreviewed.mp4','licenses/package.nupkg','../outside.cs')) {Assert-Throws {Assert-PortfolioSourcePath $path}}
    }
    Run-Check 'Controlled-build shell recipes are accepted only in their reviewed scripts scope' {
        Assert-PortfolioSourcePath 'scripts/ffmpeg/build.sh'
        foreach($path in @('scripts/other.sh','src/build.sh','scripts/ffmpeg/nested/build.sh','scripts/ffmpeg/build.exe.sh','scripts/ffmpeg/Build.sh')) {Assert-Throws {Assert-PortfolioSourcePath $path}}
    }
    Run-Check 'Downstream patch and C regression source accepted only in explicit scopes' {
        Assert-PortfolioSourcePath 'scripts/ffmpeg/patches/0001-vvc-weighted-scaled-contract.patch'
        Assert-PortfolioSourcePath 'scripts/ffmpeg/tests/vvc-weighted-scaled.c'
        foreach($path in @('scripts/other.patch','scripts/ffmpeg/a.c','src/unreviewed.c','scripts/ffmpeg/tests/other.c',
            'scripts/ffmpeg/patches/0001-test.exe.patch','scripts/ffmpeg/patches/nested/0001-test.patch')) {Assert-Throws {Assert-PortfolioSourcePath $path}}
    }
    Run-Check 'Checksum manifest duplicates are rejected' {
        $path=Join-Path $root 'bad-pins.txt'
        [IO.File]::WriteAllText($path, ('a'*64)+'  ffmpeg.exe'+"`n"+('a'*64)+'  ffmpeg.exe')
        Assert-Throws {Get-LocalToolPins $path}
    }
    foreach ($case in @('traversal','duplicate','missing-pair','wrong-tool','vendor-layout','extra-file','linked-tool')) {
        Run-Check "Pinned but unsafe/incomplete synthetic archive rejected: $case" {
            $archive=Join-Path $root "$case.zip"
            $badZip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
            try {
                $names=switch($case) {
                    'traversal' {@('../outside.exe','Tools/ffmpeg/ffmpeg.exe','Tools/ffmpeg/ffprobe.exe')}
                    'duplicate' {@('Tools/ffmpeg/ffmpeg.exe','Tools/ffmpeg/FFMPEG.exe')}
                    'missing-pair' {@('Tools/ffmpeg/ffmpeg.exe')}
                    'wrong-tool' {@('Tools/ffmpeg/ffmpeg.exe','Tools/ffmpeg/ffprobe.exe')}
                    'vendor-layout' {@('ffmpeg-9.0-essentials_build/bin/ffmpeg.exe','ffmpeg-9.0-essentials_build/bin/ffprobe.exe')}
                    'extra-file' {@('Tools/ffmpeg/ffmpeg.exe','Tools/ffmpeg/ffprobe.exe','other.txt')}
                    'linked-tool' {@('Tools/ffmpeg/ffmpeg.exe','Tools/ffmpeg/ffprobe.exe')}
                }
                foreach($name in $names) {
                    $entry=$badZip.CreateEntry($name)
                    if($case -eq 'linked-tool') {$entry.ExternalAttributes=-1610612736}
                    $stream=$entry.Open()
                    try {$stream.Write([Text.Encoding]::UTF8.GetBytes('different inert data'))} finally {$stream.Dispose()}
                }
            } finally {$badZip.Dispose()}
            $pins['GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip']=(Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()
            $caseRun=New-PortableRunDirectory $root ([Guid]::NewGuid().ToString('N'))
            $caseDestination=Join-Path $root "rejected-$case"
            Assert-Throws {Install-LocalToolArchive $archive $caseDestination $caseRun $pins}
            Assert-Check (-not(Test-Path -LiteralPath $caseDestination)) 'Rejected archive reached destination.'
            Assert-Check (-not(Test-Path -LiteralPath (Join-Path $caseRun 'outside.exe'))) 'Traversal escaped staging.'
        }
    }
    Write-Host "Source preparation helper checks passed: $passed; failed: 0. No network, execution or Git writes."
}
finally {
    $resolved=[IO.Path]::GetFullPath($root)
    Assert-PortableDirectoryChain $resolved
    if([IO.Path]::GetDirectoryName($resolved) -cne [IO.Path]::GetFullPath($parent) -or [IO.Path]::GetFileName($resolved) -cne $id -or [IO.File]::ReadAllText((Join-Path $resolved '.glyphframe-portable-package-run')) -cne $id){throw 'Unsafe test cleanup target.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
