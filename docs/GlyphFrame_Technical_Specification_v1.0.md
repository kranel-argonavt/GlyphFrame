# GlyphFrame Technical Specification v1.0

Version: 1.0  
Status: Final documentation for Phase 1  
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
- Ramp Invert.
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
- Video frame reading: OpenCvSharp
- Video encoding and audio processing: FFmpeg
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

Actual decoding support depends on OpenCvSharp and bundled FFmpeg capabilities.

Video loading must:

- Read local files from disk.
- Validate file existence.
- Validate file extension.
- Read metadata through ffprobe.
- Detect width, height, duration, FPS, frame count, video codec, audio presence, and audio codec.
- Handle videos without audio.
- Handle Unicode file paths.
- Show friendly errors for unsupported or unreadable files.

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

Luminance is calculated per cell using the average color of the sampled area.

Mapping rule:

- Darker cells map to denser/darker characters.
- Brighter cells map to lighter characters.

Ramp inversion is not included in MVP.

### 6.5 Color Mode

MVP supports only:

```text
Original Colors
```

Each rendered symbol uses the average source color of its cell.

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

## 8. Image Workflow

### 8.1 Image Input Flow

```text
1. User selects or drops an image.
2. App validates the file.
3. App reads metadata.
4. App displays the original image preview.
5. App hides video-only controls.
6. User adjusts character and render settings.
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
7. Generate temporary video-only MP4.
8. If Keep Audio is selected, mux original audio into final MP4.
9. If Remove Audio is selected, finalize video without audio.
10. Clean temporary files.
11. Show completed status.
```

### 9.3 Audio Modes

Supported audio modes:

```text
Keep Audio
Remove Audio
```

Rules:

- If `Keep Audio` is selected and source audio is compatible, stream copy audio.
- If `Keep Audio` is selected and source audio is incompatible, transcode to AAC.
- If source video has no audio, export a no-audio MP4 and show no failure.
- If `Remove Audio` is selected, final MP4 must contain no audio stream.

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
docs/mockups/GlyphFrame_Design_Reference.png
docs/mockups/GlyphFrame_Final_Mockup.html
```

Reference rules:

- `GlyphFrame_Design_Reference.png` is the approved source for layout, visual hierarchy, spacing, and desktop-tool density.
- `GlyphFrame_Final_Mockup.html` is the GlyphFrame-branded implementation reference for Phase 3.
- The application name in the native title bar is `GlyphFrame`.
- The product header title is `Video & Image to Symbol Converter`.
- The product header subtitle is `Transform videos and images into beautiful symbol art`.
- The preset quick selector remains in the upper-right product header area.
- The preview and export-progress areas form the left column.
- Input, Characters, Render, Audio, and Output form the stacked right column.
- The loaded-video/exporting state shown in the final mockup is the primary desktop reference state.
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

Render:

- Font family dropdown.
- Font size dropdown.
- Output resolution dropdown.
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

MediaLoaded:

- Original preview visible.
- Settings enabled.
- Preview action enabled.
- Export enabled only when output path is valid.

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
- Image export format.
- Video audio mode.

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
AudioMuxer
ExportService
FfmpegProcessRunner
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
    char Map(byte brightness, string characterSet);
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
    Task EncodeAsync(EncodingJob job, IProgress<ExportProgress> progress, CancellationToken cancellationToken);
}
```

```csharp
public interface IExportService
{
    Task ExportAsync(ExportJob job, IProgress<ExportProgress> progress, CancellationToken cancellationToken);
}
```

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
}
```

### 14.4 RenderSettings

```csharp
public sealed class RenderSettings
{
    public string FontFamily { get; init; } = "Cascadia Mono";
    public int FontSize { get; init; } = 10;
    public OutputResolutionMode OutputResolution { get; init; } = OutputResolutionMode.SameAsSource;
    public ColorMode ColorMode { get; init; } = ColorMode.OriginalColors;
}
```

### 14.5 CharacterPreset

```csharp
public sealed class CharacterPreset
{
    public string Name { get; init; } = "";
    public string Characters { get; init; } = "";
    public bool IsUnicode { get; init; }
}
```

### 14.6 ExportJob

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

### 14.7 ExportProgress

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
- Source resolution above 1080p warning.
- Unicode file paths.
- Output path already exists.
- Missing FFmpeg.
- Missing ffprobe.
- Cancel during export.
- Invalid/corrupted media.

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
Final UI mockup
Acceptance criteria
Updated project naming
```

Tasks:

- Create unified technical specification.
- Use `GlyphFrame` everywhere.
- Add image requirements.
- Add UI specification.
- Save final mockup.
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
- SkiaSharp symbol rendering.
- Symbolized preview.
- PNG export.
- JPG/JPEG export.

### Phase 7 - Bundled FFmpeg

- Add `ffmpeg.exe`.
- Add `ffprobe.exe`.
- Tool locator.
- Validation.
- Process runner.
- Third-party notices.
- FFmpeg status in Settings.

### Phase 8 - Video Preview

- Video frame reader.
- Middle-frame default.
- Position slider.
- Original frame preview.
- Stale symbolized preview behavior.
- Preview Symbolized Frame.

### Phase 9 - Streaming Video Export

- OpenCvSharp sequential reading.
- SkiaSharp raw frame output.
- FFmpeg stdin pipe.
- H.264 CRF 20.
- yuv420p.
- FPS normalization.
- Partial output handling.

### Phase 10 - Audio

- Keep Audio.
- Remove Audio.
- Stream copy when compatible.
- AAC fallback.
- No-audio handling.
- Final muxing.

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
- Final comparison with mockup.

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
- Main UI visibly matches `GlyphFrame_Design_Reference.png` and `GlyphFrame_Final_Mockup.html` in hierarchy, proportions, spacing, and control placement.
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

### 22.4 Image Export

- User can export symbolized PNG.
- User can export symbolized JPG/JPEG.
- PNG is the default image export format.
- Selected output resolution is respected.
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
- Incompatible source audio falls back to AAC.
- Remove Audio produces an MP4 without audio.
- Source videos without audio export successfully.

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
- Design reference.
- Acceptance criteria are included.
- Project name is updated to `GlyphFrame` in all project-created documentation.
