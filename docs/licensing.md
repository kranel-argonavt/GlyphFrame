# Licensing and distribution

## Original code and author

Original GlyphFrame code is licensed under the root [MIT License](../LICENSE), Copyright (c) 2026 Nazar Khozin (kranel-argonavt). Author profile: <https://github.com/kranel-argonavt>. MIT permits reuse, modification, redistribution and commercial use subject to its notice requirements and disclaimer. It is not a prohibition on selling copies.

The license does not relicense third-party components, their notices, or assets owned by others. The author confirmed that the current project-specific design/branding/reference are the project's own work; see [owner-reported visual provenance](design_reference/README.md). This closes the current-asset provenance question within that reported scope, not ownership of unrelated/future portfolio media. Native shell branding is generated from the in-app geometry; selected system fonts are not bundled.

The portfolio gallery has a separate [media provenance record](screenshots/README.md). Its AI-generated image and owner-reported Pixabay footage must not be conflated with ownership of the application branding or relicensed wholesale under the code's MIT license. Preparing a local transformed-video example does not establish that any standalone redistribution is permitted.

## Runtime license collection

[THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) is the component index. [licenses/README.md](../licenses/README.md) explains the retained texts, shared documents and normalization policy. About displays the root application license and the indexed local texts without executing their contents or downloading anything. GitHub opens only after activating the separate author-profile action.

`licenses/manifest.json` binds each non-project dependency in the self-contained Windows `GlyphFrame.App.deps.json` to its exact version and verified documents. This includes transitive XmpCore, DependencyInjection.Abstractions and .NET/WPF runtime packs, not just direct project references. A dependency/runtime update requires a fresh license review; do not relabel old notices for a new version.

Text SHA-256 uses UTF-8 without BOM with CRLF normalized to LF. Original upstream byte hashes and source locations are recorded separately. This allows normal Git line-ending conversion without rewriting license wording. Whole upstream notices are retained conservatively; shared identical .NET notices need not be duplicated for each referencing component.

The Windows runtime is **not wholly MIT**, even though the source repositories/runtime-pack metadata carry MIT text. Microsoft's [Windows binary mapping](https://github.com/dotnet/core/blob/6c25b9da384e8d8a16fd12c79da8a8aa6bdfc6a1/license-information-windows.md) assigns `coreclr.dll`, `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll` and `wpfgfx_cor3.dll` to the .NET Library License, and `D3DCompiler_47_cor3.dll` to the Windows SDK License. All five names occur in the current payload. The vendor terms and mapping are now retained alongside the MIT/third-party texts; do not infer runtime binary rights solely from the NuGet license expression. This corrects an omission in the initial audit/collection.

Publish, portable staging and Setup payload reuse enforce required documents, canonical text hashes and exact dependency coverage. Missing/empty/changed texts, unsafe paths, duplicates and unreviewed dependency versions fail verification. A passing integrity check is **not** a legal-compliance certificate. Local candidate reports explicitly retain incomplete licensing status and the open issues.

## Source-only portfolio scope

The portfolio publication scope is original source code, project-owned assets, synthetic test fixtures, build/installer scripts and retained notices. It excludes FFmpeg executables, restored NuGet packages, .NET/native runtime binaries and Setup/ZIP outputs. Dependencies are acquired separately by the developer; no third-party package implementation is vendored as application source. Keeping dependency references does not resolve their terms or authorize distributing their compiled contents.

