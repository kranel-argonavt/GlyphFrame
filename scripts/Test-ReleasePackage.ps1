# Inert release-definition/manifest/asset fixtures. No app, compiler, consent, network or publish.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'InstallerPackage.Helpers.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root=New-PortableRunDirectory (Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.ReleaseTests') ([Guid]::NewGuid().ToString('N'))
$fixture=Join-Path $root 'source'
$payload=Join-Path $root 'payload'
$passed=0
function Check([bool]$Value,[string]$Message='Assertion failed') {if(-not $Value){throw $Message}}
function Throws([scriptblock]$Action) {$caught=$false;try{& $Action | Out-Null}catch{$caught=$true};Check $caught 'Expected rejection'}
function Run([string]$Name,[scriptblock]$Action) {& $Action;$script:passed++;Write-Host "PASS: $Name"}
function Text([string]$Path,[string]$Value) {[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path));[IO.File]::WriteAllText($Path,$Value,[Text.UTF8Encoding]::new($false))}
function Hash([byte[]]$Value) {[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Value)).ToLowerInvariant()}
function New-Definition { $copy=[ordered]@{};foreach($entry in $originalDefinition.GetEnumerator()){$copy[$entry.Key]=$entry.Value};return $copy }
function Save-Definition {Text $definitionPath ($script:definition | ConvertTo-Json)}
function Read-Context {Get-ReleasePackageContext $fixture $definitionPath $archivePath ('a'*40)}
function Zip([string]$Path,$Entries) {
    $zip=[IO.Compression.ZipFile]::Open($Path,[IO.Compression.ZipArchiveMode]::Create)
    try {foreach($item in $Entries.GetEnumerator()){$entry=$zip.CreateEntry($item.Key);$stream=$entry.Open();try{$bytes=[Text.Encoding]::UTF8.GetBytes($item.Value);$stream.Write($bytes)}finally{$stream.Dispose()}}}finally{$zip.Dispose()}
}
try {
    $sourceBytes=[Text.Encoding]::UTF8.GetBytes('inert source archive bytes')
    $toolBytes=[Text.Encoding]::UTF8.GetBytes('inert tool archive bytes - never executed')
    $archivePath=Join-Path $root 'delivery.zip'
    Zip $archivePath @{'delivery/sources/materials.zip'='inert source archive bytes';'delivery/tools/tools.zip'='inert tool archive bytes - never executed'}
    $paths=@('LICENSE','THIRD_PARTY_NOTICES.md')+@(Get-ControlledToolLegalPaths)
    $files=@(foreach($relative in $paths){$path=Join-Path $fixture $relative;Text $path ('Synthetic inert legal text: '+$relative);@{Path=$relative;TextSha256=(Get-LegalTextHash $path)}})
    $manifest=[ordered]@{SchemaVersion=1;PublicDistributionApproved=$false;OpenIssues=@('Synthetic issue');
        Components=@(@{Dependency='Example/1.2.3';Documents=@('LICENSE')});Files=$files;
        ControlledFfmpeg=@{Version='9.0-glyphframe.2';ArchiveFile='tools.zip';ArchiveSha256=(Hash $toolBytes);SourceMaterialsFile='materials.zip';SourceMaterialsSha256=(Hash $sourceBytes)}}
    Text (Join-Path $fixture 'licenses/manifest.json') ($manifest | ConvertTo-Json -Depth 10)
    Text (Join-Path $fixture 'docs/ffmpeg-source-guide.md') 'Synthetic standalone companion'
    Text (Join-Path $fixture 'GlyphFrame.App.deps.json') '{"libraries":{"Example/1.2.3":{"type":"package"}}}'
    $definitionPath=Join-Path $fixture 'scripts/release-definitions/1.0.0.json'
    $originalDefinition=[ordered]@{SchemaVersion=1;Repository='https://github.com/kranel-argonavt/GlyphFrame';Version='1.0.0';Tag='v1.0.0';
        LegalManifestTextSha256=(Get-LegalTextHash (Join-Path $fixture 'licenses/manifest.json'));
        GuideTextSha256=(Get-LegalTextHash (Join-Path $fixture 'docs/ffmpeg-source-guide.md'));
        DeliveryArchiveSha256=(Get-FileHash $archivePath).Hash.ToLowerInvariant()}
    $script:definition=(New-Definition);Save-Definition
    Run 'Provisional default needs no release inputs' {Assert-ReleasePackagingArguments 'Provisional' '' ''}
    Run 'Release requires both explicit inputs' {Throws {Assert-ReleasePackagingArguments 'Release' '' ''};Throws {Assert-ReleasePackagingArguments 'Release' $definitionPath ''}}
    Run 'Release options cannot silently promote provisional mode' {Throws {Assert-ReleasePackagingArguments 'Provisional' $definitionPath $archivePath}}
    Run 'Unknown package mode rejected' {Throws {Assert-ReleasePackagingArguments 'Other' $definitionPath $archivePath}}
    Run 'Exact definition and two nested archive identities accepted' {$script:context=Read-Context;Check ($context.Record.State -ceq 'ReleasePrepared')}
    Run 'Versioned source URLs have no latest or unrelated service' {Check ($context.Record.SourceMaterialsUrl -ceq 'https://github.com/kranel-argonavt/GlyphFrame/releases/download/v1.0.0/materials.zip')}
    Run 'Offline build never asserts publication or source availability' {Check ($context.Record.Publication -ceq 'Not asserted by offline build' -and $context.Record.AnonymousAvailability.StartsWith('NOT TESTED'))}
    Run 'Public record contains no local archive path' {Check (-not (($context.Record | ConvertTo-Json).Contains($root)))}
    foreach($fault in @('SchemaVersion','Repository','Version','Tag','LegalManifestTextSha256','GuideTextSha256','DeliveryArchiveSha256')) {
        Run ('Invalid definition rejected: '+$fault) {
            $script:definition=(New-Definition)
            $script:definition[$fault]=switch($fault){'SchemaVersion'{'1'};'Repository'{'https://example.test/fork'};'Version'{'1.0.0/other'};'Tag'{'latest'};default{'b'*64}}
            Save-Definition;Throws {Read-Context}
        }
    }
    $script:definition=(New-Definition);Save-Definition
    Run 'Duplicate definition member rejected' {Text $definitionPath (([IO.File]::ReadAllText($definitionPath)).Replace('"SchemaVersion": 1','"SchemaVersion": 1, "SchemaVersion": 1'));Throws {Read-Context};Save-Definition}
    Run 'Unknown approval claim in definition rejected' {$definition['PublicDistributionApproved']=$true;Save-Definition;Throws {Read-Context};$definition.Remove('PublicDistributionApproved');Save-Definition}
    Run 'External definition cannot escape the versioned repository input' {
        $outside=Join-Path $root 'outside.json';[IO.File]::Copy($definitionPath,$outside,$false)
        Throws {Get-ReleasePackageContext $fixture $outside $archivePath ('a'*40)}
    }
    Run 'Invalid source revision rejected' {Throws {Get-ReleasePackageContext $fixture $definitionPath $archivePath 'main'}}
    Run 'Mismatched nested source rejected despite matching outer hash' {
        $badArchive=Join-Path $root 'bad-delivery.zip';Zip $badArchive @{'delivery/sources/materials.zip'='changed';'delivery/tools/tools.zip'='inert tool archive bytes - never executed'}
        $definition.DeliveryArchiveSha256=(Get-FileHash $badArchive).Hash.ToLowerInvariant();Save-Definition
        Throws {Get-ReleasePackageContext $fixture $definitionPath $badArchive ('a'*40)}
        $script:definition=(New-Definition);Save-Definition
    }
    Run 'Duplicate selected source entry rejected' {
        $badArchive=Join-Path $root 'duplicate-delivery.zip';Zip $badArchive @{'a/sources/materials.zip'='inert source archive bytes';'b/sources/materials.zip'='inert source archive bytes';'a/tools/tools.zip'='inert tool archive bytes - never executed'}
        $definition.DeliveryArchiveSha256=(Get-FileHash $badArchive).Hash.ToLowerInvariant();Save-Definition
        Throws {Get-ReleasePackageContext $fixture $definitionPath $badArchive ('a'*40)}
        $script:definition=(New-Definition);Save-Definition
    }
    [void][IO.Directory]::CreateDirectory($payload)
    foreach($file in Get-ChildItem -LiteralPath $fixture -Recurse -File){$relative=[IO.Path]::GetRelativePath($fixture,$file.FullName);$target=Join-Path $payload $relative;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target));[IO.File]::Copy($file.FullName,$target,$false)}
    Run 'Provisional manifest still verifies before projection' {$legal=Assert-LegalPackage $payload;Check ($legal.Mode -ceq 'Provisional')}
    Run 'Release projection adds directions/guide without changing source provenance' {Write-ReleasePackageMetadata $payload $context;Check ((Get-LegalTextHash (Join-Path $fixture 'licenses/manifest.json')) -ceq $originalDefinition.LegalManifestTextSha256)}
    Run 'Release projection requires its explicit bound context' {Throws {Assert-LegalPackage $payload}}
    Run 'Release manifest and all legal text hashes verify with context' {$legal=Assert-LegalPackage $payload $context;Check ($legal.Mode -ceq 'Release' -and $legal.DocumentsVerified -eq ($files.Count+2))}
    Run 'Generated manifest makes no fabricated public approval claim' {$generated=Get-Content (Join-Path $payload 'licenses/manifest.json') -Raw | ConvertFrom-Json;Check ($generated.SchemaVersion -eq 2 -and -not $generated.PSObject.Properties['PublicDistributionApproved'])}
    Run 'Release metadata cannot be applied twice' {Throws {Write-ReleasePackageMetadata $payload $context}}
    Run 'Tampered release source commit rejected' {
        $path=Join-Path $payload 'licenses/manifest.json';$saved=[IO.File]::ReadAllText($path);Text $path ($saved.Replace(('a'*40),('b'*40)));Throws {Assert-LegalPackage $payload $context};Text $path $saved
    }
    Run 'Tampered release directions rejected' {
        $path=Join-Path $payload 'licenses/FFmpeg/SOURCE-DIRECTIONS.txt';$saved=[IO.File]::ReadAllText($path);Text $path ($saved+'changed');Throws {Assert-LegalPackage $payload $context};Text $path $saved
    }
    Run 'Companion source/tool collection bytes stage unchanged with checksums' {
        $script:assets=Write-ReleaseCompanionAssets $root $context
        Check ((Get-FileHash (Join-Path $assets 'materials.zip')).Hash.ToLowerInvariant() -ceq (Hash $sourceBytes))
        Check ((Get-FileHash (Join-Path $assets 'GlyphFrame-ffmpeg-9.0-glyphframe.2-delivery.zip')).Hash.ToLowerInvariant() -ceq $originalDefinition.DeliveryArchiveSha256)
        Check ((Get-Content (Join-Path $assets 'SHA256SUMS.txt')).Count -eq 4)
    }
    Run 'Release companion assets cannot overwrite another run' {Throws {Write-ReleaseCompanionAssets $root $context}}
    Run 'Setup mode mismatch rejected before payload reuse' {
        $identityRoot=New-PortableRunDirectory $root ([Guid]::NewGuid().ToString('N'))
        @{Success=$true;ArchiveValidation=@{Success=$true};PublishVerification=@{Success=$true};PackageVersion='1.0.0';PackagingMode='Release'} | ConvertTo-Json | Set-Content (Join-Path $identityRoot 'report.json')
        Throws {Get-VerifiedInstallerPayload $identityRoot}
    }
    Run 'Release Setup source/context mismatch rejected before payload reuse' {
        $identityRoot=New-PortableRunDirectory $root ([Guid]::NewGuid().ToString('N'))
        @{Success=$true;ArchiveValidation=@{Success=$true};PublishVerification=@{Success=$true};PackageVersion='1.0.0';PackagingMode='Release';WorkingTreeDirty=$true;SourceCommit=('a'*40);ReleasePreparation=$context.Record} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $identityRoot 'report.json')
        Throws {Get-VerifiedInstallerPayload $identityRoot -PackagingMode 'Release' -ReleaseContext $context}
    }
    Run 'All three release metadata inputs must belong to the selected commit' {
        $script:observedGit=[Collections.Generic.List[string]]::new()
        function git {$script:observedGit.Add(($args -join '|'));$global:LASTEXITCODE=0}
        try {Assert-ReleaseTrackedInputs $fixture $context;Check ($observedGit.Count -eq 3);Check ($observedGit[0].EndsWith(':scripts/release-definitions/1.0.0.json'))}
        finally {Remove-Item Function:git}
    }
    Run 'Missing committed release input fails closed' {
        function git {$global:LASTEXITCODE=1}
        try {Throws {Assert-ReleaseTrackedInputs $fixture $context}}
        finally {Remove-Item Function:git}
    }
    Run 'All release builders retain clean-tree gates and explicit mode' {foreach($name in @('Build-PortablePackage.ps1','Build-InstallerPackage.ps1','Verify-PublishPackage.ps1')){$text=[IO.File]::ReadAllText((Join-Path $repo ('scripts/'+$name)));Check ($text.Contains("'Release mode requires a clean committed working tree.'") -and $text.Contains("[ValidateSet('Provisional','Release')]"))}}
    Run 'Standalone structural helper does not import release/application gates' {$text=[IO.File]::ReadAllText((Join-Path $repo 'scripts/PortablePackage.Helpers.ps1'));Check (-not $text.Contains('ReleasePackage.Helpers.ps1') -and -not $text.Contains('NativeTerms.Helpers.ps1'))}
    Write-Host "Release helper checks passed: $passed; failed: 0. Inert fixtures only."
} finally {Assert-PortableDirectoryChain ([IO.Path]::GetFullPath($root))}
