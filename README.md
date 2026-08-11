# GlyphFrame

GlyphFrame is a Windows desktop application for converting local videos and images into colored symbol art.

## Status

Phase 7 — Bundled FFmpeg. GlyphFrame now ships validated Windows x64 FFmpeg and ffprobe tools alongside the Phase 6 image pipeline. The application does not depend on a system FFmpeg installation or the system `PATH`. Video selection remains metadata-only until Phase 8.

## Platform and target framework

- Supported platform: Windows 10 and Windows 11, x64
- Application target: `net10.0-windows`
- Library and platform-neutral test target: `net10.0`
- Windows application test target: `net10.0-windows`
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
- FFmpeg and ffprobe 9.0 essentials build for reliable local video metadata probing and future video phases
- xUnit

MetadataExtractor is an Apache-2.0 package used only by `GlyphFrame.Infrastructure` for header metadata; it is not used to decode preview pixels or video frames. MKV metadata is read by a bounded internal EBML header reader that skips media clusters. SkiaSharp and its Windows native assets are MIT-licensed and isolated to `GlyphFrame.Rendering`. OpenCvSharp remains intentionally absent.

The bundled FFmpeg 9.0 `essentials_build` is the Windows x64 static GPLv3 distribution from Gyan Doshi. Its exact source, build type, license, source revision, and SHA-256 checksums are recorded in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and `Tools/ffmpeg/SHA256SUMS.txt`.

## Solution structure

```text
GlyphFrame/
├── GlyphFrame.sln
├── Tools/ffmpeg/                    Bundled ffmpeg.exe, ffprobe.exe, license, and checksums
├── src/
│   ├── GlyphFrame.App/             WPF UI, MVVM, and composition root
│   ├── GlyphFrame.Core/            Domain models and platform-independent logic
│   ├── GlyphFrame.Rendering/       Image decoding, symbolization, Skia rendering, and image export
│   └── GlyphFrame.Infrastructure/  Local settings, paths, validation, and metadata readers
├── tests/
│   ├── GlyphFrame.Tests/           Platform-neutral, rendering, and infrastructure tests
│   └── GlyphFrame.App.Tests/       Windows-targeted application/ViewModel integration tests
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

Video metadata includes the container, file size, resolution and duration, plus FPS, frame count, codecs, audio presence, and sample rate when ffprobe or the managed container reader can establish them reliably. The validated bundled ffprobe enriches these optional values; if it is unavailable or probing fails, GlyphFrame preserves the Phase 5 managed fallback and leaves unavailable values `Unknown` rather than inventing them. No video frames are decoded in Phase 7.

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

## Bundled FFmpeg tools

Development builds and publish output contain:

```text
Tools/ffmpeg/
├── ffmpeg.exe
├── ffprobe.exe
├── DISTRIBUTION_README.txt
├── LICENSE.txt
└── SHA256SUMS.txt
```

GlyphFrame resolves these tools relative to the running application directory, never the current working directory. It validates each executable with `-version` asynchronously at startup and caches the result for the session. Settings → Application shows availability, separate FFmpeg and ffprobe versions, the resolved folder, technical details, and a Refresh action. Missing or invalid tools do not prevent the main window from opening. Users do not need to install FFmpeg or modify `PATH`.

## Documentation

- [GlyphFrame Technical Specification v1.0](docs/GlyphFrame_Technical_Specification_v1.0.md)
- [GlyphFrame Design Reference](docs/design_reference/GlyphFrame_Design_Reference.png)

## Upcoming phases

- Phases 8–11 add video preview and processing, audio, progress, and cancellation using the Phase 7 tool infrastructure.
- Later phases cover error handling, UI polish, comprehensive testing, and portable packaging.

## Privacy

GlyphFrame is local-only software. It does not upload media and includes no telemetry or analytics. Media input is opened read-only and is never copied, renamed, modified, or deleted. Image pixels are processed only in memory, and image export writes only to the explicitly selected destination. Settings remain on the current device under the user-local application-data directory; media paths and media content are not stored in preferences.
