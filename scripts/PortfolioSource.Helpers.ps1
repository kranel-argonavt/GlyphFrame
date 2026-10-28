# Source-only export policy. Does not inspect/rewrite Git history or publish anything.
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')

function Assert-PortfolioSourcePath([string]$Path) {
    Assert-PortableRelativePath $Path
    $parts = $Path.Split('/')
    foreach ($part in $parts) {
        if ($part -match '^(?i:artifacts|bin|obj|\.git|\.vs|\.idea|\.vscode|logs|cache|temp|tmp|testresults|publish|dist|releases)$') { throw "Private/generated source-export content: $Path" }
    }
    if ($parts.Count -eq 1) {
        if ($Path -cnotin @('.gitignore','.gitattributes','LICENSE','README.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','GlyphFrame.sln')) { throw "Unexpected root source file: $Path" }
        return
    }
    if ($parts[0] -cnotin @('src','tests','scripts','docs','installer','Tools','licenses')) { throw "Unexpected source directory: $Path" }
    if ($Path -notmatch '\.(?i:cs|csproj|sln|props|targets|ps1|md|txt|json|xaml|manifest|config|ico|png|jpe?g|webp|bmp|svg|mp4|mov|mkv|avi|wav|iss)$' -and
        $Path -cnotmatch '^scripts/ffmpeg/[a-z0-9-]+\.sh$' -and
        $Path -cnotmatch '^scripts/ffmpeg/patches/[0-9]{4}-[a-z0-9-]+\.patch$' -and
        $Path -cne 'scripts/ffmpeg/tests/vvc-weighted-scaled.c') { throw "Executable/archive/unknown source file: $Path" }
    if ($Path -match '\.(?i:ico|png|jpe?g|webp|bmp|svg|mp4|mov|mkv|avi|wav)$' -and
        $Path -notmatch '^(?:tests/GlyphFrame.Tests/Fixtures/|src/GlyphFrame.App/Assets/|docs/design_reference/|docs/screenshots/)') { throw "Media outside reviewed asset locations: $Path" }
    if ($parts[0] -ceq 'Tools' -and $Path -cnotin @('Tools/ffmpeg/README.md','Tools/ffmpeg/LICENSE.txt','Tools/ffmpeg/DISTRIBUTION_README.txt','Tools/ffmpeg/SHA256SUMS.txt')) { throw 'Only tool provenance/instructions belong in source publication.' }
}
