# Experimental source/build material only; no binary release or compliance approval.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$SourceDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ControlledFfmpeg.Helpers.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$SourceDirectory=[IO.Path]::GetFullPath($SourceDirectory)
$lock=Get-ControlledSourceLock (Join-Path $PSScriptRoot 'ffmpeg/sources.lock.json')
$toolchain=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ffmpeg/toolchain.lock.json') -Raw | ConvertFrom-Json
$runtime=Get-ControlledRuntimeLock (Join-Path $PSScriptRoot 'ffmpeg/runtime.lock.json') $toolchain
Assert-ControlledRuntimeFiles (Join-Path $PSScriptRoot 'ffmpeg') $runtime
Assert-ControlledInputFiles $SourceDirectory $lock
Assert-ControlledPatchFiles (Join-Path $PSScriptRoot 'ffmpeg') $lock
$run=New-PortableRunDirectory (Join-Path $repo 'artifacts/ffmpeg-build-materials') ([Guid]::NewGuid().ToString('N'))
$stage=Join-Path $run 'GlyphFrame-ffmpeg-build-materials'
[void][IO.Directory]::CreateDirectory($stage)
$manifest=[Collections.Generic.List[object]]::new()
$success=$false; $failure=$null
try {
    $copies=@(foreach ($relative in @('LICENSE','scripts/Build-ControlledFfmpeg.ps1','scripts/ControlledFfmpeg.Helpers.ps1',
        'scripts/PortablePackage.Helpers.ps1','scripts/PackageLegal.Helpers.ps1','scripts/ffmpeg/build.sh',
        'scripts/ffmpeg/sources.lock.json','scripts/ffmpeg/toolchain.lock.json','scripts/ffmpeg/runtime.lock.json',
        'scripts/ffmpeg/runtime-notices.md','scripts/ffmpeg/distribution-notices.md','scripts/ffmpeg/README.md',
        'scripts/ffmpeg/Run-VvcWeightedScaledRegression.ps1','scripts/ffmpeg/tests/vvc-weighted-scaled.c','scripts/ffmpeg/patches/README.md')) {
        [pscustomobject]@{Relative=$relative;Source=(Join-Path $repo $relative)}
    })
    $copies+=@(foreach ($entry in $runtime.Notices) { [pscustomobject]@{Relative="scripts/ffmpeg/$($entry.Path)";Source=(Join-Path $PSScriptRoot "ffmpeg/$($entry.Path)")} })
    $copies+=@(foreach ($entry in $lock.Patches) { [pscustomobject]@{Relative="scripts/ffmpeg/$($entry.File)";Source=(Join-Path $PSScriptRoot "ffmpeg/$($entry.File)")} })
    $copies+=@(foreach ($entry in $lock.Artifacts) { [pscustomobject]@{Relative="inputs/$($entry.File)";Source=(Join-Path $SourceDirectory $entry.File)} })
    foreach ($copy in $copies) {
        Assert-PortableRelativePath $copy.Relative
        $hash=(Get-FileHash -LiteralPath $copy.Source).Hash.ToLowerInvariant()
        $target=Join-Path $stage $copy.Relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        Copy-Item -LiteralPath $copy.Source -Destination $target
        if ((Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant() -cne $hash) { throw 'Build-material copy mismatch.' }
        $manifest.Add([pscustomobject]@{Path=$copy.Relative;Length=[IO.FileInfo]::new($target).Length;SHA256=$hash})
    }
    $stagedLock=Get-ControlledSourceLock (Join-Path $stage 'scripts/ffmpeg/sources.lock.json')
    if (($stagedLock | ConvertTo-Json -Depth 8 -Compress) -cne ($lock | ConvertTo-Json -Depth 8 -Compress)) { throw 'Source lock changed during material assembly.' }
    Assert-ControlledInputFiles (Join-Path $stage 'inputs') $lock
    Assert-ControlledPatchFiles (Join-Path $stage 'scripts/ffmpeg') $stagedLock
    $stagedRuntime=Get-ControlledRuntimeLock (Join-Path $stage 'scripts/ffmpeg/runtime.lock.json') $toolchain
    if (($stagedRuntime | ConvertTo-Json -Depth 8 -Compress) -cne ($runtime | ConvertTo-Json -Depth 8 -Compress)) { throw 'Runtime lock changed during material assembly.' }
    Assert-ControlledRuntimeFiles (Join-Path $stage 'scripts/ffmpeg') $runtime
    $inventory=[ordered]@{SchemaVersion=1;Profile=$lock.Profile;CorrespondingSourceApproved=$false;
        PublicDistributionApproved=$false;WarningReviewComplete=$false;RuntimeNoticeIntegrityVerified=$true;
        CompilerAndRuntimeReview='Pinned static runtime objects and six exact notices retained; not complete Corresponding Source or publication approval';
        Scope='Pinned upstream source archives, explicit downstream patch/preimages/postimages, regression source, upstream x264 history, build recipe, host/runtime inventory, runtime notices and original-script license. No GlyphFrame private history, application binaries or toolchain binary packages.';
        Files=@($manifest)}
    $manifestPath=Join-Path $stage 'materials-manifest.json'
    $inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    $manifest.Add([pscustomobject]@{Path='materials-manifest.json';Length=[IO.FileInfo]::new($manifestPath).Length;SHA256=(Get-FileHash -LiteralPath $manifestPath).Hash.ToLowerInvariant()})
    $archive=Join-Path $run "GlyphFrame-ffmpeg-$($lock.ToolVersion)-build-materials.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$archive,[IO.Compression.CompressionLevel]::Optimal,$false)
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $zip.Entries) {
            Assert-PortableRelativePath $entry.FullName
            $expected=@($manifest | Where-Object Path -CEQ $entry.FullName)
            if (-not $seen.Add($entry.FullName) -or $expected.Count -ne 1 -or $entry.Length -ne $expected[0].Length) { throw 'Unexpected/duplicate build-material ZIP entry.' }
            $stream=$entry.Open()
            try { $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
            if ($hash -cne $expected[0].SHA256) { throw 'Archived build-material hash mismatch.' }
        }
        if ($seen.Count -ne $manifest.Count) { throw 'Build-material ZIP coverage mismatch.' }
    } finally { $zip.Dispose() }
    foreach ($copy in $copies) {
        $expected=$manifest | Where-Object Path -CEQ $copy.Relative
        if ((Get-FileHash -LiteralPath $copy.Source).Hash.ToLowerInvariant() -cne $expected.SHA256) { throw 'Input changed during material assembly.' }
    }
    Assert-ControlledInputFiles $SourceDirectory $lock
    Assert-ControlledPatchFiles (Join-Path $PSScriptRoot 'ffmpeg') $lock
    $sha=(Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$archive.sha256","$sha  $([IO.Path]::GetFileName($archive))`n")
    $success=$true
} catch { $failure=$_.Exception.Message }
finally {
    [ordered]@{Completed=$success;Failure=$failure;Run=$run;CorrespondingSourceApproved=$false;PublicDistributionApproved=$false;
        WarningReviewComplete=$false;Files=@($manifest);Archive=if($success){$archive}else{$null};ArchiveSHA256=if($success){$sha}else{$null}} |
        ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $run 'report.json') -Encoding utf8NoBOM
}
if (-not $success) { throw "Build-material assembly failed; inspect $run/report.json" }
Write-Host "Experimental build material: $archive"
Write-Host 'This is not an approved Corresponding Source bundle or public release.'
