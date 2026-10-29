# Inert fixtures only. Never vendor/legal assent, application startup, installation or network access.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')
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
    Write-Fixture 'licenses/WindowsNative/COMPONENT-TERMS.txt' 'NONOPERATIVE REVIEW DRAFT - synthetic helper fixture only'
    $profile = @{SchemaVersion=1;Purpose='ImplementationPreview';Version='synthetic';TermsPath='licenses/WindowsNative/COMPONENT-TERMS.txt';
        TermsTextSha256=(Get-LegalTextHash (Join-Path $payload 'licenses/WindowsNative/COMPONENT-TERMS.txt'));
        RuntimeFrameworks=@{'Microsoft.NETCore.App'='10.0.12';'Microsoft.WindowsDesktop.App'='10.0.12'};Components=$components;References=$references}
    Write-Fixture 'licenses/WindowsNative/profile.json' ($profile | ConvertTo-Json -Depth 8)
    Write-Fixture 'licenses/manifest.json' (@{WindowsNativeTerms=@{ProfilePath='licenses/WindowsNative/profile.json';
        ProfileTextSha256=(Get-LegalTextHash (Join-Path $payload 'licenses/WindowsNative/profile.json'));Purpose='ImplementationPreview';OperativeAgreementAdopted=$false}} | ConvertTo-Json -Depth 5)
    Write-Fixture 'GlyphFrame.App.runtimeconfig.json' '{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.12"},{"name":"Microsoft.WindowsDesktop.App","version":"10.0.12"}]}}'
}
try {
    Reset-Fixture
    Run-Check 'Complete synthetic scope has a stable nonoperative profile identity' {
        $scope=Get-NativeTermsProfile $payload
        Assert-Check ($scope.Purpose -ceq 'ImplementationPreview' -and -not $scope.OperativeAgreement -and $scope.Identity -cmatch '^[0-9a-f]{64}$') 'Incorrect scope.'
    }
    foreach ($relative in @('coreclr.dll','licenses/WindowsNative/profile.json','licenses/WindowsNative/COMPONENT-TERMS.txt','licenses/WPF/WINDOWS-SDK-LICENSE.txt','GlyphFrame.App.runtimeconfig.json')) {
        Run-Check "Changed scope rejected: $relative" {
            Write-Fixture $relative 'changed'; Assert-Throws { Get-NativeTermsProfile $payload }; Reset-Fixture
        }
    }
    Run-Check 'Synthetic fixture refuses unmarked and normal profile roots before writing' {
        Assert-Throws { New-IsolatedNativeTermsPreviewFixture $payload (Join-Path $root 'unmarked') }
        $local=[Environment]::GetFolderPath('LocalApplicationData')
        Assert-Throws { New-IsolatedNativeTermsPreviewFixture $payload $local }
        Assert-Throws { New-IsolatedNativeTermsPreviewFixture $payload (Join-Path $local 'GlyphFrame') }
    }
    Run-Check 'Marked isolated synthetic record uses the shared four-line UTC protocol' {
        $data=Join-Path $root 'marked-data'
        [IO.Directory]::CreateDirectory($data) | Out-Null
        [IO.File]::WriteAllText((Join-Path $data '.glyphframe-package-verification'),$run)
        $result=New-IsolatedNativeTermsPreviewFixture $payload $data
        $lines=[IO.File]::ReadAllLines($result.Path)
        Assert-Check ($lines.Count -eq 4 -and $lines[0] -ceq 'GlyphFrame.NativeTermsAcknowledgement.1' -and
            $lines[1] -ceq 'Purpose=ImplementationPreview' -and $lines[2] -ceq "Bundle=$((Get-NativeTermsProfile $payload).Identity)" -and
            $lines[3] -cmatch '^AcknowledgedUtc=\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$' -and $result.Kind.Contains('NOT human or legal assent')) 'Record protocol mismatch.'
        Assert-Throws { New-IsolatedNativeTermsPreviewFixture $payload $data }
        Assert-Check (-not [IO.Directory]::Exists((Join-Path $data 'GlyphFrame/Cache'))) 'Synthetic state started media work.'
    }
    Run-Check 'Installer review text contains the complete draft and all three inert references' {
        $output=Join-Path $root 'review.txt'
        $scope=Write-InstallerNativeTermsDocument $payload $output
        $text=[IO.File]::ReadAllText($output)
        Assert-Check ($text.Contains('NONOPERATIVE REVIEW DRAFT') -and [regex]::Matches($text,'Synthetic reference:').Count -eq 3) 'Incomplete document.'
        Assert-Check ($scope.Identity -ceq (Get-NativeTermsProfile $payload).Identity) 'Scope drift.'
        Assert-Throws { Write-InstallerNativeTermsDocument $payload $output }
    }
    Run-Check 'User acknowledgement is never allowed in a distributed payload' {
        Assert-Throws { Assert-PortableContentPath 'native-terms-acknowledgement.txt' }
        Assert-PortableContentPath 'licenses/WindowsNative/profile.json'
    }
    Write-Host "Native terms helper checks passed: $passed; failed: 0. Synthetic preview only."
}
finally {
    $resolved=[IO.Path]::GetFullPath($root)
    Assert-PortableDirectoryChain $resolved
    if ([IO.Path]::GetDirectoryName($resolved) -cne [IO.Path]::GetFullPath($parent) -or [IO.Path]::GetFileName($resolved) -cne $run -or
        [IO.File]::ReadAllText((Join-Path $resolved '.glyphframe-portable-package-run')) -cne $run) { throw 'Unsafe fixture cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
