# Fresh history-free local snapshot of reviewed working files. No Git writes/upload.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PortfolioSource.Helpers.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gitRoot = (& git -C $repo rev-parse --show-toplevel | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or -not [IO.Path]::GetFullPath($gitRoot).Equals($repo, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Run this exporter from its own development Git checkout, not a nested history-free copy or an ancestor repository.'
}
$head = (& git -C $repo rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot read source revision.' }
$status = @(& git -C $repo status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'Cannot read source status.' }
$paths = @(& git -c core.quotepath=false -C $repo ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source files.' }
$run = New-PortableRunDirectory (Join-Path $repo 'artifacts/portfolio-source') ([Guid]::NewGuid().ToString('N'))
$destination = Join-Path $run 'GlyphFrame'
$manifest = [Collections.Generic.List[object]]::new()
$omitted = [Collections.Generic.List[string]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$success = $false
$failure = $null
try {
    foreach ($relative in $paths | Sort-Object -Unique -CaseSensitive) {
        $source = Join-Path $repo $relative
        if ($relative -cin @('Tools/ffmpeg/ffmpeg.exe','Tools/ffmpeg/ffprobe.exe') -and -not [IO.File]::Exists($source)) {
            $omitted.Add($relative); continue
        }
        Assert-PortfolioSourcePath $relative
        if (-not $seen.Add($relative)) { throw 'Case-aliased source paths.' }
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($source))
        if (-not [IO.File]::Exists($source) -or ([IO.File]::GetAttributes($source) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Missing/linked source file: $relative" }
        $target = Join-Path $destination $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        [IO.File]::Copy($source, $target, $false)
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -cne $hash) { throw "Source changed while exporting: $relative" }
        $manifest.Add([pscustomobject]@{Path=$relative;Length=[IO.FileInfo]::new($target).Length;Sha256=$hash})
    }
    foreach ($file in $manifest) {
        if ((Get-FileHash -LiteralPath (Join-Path $repo $file.Path)).Hash.ToLowerInvariant() -cne $file.Sha256) { throw 'Source changed during snapshot verification.' }
    }
    $finalHead = (& git -C $repo rev-parse HEAD | Out-String).Trim()
    $finalStatus = @(& git -C $repo status --porcelain=v1 --untracked-files=all)
    if ($head -cne $finalHead -or ($status -join "`n") -cne ($finalStatus -join "`n")) { throw 'Source revision/status changed during export.' }
    $success = $true
}
catch { $failure = $_.Exception.Message }
finally {
    $manifest.ToArray() | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $run 'source-manifest.json') -Encoding utf8
    [ordered]@{
        Success=$success;Failure=$failure;ParentCommit=$head;WorkingTreeDirty=($status.Count -ne 0);WorkingTreeStatus=$status
        SourceDirectory=$destination;FileCount=$manifest.Count;OmittedDeletedTools=$omitted.ToArray()
        History='No .git copied; original repository, refs and history untouched. Not a commit/clone/release.'
        Distribution='Original source and reviewed project assets only; no third-party executables/runtime/archive payloads.'
        PublicRelease='NOT CREATED';SecretReview='Path/content policy is not exhaustive secret scanning; review before publication.'
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'report.json') -Encoding utf8
}
if (-not $success) { throw "Source snapshot failed: $failure; retained evidence: $run" }
Write-Host "PASS: $($manifest.Count) source files copied and SHA-256 verified; no Git history/runtime binaries."
Write-Host "Source snapshot: $destination"
Write-Host "Report/manifest: $run"
