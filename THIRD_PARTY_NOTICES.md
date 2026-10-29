# Third-Party Notices

GlyphFrame references third-party components. A source checkout contains their retained license/provenance texts, not restored packages or executable distributions. Local builds obtain NuGet dependencies separately and may explicitly acquire FFmpeg. None of this implies endorsement by their authors.

Original GlyphFrame code is Copyright (c) 2026 Nazar Khozin (kranel-argonavt), under the root [MIT LICENSE](LICENSE). That license does not relicense the components or upstream documents below. Full retained texts are in `licenses/` and `Tools/ffmpeg/`; About's local notices viewer includes them. The legal-file manifest records exact dependency versions, text hashes and unresolved distribution issues. **This provisional collection is not public-distribution approval.**

## FFmpeg 9.0-glyphframe.2 — controlled local integration

- Project: FFmpeg; Copyright (c) 2000–2026 the FFmpeg developers.
- Local runtime paths: `Tools/ffmpeg/ffmpeg.exe` and `Tools/ffmpeg/ffprobe.exe`; executables are not committed.
- Exact version: `9.0-glyphframe.2`; Windows x64 static GPLv3-or-later configuration, with static x264 and zlib, no external-library autodetection/nonfree components.
- FFmpeg source: [`d32b387f2b0a484599d4587d651891f0c63c4238`](https://github.com/FFmpeg/FFmpeg/commit/d32b387f2b0a484599d4587d651891f0c63c4238), plus the [dated/hash-bound downstream VVC correction](scripts/ffmpeg/patches/README.md).
- x264: `0480cb05fa188d37ae87e8f4fd8f1aea3711f7ee`, [VideoLAN upstream](https://code.videolan.org/videolan/x264), GPLv2 or later.
- zlib: [`da607da739fa6047df13e66a2af6b8bec7c2a498`](https://github.com/madler/zlib/commit/da607da739fa6047df13e66a2af6b8bec7c2a498), v1.3.2, zlib license.
- Static runtime material also includes libgcc, MinGW CRT/import/startup objects and winpthreads. Full GPL/Runtime Library Exception, MinGW and winpthreads notices are retained under `licenses/FFmpeg/`; conditional exceptions do not relicense unrelated code.
- Exact executable/archive pins: `Tools/ffmpeg/SHA256SUMS.txt`. Full GPLv3 text: `Tools/ffmpeg/LICENSE.txt`. Build/source/patch identities and current delivery limits: `Tools/ffmpeg/DISTRIBUTION_README.txt`.
- Full FFmpeg license overview, x264 GPL text, zlib text and additional runtime/JPEG notices: `licenses/FFmpeg/`, all verified by the legal manifest and available in About's local viewer.

**This software is based in part on the work of the Independent JPEG Group.** GlyphFrame applies no further changes to FFmpeg's `jfdctfst.c`, `jfdctint_template.c` or `jrevdct.c` relative to the pinned FFmpeg revision; this is not a claim that those files are pristine IJG originals. Their complete leading copyright/permission notices are retained in `licenses/FFmpeg/IJG-NOTICES.txt` and the exact source archive.

The [controlled build/source materials](scripts/ffmpeg/README.md) and source-delivery preparation are retained locally with exact hashes. There is no configured public binary/source download yet. Public distribution still requires final source/hash/delivery binding and the separate application/GPL, Windows-native and historical-payload review. These are not Gyan's earlier tools or notices; older packages/history remain private and are not retroactively cleared by this replacement.

GlyphFrame invokes the checksum-identified tools as separate CLI processes; that alone is not an aggregate-versus-combined-work legal determination. Original GlyphFrame code remains MIT; third-party terms remain applicable. This index records provenance/integrity, not a compliance certificate or authorization to publish.

## Other dependencies

The recorded Windows runtime dependencies and their retained documents are listed below. Package-level license labels do not imply that all embedded native material uses that same license; preserve the accompanying third-party notices.

| Component | Version | License / attribution | Retained documents |
| --- | --- | --- | --- |
| CommunityToolkit.Mvvm | 8.4.2 | MIT; .NET Foundation and Contributors | `licenses/CommunityToolkit.Mvvm/` |
| MetadataExtractor | 2.9.3 | Apache-2.0; Copyright 2002-2019 Drew Noakes and contributors; 2014 Imazen LLC (initial Java-to-C# conversion) | `licenses/MetadataExtractor/LICENSE.txt` |
| Microsoft.Extensions.DependencyInjection and Abstractions | 10.0.10 | MIT; Copyright Microsoft Corporation. All rights reserved. | Shared MIT text and package-supplied runtime notices in `licenses/dotnet/` |
| SkiaSharp and SkiaSharp.NativeAssets.Win32 | 4.150.1 | MIT; Copyright 2015-2016 Xamarin, Inc.; 2017-2018 Microsoft Corporation; native third-party terms also apply | `licenses/SkiaSharp/` |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.12 | MIT and named Windows-native exceptions under .NET Library License; .NET Foundation/Microsoft and Contributors; third-party terms also apply | `licenses/dotnet/`, including `NET-LIBRARY-LICENSE.txt` and `WINDOWS-LICENSE-MAPPING.txt` |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 / WPF | 10.0.12 | MIT plus named Windows-native exceptions under .NET Library License / Windows SDK License; desktop/WPF third-party terms also apply | `licenses/WPF/` and `licenses/dotnet/` |
| XmpCore | 6.1.10.1 | BSD-3-Clause; recovered Adobe terms and retained Adobe/.NET port attributions | `licenses/XmpCore/LICENSE.txt`, `ATTRIBUTIONS.txt` and `PROVENANCE.txt` |

XmpCore's exact source tag is commit `66b975558d9199cd3eb49093261a61deb30f8c06`, <https://github.com/drewnoakes/xmp-core-dotnet/tree/66b975558d9199cd3eb49093261a61deb30f8c06>. Its README declares Adobe's BSD terms. The now-unavailable license URL was recovered through the [archived Adobe page](https://web.archive.org/web/20210616112605/https://www.adobe.com/devnet/xmp/library/eula-xmp-library-java.html) cited by the .NET maintainer's [accepted BSD-3-Clause metadata correction](https://github.com/drewnoakes/xmp-core-dotnet/pull/56). The actual 2009 Adobe copyright/license wording is retained separately from the 2006/2006-2007 source-header notices and .NET port credits. It is not Apache-2.0 based on MetadataExtractor's license.

The contradictory confidential notice in Adobe's historical Java 6.1.10 JAR was investigated in [TIKA-3204](https://issues.apache.org/jira/browse/TIKA-3204), which records the incorrect bundled license file and its correction. The provenance record preserves this investigation and the .NET maintainer's clarification instead of silently replacing the notice with a guessed BSD template. This closes XmpCore's license-text evidence gap without changing the 6.1.10.1 dependency; FFmpeg source availability and Windows-native distribution terms remain open.

Microsoft's [Windows binary license mapping](https://github.com/dotnet/core/blob/6c25b9da384e8d8a16fd12c79da8a8aa6bdfc6a1/license-information-windows.md) applies the .NET Library License to `coreclr.dll`, `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll` and `wpfgfx_cor3.dll`, and the Windows SDK License to `D3DCompiler_47_cor3.dll`. These are shipped unchanged in this self-contained payload. The retained vendor texts are plaintext extractions of the official HTML, with paragraph spacing normalized and hyperlink destinations retained. Original HTML hashes/URLs are recorded in the legal manifest. Owner-adopted component terms 1.0 and a distinct acceptance scope are retained in `licenses/WindowsNative/` for Setup and portable. They cover those five components only; old preview records cannot satisfy this scope. Adoption and displaying the texts do not certify final incorporation/coverage, waive Publisher duties or approve public distribution. No Microsoft privacy/diagnostic behavior is overridden by the application's own no-analytics policy.

SkiaSharp.NativeAssets.macOS is restored transitively but is not shipped in the Windows x64 payload. Test packages are development-only, not bundled application components. Selected installed fonts are not bundled font files.

## Installer engine

Setup is built with Inno Setup 7.1.0 by Jordan Russell and Martijn Laan. Its license is retained in `licenses/InnoSetup/LICENSE.txt`; existing engine copyright and website occurrences must remain intact. This is not the GlyphFrame application license or an endorsement. The portable application does not require Inno Setup; both formats retain the same legal collection.