Deleting the current EXEs does **not** remove earlier Git objects containing them. The original repository/history and local candidate packages remain private. The [publication plan](portfolio.md#planned-route-preserve-development-history-in-a-reviewed-copy) now has a locally integrated controlled FFmpeg replacement; the separate history-preserving copy still needs sanitization and verification. The existing history-free source snapshot remains a conservative fallback, not evidence that the original history is publishable. Source preparation does not certify patent clearance, every upstream claim, or an eventual binary release.

## Open binary-distribution gates

**Do not publish Setup/ZIP or historical executable payloads as a cleared release yet.** The manifest's `PublicDistributionApproved=false` refers to these local binary candidates, not a license prohibition on the project's original source.

1. **FFmpeg:** local builds now use the controlled `9.0-glyphframe.2` pair, not the former Gyan distribution. Exact upstream archives, the dated VVC patch, executable build recipe/runtime inventory, full relevant notices and a hash-bound 27-file material archive are retained; isolated replay/full-suite/native checks preceded integration. The application manifest now binds the actual controlled binaries to twelve required tool documents, including full runtime/JPEG texts and the IJG credit. Complete final source/delivery review and a public hosting/distribution route are still open; no public source promise or automatic legal clearance is inferred. The old Gyan archive/release mirror was insufficient build-source evidence, and old executable Git objects/packages remain private pending separate publication-copy handling.
2. **Windows native runtime distribution terms:** the .NET Library License section 3(a)(ii) and Windows SDK License section 2(a)(ii) require protective terms for distributors/external end users. Retaining the texts alone does not demonstrate fulfillment. Agree/review a suitable distribution-terms mechanism for both Setup and portable, or explicitly choose a different deployment route. The source-code MIT grant remains separate; no new agreement was drafted or accepted on the author's behalf.

The MIT license for original code does not settle the aggregate-versus-combined-work analysis of the separate FFmpeg process. Neither automatically relicense the application as GPL nor assume CLI separation guarantees compatibility. Seek legal advice where necessary; this guide is an engineering checklist, not legal advice or jurisdiction-specific patent clearance.

For the **locally integrated controlled pair**, `licenses/FFmpeg/` contains the complete extra license/notice collection copied from pinned sources and the [runtime-material collection](../scripts/ffmpeg/runtime-notices.md). Both corrected tools have code/data-bound audit maps confirming static winpthreads, libgcc and MinGW material. The package verifier separately requires exact version/binary/archive/source-material identity and all twelve tool documents; the existing About viewer displays indexed texts offline. This integration changes neither the existing installed application nor immutable older packages, and it does not approve GPL/application, Windows deployment, source hosting or public distribution.

The [final engineering/material review](decisions/0002-controlled-ffmpeg-build.md#final-2-engineering-and-material-review) distinguishes source/build reconstruction from runtime/package acceptance. The immutable 27-file material archive recorded in `Tools/ffmpeg/DISTRIBUTION_README.txt` remains bound to the original corrected build; later application-packaging checks do not relabel it as a freshly rebuilt archive. Formal experimental/public-distribution approval flags remain false.

## XmpCore historical license resolution

The 6.1.10.1 dependency is unchanged. Its exact source-tag README declares the same BSD terms as Adobe's XMP SDK, but the legacy URL is unavailable. The license-text evidence gap was closed on 2026-10-26 using the [archived Adobe BSD terms](https://web.archive.org/web/20210616112605/https://www.adobe.com/devnet/xmp/library/eula-xmp-library-java.html) and the .NET maintainer's [accepted clarification](https://github.com/drewnoakes/xmp-core-dotnet/pull/56), merged as `c257ed72febafb68e560fcb9622c268666182f61`. The historical Java JAR's contradictory confidential notice was explained as an incorrect bundled file in [TIKA-3204](https://issues.apache.org/jira/browse/TIKA-3204); that investigation is retained, not erased or interpreted as a new .NET release.

[LICENSE.txt](../licenses/XmpCore/LICENSE.txt) retains the recovered wording and actual 2009 Adobe copyright line, including the original spelling. [ATTRIBUTIONS.txt](../licenses/XmpCore/ATTRIBUTIONS.txt) preserves the exact tag's Adobe file-header notices and .NET port credits. [PROVENANCE.txt](../licenses/XmpCore/PROVENANCE.txt) records immutable identities, source links, extraction policy and the resolution. All three are hash-bound in the manifest and displayed by About's existing offline notices viewer. This component-specific correction neither removes the remaining binary-distribution gates nor grants rights to unrelated media.

## Installer presentation

Publisher is `Nazar Khozin (kranel-argonavt)` with the GitHub URL. The stable AppId, installation path, settings, shortcuts and uninstall-data policy do not change with this display-name update. The Setup remains unsigned; this is not a verified signing identity.

Inno's [LicenseFile page](https://jrsoftware.org/ishelp/topic_setup_licensefile.htm) is optional as an Inno UI feature; this is **not** a conclusion that all bundled vendors waive end-user terms. No new EULA or forced agreement page has been introduced yet. Windows-native protective terms need a separate reviewed solution for Setup **and portable**, without placing new restrictions on the original MIT code or GPL tools. Merely adding an acceptance checkbox is not a compliance determination. Review the [Inno license](https://jrsoftware.org/files/is/license.txt) for the compiler/engine; retain its existing copyright/website occurrences.

## Closing the gates

Resolve the above evidence gaps without changing dependencies or contacting third parties on the owner's behalf implicitly. If a dependency/tool replacement is approved, rerun affected rendering/export/audio/tool-integrity tests. Then review the license collection and index, update the versioned manifest, commit, and build fresh clean-source Setup/ZIP with explicit source/hash binding. Check the installed/offline notices too. Tagging, uploading and publication still require explicit authorization.
