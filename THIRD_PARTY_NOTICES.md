# Third-Party Notices

GlyphFrame includes third-party components. Their inclusion does not imply endorsement of GlyphFrame by their authors.

## FFmpeg 9.0

- Project: FFmpeg
- Copyright: Copyright (c) 2000–2026 the FFmpeg developers
- Bundled executables: `Tools/ffmpeg/ffmpeg.exe` and `Tools/ffmpeg/ffprobe.exe`
- Reported version: `9.0-essentials_build-www.gyan.dev`
- Binary distribution: Gyan Doshi, `ffmpeg-9.0-essentials_build.zip`, Windows x64 static essentials build
- Distribution URL: <https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0-essentials_build.zip>
- Upstream project: <https://ffmpeg.org/>
- Exact upstream source revision: FFmpeg commit `d32b387f2b`
- Matching upstream source repository: <https://github.com/FFmpeg/FFmpeg>
- Matching commit page and browsable source tree: <https://github.com/FFmpeg/FFmpeg/commit/d32b387f2b>
- Gyan build configuration/scripts repository: <https://github.com/GyanD/codexffmpeg>
- Gyan build tag used for this distribution: `9.0`, <https://github.com/GyanD/codexffmpeg/releases/tag/9.0>
- License: GNU General Public License version 3 (GPLv3), as identified by the binary distributor

This distribution is a GPL-enabled build. Its reported configuration includes `--enable-gpl`, `--enable-version3`, and GPL-enabled external components such as libx264 and libx265; it is therefore not described as an LGPL-only build.

To retrieve the matching FFmpeg source, clone the upstream source repository and check out commit `d32b387f2b`, or browse that revision through the commit link above. To retrieve the build configuration and scripts, clone the Gyan `codexffmpeg` repository and check out tag `9.0`. The selected binary archive's own `DISTRIBUTION_README.txt` also identifies the same FFmpeg source revision, GPLv3 license, and release-essentials build configuration.

The complete GPLv3 license text and distribution README shipped in the selected archive are included at `Tools/ffmpeg/LICENSE.txt` and `Tools/ffmpeg/DISTRIBUTION_README.txt`. Archive, executable, license, and README SHA-256 checksums are recorded in `Tools/ffmpeg/SHA256SUMS.txt`.

GlyphFrame redistributes the two executables unchanged from the checksum-identified Gyan archive and executes them as separate programs. The repository does not include a large corresponding-source archive. Before any public GlyphFrame distribution, the release owner should review the selected binary's complete corresponding-source availability and all GPLv3 distribution obligations; this notice documents provenance and retrieval locations but does not make a legal-compliance guarantee.

## Other dependencies

NuGet dependencies and their pinned versions are declared in the project files. Notable runtime components include:

- MetadataExtractor 2.9.3 — Apache License 2.0.
- SkiaSharp 4.150.1 and SkiaSharp.NativeAssets.Win32 4.150.1 — MIT License.
- CommunityToolkit.Mvvm 8.4.2 — MIT License.
- Microsoft.Extensions.DependencyInjection 10.0.10 — MIT License.
