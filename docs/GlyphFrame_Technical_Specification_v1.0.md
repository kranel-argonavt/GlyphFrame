# GlyphFrame Technical Specification v1.0

Version: 1.0  
Status: Updated through Phase 10 — Audio
Project name: GlyphFrame  
Product subtitle: Video & Image to Symbol Converter  
Product description: Transform videos and images into beautiful symbol art.

## 1. Purpose

GlyphFrame is a Windows desktop application that converts videos and images into colored symbol art. The application works fully locally and does not upload user media to external services.

The product supports two main workflows:

- Convert an image into a symbol-art image.
- Convert a video into a symbol-art MP4 video.

The main value of the project is the custom C# symbolization pipeline:

```text
Media frame
-> Normalize orientation
-> Calculate symbol grid
-> Sample average cell color
-> Calculate luminance
-> Map luminance to a character
-> Render symbols with SkiaSharp
-> Export symbolized image or frame
```

GlyphFrame does not implement its own MP4 encoder. Video encoding, audio handling, and metadata probing are delegated to bundled FFmpeg tools.

## 2. Product Scope

### 2.1 MVP Includes

- Windows desktop application.
- Dark Fluent-style UI.
- Local image import.
- Local video import.
- Drag and drop media input.
- Original preview.
- Symbolized preview.
- Three built-in character presets.
- Font family selection.
- Font size selection.
- Output resolution selection.
- Symbol polarity selection.
- Image export to PNG and JPG/JPEG.
- Video export to MP4.
- Keep Audio and Remove Audio modes for video.
- Progress display during export.
- Cancel export.
- Local logs.
- Local settings.
- Bundled FFmpeg and ffprobe.

### 2.2 MVP Excludes

- Realtime video playback.
- Animated symbolized preview.
- Before/after draggable comparison slider.
- Batch export.
- GIF export.
- WebM export.
- PNG sequence export.
- Custom character sets.
- Save Preset.
- Automatic polarity detection.
- Auto-sort characters by density.
- Intensity slider.
- GPU acceleration.
- Hardware encoder UI.
- Pause export.
- Audio bitrate or codec controls.
- Light theme.
- Telemetry.
- Analytics.
- Installer.

## 3. Supported Platforms

Target platform:

```text
Windows 10 and Windows 11
```

Target distribution:

```text
Self-contained Windows x64 portable build
```

The user should not need to install:

- .NET Runtime.
- FFmpeg.
- FFmpeg PATH variables.

## 4. Technology Stack

### 4.1 Application

- Language: C#
- UI framework: WPF
- Architecture pattern: MVVM
- MVVM library: CommunityToolkit.Mvvm
- Rendering: SkiaSharp
- Video frame reading: bundled FFmpeg 9.0 through a bounded binary stdout pipe
- Video encoding and audio processing: bundled FFmpeg
- Metadata probing: ffprobe
- Testing: xUnit or NUnit

### 4.2 Bundled Tools

The final application package must include:

```text
GlyphFrame/
├── GlyphFrame.exe
└── Tools/
    └── ffmpeg/
        ├── ffmpeg.exe
        └── ffprobe.exe
```

FFmpeg license information must be included in:

```text
THIRD_PARTY_NOTICES.md
```

The Phase 7 bundled distribution is Gyan Doshi's FFmpeg 9.0 Windows x64 static
`essentials_build`. Because the build enables GPL and GPL-covered codecs, it is
distributed under GPLv3 rather than described as an LGPL-only build. Exact binary
and archive SHA-256 checksums, the upstream source revision, distribution source,
and the complete license text are included with the repository and copied to build
and publish output.

GlyphFrame resolves both executables relative to the actual application directory.
It does not use the current working directory or system `PATH` as its primary lookup.
Each tool is validated independently with `-version`; missing, invalid, inaccessible,
timed-out, and non-zero-exit results remain non-fatal and are presented in Settings.

## 5. Media Requirements

### 5.1 Supported Image Input Formats

GlyphFrame must support the following image input formats:

```text
PNG
JPG
JPEG
WebP
BMP
```

Image loading must:

- Read local files from disk.
- Validate file existence.
- Validate file extension.
- Reject unsupported formats with a friendly message.
- Normalize orientation when metadata requires rotation.
- Preserve source aspect ratio.
- Read basic metadata: width, height, format, file size.
- Handle Unicode file paths.
- Avoid locking the source file after loading.

### 5.2 Supported Image Output Formats

GlyphFrame must support the following image output formats:

```text
PNG
JPG
JPEG
```

Default image export format:

```text
PNG
```

Image export must:

- Use the selected output resolution.
- Preserve orientation and aspect ratio.
- Save symbolized output through SkiaSharp.
- Confirm overwrite when the target file already exists.
- Show a friendly error when the output location is unavailable.
- Export without creating temporary video frame files.

### 5.3 Supported Video Input Formats

GlyphFrame must support the following video input formats:

```text
MP4
MOV
MKV
AVI
```

Bundled FFmpeg is the authoritative video-preview decoder and bundled ffprobe is the authoritative metadata enrichment path. Neither operation depends on the system `PATH`; Unicode, spaces, and mixed-language paths are passed as individual process arguments.

Video loading must:

- Read local files from disk.
- Validate file existence.
- Validate file extension.
- Read metadata through ffprobe.
- Detect width, height, duration, FPS, frame count, video codec, audio presence, and audio codec.
- Handle videos without audio.
- Handle Unicode file paths.
- Show friendly errors for unsupported or unreadable files.
- Decode exactly one display-oriented preview frame through bundled FFmpeg without requiring a system media installation.
- Read tightly packed straight-alpha RGBA8888 through bounded binary stdout while draining bounded stderr concurrently.
- Avoid temporary frames, copied videos, and command-line string concatenation.
- Release the source file after every frame read.

### 5.4 Supported Video Output Format

GlyphFrame MVP exports video only as:

```text
MP4
```

Video export parameters:

