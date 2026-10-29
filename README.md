<p align="center">
  <img src="docs/screenshots/logo.svg" alt="GlyphFrame white GF logo" width="96" height="96">
</p>

<h1 align="center">GlyphFrame</h1>

<p align="center">Turn images and videos into colored symbol art on Windows.</p>

**Render → Review → Export.** Choose a character preset, render your media, compare the result with the original and export when ready. Processing stays on your computer, with no uploads or telemetry.

![GlyphFrame startup screen with local media selection, character presets and render settings in the Rose theme](docs/screenshots/startup.png)

## Results

### Images

| Original · AI-generated input | Symbolized · GlyphFrame output |
| --- | --- |
| <img src="docs/screenshots/car-original.jpg" alt="AI-generated silver coupe used as input" width="420"> | <img src="docs/screenshots/car-symbolized.png" alt="The same coupe rendered as colored ASCII characters" width="420"> |

### Video

[![Preview frame from the symbolized puppy video](docs/screenshots/puppy-poster.jpg)](docs/screenshots/puppy-symbolized.mp4)

[Watch the 5-second result](docs/screenshots/puppy-symbolized.mp4) · 1080p / 30 FPS · MP4

Source footage: [Pixabay, item 371301](https://pixabay.com/videos/ai-generated-puppy-pet-dog-animal-371301/), identified by the project author. This is a web-preview version of the GlyphFrame output; the original clip is not distributed. See [media provenance](docs/screenshots/README.md).

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

- **Control the look.** Three character presets, selectable fonts and sizes, two symbol polarities and source, 720p or 1080p output resolution.
- **Review before saving.** Compare Original and Symbolized views; zoom and pan images, or play, seek and inspect videos in fullscreen.
- **Export without rerendering.** Save retained image results as PNG/JPEG or copy the completed video as MP4. Video rendering supports Keep Audio and Remove Audio.
- **Work locally.** Source files stay unchanged. Settings, diagnostics and temporary review files remain on your computer.
- **Adjust the workspace.** Responsive layouts, six accent themes, reduced-motion support and settings with explicit Save/Cancel.
- **Recover from interruptions.** Cancel active work, retry failed operations and retain a previous completed result when a new render fails.

| Media | Input | Export |
| --- | --- | --- |
| Images | PNG, JPG/JPEG, WebP, BMP | PNG, JPEG |
| Video | MP4, MOV, MKV, AVI | MP4 with H.264 video and optional audio |

Continuous playback depends on Windows media components and codecs. See the [product specification](docs/product-spec.md) for exact behavior, controls and limitations.

## Build and run

**Platform:** Windows 10/11 x64. **Development:** .NET 10 SDK and access to NuGet dependencies; PowerShell 7 for tool setup and packaging scripts.

Self-contained Setup and portable packages are planned for GitHub Releases. Public downloads are not available yet; see [packaging and release status](docs/packaging.md).

From the repository root:

```powershell
dotnet restore GlyphFrame.sln
dotnet build GlyphFrame.sln -c Release --no-restore
dotnet run --project src/GlyphFrame.App/GlyphFrame.App.csproj -c Release --no-build
```

A fresh checkout supports image workflows. **Video processing and the full integration suite require the pinned FFmpeg/ffprobe pair**, prepared separately with PowerShell 7. The executables are not stored in Git.

<details>
<summary>Set up video tools and run the full test suite</summary>

Obtain the exact reviewed `9.0-glyphframe.2` tool archive from the maintainer. There is no public tool download configured yet. Replace the example path with the location of your archive:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -ArchivePath 'C:\GFInputs\GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip'
dotnet build GlyphFrame.sln -c Release --no-restore
dotnet test GlyphFrame.sln -c Release --no-build --no-restore
```

The initializer verifies the archive and executable SHA-256 hashes and prepares an ignored local cache. See [tool setup](Tools/ffmpeg/README.md) for the acquisition contract and [controlled FFmpeg build](scripts/ffmpeg/README.md) for source, patches and rebuild instructions.

</details>

## Architecture

Built with **C# / .NET 10, WPF, CommunityToolkit.Mvvm, SkiaSharp, MetadataExtractor and FFmpeg**, with dependency injection and xUnit tests.

| Project | Responsibility |
| --- | --- |
| `GlyphFrame.Core` | Models, settings, contracts and workflow rules |
| `GlyphFrame.Rendering` | Image decoding, symbol grids, glyph rendering and image export |
| `GlyphFrame.Infrastructure` | Media metadata, FFmpeg processes, settings storage and managed video artifacts |
| `GlyphFrame.App` | WPF interface, MVVM presentation and Windows playback |

Images and video frames share the same symbol-rendering pipeline. Video frames move through bounded process pipes; a completed MP4 is retained for review and repeat export. Explicit ownership, cancellation and operation identities coordinate file cleanup and asynchronous work.

Two test projects cover domain logic, real-media integration, output handling, application commands and WPF components. [Testing and verification](docs/testing.md) records commands, results and manual-check scope. The [architecture guide](docs/architecture.md) explains the processing flow and ownership rules; [ADR 0001](docs/decisions/0001-native-video-playback.md) documents the playback trade-offs.

## Documentation

- [Product specification](docs/product-spec.md) — workflows, controls and supported behavior.
- [Architecture](docs/architecture.md) — project boundaries, processing and resource ownership.
- [Testing](docs/testing.md) — automated checks, recorded results and compatibility evidence.
- [Packaging](docs/packaging.md) — portable builds, Setup and release preparation.
- [Licensing](docs/licensing.md) — dependency terms and distribution status.
- [Changelog](CHANGELOG.md) — user-visible changes.

## License and credits

Original GlyphFrame code is [MIT-licensed](LICENSE), Copyright (c) 2026 Nazar Khozin (kranel-argonavt). Third-party components retain their own licenses; see [Third-party notices](THIRD_PARTY_NOTICES.md).

Demonstration media has separate [provenance and usage notes](docs/screenshots/README.md); the code license does not relicense third-party footage. Publication preparation and binary-distribution requirements are tracked in the [publication plan](docs/portfolio.md) and [licensing guide](docs/licensing.md).
