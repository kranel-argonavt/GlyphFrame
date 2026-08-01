# GlyphFrame

GlyphFrame is a Windows desktop application for converting local videos and images into colored symbol art.

## Status

Phase 3 — UI Skeleton. The complete WPF editor shell, reusable dark visual resources, binding-driven image/video/export presentation states, and safe placeholder commands are in place. Media input, preview generation, and export are not yet implemented.

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
- xUnit

SkiaSharp, OpenCvSharp, FFmpeg, and ffprobe are planned for later phases and are intentionally not included in Phase 3.

## Solution structure

```text
GlyphFrame/
├── GlyphFrame.sln
├── src/
│   ├── GlyphFrame.App/             WPF UI, MVVM, and composition root
│   ├── GlyphFrame.Core/            Domain models and platform-independent logic
│   ├── GlyphFrame.Rendering/       Future symbolization and rendering logic
│   └── GlyphFrame.Infrastructure/  Future media, filesystem, settings, and logging
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

## Documentation

- [GlyphFrame Technical Specification v1.0](docs/GlyphFrame_Technical_Specification_v1.0.md)
- [GlyphFrame Design Reference](docs/design_reference/GlyphFrame_Design_Reference.png)

## Upcoming phases

- Phases 4–6 add settings, media input, and the image symbolization pipeline.
- Phases 7–11 add bundled FFmpeg tooling, video processing, audio, progress, and cancellation.
- Later phases cover error handling, UI polish, comprehensive testing, and portable packaging.

## Privacy

GlyphFrame processes media locally. It does not upload media and includes no telemetry or analytics. Local settings and logs will be introduced in later phases under the privacy requirements in the technical specification.
