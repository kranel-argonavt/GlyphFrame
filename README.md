# GlyphFrame

A Windows desktop application that turns local images and videos into colored symbol art.

**Render → Review → Export.** Inspect the generated result before saving it; repeat an export without rerendering. Processing stays on your computer, with no uploads or telemetry.

## Features

- PNG, JPG/JPEG, WebP and BMP image input; MP4, MOV, MKV and AVI video input.
- Three character presets, measured font-based symbol grids, color-preserving glyphs and two symbol polarities.
- Retained image review with Ctrl + wheel zoom and bounded drag panning; PNG/JPEG export.
- Full-video symbol rendering, independent Original/Symbolized playback, seek, fullscreen and audio controls.
- MP4 export copies the accepted review artifact; Keep Audio and Remove Audio are part of rendering.
- Responsive status/result layouts, six accent presets, reduced-motion behavior and draft-based Settings.
- Recoverable errors, explicit technical Details, bounded local diagnostics and owned-artifact cleanup.

See the [product specification](docs/product-spec.md) for exact formats, controls, data handling and deliberate scope limits.

## Requirements and build

Target platform: **Windows 10/11 x64**. Development requires the **.NET 10 SDK** and access to the configured NuGet sources. WPF application tests also require Windows.

From the repository root:

```powershell
dotnet restore GlyphFrame.sln
dotnet build GlyphFrame.sln -c Release --no-restore
dotnet run --project src/GlyphFrame.App/GlyphFrame.App.csproj -c Release --no-build
dotnet test GlyphFrame.sln -c Release --no-build --no-restore
```

PowerShell 7 is additionally required for the packaging/helper scripts. The self-contained portable includes .NET/WPF, Skia dependencies and FFmpeg/ffprobe; these developer prerequisites do not need to be installed separately just to run that package.

## Using the application

1. Browse or drop one local image/video.
2. Choose the character preset, font, output resolution and polarity. For video, choose Keep/Remove Audio.
3. Render the image or full video, then review Original/Symbolized. Preview Frame is a separate single-frame video action.
4. Export the retained result. Choose PNG/JPEG in the image Save dialog; video export publishes the accepted MP4 without encoding it again.

Image zoom/pan and video view scaling do not change rendered pixels. Changes to pixel/audio settings can make a retained result stale until rerendering; failed/cancelled replacement preserves useful previous state.

## Architecture and stack

C# / .NET 10, WPF, MVVM with CommunityToolkit.Mvvm, dependency injection, SkiaSharp, MetadataExtractor, System.Text.Json, bundled FFmpeg/ffprobe and xUnit.

| Project | Responsibility |
| --- | --- |
| `GlyphFrame.Core` | Models, contracts, presets, settings catalog and workflow rules; no project dependencies |
| `GlyphFrame.Rendering` | Image decoding, glyph/grid/luminance processing, symbol rendering and image encoding |
| `GlyphFrame.Infrastructure` | Metadata/files/settings, FFmpeg processes, streaming video, managed artifacts and publication |
| `GlyphFrame.App` | WPF UI, ViewModels, native playback and composition root |
| `GlyphFrame.Tests` / `GlyphFrame.App.Tests` | Logic, real-media/file integration, application and WPF component verification |

[Architecture](docs/architecture.md) explains dependencies, conversion flow, ownership and cancellation. [ADR 0001](docs/decisions/0001-native-video-playback.md) explains why independent players share WPF primitives and retain an FFmpeg fallback rather than adding another media runtime or source proxies.

## Verification and compatibility

The recorded implementation passed **1,274 Release tests**, a zero-warning/error build, formatting/diff checks and **62 separate installer/packaging/session helper checks**. Package integrity, native installer workflows and user-reported application checks have separately scoped evidence; tests do not declare a public release.

Manual clean-PC feedback was positive on the user's reported Windows 11 build and last supplied ZIP. This is not an independent runtime inventory, an exhaustive size/DPI/race matrix or a separate Windows 10 pass. Exact candidate identity, commands, evidence methods and remaining limits are in [testing](docs/testing.md) and [packaging](docs/packaging.md#recorded-candidate).

Important limits:

- Windows media components/codecs determine continuous WPF playback; import/FFmpeg support does not guarantee playback of every codec. Original has a frame-preview fallback.
- Long media/cache paths can prevent WPF review even when the MP4 is valid. A valid accepted artifact remains retained/exportable if review cannot open it.
- No batch conversion, realtime symbolized playback, GPU/hardware-encoder UI, custom ramp editor or GIF/WebM export is provided.
- Portfolio screenshots/demo/sample assets and final distribution/licensing decisions are not yet complete. No public release is declared.

## Packaging

On Windows with PowerShell 7 and the build prerequisites:

```powershell
pwsh -NoProfile -File scripts/Build-PortablePackage.ps1
```

The command creates a fresh local self-contained ZIP, checksum, manifest and report under ignored `artifacts/`; it does not upload, sign or tag a release. Extract the **whole ZIP** and keep its files and `Tools/ffmpeg/` together.

Portable means installer-free deployment, not beside-EXE data: normal settings, privacy-safe logs and managed review cache stay under `%LocalAppData%\GlyphFrame`. See [packaging](docs/packaging.md) for verification, isolation and release boundaries.

An unsigned English **per-user x64 installer** is also available as a local build workflow. With Inno Setup 7 installed explicitly, run `pwsh -NoProfile -File scripts/Build-InstallerPackage.ps1`. It packages the verified portable payload, requests no elevation, adds a Start Menu shortcut and offers an unchecked desktop shortcut. Updates reuse the installation identity; uninstall preserves settings and user exports. This does not declare a public release or signed/trusted download. See [installer policy and validation](docs/packaging.md#installer).

## Privacy and third-party software

Source media is opened read-only and never renamed, modified or deleted. Frames are processed through bounded memory; the application owns a generated review-video cache, not a source-media copy. Media paths/content are not saved in preferences or diagnostic logs. There is no telemetry, analytics or upload.

Application licensing and public-distribution review have not been finalized. [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the bundled [FFmpeg distribution information](Tools/ffmpeg/DISTRIBUTION_README.txt), [license](Tools/ffmpeg/LICENSE.txt) and [checksums](Tools/ffmpeg/SHA256SUMS.txt) record dependency attribution/provenance; they are not a release approval.

## Documentation

- [Product specification](docs/product-spec.md) — supported behavior, workflow and scope.
- [Architecture](docs/architecture.md) — project boundaries, processing flow and ownership.
- [Testing](docs/testing.md) — commands, coverage, recorded results and limitations.
- [Packaging](docs/packaging.md) — self-contained builds, extracted validation and candidate identity.
- [ADR 0001: native video playback](docs/decisions/0001-native-video-playback.md) — decision, alternatives and compatibility evidence.
- [Changelog](CHANGELOG.md) — concise user-visible work, not iteration journals.
- [Test media fixtures](tests/GlyphFrame.Tests/TestInfrastructure/MediaFixtures.md) — synthetic fixture provenance and reproduction.

Full development specifications and dated iteration/acceptance journals are preserved in local archives. Public documentation describes the current product and its engineering decisions without requiring those archives.