```text
Container: MP4
Video codec: H.264
Pixel format: yuv420p
Quality: CRF 20
Encoding preset: medium
FPS: normalized source FPS
Audio: stream copy when compatible, otherwise AAC
```

## 6. Symbolization Core

### 6.1 Shared Pipeline

The same conversion core must be used for image and video frames.

```text
Input bitmap/frame
-> Normalize orientation
-> Resolve output resolution
-> Measure selected glyph metrics
-> Calculate automatic symbol grid
-> Resolve symbol polarity and its opaque canvas background
-> For each grid cell:
   -> Sample average color
   -> Calculate luminance
   -> Select character from preset
-> Render symbols through SkiaSharp
-> Return rendered symbol-art bitmap/frame
```

### 6.2 Automatic Symbol Grid

The user must not manually edit `Columns x Rows` in the MVP.

The symbol grid must be calculated automatically from:

- Output resolution.
- Media aspect ratio.
- Orientation.
- Font family.
- Font size.
- Actual glyph width.
- Line height.

Smaller font sizes produce denser symbol art.

### 6.3 Character Presets

GlyphFrame MVP includes three built-in presets.

Simple ASCII:

```text
 .:-=+*#%@
```

Detailed ASCII:

```text
 .'`^",:;Il!i~+_-?][}{1)(|\/tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$
```

Blocks:

```text
 ░▒▓█
```

The `Blocks` preset uses Unicode characters and must be rendered with fonts that support them.

### 6.4 Luminance Mapping

Character ramps remain ordered from sparse to dense and are never reversed or duplicated. Luminance is calculated per cell from the average visible color of the sampled area using Rec. 709 coefficients.

GlyphFrame supports two explicit polarity modes:

```text
Light on Dark (default)
Black luminance -> first/sparsest character
White luminance -> last/densest character
index = round(luminance * (characterCount - 1) / 255)
```

Light on Dark uses the dark GlyphFrame symbol background (`R=13, G=15, B=20`). Bright cells therefore use denser bright symbols on a dark background, while dark cells remain sparse.

```text
Dark on Light
Black luminance -> last/densest character
White luminance -> first/sparsest character
index = round((255 - luminance) * (characterCount - 1) / 255)
```

Dark on Light uses a neutral near-white symbol background (`R=245, G=245, B=242`). Dark cells therefore use denser dark symbols on a light background, like traditional printed ASCII art.

Input pixels use straight-alpha, unpremultiplied RGBA8888. Before averaging or luminance calculation, every source pixel is composited exactly once over the background selected by the polarity. Fully transparent RGB values do not affect visible output. The symbol canvas, preview, PNG export, and JPEG export use the same resolved opaque background.

Undefined polarity values and empty character ramps are rejected. Mapping uses deterministic integer rounding and clamps the final index.

### 6.5 Color Mode

MVP supports only:

```text
Original Colors
```

Each rendered symbol uses the average source color of its cell.

The symbol frame stores its resolved opaque background color so renderers do not depend on global theme state or a later settings lookup.

## 7. Render Settings

### 7.1 Font Families

Supported fonts:

```text
Cascadia Mono
Consolas
Courier New
Lucida Console
```

Default font:

```text
Cascadia Mono
```

If the selected font is unavailable, GlyphFrame must fall back to Consolas and show no crash.

### 7.2 Font Sizes

Supported MVP font sizes:

```text
9
10
12
```

Default font size:

```text
10
```

### 7.3 Output Resolution

Supported output resolution modes:

```text
Same as source
720p
1080p
```

Landscape targets:

```text
720p:  1280 x 720
1080p: 1920 x 1080
```

Portrait targets:

```text
720p:  720 x 1280
1080p: 1080 x 1920
```

For `Same as source`, GlyphFrame uses the normalized source resolution. If the source resolution is higher than 1080p, the app must show a warning but still allow export.

### 7.4 Symbol Polarity

Supported modes:

```text
Light on Dark
Dark on Light
```

Default:

```text
Light on Dark
```

Changing polarity invalidates an existing symbolized preview because it changes both character mapping and the canvas background. Selecting the same value again does not invalidate the preview. Changing only PNG/JPEG output format does not invalidate symbol rendering.

## 8. Image Workflow

### 8.1 Image Input Flow

```text
1. User selects or drops an image.
2. App validates the file.
3. App reads metadata.
4. App displays the original image preview.
5. App hides video-only controls.
6. User adjusts character, polarity, and render settings.
7. User clicks Preview Symbolized Image.
8. App generates symbolized preview.
9. User selects output path and format.
10. User exports PNG or JPG/JPEG.
```

### 8.2 Image UI Rules

When the loaded media is an image:

- Audio section is hidden.
- Video frame slider is hidden.
- Export button label is `Export Image`.
- Preview button label is `Preview Symbolized Image`.
- Output format choices are `PNG` and `JPG/JPEG`.
- PNG is selected by default.
- Metadata row shows image width, height, format, and file size.

### 8.3 Image Acceptance Requirements

GlyphFrame must:

- Open PNG, JPG/JPEG, WebP, and BMP files.
- Show original image preview.
- Generate symbolized image preview.
- Export symbolized PNG.
- Export symbolized JPG/JPEG.
- Preserve aspect ratio.
- Respect selected output resolution.
- Respect selected symbol polarity in mapping, preview, and export.
- Composite transparent pixels over the selected polarity background exactly once.
- Keep UI responsive during preview and export.
- Show friendly errors for invalid or unsupported images.

## 9. Video Workflow

### 9.1 Video Input Flow

```text
1. User selects or drops a video.
2. App validates the file.
3. App validates bundled FFmpeg and ffprobe.
4. App reads metadata.
5. App displays an original preview frame.
6. App shows video-only controls.
7. User selects preview position with a slider.
8. User adjusts character and render settings.
9. User clicks Preview Symbolized Frame.
10. App generates symbolized preview frame.
11. User selects output path.
12. User exports MP4.
```

Phase 8 preview behavior:

- After metadata succeeds, request a frame near normalized position `0.5` and show a non-blocking loading state.
- The video slider maps start to `0.0` and end to `1.0` and reconciles to the nearest frame position actually returned by the decoder.
- Convert normalized position to an invariant FFmpeg timestamp when duration is known and back the end position away from the duration boundary to request the final available frame.
- Debounce slider input for 200 ms, cancel superseded requests, serialize extraction processes, and apply latest-request-wins generation checks.
- Preserve metadata and show a friendly retry state when frame loading fails.
- `Preview Symbolized Frame` uses the exact owned frame currently displayed in Original mode and the shared image symbolization pipeline.
- Preview identity includes media version, original-frame/source version, and render-settings version. A frame or setting change therefore makes the previous symbolized frame stale.
- Phase 8 does not implement animated playback, sequential processing, audio work, or full-video export.

### 9.2 Video Export Flow

```text
1. Validate input path.
2. Validate output path.
3. Validate ffmpeg.exe and ffprobe.exe.
4. Read video metadata.
5. Start sequential frame reading.
6. For each source frame:
   -> Convert frame to symbol frame.
   -> Render symbolized frame with SkiaSharp.
   -> Send raw frame to FFmpeg through stdin.
