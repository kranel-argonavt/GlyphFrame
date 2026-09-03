# GlyphFrame

GlyphFrame is a Windows desktop application for converting local videos and images into colored symbol art.

## Status

Phase 11B.2 — Managed Video Artifact Foundation is complete. The backend can render a complete symbolized video into one application-owned MP4 review artifact with the captured Keep Audio or Remove Audio choice. Full-video identity, safe replacement, cancellation cleanup, and orphan recovery are independent from frame-preview position. Image Render → Review → Export, video frame preview, and the transitional direct video export remain unchanged; the player and artifact-based user export arrive in later Phase 11B work.

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
- FFmpeg and ffprobe 9.0 essentials build for Unicode-safe local frame extraction, metadata probing, streaming MP4 encoding, and audio muxing
- xUnit

MetadataExtractor is an Apache-2.0 package used only by `GlyphFrame.Infrastructure` for header metadata. MKV metadata is read by a bounded internal EBML header reader that skips media clusters. SkiaSharp and its Windows native assets are MIT-licensed and isolated to `GlyphFrame.Rendering`. Video-frame decoding uses the same checksum-verified bundled FFmpeg distribution as the other video infrastructure, with no additional native decoding package.

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

Video metadata includes the container, file size, resolution and duration, plus FPS, frame count, codecs, audio presence, and sample rate when ffprobe or the managed container reader can establish them reliably. The validated bundled ffprobe enriches these optional values; if it is unavailable or probing fails, GlyphFrame preserves the Phase 5 managed fallback and leaves unavailable values `Unknown` rather than inventing them.

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
→ retain the symbolized image for review
→ export the retained result as PNG or JPEG
```

Built-in character presets are Simple ASCII, Detailed ASCII, and Unicode Blocks. Their ramps stay ordered from sparse to dense. Supported fonts are Cascadia Mono, Consolas, Courier New, and Lucida Console at sizes 9, 10, and 12. Missing fonts fall back safely, and the output modes are Same as source, 720p, and 1080p while preserving the normalized source aspect ratio.

`Light on Dark` is the default polarity: bright areas use denser symbols on the dark GlyphFrame background, making pale subjects on dark scenes remain visually bright. `Dark on Light` maps dark areas to denser symbols on a neutral near-white background for a traditional printed-ASCII appearance. Changing polarity marks the retained symbolized image stale; `Render Again` must create a current result before export becomes available.

PNG is the default output format; JPG/JPEG is also supported at a centralized quality of 90. Export uses an atomic temporary-file strategy, confirms before replacing an existing file, never overwrites the input image, and removes partial files after failure or cancellation. Changing a render setting retains the previous review but marks it stale and disables export. Output-format, destination, tab, and fit changes do not invalidate rendering. Repeated PNG/JPEG exports encode the retained result directly and never rerun symbolization.

Transparent source pixels use straight alpha and are composited exactly once over the selected opaque polarity background before color and luminance sampling. Preview, PNG export, and JPEG export therefore use the same visible-color and background rules.

## Video frame preview

MP4, MOV, MKV, and AVI selections load one original frame near the 50% position after metadata succeeds. The video-only slider maps start to `0.0` and end to `1.0`; known durations are shown as current time and total duration, while incomplete containers retain a normalized position display. Slider changes are debounced for 200 ms, superseded requests are cancelled, and one serialized FFmpeg extraction gate prevents an unlimited number of decoder processes. A media/request generation check ensures that only the newest frame can reach the UI.

GlyphFrame maps the normalized slider value to an invariant FFmpeg timestamp when duration is known and backs the `1.0` position away from the duration boundary to request the final available frame. Input paths are passed as individual Unicode-safe process arguments, including spaces, Cyrillic, and mixed-language filenames. FFmpeg selects the first video stream, decodes exactly one display-oriented frame, and writes tightly packed straight-alpha RGBA8888 bytes through an in-memory binary pipe. GlyphFrame validates the exact byte count and takes ownership of the managed buffer; it creates no copied videos or temporary frame files.

`Preview Frame` passes the currently displayed owned frame to the same grid, luminance, character-preset, polarity, and SkiaSharp renderer used for images. Moving the slider, selecting other media, or changing a render setting makes the old symbolized frame stale; output-format-only changes do not. Preview remains intentionally one frame at a time—there is no animated playback.

## Streaming video and audio export

The Phase 9 foundation opens one long-lived bundled-FFmpeg decoder for the selected video and reads one display-oriented straight-alpha RGBA frame at a time. Each frame passes through the same character preset, font measurement, symbol grid, luminance mapping, color sampling, polarity, and SkiaSharp renderer used by image export. A second long-lived bundled-FFmpeg process receives rendered RGBA frames through stdin with backpressure and writes video-only H.264 at CRF 20, the `medium` preset, and `yuv420p` into an MP4 intermediate.

The source FPS is retained when it is finite, positive, and no greater than 240 FPS; otherwise export uses a deterministic 30 FPS fallback. Because `yuv420p` requires even dimensions, an odd trailing row or column is cropped after rendering—never stretched—while preserving the source aspect ratio to within that single-pixel adjustment.

Phase 10 captures the selected `Keep Audio` or `Remove Audio` mode when export starts. Remove Audio publishes the completed video-only intermediate directly. Keep Audio runs a strict bundled-ffprobe check: a confirmed silent source also publishes video-only, AAC is stream-copied into MP4, and incompatible or unknown codecs are converted to AAC at 192 kb/s, 48 kHz stereo. Muxing always uses `-c:v copy`, so the symbolized H.264 stream is never encoded again.

The rendered frame count and normalized FPS define the authoritative video duration. FFmpeg maps the completed symbolized video plus the original audio, applies that duration as an output cap without `-shortest`, allows shorter audio to end naturally, and prevents longer audio from extending the MP4. Video and audio partials are unique files beside the destination; only a fully completed result is atomically published. Failure or cancellation removes both partials and preserves an existing destination. Unicode paths use `ArgumentList`; no temporary PNG, raw-frame, copied-source, or copied-video files are created.

Phase 11 progress moves through Preparing, frame processing, encoder finalization, optional audio inspection and merging, output finalization, and Completed. Frame counts advance only after FFmpeg accepts a rendered frame through its awaited stdin write. A valid source frame count enables finite monotonic progress up to 99%; unavailable totals use an indeterminate bar and never invent a total. Elapsed time is measured from the export start, and remaining time appears only after enough determinate frame throughput exists; it is hidden during audio and finalization stages. The UI reaches 100% only after atomic publication succeeds.

Cancel immediately changes the export state to cleanup, disables repeated cancellation and conflicting actions, and propagates one cancellation token through decoding, rendering, encoding, audio probing, and muxing. Active FFmpeg/ffprobe process trees are terminated through the existing bounded cleanup infrastructure. The UI remains in the cancelling state until streams, frames, processes, and partial files have been released; incomplete output is never published and an existing destination is preserved.

Export-status motion is presentation-only: the segmented accent ring rotates and gently pulses, determinate bar changes ease toward the latest truthful value, and meaningful stage changes crossfade. A successfully published video briefly shows a green completion mark after export resources have been released. GlyphFrame honors the Windows client-area animation preference; with reduced motion, these values and states update immediately without changing progress calculation, cancellation, or cleanup.

## Phase 11B workflow

Phase 11B separates creation from publication:

```text
Load media -> Render -> Review -> Export
```

Phase 11B.1 completes the image half of this workflow. Image mode has no separate symbolized-preview command: one stable preview-panel action starts as `Render Image` and becomes `Render Again` after a result exists. Rendering creates one reusable full image result, selects the existing Symbolized tab, and enables `Export Image` in the persistent result panel. A stale result remains reviewable until that single `Render Again` action safely replaces it, and successful export retains it for further PNG/JPEG exports. The image Output card is removed; the transitional card remains video-only.

Phase 11B.2 adds the backend for full rendering while intentionally leaving the transitional video UI in place. It composes the proven sequential FFmpeg decoder, shared symbolization and SkiaSharp renderer, H.264 encoder, and Phase 10 audio path to create a completed MP4 under `%LocalAppData%\GlyphFrame\Cache\VideoArtifacts\<session>`. Candidate files are promoted only after successful validation. Each running application session holds an exclusive filesystem lease, so startup recovery preserves every active GlyphFrame instance and removes only stale session directories. One accepted artifact may be retained; a replacement is accepted before cleanup of the previous file, and failed cleanup is tracked and retried at later lifecycle boundaries. All candidate and cleanup operations remain restricted to canonical files in the owning session.

Later Phase 11B work will add `Render Video` as the primary full-video action, play the controlled H.264/AAC-or-silent artifact in the existing Symbolized tab, and publish that exact reviewed artifact to a chosen destination without rerunning symbolization or video encoding. No player or artifact-based user export is exposed in Phase 11B.2.

The panel below the preview is now the canonical image processing/result surface: it shows truthful activity and Cancel while rendering, then settles into Image Ready, Render Stale, Exported, or operation-specific Failed/Cancelled states. Once rendered pixels exist, the panel owns PNG/JPEG selection, `Export Image` / `Export Again`, and `Open Folder`; it does not duplicate the preview-panel render action. Failed or cancelled export keeps the retained pixels available for retry, and repeated export never reruns symbolization. Later work will extend the same model to managed video artifacts and remove the transitional video Output card.

Render identity will change for character, font, size, polarity, resolution, and other symbolization settings; video audio-mode changes will also stale the full-video artifact. Image format and destination changes will not stale image rendering, and video slider movement will invalidate only the frame preview. Player handles, partial files, replaced artifacts, and orphaned cache entries will be cleaned on every applicable success, cancellation, failure, media replacement, rerender, exit, and startup-recovery path.

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

- Later Phase 11B subphases connect the managed full-video artifact to `Render Video`, add in-app rendered-video playback, and publish the reviewed artifact without rerendering.
- Phase 12 then adds expanded friendly-error presentation, technical Show Details, and local logging on top of the final workflow states.
- Later phases cover UI polish, comprehensive testing, and portable packaging.

## Privacy

GlyphFrame is local-only software. It does not upload media and includes no telemetry or analytics. Media input is opened read-only and is never copied, renamed, modified, or deleted. Image pixels, preview frames, and sequential video-render frames are processed only in bounded memory; exports write only to the explicitly selected destination. Phase 11B permits one generated review MP4 in an application-managed local cache for the active workflow, with deterministic cleanup and no source-media copy. Settings remain on the current device under the user-local application-data directory; media paths, decoded frames, and media content are not stored in preferences.
