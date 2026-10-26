# Synthetic legal-integrity regressions; no network, publish, app or installation.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')
$parent = Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.LegalTests'
$runId = [Guid]::NewGuid().ToString('N')
$root = New-PortableRunDirectory $parent $runId
$passed = 0
function Assert-Check([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Throws([scriptblock]$Action) {
    $caught = $false
    try { & $Action | Out-Null } catch { $caught = $true }
    Assert-Check $caught 'Expected legal verification to reject the fixture.'
}
function Run-Check([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:passed++
    Write-Host "PASS: $Name"
}
function Write-Fixture([string]$Relative, [string]$Text) {
    $path = Join-Path $root $Relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, $Text)
}
function Reset-Fixture {
    $script:manifest = [ordered]@{
        SchemaVersion=1; PublicDistributionApproved=$false; OpenIssues=@('Synthetic unresolved source issue')
        Components=@(@{Dependency='Example/1.2.3'; Documents=@('licenses/Example/LICENSE.txt')})
        Files=@(foreach ($path in @('LICENSE','THIRD_PARTY_NOTICES.md','Tools/ffmpeg/LICENSE.txt','Tools/ffmpeg/DISTRIBUTION_README.txt','licenses/Example/LICENSE.txt')) {
            Write-Fixture $path "Synthetic legal text`nCopyright fixture`n"
            @{ Path=$path; TextSha256=(Get-LegalTextHash (Join-Path $root $path)) }
        })
    }
    Write-Fixture 'licenses/manifest.json' ($script:manifest | ConvertTo-Json -Depth 6)
    Write-Fixture 'GlyphFrame.App.deps.json' '{"libraries":{"Example/1.2.3":{"type":"package"},"GlyphFrame.App/1.0.0":{"type":"project"}}}'
}
function Save-Manifest { Write-Fixture 'licenses/manifest.json' ($script:manifest | ConvertTo-Json -Depth 6) }
try {
    Reset-Fixture
    Run-Check 'Valid collection binds exact dependency and preserves unresolved status' {
        $result = Assert-LegalPackage $root
        Assert-Check ($result.DocumentsVerified -eq 5 -and $result.DependenciesBound -eq 1 -and $result.Status.StartsWith('INCOMPLETE')) 'False compliance claim or wrong counts.'
    }
    Run-Check 'Missing document fails' {
        [IO.File]::Delete((Join-Path $root 'licenses/Example/LICENSE.txt'))
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Empty document fails' {
        Write-Fixture 'licenses/Example/LICENSE.txt' ''
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Altered license text fails' {
        Write-Fixture 'LICENSE' 'Changed license'
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'CRLF checkout does not change canonical license identity' {
        Write-Fixture 'LICENSE' "Synthetic legal text`r`nCopyright fixture`r`n"
        [void](Assert-LegalPackage $root); Reset-Fixture
    }
    Run-Check 'Path traversal cannot escape the legal collection' {
        $script:manifest.Files[0].Path = '../outside.txt'; Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'User files cannot be selected by the legal manifest' {
        $script:manifest.Files[0].Path = 'settings.json'; Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Duplicate case-aliased documents fail' {
        $script:manifest.Files += @{ Path='license'; TextSha256=$script:manifest.Files[0].TextSha256 }; Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Required application license cannot be omitted from the manifest' {
        $script:manifest.Files = @($script:manifest.Files | Where-Object Path -CNE 'LICENSE'); Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'New runtime dependency requires legal review' {
        Write-Fixture 'GlyphFrame.App.deps.json' '{"libraries":{"Example/1.2.3":{"type":"package"},"New/1.0.0":{"type":"runtimepack"}}}'
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Changed dependency version requires legal review' {
        Write-Fixture 'GlyphFrame.App.deps.json' '{"libraries":{"Example/1.2.4":{"type":"package"}}}'
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Duplicate dependency coverage fails' {
        $script:manifest.Components += $script:manifest.Components[0]; Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Component cannot reference an unverified document' {
        $script:manifest.Components[0].Documents = @('licenses/absent.txt'); Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Provisional builds cannot claim public approval' {
        $script:manifest.PublicDistributionApproved = $true; Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Open distribution issues cannot disappear silently' {
        $script:manifest.OpenIssues = @(); Save-Manifest
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Malformed manifest cannot pass' {
        Write-Fixture 'licenses/manifest.json' 'invalid'
        Assert-Throws { Assert-LegalPackage $root }; Reset-Fixture
    }
    Run-Check 'Product payload accepts only the specific legal JSON and root LICENSE' {
        Assert-PortableFilePath 'LICENSE'
        Assert-PortableFilePath 'licenses/manifest.json'
        Assert-Throws { Assert-PortableFilePath 'licenses/settings.json' }
        Assert-Throws { Assert-PortableFilePath 'other/manifest.json' }
    }
    Write-Host "Legal helper checks passed: $passed; failed: 0. No network, publish or app launch."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    Assert-PortableDirectoryChain $resolved
    if ([IO.Path]::GetDirectoryName($resolved) -cne [IO.Path]::GetFullPath($parent) -or
        [IO.Path]::GetFileName($resolved) -cne $runId -or
        [IO.File]::ReadAllText((Join-Path $resolved '.glyphframe-portable-package-run')) -cne $runId) { throw 'Unsafe legal-test cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
