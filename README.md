# GlyphFrame

GlyphFrame is a Windows desktop application for converting local videos and images into colored symbol art.

## Status

Phase 4 — Settings Infrastructure. GlyphFrame now has strongly typed local preferences, safe JSON persistence and recovery, six live accent presets, and a dark settings dialog for editing, saving, cancelling, and resetting defaults. Media input, metadata extraction, preview generation, processing, and export are not yet implemented.

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
- xUnit

SkiaSharp, OpenCvSharp, FFmpeg, and ffprobe are planned for later phases and are intentionally not included in Phase 4. The settings screen reports FFmpeg validation as not yet available rather than fabricating a status.

## Solution structure

```text
GlyphFrame/
├── GlyphFrame.sln
├── src/
│   ├── GlyphFrame.App/             WPF UI, MVVM, and composition root
│   ├── GlyphFrame.Core/            Domain models and platform-independent logic
│   ├── GlyphFrame.Rendering/       Future symbolization and rendering logic
│   └── GlyphFrame.Infrastructure/  Local paths and JSON settings; future media and logging
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

## Documentation

- [GlyphFrame Technical Specification v1.0](docs/GlyphFrame_Technical_Specification_v1.0.md)
- [GlyphFrame Design Reference](docs/design_reference/GlyphFrame_Design_Reference.png)

## Upcoming phases

- Phases 5–6 add media input and the image symbolization pipeline.
- Phases 7–11 add bundled FFmpeg tooling, video processing, audio, progress, and cancellation.
- Later phases cover error handling, UI polish, comprehensive testing, and portable packaging.

## Privacy

GlyphFrame is local-only software. It does not upload media and includes no telemetry or analytics. Settings remain on the current device under the user-local application-data directory; media paths and media content are not stored in preferences.
