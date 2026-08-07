# GlyphFrame

GlyphFrame is a Windows desktop application for converting local videos and images into colored symbol art.

## Status

Phase 6 — Image Pipeline. GlyphFrame now supports local image decoding, orientation-normalized original previews, automatic symbol-grid calculation, two symbol polarity modes, colored SkiaSharp symbol rendering, symbolized previews, and PNG/JPEG export. Video selection remains metadata-only until the later video phases.

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
- SkiaSharp 4.150.1 with matching Win32 native assets for image decoding, glyph measurement, symbol rendering, and image encoding
- xUnit

MetadataExtractor is an Apache-2.0 package used only by `GlyphFrame.Infrastructure` for header metadata; it is not used to decode preview pixels or video frames. MKV metadata is read by a bounded internal EBML header reader that skips media clusters. SkiaSharp and its Windows native assets are MIT-licensed and isolated to `GlyphFrame.Rendering`. OpenCvSharp, FFmpeg, and ffprobe remain intentionally absent. The settings screen reports FFmpeg validation as not yet available rather than fabricating a status.

## Solution structure

```text
GlyphFrame/
├── GlyphFrame.sln
├── src/
│   ├── GlyphFrame.App/             WPF UI, MVVM, and composition root
│   ├── GlyphFrame.Core/            Domain models and platform-independent logic
│   ├── GlyphFrame.Rendering/       Image decoding, symbolization, Skia rendering, and image export
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

The logs directory is prepared for later logging work; Phase 4 does not create fabricated log entries. The persisted defaults are Detailed ASCII, Cascadia Mono at size 10, same-as-source resolution, Light on Dark symbol polarity, PNG image export, and Keep Audio for video. Available accents are Violet, Purple, Blue, Cyan, Emerald, and Rose, with Violet as the default.

Settings are written through a temporary file before replacing `settings.json`. Missing and partial settings use approved defaults, invalid values are normalized, and malformed JSON is preserved as `settings.invalid.json` (or the next numbered name) before recovery.

## Media input

Browse Media and the two drag-and-drop surfaces use the same validation and metadata pipeline. GlyphFrame accepts exactly one existing, readable file with one of these extensions, matched case-insensitively:

- Images: PNG, JPG/JPEG, WebP, BMP
- Videos: MP4, MOV, MKV, AVI

Image metadata includes the full path, filename, format, file size, source dimensions, EXIF orientation when present, and display-oriented dimensions. Phase 6 decodes the source pixels and applies the recorded orientation exactly once before preview, resolution calculation, symbolization, and export.

Video metadata includes the container, file size, resolution and duration, plus FPS, frame count, codecs, audio presence, and sample rate when the container exposes them reliably. MKV Info and Tracks elements are read without entering media clusters. Before bundled ffprobe arrives in Phase 7, optional video fields may truthfully appear as `Unknown`; no value is invented from an unreliable estimate.

Metadata loading is asynchronous, a newer selection cancels and supersedes an older one, and a failed replacement retains the previous valid selection. Selected media paths are transient and are never added to `settings.json`.

## Image pipeline

The complete local image workflow supports PNG, JPG/JPEG, WebP, and BMP input:

```text
Select or drop image
→ decode and normalize orientation
→ show the original preview
→ resolve output dimensions
→ measure the selected font with SkiaSharp
→ calculate the automatic symbol grid
→ sample visible cell colors and Rec. 709 luminance
→ map luminance to the selected character ramp and polarity
→ render colored glyphs on the polarity's opaque background
→ preview or export the symbolized image
```

Built-in character presets are Simple ASCII, Detailed ASCII, and Unicode Blocks. Their ramps stay ordered from sparse to dense. Supported fonts are Cascadia Mono, Consolas, Courier New, and Lucida Console at sizes 9, 10, and 12. Missing fonts fall back safely, and the output modes are Same as source, 720p, and 1080p while preserving the normalized source aspect ratio.

`Light on Dark` is the default polarity: bright areas use denser symbols on the dark GlyphFrame background, making pale subjects on dark scenes remain visually bright. `Dark on Light` maps dark areas to denser symbols on a neutral near-white background for a traditional printed-ASCII appearance. Changing polarity marks the symbolized preview stale, and preview plus export always regenerate from the current polarity.

PNG is the default output format; JPG/JPEG is also supported at a centralized quality of 90. Export uses an atomic temporary-file strategy, confirms before replacing an existing file, never overwrites the input image, and removes partial files after failure or cancellation. Changing a render setting marks an existing symbolized preview as stale, and export always renders with the current settings.

Transparent source pixels use straight alpha and are composited exactly once over the selected opaque polarity background before color and luminance sampling. Preview, PNG export, and JPEG export therefore use the same visible-color and background rules.

## Documentation

- [GlyphFrame Technical Specification v1.0](docs/GlyphFrame_Technical_Specification_v1.0.md)
- [GlyphFrame Design Reference](docs/design_reference/GlyphFrame_Design_Reference.png)

## Upcoming phases

- Phases 7–11 add bundled FFmpeg tooling, video preview and processing, audio, progress, and cancellation.
- Later phases cover error handling, UI polish, comprehensive testing, and portable packaging.

## Privacy

GlyphFrame is local-only software. It does not upload media and includes no telemetry or analytics. Media input is opened read-only and is never copied, renamed, modified, or deleted. Image pixels are processed only in memory, and image export writes only to the explicitly selected destination. Settings remain on the current device under the user-local application-data directory; media paths and media content are not stored in preferences.
