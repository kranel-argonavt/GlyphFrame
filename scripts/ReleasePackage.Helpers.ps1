# Explicit offline release preparation. No consent, public-approval claim, network or upload.
# Import explicitly in package consumers, not the standalone FFmpeg helper chain.
function Assert-ReleasePackagingArguments([string]$Mode,[string]$DefinitionPath,[string]$ArchivePath) {
    if ($Mode -ceq 'Provisional') {
        if ($DefinitionPath -or $ArchivePath) { throw 'Release inputs require explicit Release mode.' }
    } elseif ($Mode -cne 'Release' -or -not $DefinitionPath -or -not $ArchivePath) {
        throw 'Release mode requires an explicit definition and FFmpeg delivery archive.'
    }
}

function Read-ReleaseDefinition([string]$Path) {
    Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path)))
    if (([IO.File]::GetAttributes($Path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        [IO.FileInfo]::new($Path).Length -gt 16384) { throw 'Linked or oversized release definition.' }
    $text=[IO.File]::ReadAllText($Path,[Text.UTF8Encoding]::new($false,$true))
    $doc=[System.Text.Json.JsonDocument]::Parse($text)
    try {
        $root=$doc.RootElement
        $names=@('SchemaVersion','Repository','Version','Tag','LegalManifestTextSha256','GuideTextSha256','DeliveryArchiveSha256')
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($property in $root.EnumerateObject()) {
            if ($property.Name -cnotin $names -or -not $seen.Add($property.Name)) { throw 'Unknown or duplicate release-definition field.' }
        }
        if ($seen.Count -ne $names.Count -or $root.GetProperty('SchemaVersion').GetInt32() -ne 1) { throw 'Incomplete release definition.' }
        foreach($name in $names | Where-Object { $_ -cne 'SchemaVersion' }) { $null=$root.GetProperty($name).GetString() }
        $plan=$text | ConvertFrom-Json
        if ($plan.Repository -cne 'https://github.com/kranel-argonavt/GlyphFrame' -or
            $plan.Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or
            $plan.Tag -cne ('v'+$plan.Version)) { throw 'Unreviewed repository, version or versioned tag.' }
        foreach($name in @('LegalManifestTextSha256','GuideTextSha256','DeliveryArchiveSha256')) {
            if ($plan.$name -cnotmatch '^[a-f0-9]{64}$') { throw 'Invalid release-definition hash.' }
        }
        return [pscustomobject]@{ Plan=$plan; TextSha256=(Get-LegalTextHash $Path) }
    } finally { $doc.Dispose() }
}

function Get-ReleaseArchiveEntry($Archive,[string]$Suffix) {
    $selectedEntries=@($Archive.Entries | Where-Object { $_.FullName.EndsWith('/'+$Suffix,[StringComparison]::Ordinal) })
    if ($selectedEntries.Count -ne 1 -or $selectedEntries[0].Length -gt 268435456) { throw 'Missing, duplicate or oversized selected release-archive entry.' }
    $stream=$selectedEntries[0].Open(); $memory=[IO.MemoryStream]::new()
    try { $stream.CopyTo($memory); return ,$memory.ToArray() }
    finally { $stream.Dispose(); $memory.Dispose() }
}

function Get-ReleasePackageContext([string]$RepositoryRoot,[string]$DefinitionPath,[string]$DeliveryArchivePath,[string]$SourceCommit) {
    if (-not $DefinitionPath -or -not $DeliveryArchivePath -or $SourceCommit -cnotmatch '^[a-f0-9]{40}$') { throw 'Release mode requires an explicit definition, delivery archive and source commit.' }
    $definition=Read-ReleaseDefinition $DefinitionPath
    $expectedDefinition=Join-Path $RepositoryRoot ('scripts/release-definitions/'+$definition.Plan.Version+'.json')
    if(-not [IO.Path]::GetFullPath($DefinitionPath).Equals([IO.Path]::GetFullPath($expectedDefinition),[StringComparison]::OrdinalIgnoreCase)){throw 'Release definition must be the versioned repository input.'}
    $manifestPath=Join-Path $RepositoryRoot 'licenses/manifest.json'
    if ((Get-LegalTextHash $manifestPath) -cne $definition.Plan.LegalManifestTextSha256) { throw 'Release definition does not bind the current source legal manifest.' }
    $sourceManifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($sourceManifest.SchemaVersion -ne 1 -or $sourceManifest.PublicDistributionApproved -isnot [bool] -or
        $sourceManifest.PublicDistributionApproved -or @($sourceManifest.OpenIssues).Count -eq 0) { throw 'Expected unchanged provisional source provenance.' }
    $guidePath=Join-Path $RepositoryRoot 'docs/ffmpeg-source-guide.md'
    if ((Get-LegalTextHash $guidePath) -cne $definition.Plan.GuideTextSha256) { throw 'Source-guide identity changed.' }
    Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($DeliveryArchivePath)))
    if (([IO.File]::GetAttributes($DeliveryArchivePath) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        (Get-FileHash -LiteralPath $DeliveryArchivePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $definition.Plan.DeliveryArchiveSha256) { throw 'Unreviewed FFmpeg delivery archive.' }
    $tool=$sourceManifest.ControlledFfmpeg
    foreach($name in @($tool.ArchiveFile,$tool.SourceMaterialsFile)) {
        if ($name -cnotmatch '^[A-Za-z0-9._-]+\.zip$') { throw 'Unsafe controlled archive filename.' }
    }
    $archive=[IO.Compression.ZipFile]::OpenRead($DeliveryArchivePath)
    try {
        foreach($item in @(@{Path='sources/'+$tool.SourceMaterialsFile;Hash=$tool.SourceMaterialsSha256},@{Path='tools/'+$tool.ArchiveFile;Hash=$tool.ArchiveSha256})) {
            $bytes=Get-ReleaseArchiveEntry $archive $item.Path
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            if ($hash -cne $item.Hash) { throw 'Delivery archive does not contain the exact bound source/tools.' }
        }
    } finally { $archive.Dispose() }
    $download=$definition.Plan.Repository+'/releases/download/'+$definition.Plan.Tag+'/'
    $record=[ordered]@{ State='ReleasePrepared'; Version=$definition.Plan.Version; Tag=$definition.Plan.Tag;
        Repository=$definition.Plan.Repository; SourceCommit=$SourceCommit; DefinitionTextSha256=$definition.TextSha256;
        SourceLegalManifestTextSha256=$definition.Plan.LegalManifestTextSha256;
        SourceMaterialsUrl=$download+$tool.SourceMaterialsFile; SourceMaterialsSha256=$tool.SourceMaterialsSha256;
        DeliveryUrl=$download+"GlyphFrame-ffmpeg-$($tool.Version)-delivery.zip"; DeliverySha256=$definition.Plan.DeliveryArchiveSha256;
        GuideUrl=$download+'ffmpeg-source-guide.md'; GuideTextSha256=$definition.Plan.GuideTextSha256;
        Publication='Not asserted by offline build'; AnonymousAvailability='NOT TESTED by offline build' }
    $directions=@"
GlyphFrame $($record.Version) - controlled FFmpeg source directions
Prepared versioned destination: $($record.Repository)/releases/tag/$($record.Tag)

Matching FFmpeg source/build material:
$($record.SourceMaterialsUrl)
SHA-256: $($record.SourceMaterialsSha256)

Complete controlled tool/source/notices collection:
$($record.DeliveryUrl)
SHA-256: $($record.DeliverySha256)

Standalone rebuild companion: $($record.GuideUrl)
Canonical UTF-8/LF SHA-256: $($record.GuideTextSha256)
Offline companion: licenses/FFmpeg/SOURCE-GUIDE.md
Full tool licenses/notices: Tools/ffmpeg/ and licenses/FFmpeg/; also available in About.

Sources and GPL tools may be accessed independently, without running GlyphFrame,
accepting Microsoft-component terms, payment or a personal request. GitHub's
automatic application-source archive is not this FFmpeg source/build archive.

The retained delivery collection and DISTRIBUTION_README.txt preserve their
original engineering-candidate provenance. These versioned directions are the
current release-preparation binding; old review flags do not restrict source rights.
The offline build neither publishes these destinations nor verifies availability.
The publisher must check actual anonymous source access before publishing binaries
and keep matching source available while conveying them. No written-media offer
or legal-compliance certificate is created by this document.
"@
    return [pscustomobject]@{Record=$record;SourceManifest=$sourceManifest;GuideText=[IO.File]::ReadAllText($guidePath).Replace("`r`n","`n");
        DirectionsText=$directions.Replace("`r`n","`n");DeliveryArchivePath=[IO.Path]::GetFullPath($DeliveryArchivePath)}
}

function Get-ExpectedReleaseManifest($Context) {
    $manifest=($Context.SourceManifest | ConvertTo-Json -Depth 20) | ConvertFrom-Json
    $manifest.PSObject.Properties.Remove('PublicDistributionApproved')
    $manifest.PSObject.Properties.Remove('OpenIssues')
    $manifest.SchemaVersion=2
    $manifest | Add-Member -NotePropertyName ReleasePreparation -NotePropertyValue $Context.Record
    $extra=@(foreach($item in @(@{Path='licenses/FFmpeg/SOURCE-DIRECTIONS.txt';Text=$Context.DirectionsText},@{Path='licenses/FFmpeg/SOURCE-GUIDE.md';Text=$Context.GuideText})) {
        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($item.Text))).ToLowerInvariant()
        [pscustomobject]@{Path=$item.Path;TextSha256=$hash;Source='Generated release directions or exact retained companion; no public-approval assertion'}
    })
    $manifest.Files=@($manifest.Files)+$extra
    return $manifest
}

