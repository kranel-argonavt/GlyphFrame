# Fixed-scope implementation-preview integrity; never a legal approval or general auto-consent API.
function Get-NativeTermsProfile([string]$Root) {
    Assert-PortableDirectoryChain $Root
    $profilePath = Join-Path $Root 'licenses/WindowsNative/profile.json'
    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    $manifest = Get-Content -LiteralPath (Join-Path $Root 'licenses/manifest.json') -Raw | ConvertFrom-Json
    $hash = Get-LegalTextHash $profilePath
    if ($profile.SchemaVersion -ne 1 -or $profile.Purpose -cne 'ImplementationPreview' -or
        $profile.TermsPath -cne 'licenses/WindowsNative/COMPONENT-TERMS.txt' -or
        $manifest.WindowsNativeTerms.ProfilePath -cne 'licenses/WindowsNative/profile.json' -or
        $manifest.WindowsNativeTerms.ProfileTextSha256 -cne $hash -or
        $manifest.WindowsNativeTerms.Purpose -cne 'ImplementationPreview' -or
        $manifest.WindowsNativeTerms.OperativeAgreementAdopted) { throw 'Unreviewed native-terms implementation profile.' }
    if ((Get-LegalTextHash (Join-Path $Root $profile.TermsPath)) -cne $profile.TermsTextSha256 -or
        -not [IO.File]::ReadAllText((Join-Path $Root $profile.TermsPath)).Contains('NONOPERATIVE REVIEW DRAFT')) {
        throw 'Missing or changed preview terms.'
    }
    $names = @('coreclr.dll','PresentationNative_cor3.dll','vcruntime140_cor3.dll','wpfgfx_cor3.dll','D3DCompiler_47_cor3.dll')
    if (@($profile.Components).Count -ne 5) { throw 'Incomplete Microsoft component scope.' }
    foreach ($name in $names) {
        $entry = @($profile.Components | Where-Object Path -CEQ $name)
        $file = Join-Path $Root $name
        if ($entry.Count -ne 1 -or -not [IO.File]::Exists($file) -or
            [IO.FileInfo]::new($file).Length -ne $entry[0].Length -or
            (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -cne $entry[0].Sha256) { throw "Unreviewed native component: $name" }
    }
    $references = @('licenses/dotnet/NET-LIBRARY-LICENSE.txt','licenses/WPF/WINDOWS-SDK-LICENSE.txt','licenses/dotnet/WINDOWS-LICENSE-MAPPING.txt')
    if (@($profile.References).Count -ne 3) { throw 'Incomplete native reference scope.' }
    foreach ($relative in $references) {
        $entry = @($profile.References | Where-Object Path -CEQ $relative)
        if ($entry.Count -ne 1 -or (Get-LegalTextHash (Join-Path $Root $relative)) -cne $entry[0].TextSha256) { throw "Changed native reference: $relative" }
    }
    $runtime = Get-Content -LiteralPath (Join-Path $Root 'GlyphFrame.App.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (@($runtime.runtimeOptions.includedFrameworks).Count -ne 2) { throw 'Unreviewed runtime scope.' }
    foreach ($name in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
        $entry = @($runtime.runtimeOptions.includedFrameworks | Where-Object name -CEQ $name)
        if ($entry.Count -ne 1 -or $entry[0].version -cne $profile.RuntimeFrameworks.$name) { throw 'Runtime version changed since terms scope review.' }
    }
    return [pscustomobject]@{ Identity=$hash; Purpose='ImplementationPreview'; Version=$profile.Version; TermsPath=$profile.TermsPath; Profile=$profile; OperativeAgreement=$false }
}

function New-IsolatedNativeTermsPreviewFixture([string]$PackageRoot, [string]$DataRoot) {
    # Synthetic state only for the existing marked verifier's disposable root. Never ordinary user data.
    $local = [IO.Path]::GetFullPath([Environment]::GetFolderPath('LocalApplicationData'))
    $normal = Join-Path $local 'GlyphFrame'
    $data = [IO.Path]::GetFullPath($DataRoot)
    $marker = Join-Path $data '.glyphframe-package-verification'
    if (-not [IO.Path]::IsPathFullyQualified($DataRoot) -or $data -ieq $local -or $data -ieq $normal -or
        $data.StartsWith($normal + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.File]::Exists($marker) -or [IO.File]::ReadAllText($marker) -cnotmatch '^[0-9a-f]{32}$') {
        throw 'Synthetic preview fixture requires a marked isolated package-verification profile.'
    }
    Assert-PortableDirectoryChain $data
    $scope = Get-NativeTermsProfile $PackageRoot
    if ($scope.Purpose -cne 'ImplementationPreview' -or $scope.OperativeAgreement) { throw 'No synthetic operative agreement is permitted.' }
    $folder = Join-Path $data 'GlyphFrame'
    Assert-PortableDirectoryChain $folder
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $path = Join-Path $folder 'native-terms-acknowledgement.txt'
    if ([IO.File]::Exists($path)) { throw 'Do not overwrite a prior acknowledgement fixture.' }
    $time = [DateTime]::UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",[Globalization.CultureInfo]::InvariantCulture)
    $text = "GlyphFrame.NativeTermsAcknowledgement.1`nPurpose=ImplementationPreview`nBundle=$($scope.Identity)`nAcknowledgedUtc=$time`n"
    $stream = [IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $bytes=[Text.Encoding]::UTF8.GetBytes($text); $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
    return [pscustomobject]@{ Kind='Synthetic nonoperative preview fixture, NOT human or legal assent'; Path=$path; Bundle=$scope.Identity; Purpose=$scope.Purpose }
}

function Write-InstallerNativeTermsDocument([string]$Root, [string]$Output) {
    $scope = Get-NativeTermsProfile $Root
    if ([IO.File]::Exists($Output)) { throw 'Refuse to overwrite a generated installer terms document.' }
    $text = [IO.File]::ReadAllText((Join-Path $Root $scope.TermsPath))
    foreach ($reference in $scope.Profile.References) {
        $text += "`r`n`r`n===== Full reference: $($reference.Path) =====`r`n`r`n" + [IO.File]::ReadAllText((Join-Path $Root $reference.Path))
    }
    [IO.File]::WriteAllText($Output,$text,[Text.UTF8Encoding]::new($true))
    return $scope
}

function Write-InstallerNativeNoticesDocument([string]$Root, [string]$Output) {
    $null = Assert-LegalPackage $Root
    if ([IO.File]::Exists($Output)) { throw 'Refuse to overwrite a generated notices document.' }
    $manifest = Get-Content -LiteralPath (Join-Path $Root 'licenses/manifest.json') -Raw | ConvertFrom-Json
    $text = "Independent component licenses and source information. This viewer does not grant or record acknowledgement.`r`n"
    foreach ($entry in $manifest.Files) {
        $text += "`r`n===== $($entry.Path) =====`r`n`r`n" + [IO.File]::ReadAllText((Join-Path $Root $entry.Path))
    }
    [IO.File]::WriteAllText($Output,$text,[Text.UTF8Encoding]::new($true))
}
