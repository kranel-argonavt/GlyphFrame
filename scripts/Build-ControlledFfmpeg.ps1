# Experimental offline rebuild. Never switches GlyphFrame's active tool cache or pins.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$MsysRoot,
    [Parameter(Mandatory)][string]$BuildRoot,
    [ValidateRange(1,16)][int]$Jobs=8,
    [switch]$RunFate
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ControlledFfmpeg.Helpers.ps1')
$sourceLockPath=Join-Path $PSScriptRoot 'ffmpeg/sources.lock.json'
$toolchainLockPath=Join-Path $PSScriptRoot 'ffmpeg/toolchain.lock.json'
$runtimeLockPath=Join-Path $PSScriptRoot 'ffmpeg/runtime.lock.json'
$recipePath=Join-Path $PSScriptRoot 'ffmpeg/build.sh'
$sourceLock=Get-ControlledSourceLock $sourceLockPath
$toolchainLock=Get-Content -LiteralPath $toolchainLockPath -Raw | ConvertFrom-Json
$runtimeLock=Get-ControlledRuntimeLock $runtimeLockPath $toolchainLock
$SourceDirectory=[IO.Path]::GetFullPath($SourceDirectory)
$MsysRoot=[IO.Path]::GetFullPath($MsysRoot)
$BuildRoot=Assert-ControlledBuildDestination $BuildRoot @($SourceDirectory,$MsysRoot)
Assert-ControlledInputFiles $SourceDirectory $sourceLock
Assert-ControlledPatchFiles (Join-Path $PSScriptRoot 'ffmpeg') $sourceLock
Assert-ControlledToolchainFiles $MsysRoot $toolchainLock
Assert-ControlledRuntimeFiles (Join-Path $PSScriptRoot 'ffmpeg') $runtimeLock $MsysRoot
if ([IO.File]::ReadAllText($recipePath).Contains("`r")) { throw 'Build recipe must be checked out with LF line endings.' }
[void][IO.Directory]::CreateDirectory($BuildRoot)
[IO.File]::WriteAllText((Join-Path $BuildRoot '.glyphframe-controlled-build-run'),[Guid]::NewGuid().ToString('N'))
$buildScripts=@(foreach($relative in @('Build-ControlledFfmpeg.ps1','ControlledFfmpeg.Helpers.ps1','PortablePackage.Helpers.ps1','PackageLegal.Helpers.ps1',
    'ffmpeg/Run-VvcWeightedScaledRegression.ps1','ffmpeg/tests/vvc-weighted-scaled.c')) {
    @{File=$relative;SHA256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $relative)).Hash.ToLowerInvariant()}
})
$report=[ordered]@{SchemaVersion=1;Status='Started';Completed=$false;Profile=$sourceLock.Profile;ToolVersion=$sourceLock.ToolVersion;
    PublicDistributionApproved=$false;WarningReviewComplete=$false;CorrespondingSourceApproved=$false;Activated=$false;
    FateRequested=[bool]$RunFate;BuildRoot=$BuildRoot;MsysRoot=$MsysRoot;Jobs=$Jobs;
    SourceLockSHA256=(Get-FileHash -LiteralPath $sourceLockPath).Hash.ToLowerInvariant();
    ToolchainLockSHA256=(Get-FileHash -LiteralPath $toolchainLockPath).Hash.ToLowerInvariant();
    RuntimeLockSHA256=(Get-FileHash -LiteralPath $runtimeLockPath).Hash.ToLowerInvariant();
    RecipeSHA256=(Get-FileHash -LiteralPath $recipePath).Hash.ToLowerInvariant();BuildScripts=$buildScripts;Patches=$sourceLock.Patches;Failure=$null;Outputs=@();Warnings=@()}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $BuildRoot 'report.json') -Encoding utf8NoBOM
