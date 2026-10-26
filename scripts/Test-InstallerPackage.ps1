# Deterministic policy/manifest tests; no compiler, publish, registry writes or app launch.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'InstallerPackage.Helpers.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$parent=Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.InstallerTests'
$run=[Guid]::NewGuid().ToString('N')
$root=New-PortableRunDirectory $parent $run
$passed=0
function Assert-Check([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Assert-Throws([scriptblock]$Action){$caught=$false;try{& $Action|Out-Null}catch{$caught=$true};Assert-Check $caught 'Expected rejection.'}
function Run-Check([string]$Name,[scriptblock]$Action){& $Action;$script:passed++;Write-Host "PASS: $Name"}
try {
    # Read-only run-identity checks need no real application/compiler fixture.
    $identityRun=[Guid]::NewGuid().ToString('N')
    $identityRoot=New-PortableRunDirectory $root $identityRun
    $identityReport=Join-Path $identityRoot 'report.json'
    $completed=[ordered]@{Success=$true;ArchiveValidation=@{Success=$true};PublishVerification=@{Success=$true};PackageVersion='1.0.0'}
    $completed | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $identityReport
    foreach($case in @(
        @{Name='Canonical package directory';Suffix=''},
        @{Name='Trailing backslash';Suffix='\'},
        @{Name='Trailing forward slash';Suffix='/'},
        @{Name='Repeated trailing separators';Suffix='\\'},
        @{Name='Dot-directory with trailing separator';Suffix='\.\'}
    )) {
        Run-Check ($case.Name+' retains marked package identity') {
            $identity=Get-InstallerPackageIdentity ($identityRoot+$case.Suffix)
            Assert-Check ($identity.Root -ceq $identityRoot -and $identity.Report.PackageVersion -ceq '1.0.0') 'Package path was not canonicalized.'
        }
    }
    Run-Check 'Normalized path still rejects a mismatched run marker' {
        $marker=Join-Path $identityRoot '.glyphframe-portable-package-run'
        try {
            [IO.File]::WriteAllText($marker,[Guid]::NewGuid().ToString('N'))
            Assert-Throws {Get-InstallerPackageIdentity ($identityRoot+'\')}
        }
        finally {[IO.File]::WriteAllText($marker,$identityRun)}
    }
    foreach($flag in @('Success','ArchiveValidation','PublishVerification')) {
        Run-Check ("Normalized path still rejects failed $flag") {
            $failed=$completed | ConvertTo-Json -Depth 3 | ConvertFrom-Json
            if($flag -ceq 'Success'){$failed.Success=$false}else{$failed.$flag.Success=$false}
            try {
                $failed | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $identityReport
                Assert-Throws {Get-InstallerPackageIdentity ($identityRoot+'/')}
            }
            finally {$completed | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $identityReport}
        }
    }
    $entry=[pscustomobject]@{Path='nested/Example.dll';Length=10;Sha256=('a'*64)}
    Run-Check 'Exact manifest matches' {Assert-InstallerPayloadMatchesManifest @($entry) @($entry)}
    Run-Check 'Empty payload rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @() @()}}
    Run-Check 'Missing/extra payload rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @($entry,$entry) @($entry)}}
    Run-Check 'Hash mismatch rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @($entry) @([pscustomobject]@{Path=$entry.Path;Length=10;Sha256=('b'*64)})}}
    Run-Check 'Length mismatch rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @($entry) @([pscustomobject]@{Path=$entry.Path;Length=11;Sha256=$entry.Sha256})}}
    Run-Check 'Duplicate/case aliases rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @($entry,$entry) @($entry,$entry)}}
    Run-Check 'Path traversal rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @($entry) @([pscustomobject]@{Path='../Example.dll';Length=10;Sha256=$entry.Sha256})}}
    Run-Check 'Development/user content rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @($entry) @([pscustomobject]@{Path='settings.json';Length=10;Sha256=$entry.Sha256})}}
    Run-Check 'Malformed digest rejected' {Assert-Throws {Assert-InstallerPayloadMatchesManifest @($entry) @([pscustomobject]@{Path=$entry.Path;Length=10;Sha256='invalid'})}}
    Run-Check 'Explicit file include uses no wildcard and handles spaces/Unicode' {
        $include=Join-Path $root 'files.iss'
        $payload=Join-Path $root 'Unicode каталог'
        Write-InstallerFileList $payload @($entry) $include
        $text=[IO.File]::ReadAllText($include)
        Assert-Check ($text.Contains($payload) -and $text.Contains('DestDir: "{app}\nested"') -and -not $text.Contains('*')) 'Unsafe or incorrect include.'
        Assert-Throws {Write-InstallerFileList $payload @($entry) $include}
    }
    $policy=[IO.File]::ReadAllText((Join-Path $repo 'installer/GlyphFrame.iss'))
    Run-Check 'Stable non-versioned identity and exact Publisher' {
        Assert-Check ($policy.Contains('9A07BED0-6720-4A06-8C95-317B14C580A0') -and $policy.Contains('AppPublisher=Nazar Khozin (kranel-argonavt)') -and $policy.Contains('https://github.com/kranel-argonavt')) 'Publisher/identity policy missing.'
    }
    Run-Check 'Per-user x64 installation, no elevation override' {
        Assert-Check ($policy.Contains('PrivilegesRequired=lowest') -and $policy.Contains('ArchitecturesAllowed=x64os') -and $policy.Contains('DefaultDirName={localappdata}\Programs\GlyphFrame') -and -not $policy.Contains('PrivilegesRequiredOverridesAllowed=')) 'Install scope mismatch.'
    }
    Run-Check 'English only and desktop opt-in' {
        Assert-Check ($policy.Contains('Name: "english"') -and $policy.Contains('Description: "Create a desktop shortcut"; Flags: unchecked')) 'Language/shortcut mismatch.'
    }
    Run-Check 'No unbounded uninstall cleanup or auto startup/associations' {
        Assert-Check ($policy -notmatch '(?im)^\[(UninstallDelete|Registry|UninstallRun)\]' -and $policy -notmatch '(?im)^Changes(Associations|Environment)=' -and -not $policy.Contains('userstartup')) 'Unsafe extra install/uninstall behavior.'
    }
    Run-Check 'Running-app interlock and downgrade guard without forced closure' {
        Assert-Check ($policy -notmatch '(?im)^AppMutex=' -and $policy.Contains('function PrepareToInstall') -and $policy.Contains('CheckForMutexes(InstallationMutex') -and $policy.Contains('GetSHA256OfUnicodeString') -and $policy.Contains('CloseApplications=no') -and $policy.Contains('InitializeUninstall') -and $policy.Contains('PreviousVersion > NextVersion')) 'Lifetime/upgrade policy missing or early app-directory expansion restored.'
    }
    Run-Check 'Missing compiler rejected instead of downloading' {Assert-Throws {Get-InstallerCompiler (Join-Path $root 'absent.exe')}}
    Run-Check 'Multi-size shell asset is wired to EXE, Setup and both shortcuts' {
        $icon=Join-Path $repo 'src/GlyphFrame.App/Assets/GlyphFrame.ico'
        Assert-Check (@(Get-ShellIconFrames $icon).Count -eq 9) 'Shell sizes missing.'
        $project=[IO.File]::ReadAllText((Join-Path $repo 'src/GlyphFrame.App/GlyphFrame.App.csproj'))
        Assert-Check ($project.Contains('<ApplicationIcon>Assets\GlyphFrame.ico</ApplicationIcon>') -and $policy.Contains('SetupIconFile=..\src\GlyphFrame.App\Assets\GlyphFrame.ico') -and ([regex]::Matches($policy, 'IconFilename: "\{app\}\\GlyphFrame\.App\.exe"; IconIndex: 0')).Count -eq 2) 'Shell asset wiring missing.'
    }
    Run-Check 'Non-PE file cannot pass embedded shell-resource validation' {
        Assert-Throws {Assert-ExecutableShellIcon (Join-Path $repo 'README.md') (Join-Path $repo 'src/GlyphFrame.App/Assets/GlyphFrame.ico')}
    }
    Write-Host "Installer helper checks passed: $passed; failed: 0. No publish, install or app launch performed."
}
finally {
    $resolved=[IO.Path]::GetFullPath($root)
    Assert-PortableDirectoryChain $resolved
    if([IO.Path]::GetDirectoryName($resolved) -cne [IO.Path]::GetFullPath($parent) -or [IO.Path]::GetFileName($resolved) -cne $run -or [IO.File]::ReadAllText((Join-Path $resolved '.glyphframe-portable-package-run')) -cne $run){throw 'Unsafe test cleanup target.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
