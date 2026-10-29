# Inert fixtures only. Never vendor/legal assent, application startup, installation or network access.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')
. (Join-Path $PSScriptRoot 'NativeTerms.Helpers.ps1')
$parent = Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.NativeTermsHelperTests'
$run = [Guid]::NewGuid().ToString('N')
$root = New-PortableRunDirectory $parent $run
$payload = Join-Path $root 'inert-payload'
$passed = 0
function Assert-Check([bool]$Value,[string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Throws([scriptblock]$Action) {
    $caught = $false
    try { & $Action | Out-Null } catch { $caught = $true }
    Assert-Check $caught 'Expected fixture rejection.'
}
function Run-Check([string]$Name,[scriptblock]$Action) { & $Action; $script:passed++; Write-Host "PASS: $Name" }
function Write-Fixture([string]$Relative,[string]$Text) {
    $path = Join-Path $payload $Relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path,$Text)
}
function Reset-Fixture {
    $components = @(foreach ($name in @('coreclr.dll','PresentationNative_cor3.dll','vcruntime140_cor3.dll','wpfgfx_cor3.dll','D3DCompiler_47_cor3.dll')) {
        Write-Fixture $name "Synthetic inert bytes: $name"
        @{Path=$name;Length=[IO.FileInfo]::new((Join-Path $payload $name)).Length;Sha256=(Get-FileHash (Join-Path $payload $name)).Hash.ToLowerInvariant()}
    })
    $references = @(foreach ($relative in @('licenses/dotnet/NET-LIBRARY-LICENSE.txt','licenses/WPF/WINDOWS-SDK-LICENSE.txt','licenses/dotnet/WINDOWS-LICENSE-MAPPING.txt')) {
        Write-Fixture $relative "Synthetic reference: $relative"
        @{Path=$relative;TextSha256=(Get-LegalTextHash (Join-Path $payload $relative))}
    })
    Write-Fixture 'licenses/WindowsNative/COMPONENT-TERMS.txt' 'VERSION 1.0 — 29 OCTOBER 2026; SYNTHETIC TEST TERMS ONLY - no vendor agreement'
    $profile = @{SchemaVersion=1;Purpose='ComponentTermsAcceptance';Version='1.0';TermsPath='licenses/WindowsNative/COMPONENT-TERMS.txt';
        TermsTextSha256=(Get-LegalTextHash (Join-Path $payload 'licenses/WindowsNative/COMPONENT-TERMS.txt'));
        RuntimeFrameworks=@{'Microsoft.NETCore.App'='10.0.12';'Microsoft.WindowsDesktop.App'='10.0.12'};Components=$components;References=$references}
    Write-Fixture 'licenses/WindowsNative/profile.json' ($profile | ConvertTo-Json -Depth 8)
    Write-Fixture 'licenses/manifest.json' (@{WindowsNativeTerms=@{ProfilePath='licenses/WindowsNative/profile.json';
        ProfileTextSha256=(Get-LegalTextHash (Join-Path $payload 'licenses/WindowsNative/profile.json'));Purpose='ComponentTermsAcceptance';OperativeAgreementAdopted=$true}} | ConvertTo-Json -Depth 5)
    Write-Fixture 'GlyphFrame.App.runtimeconfig.json' '{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.12"},{"name":"Microsoft.WindowsDesktop.App","version":"10.0.12"}]}}'
}
try {
    Reset-Fixture
    Run-Check 'Complete synthetic scope has a stable adopted-protocol identity, not vendor assent' {
        $scope=Get-NativeTermsProfile $payload
        Assert-Check ($scope.Purpose -ceq 'ComponentTermsAcceptance' -and $scope.OperativeAgreement -and $scope.Identity -cmatch '^[0-9a-f]{64}$') 'Incorrect scope.'
    }
    foreach ($relative in @('coreclr.dll','licenses/WindowsNative/profile.json','licenses/WindowsNative/COMPONENT-TERMS.txt','licenses/WPF/WINDOWS-SDK-LICENSE.txt','GlyphFrame.App.runtimeconfig.json')) {
        Run-Check "Changed scope rejected: $relative" {
            Write-Fixture $relative 'changed'; Assert-Throws { Get-NativeTermsProfile $payload }; Reset-Fixture
        }
    }
    Run-Check 'Old preview-purpose profile is rejected even when rebound' {
        $path=Join-Path $payload 'licenses/WindowsNative/profile.json'
        [IO.File]::WriteAllText($path,([IO.File]::ReadAllText($path).Replace('ComponentTermsAcceptance','ImplementationPreview')))
        $manifest=Get-Content (Join-Path $payload 'licenses/manifest.json') -Raw | ConvertFrom-Json
        $manifest.WindowsNativeTerms.ProfileTextSha256=Get-LegalTextHash $path
        Write-Fixture 'licenses/manifest.json' ($manifest | ConvertTo-Json -Depth 5)
        Assert-Throws {Get-NativeTermsProfile $payload}; Reset-Fixture
    }
    Run-Check 'Unadopted manifest is rejected' {
        $path=Join-Path $payload 'licenses/manifest.json'
        [IO.File]::WriteAllText($path,([IO.File]::ReadAllText($path).Replace('true','false')))
        Assert-Throws {Get-NativeTermsProfile $payload}; Reset-Fixture
    }
    Run-Check 'Unapproved edition is rejected even with matching text and rebound profile' {
        $path=Join-Path $payload 'licenses/WindowsNative/profile.json'
        $profile=Get-Content $path -Raw | ConvertFrom-Json
        $profile.Version='0.2'
        Write-Fixture 'licenses/WindowsNative/profile.json' ($profile | ConvertTo-Json -Depth 8)
        $manifest=Get-Content (Join-Path $payload 'licenses/manifest.json') -Raw | ConvertFrom-Json
        $manifest.WindowsNativeTerms.ProfileTextSha256=Get-LegalTextHash $path
        Write-Fixture 'licenses/manifest.json' ($manifest | ConvertTo-Json -Depth 5)
        Assert-Throws {Get-NativeTermsProfile $payload}; Reset-Fixture
    }
    foreach ($invalidFlag in @('true','false',1,$null)) {
        Run-Check "Adoption flag must be a real JSON boolean, not '$invalidFlag'" {
            $manifest=Get-Content (Join-Path $payload 'licenses/manifest.json') -Raw | ConvertFrom-Json
            $manifest.WindowsNativeTerms.OperativeAgreementAdopted=$invalidFlag
            Write-Fixture 'licenses/manifest.json' ($manifest | ConvertTo-Json -Depth 5)
            Assert-Throws {Get-NativeTermsProfile $payload}; Reset-Fixture
        }
    }
    foreach ($damage in @('schema-string','schema-array','edition-number','edition-array','extra-profile-framework','component-length-string')) {
        Run-Check "Malformed rebound profile is rejected: $damage" {
            $path=Join-Path $payload 'licenses/WindowsNative/profile.json'
            $profile=Get-Content $path -Raw | ConvertFrom-Json
            switch($damage) {
                'schema-string' {$profile.SchemaVersion='1'}
                'schema-array' {$profile.SchemaVersion=@(1)}
                'edition-number' {$profile.Version=1.0}
                'edition-array' {$profile.Version=@('1.0')}
                'extra-profile-framework' {$profile.RuntimeFrameworks | Add-Member -NotePropertyName 'Unexpected.Framework' -NotePropertyValue '10.0.12'}
                'component-length-string' {$profile.Components[0].Length=$profile.Components[0].Length.ToString()}
            }
            Write-Fixture 'licenses/WindowsNative/profile.json' ($profile | ConvertTo-Json -Depth 8)
            $manifest=Get-Content (Join-Path $payload 'licenses/manifest.json') -Raw | ConvertFrom-Json
            $manifest.WindowsNativeTerms.ProfileTextSha256=Get-LegalTextHash $path
            Write-Fixture 'licenses/manifest.json' ($manifest | ConvertTo-Json -Depth 5)
            Assert-Throws {Get-NativeTermsProfile $payload}; Reset-Fixture
        }
    }
    Run-Check 'A rebound draft cannot masquerade as adopted terms' {
        Write-Fixture 'licenses/WindowsNative/COMPONENT-TERMS.txt' 'NONOPERATIVE REVIEW DRAFT'
        $path=Join-Path $payload 'licenses/WindowsNative/profile.json'
        $profile=Get-Content $path -Raw | ConvertFrom-Json
        $profile.TermsTextSha256=Get-LegalTextHash (Join-Path $payload $profile.TermsPath)
        Write-Fixture 'licenses/WindowsNative/profile.json' ($profile | ConvertTo-Json -Depth 8)
        $manifest=Get-Content (Join-Path $payload 'licenses/manifest.json') -Raw | ConvertFrom-Json
        $manifest.WindowsNativeTerms.ProfileTextSha256=Get-LegalTextHash $path
        Write-Fixture 'licenses/manifest.json' ($manifest | ConvertTo-Json -Depth 5)
        Assert-Throws {Get-NativeTermsProfile $payload}; Reset-Fixture
    }
    Run-Check 'Packaging cannot synthesize acceptance of a real component-terms bundle' {
        Assert-Check ($null -eq (Get-Command New-IsolatedNativeTermsPreviewFixture -ErrorAction SilentlyContinue)) 'Old acknowledgement writer still exported.'
        $verifier=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Verify-PublishPackage.ps1'))
        Assert-Check (-not $verifier.Contains('New-IsolatedNativeTermsPreviewFixture') -and
            $verifier.Contains("NativeTermsHumanOrLegalAssent = 'NOT CREATED; no synthetic acceptance of adopted terms'") -and
            $verifier.Contains("VerificationScope = 'PackageIntegrityAndUnacceptedStartupOnly'") -and
            $verifier.Contains('Assert-NativeTermsPendingState -WindowTitle $application.MainWindowTitle') -and
            $verifier.Contains('SessionObservation = $null') -and $verifier.Contains('SessionCleanup = $null')) 'Verifier invents acceptance or overstates workspace coverage.'
    }
    Run-Check 'Installer review text contains the complete inert terms and all three inert references' {
        $output=Join-Path $root 'review.txt'
        $scope=Write-InstallerNativeTermsDocument $payload $output
        $text=[IO.File]::ReadAllText($output)
        Assert-Check ($text.Contains('SYNTHETIC TEST TERMS ONLY') -and [regex]::Matches($text,'Synthetic reference:').Count -eq 3) 'Incomplete document.'
        Assert-Check ($scope.Identity -ceq (Get-NativeTermsProfile $payload).Identity) 'Scope drift.'
        Assert-Throws { Write-InstallerNativeTermsDocument $payload $output }
    }
    Run-Check 'User acknowledgement is never allowed in a distributed payload' {
        Assert-Throws { Assert-PortableContentPath 'native-terms-acknowledgement.txt' }
        Assert-PortableContentPath 'licenses/WindowsNative/profile.json'
    }
    Run-Check 'Pending valid terms without record/cache are observable, not accepted' {
        Assert-NativeTermsPendingState 'Microsoft component terms — GlyphFrame' $false $false
    }
    foreach ($title in @('Microsoft component verification failed — GlyphFrame','GlyphFrame','')) {
        Run-Check "Invalid or unrelated window cannot pass the pending gate: '$title'" {
            Assert-Throws {Assert-NativeTermsPendingState $title $false $false}
        }
    }
    Run-Check 'An existing acknowledgement cannot pass fresh pending verification' {
        Assert-Throws {Assert-NativeTermsPendingState 'Microsoft component terms — GlyphFrame' $true $false}
    }
    Run-Check 'A media cache cannot pass fresh pending verification' {
        Assert-Throws {Assert-NativeTermsPendingState 'Microsoft component terms — GlyphFrame' $false $true}
    }
    Run-Check 'Native text reader rejects oversize, invalid UTF-8 and package escape' {
        Write-Fixture 'probe.txt' 'bounded synthetic text'
        Assert-Throws {Read-NativeTermsText $payload 'probe.txt' 3}
        [IO.File]::WriteAllBytes((Join-Path $payload 'probe.txt'),[byte[]]@(255))
        Assert-Throws {Read-NativeTermsText $payload 'probe.txt' 16}
        Assert-Throws {Read-NativeTermsText $payload '../outside.txt' 16}
    }
    Write-Host "Native terms helper checks passed: $passed; failed: 0. Inert fixtures only; no real acceptance created."
}
finally {
    $resolved=[IO.Path]::GetFullPath($root)
    Assert-PortableDirectoryChain $resolved
    if ([IO.Path]::GetDirectoryName($resolved) -cne [IO.Path]::GetFullPath($parent) -or [IO.Path]::GetFileName($resolved) -cne $run -or
        [IO.File]::ReadAllText((Join-Path $resolved '.glyphframe-portable-package-run')) -cne $run) { throw 'Unsafe fixture cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
