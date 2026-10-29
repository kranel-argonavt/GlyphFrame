# Local unsigned per-user Setup, from exactly the verified portable payload. No install/upload.
[CmdletBinding()]
param([string]$CompilerPath, [string]$PortablePackageDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 on Windows is required.' }
. (Join-Path $PSScriptRoot 'InstallerPackage.Helpers.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$compiler = Get-InstallerCompiler $CompilerPath
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = New-PortableRunDirectory (Join-Path $repo 'artifacts/installer-packages') $runId
$success = $false
$failure = $null
$payload = $null
$setup = $null
$hash = $null
$initialHead = (& git -C $repo rev-parse HEAD)
$initialStatus = @(& git -C $repo status --porcelain=v1 --untracked-files=all)
try {
    if (-not $PortablePackageDirectory) {
        & (Join-Path $PSScriptRoot 'Build-PortablePackage.ps1') -RunId $runId | Out-Host
        $PortablePackageDirectory = Join-Path $repo "artifacts/portable-packages/$runId"
    }
    $payload = Get-VerifiedInstallerPayload $PortablePackageDirectory
    $include = Join-Path $runRoot 'payload-files.iss'
    Write-InstallerFileList $payload.Root $payload.Files $include
    $termsFile = Join-Path $runRoot 'native-component-terms.txt'
    $noticesFile = Join-Path $runRoot 'native-component-notices.txt'
    $termsScope = Write-InstallerNativeTermsDocument $payload.Root $termsFile
    Write-InstallerNativeNoticesDocument $payload.Root $noticesFile
    $arguments = @('--quiet-progress', '--no-ide-signtools', '--no-signing', "--output-dir=$runRoot", "--define=ProductVersion=$($payload.Report.PackageVersion)", "--define=PayloadFiles=$include",
        "--define=NativeTermsFile=$termsFile", "--define=NativeNoticesFile=$noticesFile", "--define=NativeTermsBundle=$($termsScope.Identity)", (Join-Path $repo 'installer/GlyphFrame.iss'))
    & $compiler @arguments 2>&1 | Tee-Object -FilePath (Join-Path $runRoot 'compile.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed: $LASTEXITCODE" }
    $setup = Join-Path $runRoot "GlyphFrame-$($payload.Report.PackageVersion)-win-x64-Setup.exe"
    if (-not [IO.File]::Exists($setup)) { throw 'Compiler did not create the expected Setup.' }
    Assert-ExecutableShellIcon $setup (Join-Path $repo 'src/GlyphFrame.App/Assets/GlyphFrame.ico')
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($setup)
    # Inno pads string version resources; compare trimmed Publisher and numeric version.
    $numericVersion = '{0}.{1}.{2}.{3}' -f $version.FileMajorPart, $version.FileMinorPart, $version.FileBuildPart, $version.FilePrivatePart
    if ($version.CompanyName.Trim() -cne 'Nazar Khozin (kranel-argonavt)' -or $numericVersion -cne "$($payload.Report.PackageVersion).0") { throw 'Setup Publisher/version mismatch.' }
    Assert-InstallerPayloadMatchesManifest @(Get-PortablePayload $payload.Root) $payload.Files
    if ((Get-NativeTermsProfile $payload.Root).Identity -cne $termsScope.Identity) { throw 'Terms scope changed during compilation.' }
    $hash = Write-PortableChecksum $setup ($setup + '.sha256')
    $success = $true
}
catch { $failure = $_.Exception.ToString() }
finally {
    @{ Success=$success; Failure=$failure; SourceCommit=$initialHead; WorkingTreeDirty=($initialStatus.Count -ne 0); WorkingTreeStatus=$initialStatus;
       Compiler=$compiler; CompilerVersion=(& $compiler '--version' | Out-String).Trim();
       Publisher='Nazar Khozin (kranel-argonavt)'; PublisherUrl='https://github.com/kranel-argonavt'; AppId='9A07BED0-6720-4A06-8C95-317B14C580A0';
       Architecture='x64 Windows only'; Language='English'; InstallMode='Per-user, no elevation requested'; Signed=$false;
       PortablePackageDirectory=$PortablePackageDirectory; PortableReport=if($payload){$payload.Report}else{$null};
       PayloadFileCount=if($payload){@($payload.Files).Count}else{0}; SetupPath=$setup; SetupSha256=$hash;
       NativeInstallUpgradeUninstallAcceptance='NOT TESTED by compilation'; PublicRelease='NOT CREATED';
       Licensing=if($payload){$payload.Licensing}else{$null};
       NativeTerms=if($payload){$payload.NativeTerms | Select-Object Identity,Purpose,Version,OperativeAgreement}else{$null};
       Retention='Run-owned build outputs and failures retained; no automatic compiler download/install or cleanup';
       FinalSourceCommit=(& git -C $repo rev-parse HEAD); FinalWorkingTreeStatus=@(& git -C $repo status --porcelain=v1 --untracked-files=all) } |
        ConvertTo-Json -Depth 13 | Set-Content -LiteralPath (Join-Path $runRoot 'report.json')
}
Write-Host "Installer report: $(Join-Path $runRoot 'report.json')"
if (-not $success) { throw "Installer build failed: $failure" }
Write-Host "PASS: Unsigned Setup built from $(@($payload.Files).Count) verified portable files"
Write-Host "Setup: $setup"
Write-Host "SHA-256: $hash"
