# Phase 15A: one fresh verified publish, then a verified portable ZIP. No release/upload.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 on Windows is required.' }
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = New-PortableRunDirectory (Join-Path $repositoryRoot 'artifacts/portable-packages') $runId
$checks = [Collections.Generic.List[string]]::new()
$failure = $null
$completed = $false
$step = 'Source provenance'
$version = $null
$assemblyVersion = $null
$archivePath = $null
$checksumPath = $null
$hash = $null
$archiveEvidence = $null
$verification = $null
$verificationReport = Join-Path $repositoryRoot "artifacts/package-verification/$runId/report.json"
$sourceCommit = $null
$sourceStatus = @()
$sdkVersion = $null
$excludedSymbols = @()
try {
    $sourceCommit = (& git -C $repositoryRoot rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit.' }
    $sourceStatus = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve working-tree state.' }
    $sdkVersion = (& dotnet --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve .NET SDK version.' }
    $checks.Add('Source commit, dirty-tree state and SDK recorded')

    $step = 'Fresh publish and startup/session verification'
    # Reuse the independently runnable verifier; no second publish or bin/ copy.
    & (Join-Path $PSScriptRoot 'Verify-PublishPackage.ps1') -RunId $runId | Out-Host
    $verification = Get-Content -LiteralPath $verificationReport -Raw | ConvertFrom-Json
    if (-not $verification.Success) { throw 'Publish verifier did not report success.' }
    $checks.Add('Fresh self-contained folder publish passed existing structural/tool/startup/session checks')

    $step = 'Version and portable staging'
    $assemblyPath = Join-Path $verification.PublishDirectory 'GlyphFrame.App.dll'
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()
    $version = Get-PortablePackageVersion $assemblyPath
    $applicationDirectory = "GlyphFrame-$version-win-x64"
    $payloadRoot = Join-Path $runRoot $applicationDirectory
    [IO.Directory]::CreateDirectory($payloadRoot) | Out-Null
    $published = @(Get-PortablePayload $verification.PublishDirectory -AllowSymbols)
    $excludedSymbols = @($published | Where-Object Path -Match '\.(?i:pdb)$' | Select-Object -ExpandProperty Path)
    foreach ($file in $published | Where-Object Path -NotMatch '\.(?i:pdb)$') {
        $target = Join-Path $payloadRoot $file.Path
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        [IO.File]::Copy((Join-Path $verification.PublishDirectory $file.Path), $target, $false)
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.Sha256) {
            throw "Published file changed during staging: $($file.Path)"
        }
    }
    $readme = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/Portable_README.md') -Raw
    [IO.File]::WriteAllText((Join-Path $payloadRoot 'README.md'), $readme.Replace('{{VERSION}}', $version), [Text.UTF8Encoding]::new($false))
    Assert-PackageRequiredFiles $payloadRoot
    $payload = @(Get-PortablePayload $payloadRoot)
    $payload | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runRoot 'payload-manifest.json') -Encoding utf8
    $checks.Add('Version matches published assembly/About; required staged files and forbidden-content policy passed; PDBs excluded')

    $step = 'Archive verification'
    $archivePath = Join-Path $runRoot "GlyphFrame-$version-win-x64-portable.zip"
    $partialArchive = $archivePath + '.incomplete'
    New-PortableArchive $payloadRoot $partialArchive $applicationDirectory $payload
    $archiveEvidence = Test-PortableArchive $partialArchive $applicationDirectory $payload
    # Re-read staging as well, rejecting modification after the manifest was captured.
    $settledPayload = @(Get-PortablePayload $payloadRoot)
    if (($payload | ConvertTo-Json -Compress) -cne ($settledPayload | ConvertTo-Json -Compress)) {
        throw 'Staged payload changed during archive creation.'
    }
    [IO.File]::Move($partialArchive, $archivePath, $false)
    $checks.Add('Actual ZIP paths, conflicts, forbidden contents, counts, lengths and every payload SHA-256 verified')

    $step = 'ZIP checksum'
    $checksumPath = $archivePath + '.sha256'
    $hash = Write-PortableChecksum $archivePath $checksumPath
    $checks.Add('Completed ZIP SHA-256 checksum written')
    $step = 'Completed'
    $completed = $true
}
catch { $failure = $_.Exception.Message }
finally {
    $report = New-PortablePackagingReport $completed $failure $step $checks.ToArray()
    $report['PackageVersion'] = $version
    $report['VersionSource'] = 'Published GlyphFrame.App assembly Major.Minor.Build, matching AboutDialogService'
    $report['AssemblyVersion'] = $assemblyVersion
    $report['SourceCommit'] = $sourceCommit
    $report['WorkingTreeDirty'] = $sourceStatus.Count -ne 0
    $report['WorkingTreeStatus'] = $sourceStatus
    $report['SourceCommitCoversUncommittedChanges'] = $false
    $report['Build'] = [ordered]@{ Configuration = 'Release'; Runtime = 'win-x64'; SelfContained = $true; PublishSingleFile = $false; PublishTrimmed = $false }
    $report['Environment'] = [ordered]@{ DotnetSdk = $sdkVersion; PowerShell = $PSVersionTable.PSVersion.ToString(); Windows = [Environment]::OSVersion.VersionString; ProcessArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString() }
    $toolVersions = [ordered]@{}
    foreach ($tool in @('ffmpeg', 'ffprobe')) {
        $evidence = Join-Path ([IO.Path]::GetDirectoryName($verificationReport)) "$tool-version.txt"
        $toolVersions[$tool] = if ([IO.File]::Exists($evidence)) { [IO.File]::ReadLines($evidence) | Select-Object -First 1 } else { $null }
    }
    $report['BundledToolVersions'] = $toolVersions
    $report['ZipPath'] = $archivePath
    $report['ZipSha256'] = $hash
    $report['ChecksumPath'] = $checksumPath
    $report['ZipBytes'] = if ($completed) { [IO.FileInfo]::new($archivePath).Length } else { $null }
    $report['PayloadManifest'] = Join-Path $runRoot 'payload-manifest.json'
    $report['ExcludedDebugSymbols'] = $excludedSymbols
    $report['ArchiveValidation'] = $archiveEvidence
    $report['PublishVerificationReport'] = $verificationReport
    $report['PublishVerification'] = $verification
    $report['FailureEvidenceRetention'] = 'Run-owned staging, incomplete ZIP and verification evidence retained; no recursive cleanup performed.'
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runRoot 'report.json') -Encoding utf8
}
Write-Host "Packaging report: $(Join-Path $runRoot 'report.json')"
if (-not $completed -or $null -ne $failure) { throw "Portable packaging FAILED at '$step': $failure" }
Write-Host "PASS: Portable payload and archive verified ($($archiveEvidence.FileCount) files)"
Write-Host "ZIP: $archivePath"
Write-Host "SHA-256: $checksumPath ($hash)"
