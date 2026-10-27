# Distributed license texts

The root [LICENSE](../LICENSE) covers original GlyphFrame code by Nazar Khozin (kranel-argonavt). It does not relicense third-party components, upstream notices or assets owned by others. See [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) for the component index and open distribution issues.

This directory retains the license/notice texts of the recorded Windows dependencies and the Setup engine. Original text files are unchanged except CRLF/LF normalization and the final newline; they are not translated or rewritten. For vendor HTML terms, the visible body is extracted to plain text with paragraph whitespace normalized and link destinations retained; the original HTML byte hashes/URLs and extraction policy are recorded separately. `manifest.json` records source locations, versions and canonical text hashes. Hashes use UTF-8 without BOM after normalizing CRLF to LF, so normal Git line-ending conversion does not invalidate them.

- CommunityToolkit.Mvvm: 8.4.2 package License.md and ThirdPartyNotices.txt.
- SkiaSharp/Win32 native assets: 4.150.1 package LICENSE.txt and native THIRD-PARTY-NOTICES.txt.
- MetadataExtractor: full Apache license/attribution from its package-recorded source commit.
- dotnet: 10.0.12 runtime-pack MIT text/notices **plus .NET Library License for named Windows-native binaries** and Microsoft's mapping. The MIT text also applies to DependencyInjection/Abstractions 10.0.10; their package copyright is recorded in the notices index. Shared identical .NET notices are retained once.
- WPF: 10.0.12 desktop runtime-pack MIT text/project notices **plus Windows SDK License for D3DCompiler_47_cor3.dll**; other named native exceptions use .NET Library License. Runtime-pack MIT metadata does not override these binary-specific terms.
- InnoSetup: installed 7.1.0 compiler's license, applicable to the Setup engine, not to GlyphFrame code. The same collection travels in both package formats for consistency; the portable application does not require Inno Setup.
- XmpCore 6.1.10.1: recovered historical Adobe BSD-3-Clause text, Adobe source-header/.NET port attributions and a provenance record linking the archived terms to the .NET maintainer's accepted clarification. The license wording and actual dates are retained; the dependency is unchanged.

FFmpeg's original GPLv3 text and distributor README/checksums remain under `Tools/ffmpeg/` and are covered by the legal-file manifest. The mirror repository is not a source/build-script bundle.

**This is still an incomplete release-licensing set.** FFmpeg complete Corresponding Source and Windows-native end-user/distributor protective terms remain open. XmpCore's historical license-text evidence gap is resolved in this collection; that does not approve the complete application distribution. The owner confirmed current project visual provenance separately; that is not a blanket declaration about future imported assets. Local candidate builds are permitted by the engineering workflow, but a passing hash check is not legal approval or authorization to publish. Sources, versions and required documents must be reviewed together when dependencies/runtime packs change.
