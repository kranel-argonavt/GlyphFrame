# Legal-file integrity and version binding, not legal-compliance certification.
function Get-LegalTextHash([string]$Path) {
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()
}

function Get-ControlledToolLegalPaths {
    return @('Tools/ffmpeg/LICENSE.txt','Tools/ffmpeg/DISTRIBUTION_README.txt',
        'licenses/FFmpeg/FFMPEG-LICENSE.txt','licenses/FFmpeg/X264-COPYING.txt','licenses/FFmpeg/ZLIB-LICENSE.txt',
        'licenses/FFmpeg/GCC-COPYING3.txt','licenses/FFmpeg/GCC-RUNTIME-EXCEPTION.txt',
        'licenses/FFmpeg/MINGW-COPYING.txt','licenses/FFmpeg/MINGW-RUNTIME.txt','licenses/FFmpeg/MINGW-SCOPE.txt',
        'licenses/FFmpeg/WINPTHREADS-COPYING.txt','licenses/FFmpeg/IJG-NOTICES.txt')
}

# Separate from NuGet dependency coverage: binds the actual CLI pair to its documents.
function Assert-ControlledToolLegalBinding([string]$Root, $Pins) {
    Assert-PortableDirectoryChain $Root
    $manifest=Get-Content -LiteralPath (Join-Path $Root 'licenses/manifest.json') -Raw | ConvertFrom-Json
    if (-not $manifest.PSObject.Properties['ControlledFfmpeg']) { throw 'Missing controlled-tool legal binding.' }
    $tool=$manifest.ControlledFfmpeg
    if ($tool.Version -cne '9.0-glyphframe.2' -or
        $tool.ArchiveFile -cne 'GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip' -or
        $tool.ArchiveSha256 -cne $Pins[$tool.ArchiveFile] -or
        $tool.FfmpegSha256 -cne $Pins['ffmpeg.exe'] -or $tool.FfprobeSha256 -cne $Pins['ffprobe.exe'] -or
        $tool.SourceMaterialsFile -cne 'GlyphFrame-ffmpeg-9.0-glyphframe.2-build-materials.zip' -or
        $tool.SourceMaterialsSha256 -cne 'c2172927ec454ead9eed21cdc6d9be1904816fef085eb539492c3d54a035f5aa') {
        throw 'Controlled version/binary/source-material legal identity differs from reviewed pins.'
    }
    $required=@(Get-ControlledToolLegalPaths)
    if (@($tool.Documents).Count -ne $required.Count -or
        @(Compare-Object ($required|Sort-Object) @($tool.Documents|Sort-Object)).Count) { throw 'Incomplete controlled-tool notice set.' }
    foreach ($document in $required) {
        $entry=@($manifest.Files | Where-Object Path -CEQ $document)
        $path=Join-Path $Root $document
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
        if ($entry.Count -ne 1 -or -not [IO.File]::Exists($path) -or
            ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-LegalTextHash $path) -cne $entry[0].TextSha256) { throw 'Unverified controlled-tool legal document.' }
    }
    foreach ($name in @('ffmpeg.exe','ffprobe.exe')) {
        $path=Join-Path $Root "Tools/ffmpeg/$name"
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
        if (-not [IO.File]::Exists($path) -or ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $Pins[$name]) { throw 'Controlled legal binding does not match actual tool bytes.' }
    }
    return [pscustomobject]@{Version=$tool.Version;DocumentsVerified=$required.Count;SourceDelivery='Local material identity only; public hosting/distribution approval remains open'}
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
