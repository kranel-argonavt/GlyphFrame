# Legal-file integrity and version binding, not legal-compliance certification.
function Get-LegalTextHash([string]$Path) {
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
}

function Assert-LegalPackage([string]$Root) {
    Assert-PortableDirectoryChain $Root
    $manifestPath = Join-Path $Root 'licenses/manifest.json'
    Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($manifestPath))
    if (([IO.File]::GetAttributes($manifestPath) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked legal manifest is forbidden.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.SchemaVersion -ne 1 -or $manifest.PublicDistributionApproved -isnot [bool] -or
        $manifest.PublicDistributionApproved -or @($manifest.OpenIssues).Count -eq 0) {
        throw 'Expected a version-1 provisional legal manifest with explicit unresolved distribution issues.'
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest.Files) {
        Assert-PortableRelativePath $file.Path
        if (($file.Path -cnotin @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'Tools/ffmpeg/LICENSE.txt', 'Tools/ffmpeg/DISTRIBUTION_README.txt')) -and
            $file.Path -cnotmatch '^licenses/.+\.(txt|md)$') { throw 'Unexpected legal-document path.' }
        if (-not $seen.Add($file.Path) -or $file.TextSha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Duplicate/invalid legal manifest entry.' }
        $path = Join-Path $Root $file.Path
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
        if (-not [IO.File]::Exists($path) -or [IO.FileInfo]::new($path).Length -eq 0 -or
            ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-LegalTextHash $path) -cne $file.TextSha256) { throw "Missing, empty, linked or changed legal document: $($file.Path)" }
    }
    foreach ($required in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'Tools/ffmpeg/LICENSE.txt', 'Tools/ffmpeg/DISTRIBUTION_README.txt')) {
        if (-not $seen.Contains($required)) { throw "Legal manifest omits $required" }
    }
    $deps = Get-Content -LiteralPath (Join-Path $Root 'GlyphFrame.App.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($component in $manifest.Components) {
        if (-not $keys.Add($component.Dependency) -or -not $deps.libraries.ContainsKey($component.Dependency) -or
            $deps.libraries[$component.Dependency].type -notin @('package', 'runtimepack') -or @($component.Documents).Count -eq 0) {
            throw 'Missing, duplicate or unversioned dependency in legal coverage.'
        }
        foreach ($document in $component.Documents) {
            if (-not $seen.Contains($document)) { throw 'Component references an unverified legal document.' }
        }
    }
    foreach ($library in $deps.libraries.GetEnumerator()) {
        if ($library.Value.type -in @('package', 'runtimepack') -and -not $keys.Contains($library.Key)) {
            throw "Dependency changed without legal review: $($library.Key)"
        }
    }
    return [pscustomobject]@{ Status='INCOMPLETE: public distribution not approved'; DocumentsVerified=$seen.Count; DependenciesBound=$keys.Count; OpenIssues=@($manifest.OpenIssues) }
}
