<p align="center">
  <img src="docs/screenshots/logo.svg" alt="GlyphFrame white GF logo" width="96" height="96">
</p>

<h1 align="center">GlyphFrame</h1>

<p align="center">A Windows desktop application that turns local images and videos into colored symbol art.</p>

**Render → Review → Export.** Inspect the generated result before saving it; repeat an export without rerendering. Processing stays on your computer, with no uploads or telemetry.

This is a **source-only portfolio project**. Original application code, tests, design assets and local installer/build scripts are included; FFmpeg executables, restored packages and ready-to-run Setup/ZIP downloads are not. Binary-release licensing review remains open; see [licensing](docs/licensing.md).

![GlyphFrame startup screen with local media selection, character presets and render settings in the Rose theme](docs/screenshots/startup.png)

## Results

### Image conversion

| Original · AI-generated input | Symbolized · GlyphFrame output |
| --- | --- |
| <img src="docs/screenshots/car-original.jpg" alt="AI-generated silver coupe used as input" width="420"> | <img src="docs/screenshots/car-symbolized.png" alt="The same coupe rendered as colored ASCII characters" width="420"> |

### Video conversion

[![Preview frame from the symbolized puppy video](docs/screenshots/puppy-poster.jpg)](docs/screenshots/puppy-symbolized.mp4)

[Open the 5-second MP4 result](docs/screenshots/puppy-symbolized.mp4) · 1080p / 30 FPS. The video opens on demand; it is not an autoplaying application walkthrough. Source footage: Pixabay, as reported by the project author. See [media provenance and review limits](docs/screenshots/README.md).

<details>
<summary>More screenshots</summary>

**Video review and export**

![GlyphFrame showing a rendered video, playback controls and export settings in the Rose theme](docs/screenshots/application.png)

**Image review and export**

![An image render ready for review and export](docs/screenshots/image-ready.png)

**Compact layout after video export**

![A narrower GlyphFrame window with Export Again, Open Folder and Copy path actions](docs/screenshots/compact-export.png)

</details>

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
```

A fresh source checkout builds without FFmpeg: image workflows are available, while video tooling is reported unavailable. To enable video and the full integration suite, explicitly prepare the pinned local tools with **PowerShell 7**, then rebuild:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -Download
dotnet build GlyphFrame.sln -c Release --no-restore
dotnet test GlyphFrame.sln -c Release --no-build --no-restore
```

The acquisition command downloads the fixed vendor archive, verifies archive/executable SHA-256 and extracts only FFmpeg/ffprobe into ignored `artifacts/local-runtime/`. It does not run an installer or modify PATH. Builds never download these tools automatically. An offline `-ArchivePath` option and verification-only mode are documented in [Tools/ffmpeg/README.md](Tools/ffmpeg/README.md). NuGet restore separately obtains the declared .NET package dependencies; source-only does not mean dependency-free.

## Workflow

Open local media → choose characters/font/resolution → render → compare Original/Symbolized → export. Image export offers PNG/JPEG; video export copies the accepted MP4 without encoding it again. See [supported behavior and controls](docs/product-spec.md) for details.

## Architecture and stack

C# / .NET 10, WPF, MVVM with CommunityToolkit.Mvvm, dependency injection, SkiaSharp, MetadataExtractor, System.Text.Json, bundled FFmpeg/ffprobe and xUnit.

Core contracts, rendering, infrastructure and WPF presentation live in separate projects; two test projects cover logic, real-media integration and application components.

[Architecture](docs/architecture.md) explains dependencies, conversion flow, ownership and cancellation. [ADR 0001](docs/decisions/0001-native-video-playback.md) explains why independent players share WPF primitives and retain an FFmpeg fallback rather than adding another media runtime or source proxies.

## Verification and compatibility

Recorded verification: **1,283 Release tests**, **92 separate helper checks**, a zero-warning/error build and formatting/diff checks. A fresh source copy built without FFmpeg; explicit tool acquisition and media integration were checked separately. [Testing](docs/testing.md) records commands, candidate scope, native/user-reported observations and remaining limits; these results are not a public-release certificate.

Continuous playback depends on Windows media components/codecs, and long media/cache paths can prevent WPF review even when an accepted MP4 remains exportable. Batch processing, realtime conversion, GPU controls and GIF/WebM export are outside the current scope. See [product limits](docs/product-spec.md).

## Packaging

Local self-contained portable ZIP and unsigned English per-user x64 Setup workflows are retained for development, **not offered as cleared public downloads**. See [packaging](docs/packaging.md) for commands, integrity/startup verification, installation/update/uninstall policy and release boundaries. These scripts do not sign, tag or upload a release.

## Privacy and third-party software

Source media is opened read-only. Processing, settings and managed review artifacts stay local; the application does not upload media or send telemetry. See [data handling](docs/product-spec.md) for storage, diagnostics and cleanup behavior.

Original GlyphFrame code is [MIT-licensed](LICENSE), Copyright (c) 2026 Nazar Khozin (kranel-argonavt). Third-party components retain their own licenses; see [notices](THIRD_PARTY_NOTICES.md) and [open binary-distribution gates](docs/licensing.md). No Setup/ZIP release is declared.

Demonstration media has separate provenance; the code's MIT license does not relicense Pixabay footage. See [media notes](docs/screenshots/README.md).

## Documentation

- [Product specification](docs/product-spec.md) — supported behavior, workflow and scope.
- [Architecture](docs/architecture.md) — project boundaries, processing flow and ownership.
- [Testing](docs/testing.md) — commands, coverage, recorded results and limitations.
- [Packaging](docs/packaging.md) — self-contained builds, extracted validation and candidate identity.
- [Licensing](docs/licensing.md) — code license, dependency texts, version binding and open distribution gates.
- [Portfolio source preparation](docs/portfolio.md) — planned history-preserving publication, the history-free fallback and local-only boundaries.
- [ADR 0001: native video playback](docs/decisions/0001-native-video-playback.md) — decision, alternatives and compatibility evidence.
- [Changelog](CHANGELOG.md) — concise user-visible work, not iteration journals.
- [Test media fixtures](tests/GlyphFrame.Tests/TestInfrastructure/MediaFixtures.md) — synthetic fixture provenance and reproduction.

Full development specifications and dated iteration/acceptance journals are preserved in local archives. Public documentation describes the current product and its engineering decisions without requiring those archives.
