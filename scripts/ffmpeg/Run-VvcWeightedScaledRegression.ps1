# Actual-library regression; no tool activation, downloads or application changes.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRoot,[Parameter(Mandatory)][string]$MsysRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../ControlledFfmpeg.Helpers.ps1')
Assert-PortableDirectoryChain $BuildRoot
if (-not [IO.File]::Exists((Join-Path $BuildRoot '.glyphframe-controlled-build-run'))) { throw 'Expected an owned controlled-build root.' }
$out=Join-Path $BuildRoot 'weighted-regression'
if (Test-Path -LiteralPath $out) { throw 'Refuse to overwrite regression evidence.' }
[void][IO.Directory]::CreateDirectory($out)
$source=Join-Path $PSScriptRoot 'tests/vvc-weighted-scaled.c'
$exe=Join-Path $out 'vvc-weighted-scaled.exe'
$null=Invoke-ControlledProcess (Join-Path $MsysRoot 'ucrt64/bin/gcc.exe') @('-std=c17','-O2','-Wall','-Wextra',
    "-I$BuildRoot/work/ffmpeg","-I$BuildRoot/sources/ffmpeg",'-o',$exe,$source,
    "$BuildRoot/prefix/lib/libavcodec.a","$BuildRoot/prefix/lib/libavutil.a","-L$BuildRoot/prefix/lib",
    '-lm','-lz','-lbcrypt','-luser32','-latomic','-static') $out (Join-Path $out 'compile') $MsysRoot
$result=Invoke-ControlledProcess $exe @() $out (Join-Path $out 'run')
if ($result -notmatch '^PASS 9072 weighted scaled cases:') { throw 'Regression did not report complete coverage.' }
[ordered]@{Passed=$true;Cases=9072;SourceSHA256=(Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant();
    ExecutableSHA256=(Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant();
    LibrarySHA256=(Get-FileHash -LiteralPath "$BuildRoot/prefix/lib/libavcodec.a").Hash.ToLowerInvariant();
    Scope='Baseline CPU dispatch, integer positions/unit spatial ratio; actual declared interface, independent formula, ordinary weighted callback and output guards. Not external-file/sanitizer or complete RPR coverage.'} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding utf8NoBOM
Write-Host $result
