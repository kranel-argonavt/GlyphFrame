# GlyphFrame

GlyphFrame is a Windows desktop application for converting local videos and images into colored symbol art.

## Status

Phase 5 — Media Input. GlyphFrame now supports local Browse Media and drag-and-drop workflows, deterministic image/video detection, friendly validation, asynchronous metadata loading, image orientation metadata, and binding-driven image/video/error states. Pixel preview, symbolization, processing, and export are not yet implemented.

## Platform and target framework

- Supported platform: Windows 10 and Windows 11, x64
- Application target: `net10.0-windows`
- Library and test target: `net10.0`
- Required development SDK: .NET 10 SDK

.NET 10 was selected because it is the current active Long Term Support release and supports WPF. See the [official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Technology stack

- C# and .NET 10
- WPF
- MVVM with CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- System.Text.Json
- MetadataExtractor 2.9.3 for managed image, MP4, MOV, and AVI metadata
- xUnit

MetadataExtractor is an Apache-2.0 package used only by `GlyphFrame.Infrastructure` for header metadata; it is not used to decode preview pixels or video frames. MKV metadata is read by a bounded internal EBML header reader that skips media clusters. SkiaSharp, OpenCvSharp, FFmpeg, and ffprobe are planned for later phases and remain intentionally absent. The settings screen reports FFmpeg validation as not yet available rather than fabricating a status.

## Solution structure

```text
GlyphFrame/
├── GlyphFrame.sln
├── src/
│   ├── GlyphFrame.App/             WPF UI, MVVM, and composition root
│   ├── GlyphFrame.Core/            Domain models and platform-independent logic
│   ├── GlyphFrame.Rendering/       Future symbolization and rendering logic
│   └── GlyphFrame.Infrastructure/  Local settings, paths, validation, and metadata readers
├── tests/
│   └── GlyphFrame.Tests/           Automated tests
└── docs/                            Specification and design reference
```

Project dependencies flow toward Core:

```text
GlyphFrame.App ───────────┬──> GlyphFrame.Core
                         ├──> GlyphFrame.Rendering ──────> GlyphFrame.Core
                         └──> GlyphFrame.Infrastructure ─> GlyphFrame.Core

GlyphFrame.Tests ─────────┬──> GlyphFrame.Core
                         ├──> GlyphFrame.Rendering
                         └──> GlyphFrame.Infrastructure
```

`GlyphFrame.Core` has no project dependencies.

## Build, run, and test

Run these commands from the repository root:

```powershell
dotnet restore
dotnet build GlyphFrame.sln
dotnet run --project src/GlyphFrame.App/GlyphFrame.App.csproj
dotnet test GlyphFrame.sln
```

## Local settings

Preferences are stored for the current Windows user under `%LocalAppData%\GlyphFrame`:

```text
%LocalAppData%\GlyphFrame\
├── settings.json
└── logs\
```

The logs directory is prepared for later logging work; Phase 4 does not create fabricated log entries. The persisted defaults are Detailed ASCII, Cascadia Mono at size 10, same-as-source resolution, PNG image export, and Keep Audio for video. Available accents are Violet, Purple, Blue, Cyan, Emerald, and Rose, with Violet as the default.

Settings are written through a temporary file before replacing `settings.json`. Missing and partial settings use approved defaults, invalid values are normalized, and malformed JSON is preserved as `settings.invalid.json` (or the next numbered name) before recovery.

## Media input

Browse Media and the two drag-and-drop surfaces use the same validation and metadata pipeline. GlyphFrame accepts exactly one existing, readable file with one of these extensions, matched case-insensitively:

- Images: PNG, JPG/JPEG, WebP, BMP
- Videos: MP4, MOV, MKV, AVI

Image metadata includes the full path, filename, format, file size, source dimensions, EXIF orientation when present, and display-oriented dimensions. Phase 5 preserves orientation metadata but does not rotate or render image pixels; orientation-normalized decoding starts in Phase 6.

Video metadata includes the container, file size, resolution and duration, plus FPS, frame count, codecs, audio presence, and sample rate when the container exposes them reliably. MKV Info and Tracks elements are read without entering media clusters. Before bundled ffprobe arrives in Phase 7, optional video fields may truthfully appear as `Unknown`; no value is invented from an unreliable estimate.

Metadata loading is asynchronous, a newer selection cancels and supersedes an older one, and a failed replacement retains the previous valid selection. Selected media paths are transient and are never added to `settings.json`.

## Documentation

- [GlyphFrame Technical Specification v1.0](docs/GlyphFrame_Technical_Specification_v1.0.md)
- [GlyphFrame Design Reference](docs/design_reference/GlyphFrame_Design_Reference.png)

## Upcoming phases

- Phase 6 adds image decoding, orientation normalization, symbolization, preview, and image export.
- Phases 7–11 add bundled FFmpeg tooling, video processing, audio, progress, and cancellation.
- Later phases cover error handling, UI polish, comprehensive testing, and portable packaging.

## Privacy

GlyphFrame is local-only software. It does not upload media and includes no telemetry or analytics. Media input is opened read-only and is never copied, renamed, modified, or deleted. Settings remain on the current device under the user-local application-data directory; media paths and media content are not stored in preferences.