try {
    $inputs=Join-Path $BuildRoot 'inputs'; [void][IO.Directory]::CreateDirectory($inputs)
    foreach ($entry in $sourceLock.Artifacts) { Copy-Item -LiteralPath (Join-Path $SourceDirectory $entry.File) -Destination (Join-Path $inputs $entry.File) }
    Assert-ControlledInputFiles $inputs $sourceLock
    foreach ($patch in $sourceLock.Patches) {
        $target=Join-Path $BuildRoot $patch.File
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ffmpeg/$($patch.File)") -Destination $target
    }
    Assert-ControlledPatchFiles $BuildRoot $sourceLock
    foreach ($entry in $sourceLock.Artifacts | Where-Object Name -ne 'x264-history') {
        $entries=@(& "$env:SystemRoot/System32/tar.exe" -tf (Join-Path $inputs $entry.File))
        if ($LASTEXITCODE -ne 0) { throw 'Pinned source archive inspection failed.' }
        Assert-ControlledArchiveEntries $entries
        $types=@(& "$env:SystemRoot/System32/tar.exe" -tvf (Join-Path $inputs $entry.File))
        if ($LASTEXITCODE -ne 0) { throw 'Source archive type inspection failed.' }
        Assert-ControlledArchiveTypes $types
        $target=Join-Path $BuildRoot "sources/$($entry.Name)"; [void][IO.Directory]::CreateDirectory($target)
        & "$env:SystemRoot/System32/tar.exe" -xf (Join-Path $inputs $entry.File) --strip-components=1 -C $target
        if ($LASTEXITCODE -ne 0) { throw 'Pinned source extraction failed.' }
    }
    Invoke-ControlledSourcePatches (Join-Path $BuildRoot 'sources/ffmpeg') $BuildRoot $sourceLock $MsysRoot $BuildRoot
    $packages=Invoke-ControlledProcess (Join-Path $MsysRoot 'usr/bin/bash.exe') @('--noprofile','--norc','-c','pacman -Q') $BuildRoot (Join-Path $BuildRoot 'package-inventory') $MsysRoot
    Assert-ControlledPackageInventory ($packages -split '\r?\n') $toolchainLock
    $recipe=Join-Path $BuildRoot 'build.sh'; Copy-Item -LiteralPath $recipePath -Destination $recipe
    $fate=if ($RunFate) {'1'} else {'0'}
    $x264Revision=($sourceLock.Artifacts | Where-Object Name -eq 'x264').Revision
    Invoke-ControlledProcess (Join-Path $MsysRoot 'usr/bin/bash.exe') @('--noprofile','--norc',$recipe,$BuildRoot,$Jobs.ToString(),$sourceLock.SourceDateEpoch.ToString(),$fate,$x264Revision) $BuildRoot (Join-Path $BuildRoot 'build') $MsysRoot | Write-Host
    & (Join-Path $PSScriptRoot 'ffmpeg/Run-VvcWeightedScaledRegression.ps1') -BuildRoot $BuildRoot -MsysRoot $MsysRoot
    foreach ($tool in @('ffmpeg','ffprobe')) {
        $path=Join-Path $BuildRoot "prefix/bin/$tool.exe"
        if (-not [IO.File]::Exists($path)) { throw 'Expected candidate executable is absent.' }
        $version=Invoke-ControlledProcess $path @('-version') $BuildRoot (Join-Path $BuildRoot "$tool-native-version")
        if ($version -notmatch "(?m)^$tool version $([regex]::Escape($sourceLock.ToolVersion))\s") { throw 'Native tool identity mismatch.' }
        $report.Outputs+=@{File="prefix/bin/$tool.exe";Length=[IO.FileInfo]::new($path).Length;SHA256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()}
    }
    foreach ($component in @('zlib','x264','ffmpeg')) {
        $log=Join-Path $BuildRoot "work/$component/build.log"
        $diagnostics=@(Select-String -LiteralPath $log -Pattern '\bwarning:' -CaseSensitive | ForEach-Object {$_.Line})
        $report.Warnings+=@{Component=$component;Count=$diagnostics.Count;Diagnostics=$diagnostics}
    }
    Assert-ControlledInputFiles $SourceDirectory $sourceLock
    Assert-ControlledPatchFiles (Join-Path $PSScriptRoot 'ffmpeg') $sourceLock
    Assert-ControlledPatchFiles $BuildRoot $sourceLock
    foreach ($entry in $sourceLock.Patches[0].Files) {
        if ((Get-FileHash -LiteralPath (Join-Path $BuildRoot "sources/ffmpeg/$($entry.Path)")).Hash.ToLowerInvariant() -cne $entry.AfterSHA256) { throw 'Patched source changed during compilation.' }
    }
    Assert-ControlledToolchainFiles $MsysRoot $toolchainLock
    Assert-ControlledRuntimeFiles (Join-Path $PSScriptRoot 'ffmpeg') $runtimeLock $MsysRoot
    foreach ($script in $buildScripts) { if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $script.File)).Hash.ToLowerInvariant() -cne $script.SHA256) { throw 'Build driver/helper changed during execution.' } }
    foreach ($file in @(@{Path=$sourceLockPath;Hash=$report.SourceLockSHA256},@{Path=$toolchainLockPath;Hash=$report.ToolchainLockSHA256},@{Path=$runtimeLockPath;Hash=$report.RuntimeLockSHA256},@{Path=$recipePath;Hash=$report.RecipeSHA256})) {
        if ((Get-FileHash -LiteralPath $file.Path).Hash.ToLowerInvariant() -cne $file.Hash) { throw 'Recipe/lock changed during the build.' }
    }
    $report.Status='BuiltExperimental'; $report.Completed=$true
} catch { $report.Status='Failed'; $report.Failure=$_.Exception.Message }
finally { $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $BuildRoot 'report.json') -Encoding utf8NoBOM }
if (-not $report.Completed) { throw "Controlled build failed; inspect $BuildRoot/report.json" }
Write-Host "Experimental pair built: $BuildRoot/prefix/bin. Not activated; warning/source/distribution review remains open."
