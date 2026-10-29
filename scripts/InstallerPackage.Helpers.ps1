# Installer policy and manifest helpers. No compiler download, publishing or installation.
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')
. (Join-Path $PSScriptRoot 'NativeTerms.Helpers.ps1')
. (Join-Path $PSScriptRoot 'ShellIcon.Helpers.ps1')
. (Join-Path $PSScriptRoot 'LocalTools.Helpers.ps1')

function Get-InstallerCompiler([string]$Path) {
    $candidates = if ($Path) { @($Path) } else {
        @((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/Inno Setup 7/ISCC.exe'),
          (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Inno Setup 7/ISCC.exe'))
    }
    foreach ($candidate in $candidates) {
        if ([IO.File]::Exists($candidate)) {
            $file = [Diagnostics.FileVersionInfo]::GetVersionInfo($candidate)
            if ($file.ProductName -notmatch 'Inno Setup') { throw 'An Inno Setup compiler is required.' }
            # ISCC 7.1.0's PE FileVersion is 0.0.0.0; the engine reports its real version.
            $engineVersion = (& $candidate '--version' 2>&1 | Out-String).Trim()
            if ($LASTEXITCODE -ne 0 -or $engineVersion -cnotmatch '^7\.\d+\.\d+$') { throw 'Inno Setup 7 ISCC is required.' }
            return [IO.Path]::GetFullPath($candidate)
        }
    }
    throw 'Inno Setup 7 compiler not found. Install it explicitly or pass -CompilerPath; no automatic downloads are performed.'
}

function Get-InstallerPackageIdentity([string]$PackageDirectory) {
    # Preserve drive/UNC roots while accepting normal folder paths with a final slash.
    $root = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($PackageDirectory))
    Assert-PortableDirectoryChain $root
    $report = Get-Content -LiteralPath (Join-Path $root 'report.json') -Raw | ConvertFrom-Json
    $run = [IO.File]::ReadAllText((Join-Path $root '.glyphframe-portable-package-run'))
    if ($run -cnotmatch '^[0-9a-f]{32}$' -or [IO.Path]::GetFileName($root) -cne $run -or
        -not $report.Success -or -not $report.ArchiveValidation.Success -or -not $report.PublishVerification.Success) { throw 'A completed, marked verified portable run is required.' }
    if ($report.PackageVersion -cnotmatch '^\d+\.\d+\.\d+$') { throw 'Invalid application version.' }
    return [pscustomobject]@{ Root=$root; Report=$report }
}

function Get-VerifiedInstallerPayload([string]$PackageDirectory) {
    $identity = Get-InstallerPackageIdentity $PackageDirectory
    $root = $identity.Root
    $report = $identity.Report
    $payloadRoot = Join-Path $root "GlyphFrame-$($report.PackageVersion)-win-x64"
    $expectedZip = Join-Path $root "GlyphFrame-$($report.PackageVersion)-win-x64-portable.zip"
    if ([IO.Path]::GetFullPath($report.ZipPath) -ine $expectedZip -or
        (Get-FileHash -LiteralPath $expectedZip).Hash.ToLowerInvariant() -cne $report.ZipSha256) { throw 'Portable ZIP identity mismatch.' }
    $manifest = @(Get-Content -LiteralPath (Join-Path $root 'payload-manifest.json') -Raw | ConvertFrom-Json)
    $actual = @(Get-PortablePayload $payloadRoot)
    Assert-InstallerPayloadMatchesManifest $actual $manifest
    Assert-PackageRequiredFiles $payloadRoot
    $legal = Assert-LegalPackage $payloadRoot
    $nativeTerms = Get-NativeTermsProfile $payloadRoot
    $null = Assert-ControlledToolLegalBinding $payloadRoot (Get-LocalToolPins (Join-Path $PSScriptRoot '../Tools/ffmpeg/SHA256SUMS.txt'))
    Assert-ExecutableShellIcon (Join-Path $payloadRoot 'GlyphFrame.App.exe') (Join-Path $PSScriptRoot '../src/GlyphFrame.App/Assets/GlyphFrame.ico')
    if ((Get-PortablePackageVersion (Join-Path $payloadRoot 'GlyphFrame.App.dll')) -cne $report.PackageVersion) { throw 'Assembly/package version mismatch.' }
    # Reject pre-installer application builds: their running-process guard is absent.
    if (-not [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $payloadRoot 'GlyphFrame.App.dll'))).Contains('InstallationLifetimeMarker')) {
        throw 'This application predates the installation lifetime protocol; build a fresh portable candidate.'
    }
    if (-not [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $payloadRoot 'GlyphFrame.App.dll'))).Contains('NativeTermsStartupGate')) {
        throw 'This application predates the native-terms startup protocol; build a fresh portable candidate.'
    }
    return [pscustomobject]@{ Root=$payloadRoot; Files=$actual; Report=$report; PackageDirectory=$root; Licensing=$legal; NativeTerms=$nativeTerms }
}

function Assert-InstallerPayloadMatchesManifest($Actual, $Manifest) {
    if (@($Actual).Count -eq 0 -or @($Actual).Count -ne @($Manifest).Count) { throw 'Payload/manifest count mismatch.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $Manifest) {
        Assert-PortableFilePath $entry.Path
        if (-not $seen.Add($entry.Path) -or $entry.Sha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Duplicate/invalid manifest entry.' }
        $match = @($Actual | Where-Object Path -CEQ $entry.Path)
        if ($match.Count -ne 1 -or $match[0].Length -ne $entry.Length -or $match[0].Sha256 -cne $entry.Sha256) { throw "Payload hash/length/path mismatch: $($entry.Path)" }
    }
}

function Write-InstallerFileList([string]$Root, $Files, [string]$Output) {
    if (Test-Path -LiteralPath $Output) { throw 'Refuse to overwrite generated installer include.' }
    $lines = @($Files | ForEach-Object {
        Assert-PortableFilePath $_.Path
        $source = [IO.Path]::GetFullPath((Join-Path $Root $_.Path))
        if ($source.Contains('"')) { throw 'Quoted source path cannot be represented safely.' }
        $relativeParent = [IO.Path]::GetDirectoryName($_.Path.Replace('/', '\'))
        $destination = if ($relativeParent) { '{app}\' + $relativeParent } else { '{app}' }
        'Source: "' + $source + '"; DestDir: "' + $destination + '"; Flags: ignoreversion'
    })
    [IO.File]::WriteAllLines($Output, $lines, [Text.UTF8Encoding]::new($false))
}
