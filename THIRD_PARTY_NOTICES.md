# Third-Party Notices

GlyphFrame references third-party components. A source checkout contains their retained license/provenance texts, not restored packages or executable distributions. Local builds obtain NuGet dependencies separately and may explicitly acquire FFmpeg. None of this implies endorsement by their authors.

Original GlyphFrame code is Copyright (c) 2026 Nazar Khozin (kranel-argonavt), under the root [MIT LICENSE](LICENSE). That license does not relicense the components or upstream documents below. Full retained texts are in `licenses/` and `Tools/ffmpeg/`; About's local notices viewer includes them. The legal-file manifest records exact dependency versions, text hashes and unresolved distribution issues. **This provisional collection is not public-distribution approval.**

## FFmpeg 9.0

- Project: FFmpeg
- Copyright: Copyright (c) 2000–2026 the FFmpeg developers
- Local runtime output paths: `Tools/ffmpeg/ffmpeg.exe` and `Tools/ffmpeg/ffprobe.exe`; neither executable is included in the source snapshot
- Reported version: `9.0-essentials_build-www.gyan.dev`
- Binary distribution: Gyan Doshi, `ffmpeg-9.0-essentials_build.zip`, Windows x64 static essentials build
- Distribution URL: <https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0-essentials_build.zip>
- Upstream project: <https://ffmpeg.org/>
- Exact upstream source revision: FFmpeg commit `d32b387f2b`
- Matching upstream source repository: <https://github.com/FFmpeg/FFmpeg>
- Matching commit page and browsable source tree: <https://github.com/FFmpeg/FFmpeg/commit/d32b387f2b>
- Distributor support/release mirror: <https://github.com/GyanD/codexffmpeg>
- Distributor release tag: `9.0`, <https://github.com/GyanD/codexffmpeg/releases/tag/9.0>
- License: GNU General Public License version 3 (GPLv3), as identified by the binary distributor

This distribution is a GPL-enabled build. Its reported configuration includes `--enable-gpl`, `--enable-version3`, and GPL-enabled external components such as libx264 and libx265; it is therefore not described as an LGPL-only build.

The upstream FFmpeg revision can be retrieved by checking out commit `d32b387f2b` in the upstream repository. This is not a complete Corresponding Source bundle for the statically linked distribution. The distributor's `codexffmpeg` tag `9.0` is a release mirror, not a repository of build scripts. The selected archive's `DISTRIBUTION_README.txt` records configuration and many library versions, but does not establish all library sources, modifications and build-control material required for the exact binaries. Complete Corresponding Source availability and a compliant distribution route remain unresolved; do not treat the mirror's automatically generated source ZIP as that bundle.

The complete GPLv3 license text and distribution README shipped in the selected archive are included at `Tools/ffmpeg/LICENSE.txt` and `Tools/ffmpeg/DISTRIBUTION_README.txt`. Archive, executable, license, and README SHA-256 checksums are recorded in `Tools/ffmpeg/SHA256SUMS.txt`.

GlyphFrame executes the unchanged checksum-identified tools as separate programs. Separate process execution is not by itself a legal determination of aggregate/combined-work status. Before publicly distributing these binaries, resolve complete Corresponding Source availability and applicable GPLv3 obligations. The portfolio source snapshot excludes both EXEs and Git history; the original private development history still contains them and must not simply be made public as though deleting current files removed historical objects. See [source preparation](docs/portfolio.md). This notice documents provenance, not a legal-compliance guarantee.

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
| XmpCore | 6.1.10.1 | Upstream README declares Adobe XMP SDK BSD terms; exact historical license/attribution verification remains open | `licenses/XmpCore/PROVENANCE.txt` is evidence, **not a license** |

XmpCore's legacy Adobe license URL returns 404; the package and exact source tag do not contain a standalone license file. Its upstream tag is commit `66b975558d9199cd3eb49093261a61deb30f8c06`, <https://github.com/drewnoakes/xmp-core-dotnet/tree/66b975558d9199cd3eb49093261a61deb30f8c06>. Do not infer Apache-2.0 from its parent dependency, MetadataExtractor, or substitute a generic modern BSD template. This is a second open distribution gate.

Historical investigation found a conflict: the original Adobe XMPCore 6.1.10 Maven POM declares BSD, but the matching JAR's `META-INF/LICENSE` contains an Adobe confidential/proprietary notice. The same text appears in the reconstructed 6.1.10 Java source tag. See `licenses/XmpCore/PROVENANCE.txt` for artifact identity; clarification or an approved alternative is still required.

Microsoft's [Windows binary license mapping](https://github.com/dotnet/core/blob/6c25b9da384e8d8a16fd12c79da8a8aa6bdfc6a1/license-information-windows.md) applies the .NET Library License to `coreclr.dll`, `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll` and `wpfgfx_cor3.dll`, and the Windows SDK License to `D3DCompiler_47_cor3.dll`. These are shipped unchanged in this self-contained payload. The retained vendor texts are plaintext extractions of the official HTML, with paragraph spacing normalized and hyperlink destinations retained. Original HTML hashes/URLs are recorded in the legal manifest. End-user/distributor protective-term requirements remain to be reviewed/implemented for both package formats; showing these texts is not a claim that the requirements are fulfilled. No Microsoft privacy/diagnostic behavior is overridden by the application's own no-analytics policy.

SkiaSharp.NativeAssets.macOS is restored transitively but is not shipped in the Windows x64 payload. Test packages are development-only, not bundled application components. Selected installed fonts are not bundled font files.

## Installer engine

Setup is built with Inno Setup 7.1.0 by Jordan Russell and Martijn Laan. Its license is retained in `licenses/InnoSetup/LICENSE.txt`; existing engine copyright and website occurrences must remain intact. This is not the GlyphFrame application license or an endorsement. The portable application does not require Inno Setup; both formats retain the same legal collection.