function Write-ReleasePackageMetadata([string]$Root,$Context) {
    $manifestPath=Join-Path $Root 'licenses/manifest.json'
    if ((Get-LegalTextHash $manifestPath) -cne $Context.Record.SourceLegalManifestTextSha256) { throw 'Published provenance differs before release preparation.' }
    foreach($item in @(@{Path='licenses/FFmpeg/SOURCE-DIRECTIONS.txt';Text=$Context.DirectionsText},@{Path='licenses/FFmpeg/SOURCE-GUIDE.md';Text=$Context.GuideText})) {
        $path=Join-Path $Root $item.Path
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
        if ([IO.File]::Exists($path)) { throw 'Refuse to replace existing release directions.' }
        [IO.File]::WriteAllText($path,$item.Text,[Text.UTF8Encoding]::new($false))
    }
    $manifest=Get-ExpectedReleaseManifest $Context
    [IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
}

function Assert-ReleaseManifest($Manifest,$Context) {
    $expected=Get-ExpectedReleaseManifest $Context
    if (($Manifest | ConvertTo-Json -Depth 20 -Compress) -cne ($expected | ConvertTo-Json -Depth 20 -Compress)) { throw 'Release manifest differs from its explicit, hash-bound context.' }
}

function Write-ReleaseCompanionAssets([string]$RunRoot,$Context) {
    $root=Join-Path $RunRoot 'release-assets'
    Assert-PortableDirectoryChain $RunRoot
    if (Test-Path -LiteralPath $root) { throw 'Refuse to overwrite release companion assets.' }
    [void][IO.Directory]::CreateDirectory($root)
    $archive=[IO.Compression.ZipFile]::OpenRead($Context.DeliveryArchivePath)
    try {
        $sourceName=$Context.SourceManifest.ControlledFfmpeg.SourceMaterialsFile
        [IO.File]::WriteAllBytes((Join-Path $root $sourceName),(Get-ReleaseArchiveEntry $archive ('sources/'+$sourceName)))
    } finally { $archive.Dispose() }
    $deliveryName="GlyphFrame-ffmpeg-$($Context.SourceManifest.ControlledFfmpeg.Version)-delivery.zip"
    [IO.File]::Copy($Context.DeliveryArchivePath,(Join-Path $root $deliveryName),$false)
    [IO.File]::WriteAllText((Join-Path $root 'ffmpeg-source-guide.md'),$Context.GuideText,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $root 'SOURCE-DIRECTIONS.txt'),$Context.DirectionsText,[Text.UTF8Encoding]::new($false))
    $sums=@(foreach($file in Get-ChildItem -LiteralPath $root -File | Sort-Object Name) { (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$file.Name })
    [IO.File]::WriteAllText((Join-Path $root 'SHA256SUMS.txt'),($sums -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
    if ((Get-FileHash -LiteralPath (Join-Path $root $sourceName)).Hash.ToLowerInvariant() -cne $Context.Record.SourceMaterialsSha256 -or
        (Get-FileHash -LiteralPath (Join-Path $root $deliveryName)).Hash.ToLowerInvariant() -cne $Context.Record.DeliverySha256) { throw 'Source/delivery identity changed while staging release assets.' }
    return $root
}

function Assert-ReleaseTrackedInputs([string]$RepositoryRoot,$Context) {
    foreach($relative in @(('scripts/release-definitions/'+$Context.Record.Version+'.json'),'licenses/manifest.json','docs/ffmpeg-source-guide.md')) {
        & git -C $RepositoryRoot cat-file -e ($Context.Record.SourceCommit+':'+$relative) 2>$null
        if($LASTEXITCODE -ne 0){throw 'Release definition/legal/guide inputs must belong to the selected source commit.'}
    }
}