7. Close encoder stdin and verify successful FFmpeg completion.
8. Apply the captured audio mode.
9. For Remove Audio or a confirmed silent source, publish the video-only partial.
10. For Keep Audio, map original audio into a second MP4 partial using video stream copy plus audio copy or AAC fallback.
11. Publish only the completed final partial to the requested destination.
12. Clean every intermediate after success, failure, or cancellation.
13. Show the basic completed or failure status.
```

The Phase 9 encoder continues to pass `-an` and creates an authoritative video-only intermediate. Phase 10 performs its audio decision only after that intermediate completes and never decodes or re-encodes the symbolized H.264 stream during muxing.

### 9.3 Audio Modes

Supported audio modes:

```text
Keep Audio
Remove Audio
```

Rules:

- Audio mode is captured when export begins and settings remain disabled during the operation.
- `Keep Audio` requires a strict bundled-ffprobe result; unavailable audio metadata fails safely instead of silently dropping a possible stream.
- AAC is the MVP MP4-compatible stream-copy codec, compared case-insensitively after trimming.
- If `Keep Audio` is selected and source audio is AAC, use `-c:v copy -c:a copy`.
- If `Keep Audio` is selected and source audio is incompatible, unknown, or unnamed, use `-c:v copy -c:a aac -b:a 192k -ar 48000 -ac 2`.
- If source video has no audio, export a no-audio MP4 and show no failure.
- If `Remove Audio` is selected, final MP4 must contain no audio stream.
- Map the completed symbolized video as `0:v:0` and original source audio as `1:a:0`.
- The rendered frame count divided by normalized FPS is the authoritative duration. Apply it as the output duration cap without `-shortest`: shorter audio may end naturally, while longer audio must not extend the completed video.
- Stream-copy, AAC fallback, no-source-audio, and removed-audio outcomes are reported distinctly.
- Video-only and mux partials live beside the destination, remain unpublished until complete success, and are cleaned on every failure or cancellation path.

## 10. UI Specification

### 10.1 Overall Style

The UI must follow:

```text
Dark Fluent-style desktop editor
```

Design traits:

- Dark charcoal application background.
- Violet/purple accent color.
- Large preview area on the left.
- Settings panel on the right.
- Export status panel at the bottom.
- Minimal footer.
- Rounded cards with radius 8 px or less.
- Clean, practical desktop-tool layout.
- No marketing landing page.
- No light theme in MVP.

The approved reference controls the composition, proportions, information density, and placement of controls. The final implementation must not reinterpret the reference as a generic card dashboard.

### 10.2 Approved Visual Reference

Authoritative visual artifacts:

```text
docs/design_reference/GlyphFrame_Design_Reference.png
```

Reference rules:

- `GlyphFrame_Design_Reference.png` is the approved GlyphFrame-branded implementation reference for Phase 3, including layout, visual hierarchy, spacing, and desktop-tool density.
- The application name in the native title bar is `GlyphFrame`.
- The product header title is `Video & Image to Symbol Converter`.
- The product header subtitle is `Transform videos and images into beautiful symbol art`.
- The preset quick selector remains in the upper-right product header area.
- The preview and export-progress areas form the left column.
- Input, Characters, Render, Audio, and Output form the stacked right column.
- The loaded-video/exporting state shown in the design reference is the primary desktop reference state.
- Image mode uses the same shell and spacing while applying the conditional rules in section 8.2.
- Later implementation may improve accessibility and DPI behavior, but must preserve the approved hierarchy and recognizable composition.

### 10.3 Color Palette

```text
Main background:   #0D0F14
Card background:   #151820
Panel background:  #1B1F2A
Border:            #2A2F3A
Primary text:      #F3F4F6
Secondary text:    #9CA3AF
Accent violet:     #8B5CF6
Accent hover:      #A78BFA
Error:             #EF4444
Success:           #22C55E
Warning:           #F59E0B
```

### 10.4 Main Window Layout

```text
MainWindow
|
+-- Native-style Title Bar
|   +-- GlyphFrame app mark and name
|   +-- Minimize / Maximize / Close controls
|
+-- Product Header
|   +-- Video & Image to Symbol Converter
|   +-- Product description
|   +-- Presets quick selector
|
+-- Main Content
|   +-- Left Column
|   |   +-- Original / Symbolized tabs
|   |   +-- Preview surface
|   |   +-- Video frame slider when media is video
|   |   +-- Metadata row and preview action
|   |   +-- Export Status Panel
|   |
|   +-- Right Column
|       +-- Input
|       +-- Characters
|       +-- Render
|       +-- Audio when media is video
|       +-- Output
|
+-- Footer
    +-- Version
    +-- About
    +-- Settings
