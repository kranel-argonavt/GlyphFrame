# Adopted component-scope integrity; not public approval or an auto-consent API.
function Read-NativeTermsText([string]$Root, [string]$Relative, [int]$Maximum) {
    $rootPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Root))
    $path = [IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
    if (-not $path.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Component document is outside the package.'
    }
    Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
    if (([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        [IO.FileInfo]::new($path).Length -le 0 -or [IO.FileInfo]::new($path).Length -gt $Maximum) {
        throw 'Missing, linked or oversized component document.'
    }
    return [IO.File]::ReadAllText($path, [Text.UTF8Encoding]::new($false, $true))
}

function Get-NativeTermsTextHash([string]$Text) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($Text.Replace("`r`n", "`n")))).ToLowerInvariant()
}

function Get-NativeTermsProfile([string]$Root) {
    # Match NativeTermsBundle.Load: do not use PowerShell coercion to validate JSON types.
    $ErrorActionPreference = 'Stop'
    Assert-PortableDirectoryChain $Root
    $profileText = Read-NativeTermsText $Root 'licenses/WindowsNative/profile.json' 65536
    $documents = [Collections.Generic.List[System.Text.Json.JsonDocument]]::new()
    try {
        $profileDocument = [System.Text.Json.JsonDocument]::Parse($profileText)
        $documents.Add($profileDocument)
        $manifestDocument = [System.Text.Json.JsonDocument]::Parse((Read-NativeTermsText $Root 'licenses/manifest.json' 262144))
        $documents.Add($manifestDocument)
        $runtimeDocument = [System.Text.Json.JsonDocument]::Parse((Read-NativeTermsText $Root 'GlyphFrame.App.runtimeconfig.json' 65536))
        $documents.Add($runtimeDocument)
        $profile = $profileDocument.RootElement
        $binding = $manifestDocument.RootElement.GetProperty('WindowsNativeTerms')
        $hash = Get-NativeTermsTextHash $profileText
        if ($profile.GetProperty('SchemaVersion').GetInt32() -ne 1 -or
            $profile.GetProperty('Purpose').GetString() -cne 'ComponentTermsAcceptance' -or
            $profile.GetProperty('Version').GetString() -cne '1.0' -or
            $profile.GetProperty('TermsPath').GetString() -cne 'licenses/WindowsNative/COMPONENT-TERMS.txt' -or
            $binding.GetProperty('ProfilePath').GetString() -cne 'licenses/WindowsNative/profile.json' -or
            $binding.GetProperty('ProfileTextSha256').GetString() -cne $hash -or
            $binding.GetProperty('Purpose').GetString() -cne 'ComponentTermsAcceptance' -or
            -not $binding.GetProperty('OperativeAgreementAdopted').GetBoolean()) {
            throw 'Unreviewed adopted native-terms profile.'
        }
        $frameworks = $profile.GetProperty('RuntimeFrameworks')
        $actualFrameworks = @($runtimeDocument.RootElement.GetProperty('runtimeOptions').GetProperty('includedFrameworks').EnumerateArray())
        if (@($frameworks.EnumerateObject()).Count -ne 2 -or $actualFrameworks.Count -ne 2) { throw 'Unreviewed runtime scope.' }
        foreach ($name in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
            $version = $frameworks.GetProperty($name).GetString()
            $entry = @($actualFrameworks | Where-Object {
                $_.GetProperty('name').GetString() -ceq $name -and $_.GetProperty('version').GetString() -ceq $version
            })
            if ($entry.Count -ne 1) { throw 'Runtime version changed since terms scope review.' }
        }
        $components = @($profile.GetProperty('Components').EnumerateArray())
        if ($components.Count -ne 5) { throw 'Incomplete Microsoft component scope.' }
        foreach ($name in @('coreclr.dll','PresentationNative_cor3.dll','vcruntime140_cor3.dll','wpfgfx_cor3.dll','D3DCompiler_47_cor3.dll')) {
            $entry = @($components | Where-Object { $_.GetProperty('Path').GetString() -ceq $name })
            if ($entry.Count -ne 1) { throw "Missing or duplicate native component: $name" }
            $file = Join-Path $Root $name
            if (([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                [IO.FileInfo]::new($file).Length -ne $entry[0].GetProperty('Length').GetInt64() -or
                (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -cne $entry[0].GetProperty('Sha256').GetString()) {
                throw "Unreviewed native component: $name"
            }
        }
        $terms = Read-NativeTermsText $Root 'licenses/WindowsNative/COMPONENT-TERMS.txt' 131072
        if ((Get-NativeTermsTextHash $terms) -cne $profile.GetProperty('TermsTextSha256').GetString() -or
            -not $terms.Contains('VERSION 1.0 — 29 OCTOBER 2026') -or $terms.Contains('NONOPERATIVE REVIEW DRAFT')) {
            throw 'Missing or changed adopted component terms.'
        }
        $references = @($profile.GetProperty('References').EnumerateArray())
        if ($references.Count -ne 3) { throw 'Incomplete native reference scope.' }
        foreach ($relative in @('licenses/dotnet/NET-LIBRARY-LICENSE.txt','licenses/WPF/WINDOWS-SDK-LICENSE.txt','licenses/dotnet/WINDOWS-LICENSE-MAPPING.txt')) {
            $entry = @($references | Where-Object { $_.GetProperty('Path').GetString() -ceq $relative })
            $text = Read-NativeTermsText $Root $relative 262144
            if ($entry.Count -ne 1 -or (Get-NativeTermsTextHash $text) -cne $entry[0].GetProperty('TextSha256').GetString()) {
                throw "Changed native reference: $relative"
            }
        }
        # Keep the callers' existing plain-object contract, not disposed JsonElement values.
        return [pscustomobject]@{ Identity=$hash; Purpose='ComponentTermsAcceptance'; Version='1.0';
            TermsPath='licenses/WindowsNative/COMPONENT-TERMS.txt'; Profile=($profileText | ConvertFrom-Json); OperativeAgreement=$true }
    }
    finally { foreach ($document in $documents) { $document.Dispose() } }
}

function Assert-NativeTermsPendingState([string]$WindowTitle, [bool]$AcknowledgementPresent, [bool]$MediaCachePresent) {
    if ($WindowTitle -cne 'Microsoft component terms — GlyphFrame' -or $AcknowledgementPresent -or $MediaCachePresent) {
        throw 'Fresh package did not remain at the valid, unaccepted component-terms gate before media work.'
    }
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