```

Desktop layout constraints:

- Default reference viewport is approximately 1440 x 1080 at 100% scaling.
- Main content uses two columns with the left column approximately 1.65 times the width of the right column.
- The left and right columns align at their top edge.
- Preview is the dominant surface and uses a 16:9 media stage.
- Export status sits directly below the preview, not across the full window width.
- Settings cards remain compact and vertically stacked with approximately 8-12 px gaps.
- Main outer spacing is approximately 20-24 px; card radius is no more than 8 px.
- At narrow window sizes, the layout may reflow vertically without clipping controls or text.

### 10.5 Title Bar and Product Header

Title bar content:

- App name: `GlyphFrame`
- App mark.
- Native window controls.

Product header content:

- Title: `Video & Image to Symbol Converter`
- Subtitle: `Transform videos and images into beautiful symbol art`
- `Presets` quick selector aligned to the right.

Header must not contain:

- Light theme toggle.
- Save Preset.

### 10.6 Preview Panel

Preview panel contains:

- `Original` tab.
- `Symbolized` tab.
- Large media preview surface.
- Empty state drop area.
- Metadata row.
- Preview action button.
- Video frame-position slider for videos only.
- Fit selector and fullscreen preview action.

Preview panel behavior:

- Initial state shows a drop area and `Browse Media`.
- Original tab becomes available after media load.
- Symbolized tab becomes available after symbolized preview generation.
- Changing render settings after preview marks the symbolized preview as stale.
- Export remains available only when input and output requirements are valid.
- Video selection loads an original frame near the middle, enables the normalized position slider, and displays current/total time when duration is known.
- Rapid slider changes are coalesced; a superseded or cancelled read never replaces the newest frame and is not shown as an error.
- Video frame loading and failure states are visually distinct while retaining successfully loaded metadata.
- `Preview Symbolized Frame` is enabled only after an owned original frame is available. In Phase 9, video export is enabled when valid video metadata is selected and no conflicting operation is active.

### 10.7 Settings Panel

Right settings sections:

```text
Input
Characters
Render
Audio
Output
```

Input:

- Browse Media button.
- File name.
- Media type.
- Source metadata.

Characters:

- Character preset dropdown.
- Ramp preview.
- Density labels `Sparse` and `Dense`.

Render:

- Font family dropdown.
- Font size dropdown.
- Output resolution dropdown.
- Symbol Polarity dropdown with `Light on Dark` and `Dark on Light`.
- Color mode display set to Original Colors.

Audio:

- Visible only for video.
- Keep Audio option.
- Remove Audio option.

Output:

- Output path picker.
- Output format selector for images.
- MP4 output indicator for videos.
- Export button.

### 10.8 Export Status Panel

The export status panel must support:

- Ready state.
- Preview generation state.
- Exporting state.
- Completed state.
- Cancelled state.
- Error state.

During export it shows:

- Status text.
- Progress bar.
- Current frame and total frame count for video.
- Elapsed time.
- Estimated remaining time when available.
- Cancel button.

It must not include:

- Pause button.
- GPU acceleration indicator.
- Hardware encoder selector.

### 10.9 Footer

Footer contains:

- `v1.0.0`
- About button.
- Settings button.

Footer does not contain:

- GPU text.
- Theme toggle.
- Hardware information.

## 11. UI States

### 11.1 State List

```text
Empty
LoadingMedia
LoadingVideoFrame
MediaLoaded
GeneratingPreview
PreviewReady
Exporting
ExportCompleted
Cancelled
Error
```

### 11.2 State Rules

Empty:

- No media loaded.
- Drop area visible.
- Export disabled.

LoadingMedia:

- Media validation and metadata reading in progress.
- Browse and drop disabled.
- Loading indicator visible.

LoadingVideoFrame:

- Selected video metadata remains visible.
- Original-frame decoding runs away from the UI thread.
- Conflicting preview actions are temporarily disabled; the position slider remains available so a newer request can supersede the active read.
- Superseded frame reads are cancelled without displaying an error.

MediaLoaded:

- Original preview visible.
- Settings enabled.
- Preview action enabled.
- Export enabled only when output path is valid.
- In video mode, the currently resolved original frame and position controls remain available, and Phase 10 enables MP4 export with the captured Keep Audio or Remove Audio choice when prerequisites are valid.

GeneratingPreview:

- Preview button disabled.
- Render settings temporarily disabled.
- Loading indicator visible in preview panel.

PreviewReady:

- Symbolized preview visible.
- Original and Symbolized tabs enabled.

Exporting:

- Export button disabled.
- Cancel button enabled.
- Progress panel active.
- UI remains responsive.

ExportCompleted:

- Success status shown.
- Open output location action available.
- Temporary files cleaned.

Cancelled:

- Cancelled status shown.
- Partial output removed or clearly marked invalid.
- Temporary files cleaned.

Error:

- Friendly message shown.
- `Show Details` available for technical detail.
- Raw exception text is not shown as the primary message.

## 12. Settings

Settings are stored locally in a JSON file under the application data folder.

### 12.1 Appearance

Accent options:

```text
Violet
Purple
Blue
Cyan
Emerald
Rose
```

Default accent:

```text
Violet
```

Light theme is not included in MVP.

### 12.2 Defaults

Settings must allow default values for:

- Character preset.
- Font family.
- Font size.
- Output resolution.
- Symbol polarity (`Light on Dark` by default).
- Image export format.
- Video audio mode.

The polarity is stored as a readable enum name. Existing settings files without the property use `LightOnDark`; invalid values normalize to that default. Reset Settings also restores `LightOnDark` without changing unrelated defaults or transient media state.

### 12.3 Application

Settings must include:

- Open logs folder.
- FFmpeg status.
- Reset settings.
- Application data location.

## 13. Architecture

### 13.1 Solution Structure

```text
GlyphFrame.sln

GlyphFrame.App
GlyphFrame.Core
GlyphFrame.Rendering
GlyphFrame.Infrastructure
GlyphFrame.Tests
```

### 13.2 Folder Structure

```text
GlyphFrame
├── GlyphFrame.App
│   ├── Views
│   ├── ViewModels
│   ├── Controls
│   ├── Resources
│   │   ├── Styles
│   │   ├── Icons
│   │   └── Themes
│   └── Services
├── GlyphFrame.Core
│   ├── Models
│   ├── Interfaces
│   ├── Enums
│   ├── Presets
│   └── Validation
├── GlyphFrame.Rendering
│   ├── Grid
│   ├── Mapping
│   ├── Conversion
│   └── Skia
├── GlyphFrame.Infrastructure
│   ├── Ffmpeg
│   ├── Media
│   ├── Audio
│   ├── Images
│   ├── Settings
│   ├── Logging
│   └── FileSystem
└── GlyphFrame.Tests
```

### 13.3 Core Components

Required components:

```text
MediaTypeDetector
ImageMetadataReader
VideoMetadataReader
ImageReader
VideoFrameReader
PreviewFrameService
CharacterPresetProvider
SymbolGridCalculator
BrightnessToSymbolMapper
FrameToSymbolConverter
SkiaSymbolFrameRenderer
ImageExporter
StreamingVideoEncoder
AudioStreamProbe
AudioCompatibilityPolicy
AudioMuxer
ExportService
FfmpegProcessRunner
BinaryProcessRunner
TempFileManager
SettingsService
LoggingService
StartupValidationService
```

### 13.4 Key Interfaces

```csharp
public interface IMediaTypeDetector
{
    MediaType Detect(string path);
}
```

```csharp
public interface IImageMetadataReader
{
    Task<ImageMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken);
}
```

```csharp
public interface IVideoMetadataReader
{
    Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken);
}
```

```csharp
public interface IVideoFrameReader
{
    Task<VideoFrameReadResult> ReadAsync(
        string inputPath,
        double normalizedPosition,
        VideoMetadata? metadataContext,
        CancellationToken cancellationToken);
}
```

`IVideoFrameReader` is platform-neutral. FFmpeg paths, process arguments, binary pipes, exit codes, and backend diagnostics remain isolated in Infrastructure. A successful result owns an exact, tightly packed, straight-alpha RGBA8888 managed buffer independently of the completed process and reports requested and resolved position/timestamp/frame context only when available.

```csharp
public interface ICharacterPresetProvider
{
    IReadOnlyList<CharacterPreset> GetPresets();
}
```

```csharp
public interface ISymbolGridCalculator
{
    SymbolGrid Calculate(MediaDimensions dimensions, RenderSettings settings);
}
```

```csharp
public interface IBrightnessToSymbolMapper
{
    char Map(byte luminance, string characterSet, SymbolPolarity polarity);
}
```

```csharp
public interface IFrameToSymbolConverter
{
    SymbolFrame Convert(FrameBitmap frame, RenderSettings settings, CharacterPreset preset);
}
```

```csharp
public interface ISymbolFrameRenderer
{
    RenderedFrame Render(SymbolFrame frame, RenderSettings settings);
}
```

```csharp
public interface IImageExporter
{
    Task ExportAsync(RenderedFrame frame, ImageExportOptions options, CancellationToken cancellationToken);
}
```

```csharp
public interface IVideoEncoder
{
    Task<VideoEncodingResult> EncodeAsync(
        VideoEncodingJob job,
        IAsyncEnumerable<RenderedImage> frames,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IVideoExportService
{
    Task<VideoExportResult> ExportAsync(VideoExportJob job, CancellationToken cancellationToken);
}
```

`ISequentialVideoFrameReader` is separate from the random-access preview reader. It owns one bundled-FFmpeg decoder for the export lifetime and yields exactly one owned display-oriented RGBA8888 frame at a time. `IVideoEncoder` owns one bundled-FFmpeg encoder and awaits every stdin write so pipe backpressure bounds memory. `IVideoExportService` coordinates those contracts with the shared image pipeline and never exposes processes, WPF, or native rendering types through Core.

## 14. Data Models

### 14.1 MediaType

```csharp
public enum MediaType
{
    Image,
    Video
}
```

### 14.2 ImageMetadata

```csharp
public sealed class ImageMetadata
{
    public string FilePath { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public bool HasOrientationMetadata { get; init; }
}
```

### 14.3 VideoMetadata

```csharp
public sealed class VideoMetadata
{
    public string FilePath { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public double Fps { get; init; }
    public TimeSpan Duration { get; init; }
    public int FrameCount { get; init; }
    public bool HasAudio { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public int? AudioSampleRate { get; init; }
    public int? RotationDegrees { get; init; }
    public int? DisplayWidth { get; }
    public int? DisplayHeight { get; }
}
```

Encoded dimensions remain available for diagnostics. Display dimensions apply normalized quarter-turn rotation metadata and define the exact display-oriented RGBA frame layout expected from FFmpeg's default autorotation behavior.

### 14.4 VideoFrameReadResult

The Phase 8 result model contains:

```text
Status and typed error category
Owned DecodedImage frame on success
Requested normalized position
Actual normalized position when available
Requested and actual timestamps when available
Actual frame index when available
Duration and frame-count context when available
Friendly user message
Separate technical detail
```

Cancellation is represented explicitly and superseded cancellation is not treated as a user-facing error. Unknown or unreliable timing fields remain absent rather than being fabricated.

### 14.5 RenderSettings

```csharp
public sealed class RenderSettings
{
    public string FontFamily { get; init; } = "Cascadia Mono";
    public int FontSize { get; init; } = 10;
    public OutputResolutionMode OutputResolution { get; init; } = OutputResolutionMode.SameAsSource;
    public SymbolPolarity SymbolPolarity { get; init; } = SymbolPolarity.LightOnDark;
    public ColorMode ColorMode { get; init; } = ColorMode.OriginalColors;
}
```

The platform-neutral `SymbolFrame` stores the resolved opaque `BackgroundColor`. This makes preview, still-image export, and the future video pipeline deterministic without consulting mutable application settings during rendering.

### 14.6 CharacterPreset

```csharp
public sealed class CharacterPreset
{
    public string Name { get; init; } = "";
    public string Characters { get; init; } = "";
    public bool IsUnicode { get; init; }
}
```

### 14.7 ExportJob

```csharp
public sealed class ExportJob
{
    public string InputPath { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public MediaType MediaType { get; init; }
    public RenderSettings RenderSettings { get; init; } = new();
    public CharacterPreset CharacterPreset { get; init; } = new();
    public ImageExportFormat? ImageFormat { get; init; }
    public AudioExportMode? AudioMode { get; init; }
}
```

### 14.8 ExportProgress

```csharp
public sealed class ExportProgress
{
    public ExportStatus Status { get; init; }
    public int CurrentFrame { get; init; }
    public int TotalFrames { get; init; }
    public double Percentage { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? Remaining { get; init; }
    public string Message { get; init; } = "";
}
```

## 15. Enums

```csharp
public enum AudioExportMode
{
    KeepAudio,
    RemoveAudio
}
```

```csharp
public enum ColorMode
{
    OriginalColors
}
```

```csharp
public enum ExportStatus
{
    Ready,
    LoadingMedia,
    GeneratingPreview,
    Exporting,
    MergingAudio,
    Completed,
    Cancelled,
    Failed
}
```

```csharp
public enum OutputResolutionMode
{
    SameAsSource,
    HD720,
    FullHD1080
}
```

```csharp
public enum ImageExportFormat
{
    Png,
    Jpeg
}
```

```csharp
public enum SymbolPolarity
{
    LightOnDark,
    DarkOnLight
}
```

## 16. Privacy and Local Data

GlyphFrame must follow these privacy rules:

- All processing is local.
- No file upload.
- No telemetry.
- No analytics.
- Logs are stored locally.
- Logs must not contain image pixels, video frames, or media content.
- Temporary files are removed after success, failure, or cancel.
- Settings are stored locally.
- The current video preview frame exists only in memory; it is not copied to application data, preferences, logs, or a temporary image file.

## 17. Error Handling

The app must show friendly messages for:

- Input file not found.
- Unsupported format.
- Corrupted or unreadable media.
- ffmpeg.exe not found.
- ffprobe.exe not found.
- Output path unavailable.
- Permission denied.
- Not enough disk space.
- Video frame reading error.
- Missing, invalid, or inaccessible bundled FFmpeg executable.
- Video open, seek, frame decode, timeout, or incomplete binary-frame failure.
- Encoding error.
- Audio muxing error.
- Export cancellation.

Primary UI messages must be user-friendly. Technical details may be available behind `Show Details`.

## 18. Performance Requirements

GlyphFrame must:

- Keep the UI responsive during preview generation.
- Keep the UI responsive during export.
- Run long operations asynchronously.
- Show progress during video export.
- Support cancellation.
- Avoid writing thousands of temporary PNG frames for video export.
- Stream rendered frames to FFmpeg through stdin.
- Dispose bitmaps, frames, and process resources correctly.
- Keep at most the current decoded video frame in memory; do not cache an unbounded frame history.
- Serialize random-access FFmpeg extractions, debounce slider input, and reject late results with generation identifiers in addition to cancellation.
- Bound extraction time, RGBA stdout, and stderr; terminate the process tree on timeout or cancellation.
- Never retain process handles, pipe buffers, or source-file handles after a frame read completes.
- Probe Keep Audio strictly with bundled ffprobe; never treat unknown audio presence as confirmed silence.
- Preserve the already encoded symbolized H.264 stream with `-c:v copy` during audio muxing.
- Bound mux stderr and completion time, terminate the process tree on cancellation, and clean both video and audio partials.

## 19. Testing Requirements

Minimum test matrix:

- PNG input.
- JPG/JPEG input.
- WebP input.
- BMP input.
- MP4 input.
- MOV input.
- MKV input.
- AVI input.
- Landscape media.
- Portrait media.
- Rotation metadata.
- Video with audio.
- Video without audio.
- Keep Audio.
- Remove Audio.
- 720p output.
- 1080p output.
- Same as source output.
- Light on Dark endpoint, midpoint, and boundary mapping.
- Dark on Light endpoint, midpoint, and boundary mapping.
- Exact dark and light symbol backgrounds.
- Straight-alpha compositing over both symbol backgrounds.
- Polarity persistence, missing-property migration, invalid-value normalization, and reset.
- Preview invalidation and stale async-result rejection after polarity changes.
- PNG and JPEG export in both polarity modes.
- Source resolution above 1080p warning.
- Unicode file paths.
- Output path already exists.
- Missing FFmpeg.
- Missing ffprobe.
- Cancel during export.
- Invalid/corrupted media.
- Real bundled-FFmpeg frame decoding for MP4, MOV, MKV, and AVI with committed offline fixtures.
- Genuine Cyrillic, spaces, and mixed-language directory and filename paths passed directly to FFmpeg.
- Start, middle, near-end, short-video, normalized-clamping, owned-buffer, channel-order, cancellation, and file-lock frame-reader behavior.
- Middle-frame ViewModel loading, retryable failure, slider visibility, seek coalescing, latest-request-wins, and media-switch races.
- Video symbolization from the displayed frame plus stale identity after frame, media, or render-setting changes.
- Strict audio-present/no-audio/probe-failure behavior, compatible-codec policy, AAC fallback, mux arguments, process lifecycle, and cleanup.
- Real bundled-FFmpeg Keep Audio stream-copy, incompatible-audio AAC fallback, Remove Audio, silent-source, short-audio, and long-audio export fixtures.
- Debug, Release, and publish inclusion of FFmpeg, ffprobe, checksums, licenses, and third-party notices with no obsolete video-decoder native assets.

## 20. Packaging Requirements

MVP packaging target:

```text
Portable ZIP
```

Package must include:

- GlyphFrame executable.
- Required .NET runtime files for self-contained publish.
- Required native dependencies.
- FFmpeg and ffprobe.
- THIRD_PARTY_NOTICES.md.
- README.md.

The app should be tested on a clean Windows machine before release.

## 21. Roadmap

### Phase 1 - Final Documentation

Result:

```text
Final GlyphFrame Technical Specification v1.0
Final UI design reference
Acceptance criteria
Updated project naming
```

Tasks:

- Create unified technical specification.
- Use `GlyphFrame` everywhere.
- Add image requirements.
- Add UI specification.
- Save the final UI design reference.
- Record acceptance criteria.

### Phase 2 - Repository and Solution

- Create repository `GlyphFrame`.
- Create `GlyphFrame.sln`.
- Add projects:
  - `GlyphFrame.App`
  - `GlyphFrame.Core`
  - `GlyphFrame.Rendering`
  - `GlyphFrame.Infrastructure`
  - `GlyphFrame.Tests`
- Configure project references.
- Add `.gitignore`.
- Add README skeleton.
- Configure dependency injection.
- Add CommunityToolkit.Mvvm.
- Create test project.

### Phase 3 - UI Skeleton

- MainWindow.
- Header.
- Preview panel.
- Settings cards.
- Export status panel.
- Footer.
- Resource dictionaries.
- Accent colors.
- Image/video conditional states.
- Placeholder ViewModels and commands.

### Phase 4 - Settings Infrastructure

- Local `settings.json`.
- Accent presets.
- Default character preset.
- Default font.
- Default font size.
- Default resolution.
- Default symbol polarity.
- Default image format.
- Default audio mode.
- Logs folder.
- Reset settings.

### Phase 5 - Media Input

- Browse Media.
- Drag and drop.
- Media type detection.
- Extension validation.
- Image metadata.
- Video metadata.
- Orientation handling.
- Display metadata in UI.

### Phase 6 - Image Pipeline

- Image loading.
- Original preview.
- Character presets.
- Glyph measurement.
- Automatic grid calculation.
- Luminance mapping.
- Light on Dark and Dark on Light polarity mapping.
- Polarity-dependent opaque symbol backgrounds and transparent-pixel compositing.
- SkiaSharp symbol rendering.
- Symbolized preview.
- PNG export.
- JPG/JPEG export.

### Phase 7 - Bundled FFmpeg

- Bundle real Windows x64 `ffmpeg.exe` and `ffprobe.exe` binaries.
- Copy tools, license, checksums, and notices to Debug, Release, and publish output.
- Resolve tools relative to the application directory without relying on `PATH`.
- Validate each tool asynchronously, cache session results, and support refresh.
- Provide a reusable safe process runner with argument lists, redirected output,
  cancellation, timeout, and process-tree termination.
- Use validated ffprobe output to enrich optional video metadata while preserving
  the managed Phase 5 fallback when probing is unavailable.
- Record distribution provenance, GPLv3 licensing, source revision, and checksums.
- Display real FFmpeg and ffprobe status, versions, path, and diagnostics in Settings.

### Phase 8 - Video Preview

- Platform-neutral structured video-frame reader contract.
- Bundled FFmpeg frame extraction isolated in Infrastructure behind `IVideoFrameReader` and a binary-safe process abstraction.
- Unicode-safe `ArgumentList` paths and normalized-position-to-timestamp random access.
- One display-oriented straight-alpha RGBA8888 frame through bounded in-memory stdout with exact-length validation.
- Bounded timeout, concurrent stderr draining, process-tree cancellation, and deterministic process/file-handle release.
- Asynchronous middle-frame default at normalized position `0.5`.
- Video-only normalized position slider with 200 ms debounce, cancellation, one-read serialization, and latest-request-wins identity checks.
- Original frame preview with loading, ready, and friendly failure states.
- Shared symbolization of the currently displayed frame.
- Media/source/settings preview identity and stale symbolized-frame behavior.
- No animated playback or full-video export.

### Phase 9 - Streaming Video Export

- One long-lived bundled-FFmpeg decoder emits display-oriented RGBA8888 frames sequentially through stdout.
- At most the active decoded frame, active rendered frame, and bounded pipe buffers are retained.
- Every frame uses the shared preset, grid, luminance, color, polarity, and SkiaSharp rendering pipeline.
- One long-lived bundled-FFmpeg encoder accepts rendered RGBA frames through awaited stdin writes.
- Output is video-only MP4 using H.264, CRF 20, the `medium` preset, and `yuv420p`.
- Trustworthy finite source FPS values from `(0, 240]` are retained; unavailable or invalid FPS uses a deterministic 30 FPS fallback.
- Odd output dimensions crop at most the trailing row or column to satisfy `yuv420p`; frames are never stretched.
- Unicode input and output paths use `ArgumentList`; neither system `PATH` nor temporary media copies are used.
- A unique partial MP4 is created in the destination directory, then atomically moved or replaced only after successful completion.
- Failure and cancellation terminate child processes, preserve an existing destination, and remove partial output.
- The Phase 9 encoder remains the video-only foundation. Phase 10 muxes audio afterward without re-encoding video; detailed progress and remaining-time UX remain Phase 11.

### Phase 10 - Audio

- Capture Keep Audio or Remove Audio when export begins.
- Confirm source audio with bundled ffprobe; uncertain probe results fail safely.
- Stream-copy AAC into MP4; transcode incompatible or unknown codecs to AAC at 192 kb/s, 48 kHz stereo.
- Keep the completed symbolized H.264 video unchanged with `-c:v copy`.
- Allow shorter audio to end naturally and cap longer audio at the authoritative rendered-video duration without `-shortest`.
- Publish a silent result successfully when the source has no audio or Remove Audio is selected.
- Use separate video-only and mux partials, atomically publish only complete output, and preserve existing destinations on failure or cancellation.

### Phase 11 - Progress and Cancellation

- Progress ring.
- Progress bar.
- Current frame.
- Elapsed time.
- Estimated remaining time.
- Cancel.
- Process-tree termination.
- Temporary file cleanup.

### Phase 12 - Error Handling

- Friendly errors.
- Show Details.
- Local logs.
- Invalid input.
- Missing FFmpeg.
- Write-permission errors.
- Insufficient disk space.
- Encoder failures.
- Overwrite confirmation.

### Phase 13 - UI Polish

- Hover states.
- Focus states.
- Disabled states.
- Empty states.
- Loading states.
- Settings dialog.
- About dialog.
- DPI scaling.
- Resizing.
- Final comparison with the design reference.

### Phase 14 - Testing

- Full media format matrix.
- Portrait and landscape tests.
- Audio tests.
- Cancellation tests.
- Error handling tests.
- Packaging smoke test.

### Phase 15 - Packaging

- Self-contained Windows x64 publish.
- Bundled native dependencies.
- Bundled FFmpeg.
- Portable ZIP.
- Clean Windows machine test.
- Licenses and notices.

### Phase 16 - Portfolio Release

- Screenshots.
- Sample input/output.
- Demo video.
- Architecture diagram.
- README.
- Known limitations.
- GitHub Release `v1.0.0`.

## 22. Acceptance Criteria

MVP is accepted when all criteria below are met.

### 22.1 Application Startup

- App launches on Windows 10 or Windows 11.
- App launches from a self-contained portable folder.
- App does not require separately installed .NET Runtime.
- App does not require separately installed FFmpeg.
- Missing bundled tools are detected with a friendly error.

### 22.2 UI

- App name is `GlyphFrame` everywhere in the UI.
- No UI text uses a previous project name.
- Main UI visibly matches `GlyphFrame_Design_Reference.png` in hierarchy, proportions, spacing, and control placement.
- Native-style title bar shows the GlyphFrame mark, `GlyphFrame`, and window controls.
- Product header shows `Video & Image to Symbol Converter`, its description, and the upper-right preset selector.
- Left column contains the preview panel and the export status panel.
- Right column contains Input, Characters, Render, Audio for video, and Output in that order.
- Preview panel uses equal-width Original and Symbolized tabs and a 16:9 media stage.
- Loaded-video mode shows the frame slider, fit action, fullscreen action, media metadata, and `Preview Symbolized Frame` action.
- Exporting mode shows the circular percentage indicator, progress bar, frame progress, elapsed time, remaining time, and Cancel action.
- Footer is present.
- Image/video controls appear conditionally.
- At 100% Windows scaling, all labels and controls fit at the approved desktop viewport without overlap or clipping.
- Narrow layouts reflow without losing controls or introducing horizontal page scrolling.
- UI remains responsive during preview generation and export.

### 22.3 Image Input and Preview

- User can select PNG, JPG/JPEG, WebP, and BMP files.
- User can drag and drop supported image files.
- App shows original image preview.
- App shows image metadata.
- App hides video-only controls for images.
- App generates symbolized image preview.
- App preserves source aspect ratio.
- Light on Dark is the default and maps bright cells to denser symbols on a dark background.
- Dark on Light maps dark cells to denser symbols on a light background.
- Transparent pixels composite exactly once over the selected symbol background.

### 22.4 Image Export

- User can export symbolized PNG.
- User can export symbolized JPG/JPEG.
- PNG is the default image export format.
- Selected output resolution is respected.
- Selected symbol polarity is respected and matches the symbolized preview.
- PNG and JPEG outputs use the same polarity-dependent opaque background.
- Output overwrite requires confirmation.
- Unsupported or invalid images show friendly errors.

### 22.5 Video Input and Preview

- User can select MP4, MOV, MKV, and AVI files.
- User can drag and drop supported video files.
- App reads and displays video metadata.
- App shows original preview frame.
- User can select frame position with a slider.
- App generates symbolized preview frame.
- App shows video-only controls for videos.
- App automatically requests an original frame near the middle after metadata succeeds.
- Slider seeking remains responsive, reconciles to the nearest resolved frame, and cannot publish an obsolete result.
- Frame pixels retain correct red/blue channel order and remain valid after the FFmpeg process and pipes are disposed.
- Cyrillic, spaces, and mixed-language input paths decode directly without renaming or temporary copies.
- Source videos are not left locked after preview reads, failures, cancellation, or repeated seeking.
- The symbolized frame uses the exact currently displayed original frame and the shared image pipeline.
- Frame/media/render-setting changes mark the previous symbolized frame stale; output-format-only changes do not.
- Missing native dependencies, unsupported codecs, and frame failures remain non-fatal and user-friendly.

### 22.6 Video Export

- User can export MP4.
- MP4 uses H.264.
- MP4 uses yuv420p pixel format.
- MP4 uses CRF 20.
- MP4 uses medium preset.
- Export uses normalized source FPS.
- Rendered frames are streamed to FFmpeg through stdin.
- The app does not create thousands of temporary PNG files.

### 22.7 Audio

- Keep Audio preserves audio when source audio exists.
- AAC source audio is stream-copied and the symbolized H.264 video is never re-encoded.
- Incompatible or unknown source audio falls back to AAC at 192 kb/s, 48 kHz stereo.
- Remove Audio produces an MP4 without audio.
- Source videos without audio export successfully.
- Keep Audio fails safely when bundled ffprobe cannot reliably determine audio presence.
- Shorter audio does not truncate video; longer audio is capped at the completed symbolized-video duration.
- Both video-only and mux partials are cleaned and an existing destination survives failure or cancellation.

### 22.8 Progress and Cancel

- Export progress is visible.
- Current frame and total frame count are visible for video exports.
- Elapsed time is visible.
- Estimated remaining time is visible when available.
- Cancel stops export.
- Temporary files are cleaned after success, failure, or cancel.

### 22.9 Settings and Persistence

- Accent color setting is persisted.
- Default character preset is persisted.
- Default font and font size are persisted.
- Default output resolution is persisted.
- Default symbol polarity is persisted, safely defaults when missing, and is restored by Reset Settings.
- Default image format is persisted.
- Default audio mode is persisted.
- Reset settings works.

### 22.10 Privacy

- No media files are uploaded.
- No telemetry is sent.
- No analytics are sent.
- Logs are local.
- Logs do not contain image pixels, video frames, or source media content.

### 22.11 Documentation

- `GlyphFrame Technical Specification v1.0` exists.
- `docs/design_reference/GlyphFrame_Design_Reference.png` exists.
- Acceptance criteria are included.
- Project name is updated to `GlyphFrame` in all project-created documentation.
