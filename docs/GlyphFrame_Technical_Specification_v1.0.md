# GlyphFrame Technical Specification v1.0

Version: 1.0  
Status (updated 2026-10-25): Committed baseline `e5e743f47719c3cf348dbd06f58708c2221cfaf4` includes the progress/footer, preview/navigation/status and Settings refinements. The current uncommitted follow-up fixes live native-window-icon accents; its precommit recheck passed 1,258 full Release tests, a zero-warning/error forced build, formatting and diff checks. Latest portable `2ef20d96` is a dirty-source provisional, not a clean-source acceptance pass. The earlier clean A1/A2 for `e5e743f` / package `8399abc6` retain their October 24 scope. A5 has a positive user-reported manual outcome for the newest local version; final candidate identity remains to be bound, while A4 and broader acceptance remain open. See Phase 14/15 for provenance and next steps. This documentation update is not a new execution or release sign-off.
Acceptance: Phase 14 remains open; Phase 15A packaging is implemented, Phase 15B acceptance remains partial/open, and Phase 15C clean-machine execution is NOT TESTED. Dated native outcomes and remaining checks are tracked in Phase_14_Coverage.md and Phase_15_Packaging.md; historical evidence is preserved.
The [current release acceptance checklist](Phase_14_Coverage.md#release-acceptance-checklist) consolidates outstanding work; the roadmap below distinguishes implementation from acceptance.
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
-> Review or export the rendered symbolized result
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
- Explicit Render -> Review -> Export workflow.
- In-app playback of a completed GlyphFrame-rendered MP4.
- Runtime-gated Original-video playback for the evaluated compatibility matrix; original-frame preview remains the fallback and Phase 11B.5 validation is complete.
- Three built-in character presets.
- Font family selection.
- Font size selection.
- Output resolution selection.
- Symbol polarity selection.
- Image export to PNG and JPG/JPEG.
- Video export to MP4.
- Keep Audio and Remove Audio modes for video.
- Progress display during rendering and export publication.
- Cancel active rendering or export publication.
- Local logs.
- Local settings.
- Bundled FFmpeg and ffprobe.

### 2.2 MVP Excludes

- Realtime or on-the-fly video symbolization during playback.
- Guaranteed playback of every codec/container combination or arbitrary input formats outside the validated compatibility matrix. Runtime Original playback is an environment-dependent Phase 11B.5 feature and is not part of Phase 11B.3.
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
- Testing: xUnit (platform-neutral and Windows/WPF test projects)

### 4.2 Bundled Tools

The final application package must include:

```text
GlyphFrame/
├── GlyphFrame.App.exe
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

For `Same as source`, GlyphFrame uses the display-oriented source resolution. In image mode only, a warning appears when either dimension exceeds its orientation-specific boundary: 1920×1080 for landscape/square or 1080×1920 for portrait. It does not limit rendering or export. Video mode does not show this image-specific warning; its output still follows the selected resolution and yuv420p normalization rules.

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

Changing polarity invalidates an existing symbolized render because it changes both character mapping and the canvas background. Selecting the same value again does not invalidate the result. Changing only PNG/JPEG output format does not invalidate symbol rendering.

## 8. Image Workflow

### 8.1 Image Input Flow

```text
1. User selects or drops an image.
2. App validates the file.
3. App reads metadata.
4. App displays the original image preview.
5. App hides video-only controls.
6. User adjusts character, polarity, and render settings.
7. User clicks Render Image.
8. App creates and retains one current full-resolution symbolized image result.
9. App selects the Symbolized tab and changes the processing panel to Image Ready.
10. User reviews Original and Symbolized without rerendering.
11. User clicks Export Image to open the native Save dialog.
12. User chooses the destination and PNG or JPG/JPEG in Save; the accepted filter controls encoding and extension normalization without rerunning symbolization.
```

### 8.2 Image UI Rules

When the loaded media is an image:

- Video audio remains inside Render, visibly disabled with an image-specific explanation. The stored audio preference is preserved.
- Video frame slider is hidden.
- Image mode has no separate frame-preview command.
- The preview-panel action is `Render Image`, changing to neutral secondary `Render Again` when a retained result exists; Export Image is the result card's primary action.
- Export button label is `Export Image`.
- The native Save dialog offers `PNG` and `JPG/JPEG`. The persisted default image format initializes its selection; the application default is PNG.
- Metadata row shows image width, height, format, and file size.
- Export actions appear in the persistent result panel only after a rendered image exists. It has no output-format selector in any state; format selection belongs to Save. Export remains disabled while that retained result is stale.
- Render and export failures or cancellations remain visible in the result panel with operation-specific status text; they do not discard an earlier retained render.

### 8.3 Image Acceptance Requirements

GlyphFrame must:

- Open PNG, JPG/JPEG, WebP, and BMP files.
- Show original image preview.
- Render one reusable symbolized image result and show it in the Symbolized tab.
- Never expose a redundant image-preview command.
- Export symbolized PNG.
- Export symbolized JPG/JPEG.
- Preserve aspect ratio.
- Respect selected output resolution.
- Respect selected symbol polarity in mapping, preview, and export.
- Composite transparent pixels over the selected polarity background exactly once.
- Keep UI responsive during rendering, review, and export.
- Reuse the current rendered pixels for PNG/JPEG export; changing only destination or image format must not rerun symbolization.
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
9. User may click Preview Frame.
10. App generates symbolized preview frame.
11. User clicks Render Video to process the complete video.
12. App creates one managed, completed symbolized MP4 review artifact using the captured audio mode.
13. App opens the artifact in the Symbolized tab and changes the processing panel to Video Ready.
14. User reviews the rendered video in the in-app player.
15. User clicks Export Video and selects a destination.
16. App atomically publishes the current review artifact without rerunning symbolization or video encoding.
```

Phase 8 preview behavior:

- After metadata succeeds, request a frame near normalized position `0.5` and show a non-blocking loading state.
- The video slider maps start to `0.0` and end to `1.0` and reconciles to the nearest frame position actually returned by the decoder.
- Convert normalized position to an invariant FFmpeg timestamp when duration is known and back the end position away from the duration boundary to request the final available frame.
- Debounce slider input for 200 ms, cancel superseded requests, serialize extraction processes, and apply latest-request-wins generation checks.
- Preserve metadata and show a friendly retry state when frame loading fails.
- `Preview Frame` uses the exact owned frame currently displayed in Original mode and the shared image symbolization pipeline.
- Preview identity includes media version, original-frame/source version, and render-settings version. A frame or setting change therefore makes the previous symbolized frame stale.
- Phase 8 does not implement animated playback, sequential processing, audio work, or full-video export.

### 9.2 Video Render, Review, and Export Flow

```text
1. Validate input path and the managed review-artifact location.
2. Validate ffmpeg.exe and ffprobe.exe.
3. Read video metadata and capture render settings plus audio mode.
4. Start sequential frame reading.
5. For each source frame:
   -> Convert frame to symbol frame.
   -> Render symbolized frame with SkiaSharp.
   -> Send raw frame to FFmpeg through stdin.
6. Close encoder stdin and verify successful FFmpeg completion.
7. Apply the captured audio mode.
8. For Remove Audio or a confirmed silent source, promote the completed video-only partial to the review artifact.
9. For Keep Audio, map original audio into a second MP4 partial using video stream copy plus audio copy or AAC fallback.
10. Promote only a complete final MP4 to the managed review-artifact path.
11. Release render resources, clean intermediates, and retain only the review artifact.
12. Review that exact artifact through the in-app player.
13. On Export Video, choose a destination and atomically copy or replace from the review artifact without rerendering.
14. Preserve the review artifact for repeated export until it becomes stale, is replaced, or its media session ends.
```

The Phase 9 encoder continues to pass `-an` and creates an authoritative video-only intermediate. Phase 10 performs its audio decision only after that intermediate completes and never decodes or re-encodes the symbolized H.264 stream during muxing. Phase 11B relocates final publication from a user-selected destination to a managed review artifact; later export publishes that already-reviewed artifact.

Phase 11B.2 implements the managed-artifact backend. Completed candidates live under `%LocalAppData%\GlyphFrame\Cache\VideoArtifacts\<session>`, use generated identifiers rather than source filenames, and are promoted from a distinguishable pending MP4 only after the existing streaming pipeline completes. Each live session holds an exclusive lease file from store initialization through shutdown cleanup. Recovery probes sibling leases and skips the entire directory whenever another process still holds the lease; an exclusively acquirable lease identifies a stale session, while a short initialization grace period protects a newly created lease-less directory from a concurrent-startup race. The platform-neutral full-video identity captures media/source generation, preset and ramp, font, size, resolution, polarity, and audio mode while excluding frame-preview position. An application session retains at most one accepted artifact; failed, cancelled, superseded, rejected, replaced, shutdown, and recovered orphan files are deleted only through canonical, cache-root-contained operations. Failed current-session cleanup is deduplicated, retained as technical diagnostics, and retried at later lifecycle boundaries.

Phase 11B.3 connects that backend to the video editor. The preview action row keeps independent secondary `Preview Frame` and primary `Render Video` / `Render Again` actions. A full render captures one immutable job and is accepted only while its operation, media generation, and full-video identity remain current. The existing player handle is stopped and closed before session acceptance may delete a replaced artifact. Only after acceptance and successful player opening does the workflow enter Video Ready and select Symbolized; obsolete candidates are rejected, and failed or cancelled replacements preserve the previously accepted artifact. The App-layer WPF player opens the exact managed MP4 with explicit manual lifetime, supports play/pause/restart, seek, current and total time, volume/mute, fit, and fullscreen, and releases its timer and file handle before media replacement or shutdown cleanup. A stale full artifact stays playable, audio-mode changes stale only that artifact, and frame-slider movement affects only frame preview. Phase 11B.4 publishes the exact accepted current MP4 by byte copy, without invoking this rendering pipeline again; the transitional direct-export UI is removed.

### 9.3 Audio Modes

Supported audio modes:

```text
Keep Audio
Remove Audio
```

Rules:

- Audio mode is captured when full-video rendering begins and settings remain disabled during that operation.
- `Keep Audio` requires a strict bundled-ffprobe result; unavailable audio metadata fails safely instead of silently dropping a possible stream.
- AAC is the MVP MP4-compatible stream-copy codec, compared case-insensitively after trimming.
- If `Keep Audio` is selected and source audio is AAC, use `-c:v copy -c:a copy`.
- If `Keep Audio` is selected and source audio is incompatible, unknown, or unnamed, use `-c:v copy -c:a aac -b:a 192k -ar 48000 -ac 2`.
- If source video has no audio, export a no-audio MP4 and show no failure.
- If `Remove Audio` is selected, final MP4 must contain no audio stream.
- Map the completed symbolized video as `0:v:0` and original source audio as `1:a:0`.
- The rendered frame count divided by normalized FPS is the authoritative duration. Apply it as the output duration cap without `-shortest`: shorter audio may end naturally, while longer audio must not extend the completed video.
- Stream-copy, AAC fallback, no-source-audio, and removed-audio outcomes are reported distinctly.
- Video-only and mux partials live beside the managed review artifact, remain unpublished until complete success, and are cleaned on every failure or cancellation path.
- The audio mode is part of rendered-video identity. Changing it makes an existing full-video artifact stale but does not invalidate an independent frame preview.

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
- Card surfaces normally use an 8-DIP radius; the processing/result shell uses its explicit 10-DIP radius.
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
- The sole character-preset selector is in Characters; the agreed composition follow-up removes the redundant header selector.
- The preview and persistent processing/result areas form the left column.
- Input and Characters are compact; Render fills the remaining finite right-pane height and includes persistent Video audio.
- The loaded-video/exporting state shown in the design reference is the primary desktop reference state.
- Image mode uses the same shell and spacing while applying the conditional rules in section 8.2.
- Phase 11B supersedes the reference's permanent right-column Output card: result/export actions appear below the preview. Image output format and destination are selected in the native Save dialog, not in a persistent format selector.
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

The main window starts maximized within the monitor work area. Its restored size is 1320×820 WPF DIPs, centered and reduced as needed by the existing monitor work-area placement policy. The supported minimum remains 1024×720 DIPs.

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
|   +-- Scalable GF/frame mark (preset selection lives in Characters)
|
+-- Main Content
|   +-- Left Column
|   |   +-- Original / Symbolized tabs
|   |   +-- Preview surface
|   |   +-- Video frame slider when media is video
|   |   +-- Metadata row and render/preview actions
|   |   +-- Processing and Result Panel
|   |
|   +-- Right Column
|       +-- Input
|       +-- Characters
|       +-- Render
|           +-- Persistent Video audio subsection
|
+-- Footer
    +-- Assembly-derived version
    +-- Concise current operation status
    +-- About
    +-- Settings
```

Desktop layout constraints:

- Default reference viewport is approximately 1440 x 1080 at 100% scaling.
- Main content uses two columns with the left column approximately 1.65 times the width of the right column.
- The left and right columns align at their top edge.
- Preview is the dominant surface. The result/status content is measured first; the preview receives the remaining finite pane height, bounded by its minimum. Media retains its own aspect ratio (including 16:9) inside that allocation.
- The processing/result panel sits directly below the preview, not across the full window width.
- Status height follows its visible content and chosen composition; there is no fixed state-independent shell height. If preview minimum plus status height and their gap exceed the viewport, the existing left-pane scroller exposes the combined content. A stable outer scrollbar gutter preserves width; internal 100% media scrolling is separate. Retained canvas extents do not determine parent allocation.
- Settings cards remain compact and vertically stacked with approximately 8-12 px gaps.
- Main outer spacing is approximately 20–24 DIPs; ordinary card radius is 8 DIPs and the processing/result shell uses 10 DIPs.
- Within the supported two-column shell, internal controls reflow or use their designated vertical overflow paths without horizontal page scrolling. The main window retains its 1024×720-DIP minimum; it does not switch to a single-column page.

### 10.5 Title Bar and Product Header

Title bar content:

- App name: `GlyphFrame`
- App mark.
- Native window controls.

Product header content:

- Title: `Video & Image to Symbol Converter`
- Subtitle: `Transform videos and images into beautiful symbol art`
- Preset selection stays in Characters, without a duplicate header selector.

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
- `Render Image` for images.
- `Preview Frame` and `Render Video` for videos.
- Video frame-position slider for videos only.
- Still images: Ctrl+wheel zoom and left-drag pan, without a scaling toolbar, percentage button, Fit button or image scaling dropdown. The removed toolbar reserves no height or margins.
- Videos retain Fit / Fill / 100%, transport and fullscreen behavior. Continuous Original and Symbolized video players each own working fullscreen entry/exit; the static original-frame fallback does not expose a no-op fullscreen action.

Preview panel behavior:

- Initial state shows a drop area and `Browse Media`.
- Original tab becomes available after media load.
- Symbolized tab becomes available after an image render, video-frame preview, or full-video render succeeds, and is selected automatically for the new result.
- Image mode never shows a separate preview command; `Render Image` produces the reusable full image result.
- Still-image navigation is transient viewing state. Each successfully accepted replacement starts centered in Fit; cancelled/failed replacement preserves the accepted framing. Original/Symbolized and same-source rerenders preserve normalized center and relative-to-Fit magnification when raster dimensions change, clamping at image edges and zoom bounds. A viewport resize recomputes Fit in Fit mode; manual mode retains actual scale and a bounded center unless it falls below the new Fit, in which case the view returns to centered Fit.
- Zoom uses multiplicative 1.25× steps. The lower bound is always the current complete-image Fit scale, including very small/extreme images; zooming out cannot make the image smaller than Fit. Reaching that bound centers both axes and re-enters Fit mode, so subsequent resizing follows Fit. The upper bound is the larger of 3200% and Fit (also retaining a larger current manual scale across resize/DPI). Internal scale remains based on physical-pixel magnification using the viewport's current per-monitor DPI.
- Ctrl+wheel inside the image viewport zooms around the pointer. Left drag pans oversized images; each smaller axis stays centered, with offsets clamped on larger axes. Capture is released on mouse-up, Escape, lost capture, media/raster transition, hide and unload. Ordinary wheel never zooms: it pans oversized vertical content in the normal scroll direction where it can move, otherwise the same routed event bubbles to the enclosing pane. In ready-image mode the internal ScrollViewer bypasses its own wheel handling; no event is cloned or forwarded and only one scroll owner advances. Sub-micro-DIP boundary noise is not a meaningful pan. Legacy video callers retain native internal scrolling.
- Image navigation transforms the existing visual, never decoding/resymbolizing, changing render dimensions/orientation/grid/identity/staleness, exporting, or saving preferences. State does not own raster references. Legacy video viewport callers retain their existing scaling/scroll behavior. Automated geometry/fixture checks do not establish native A3, mixed-DPI, clean-Windows or release acceptance.
- Video mode presents `Preview Frame` as a secondary action before full-video artifact acceptance and `Render Video` as the primary action. Once an accepted full artifact has presentation priority, Preview Frame is hidden; showing a new symbolized frame would require a future explicit review-mode switch.
- Before full-video render, Symbolized may show the latest frame preview. After full-video render, the completed video player takes priority.
- Changing a render-affecting setting marks the relevant rendered result stale. A stale result remains reviewable, but export is disabled until Render Again succeeds.
- Moving the video frame-position slider invalidates only the frame preview and never the completed full-video artifact.
- Export becomes available only when a current, non-stale rendered result exists.
- Video selection loads an original frame near the middle, enables the normalized position slider, and displays current/total time when duration is known.
- Rapid slider changes are coalesced; a superseded or cancelled read never replaces the newest frame and is not shown as an error.
- Video frame loading and failure states are visually distinct while retaining successfully loaded metadata.
- `Preview Frame` is enabled only after an owned original frame is available and before a full managed artifact is accepted. `Render Video` depends on valid video metadata and bundled tools, not on a frame preview.
- The rendered-video player supports play/pause, restart after completion, seek, current time/duration, volume/mute, fit, and fullscreen review. It releases media handles before artifact replacement or cleanup.

### 10.7 Settings Panel

Current agreed composition: SettingsPanePanel measures content-sized Input/Characters and allocates finite remaining height to RenderSettingsPanel. Render pins its heading and persistent Video audio around a parameter-body ScrollViewer. Normal overflow belongs only to that body. If measured pinned sections plus a usable **88-DIP** parameter viewport cannot fit, whole-pane fallback disables internal scrolling and exposes natural content height to the outer scroller. Stable gutters, no horizontal scrolling and focus BringIntoView are retained. Settings-only padding is **12 horizontal / 8 vertical DIPs**, preserving controls/fonts while reclaiming 24 DIPs versus 12-all-around. Preset and Ramp share an 86-DIP label column; Sparse–Dense is outside the Ramp field row. Font pairs have left-side 86-DIP labels, share a row at **390 DIPs of actual parameter width** (230 family + 150 size + 10 gap), and otherwise stack. Font Family receives remaining width; all size choices remain. Read-only Original colors is unchanged.

The conditional More render settings / Coming soon placeholder is measured as a noninteractive overlay. It appears only when measured spare parameter-body height includes its own height plus 16-DIP clearance, centers within that spare region, and hides for overflow or whole-pane fallback. It never adds desired height, scroll extent or a reserved hidden slot. Heading, parameters and persistent Video audio retain their finite allocation and single scroll-owner policy. Input has no Formats pseudo-control; supported filters and accessible Browse/Change file guidance remain. Source ready appears first in one left-aligned, wrapping metadata line below the filename, with filename containment/full-path help; failed replacement retains media/result but suppresses readiness and keeps its own error/Details. Audio uses one fixed subsection: a passive dark informational surface with the Input drop background, neutral 1-DIP border, muted readable text, Arrow cursor and no focus/interaction covers only genuinely disabled buttons with Select a video first / Audio does not apply to images / No audio track in this video. Its reason is not duplicated above; the rendered-video explanation is static. Busy locks show no absence overlay and preserve a readable selected mode/check. Preference and accepted AUDIO are unchanged.

Vector resources use a frame with a curved capital S for initial Render, sliders for Render settings, refresh for Render Again, matching outlined Sound/diagonal Mute with identical optical bounds, fixed 28-DIP ComboBox arrow cells (down/up without geometry shift), Copy for path/details, and shared GF/frame dialog branding. Decorative paths/images do not add focus/reader stops. Planned TARGET/video AUDIO remain far right; accepted FILE SIZE uses ManagedVideoArtifact.FileSizeBytes and binary-unit formatting, not a filesystem getter/source estimate. Current status dimensions and exported-action compositions are specified in section 10.8; these views do not change processing, player ownership, identity or privacy contracts.



Right settings sections:

Unavailable Audio buttons retain layout/preference and genuine disabled state with local zero opacity under the opaque passive informational explanation, preventing label/checkmark bleed-through; ordinary operation locks retain normal opacity and no absence surface. Dedicated outlined flag/hash resources serve Ready to render/Characters only; Symbolized and completion vectors are unchanged. The shared player template centers content; a fixed 16-DIP Play/Pause slot uses a Play-only 1-DIP horizontal optical correction (approximately half the fitted triangle's centroid imbalance). Pause and other icons have no such offset. No command or player-timing changes are involved; the current content-driven status-height policy is documented in section 10.8.

```text
Input
Characters
Render
  Video audio (persistent subsection)
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

- Always physically present inside Render, below a thin divider. No media and images disable the segmented controls with explicit explanations; a confirmed silent video says No audio track in this video without changing the stored preference.
- Keep Audio option.
- Remove Audio option.

The right column has no permanent Output card after Phase 11B. Export actions belong to the ready-result state below the preview. Image format and destination are selected in the native Save dialog only when Export is invoked; no status-panel format selector is present.

### 10.8 Processing and Result Panel

The current export-result path interaction is a fixed-width, real **Copy path** Button for images and videos. It appears only for a successful destination associated with the displayed retained result; tooltip and explicit Unicode clipboard copy expose that full path without an inline destination/filename row. The existing injected clipboard service handles contention. A two-second **Copied!** indication or concise **Copy failed** label/retry tooltip does not resize neighboring controls or alter export outcome. Feedback is cancelled at result invalidation/replacement and disposal, with generation/lifetime checks rejecting late continuations. A failed/cancelled retry or declined chooser cannot replace the last successful destination; new accepted results/media clear it. Open Folder remains separate and follows its existing availability rules. Success footers are `Image exported successfully.` / `Video exported successfully.`; full diagnostic details remain in their independent error contexts. Clipboard tests use doubles, not the system clipboard.

The panel is the canonical presentation for long-running work and completed results. It must support:

- Rendering state.
- Render Ready state.
- Render Stale state.
- Exporting state.
- Exported state.
- Cancelling and Cancelled states.
- Error state.

During full rendering the processing/result panel shows:

- Operation title with the current/total video frame count trailing in the same header row when space permits.
- Progress bar below the header.
- Current stage description below the bar, with estimated remaining time trailing when available.
- Cancel button, disabled when cancellation is no longer allowed and absent during the completion-only presentation.

Frame count reflows below the title when that header pair cannot fit. Only WorkingFooter opts into leading-description wrapping: reserve the remaining-time field's measured width and the existing 12-DIP gap, provided the description retains a readable column. That minimum is the greater of its widest measured text-element advance/ink plus text chrome and a practical 10-em reading budget capped at the complete description's natural width. Em units follow the effective font size; glyph measurement uses the actual typeface and DPI, with padding/margins included. Short complete text need not occupy 10 em. Typography/content measurements are cached and refreshed when their inputs change; no window-width breakpoint is involved.

When that minimum no longer fits, the description receives the full available width and Remaining moves to its own right-aligned row with the established 4-DIP vertical gap. Ordinary wrapping in a usable column does not move Remaining down; inline fields align at their first line. Absent/empty metadata returns the full width without an empty row or reserved gap. Header/frame admission is unchanged. Elapsed time is not displayed; internal timing/estimation and full commit/cancellation guidance are unchanged. Heights remain content-driven, retaining the shared minimum footprint only for ordinary side-by-side working/spacious result transitions. Header, bar and footer retain their 5-DIP vertical gaps and the bar its 5-DIP height.

The preview busy overlay is limited to the media viewport and exists only while an operation is active. It contains one compact segmented activity ring without a numeric percentage; the ring serves as a neutral activity indication for determinate and indeterminate work alike. The lower processing/result panel alone presents percentage, operation title, stage description, linear bar, frame/timing details, and Cancel. The overlay disappears immediately when processing and cleanup finish; the brief completion animation occurs only in this lower panel and does not cover the completed preview.

Normal image/video previews do not overlay symbol-grid/output-dimension captions on the media. Existing result metrics remain in the lower status cards. The preview status badge is reserved for a stale Symbolized result; it disappears on Original and after a fresh render, with no empty badge. Loading, playback-opening/fallback, unavailable-review and error/Details overlays retain their existing behavior.

After a render succeeds, the panel remains visible and settles into `Image Ready` or `Video Ready`. It shows the completion mark and concise `Ready for review and export.` summary; after export the summary is `Ready for review and another export.`. Stale, failure, cancellation, suspension, opening and review-unavailable guidance remains state-specific.

Image Ready exposes `Export Image`; PNG/JPEG is selected only in the native Save dialog. Video Ready exposes `Export Video` without a format selector and shows accepted-artifact `FILE SIZE`, `OUTPUT` and `AUDIO` metrics. AUDIO reports the retained artifact's actual mux outcome (`Original`, `AAC`, `Removed`, or `No source`), not the current audio selector. Candidate or missing metadata is not presented as an accepted metric.

After export, applicable metrics remain visible with `Export Again`, `Open Folder` and `Copy path`. A stale result remains reviewable with metrics, disables export, and explains the need to rerender. The metadata/action row below the media viewport is the sole location for `Render Image` / `Render Again` and `Render Video` / `Render Again`; the result panel does not duplicate those render actions. Playback controls remain with the media surface.

Measured responsive composition:

- `AdaptiveResultPanel` chooses modes from actual inner width, populated controls, measured minimums and practical text allowances. The current selector has no previous-mode hysteresis band or global Window.Width breakpoint.
- When all groups fit, spacious mode puts badge/text, metrics and actions on one vertically centered row.
- A standalone Export uses a normal 150–252-DIP allocation, with a larger readable minimum when measured text requires it. Below spacious mode it stays beside the combined left header/metrics composition while the minimum layout fits, shrinking before moving below. Both sides share a vertical center; height is the larger of the measured left group (including its 12-DIP row gap) and the action. A taller action centers the left group as one unit. It does not reserve space for future secondary actions.
- An exported action group normally uses a 252-DIP wide composition: Export Again above paired Open Folder/Copy path, spanning exactly their combined widths and 8-DIP gap. It does not continuously shrink like a standalone Export. When that composition no longer fits, all three actions use an equal-width rail (normally 150 DIPs); when the rail cannot fit beside the information, actions move below.
- Compact result content is centered inside a maximum 360-DIP video or 296-DIP image envelope. Export sits above the secondary pair; the pair can fall back to a column if its own readable minimum cannot fit. Metric groups are centered, use equal columns per row and center a shorter last row.
- Metric cards share nominal 88–112-DIP budgets in regular and compact modes. Explicit content/minimum exceptions and wrapping preserve readability; unused width is not automatically assigned to cards.
- Height follows visible content without future-action reservation. Side-by-side working and spacious-result compositions share a 54-DIP minimum inner height, derived from the working indicator footprint; ordinary unwrapped Rendering/commit/completion/Ready transitions therefore use an 88-DIP shell without reallocating preview height. This is not a fixed or maximum height: wrapped/enlarged text, long metadata, exported groups and narrower multi-row layouts grow as required; equal heights are not forced across those compositions. The shell has a 1-DIP border, 16-DIP padding and 10-DIP radius; result row/group/card/action gaps are 12/16/8/8 DIPs. Static badges use 42 DIPs; the working indicator uses 54 DIPs with a 12-DIP text gap, 3-DIP rings, 2-DIP colored-ring inset and fixed 14-DIP percentage text.
- The working circle is centered against the full information/progress body, sharing Cancel's vertical center when side-by-side. A stacked Cancel has its own row and is excluded from the circle's centering. Exported groups are centered against the adjacent information group.
- Image/video result-warning triangle/exclamation paths use `ResultWarningIconStyle`, based on `VectorIconStyle` (1.6-DIP stroke, rounded caps/joins), retaining semantic WarningBrush and the 22×20-DIP artwork in the existing 42-DIP badge. Optical alignment centers the triangle's circumcircle: at this size its center lies 2.5485 DIPs below the rectangular artwork center, corrected by a shared -2.55-DIP visual translation. Geometry, layout footprint, success/error artwork and warning-state semantics are unchanged.
- Accepted image symbol-grid/output metrics, retained font warnings and Show Details preserve their bindings and semantics. Long guidance increases natural height or uses the established outer overflow path.

Dated outcomes, superseded layout decisions and native limitations are consolidated in [UI refinement history](UI_Refinement_History.md). Complete iteration records are retained locally outside Git as described there; historical measurements are not current requirements or acceptance of later builds.

Before an image render exists, the result panel may remain visible to report a failed or cancelled render, but it hides all export-related action containers. The status panel has no output-format selector in any state. A stale retained render keeps Export disabled; the metadata-row `Render Again` action remains the single rerender control, with the existing neutral retained-result styling. Failed and cancelled states identify whether rendering or exporting was affected; export failure or cancellation preserves a current retained render for retry without rerendering.

Successful image rendering gives the footer the concise `Image ready for review and export.` state message. Symbol-grid and output dimensions remain in the result-panel metric cards; a font-fallback diagnostic, when present for the accepted retained render, appears as a warning beside that result rather than extending the footer. The warning persists with a stale or otherwise retained render and refreshes on successful replacement.

In the lower processing panel, the segmented accent ring rotates around stationary percentage text and uses a restrained opacity pulse while rendering or cancellation cleanup is active. Its four equal-angle arcs have even gaps at both the compact preview and detailed panel sizes; the preview overlay uses the same ring motion without percentage text. Determinate bar values ease monotonically toward the latest truthful percentage, and meaningful stage changes crossfade without animating frame or timing updates. Successful review-artifact publication replaces the percentage with a green vector completion mark before the panel settles into its persistent ready state; failure and cancellation never show success. These effects are presentation-only and are disabled when Windows client-area animations are disabled.

It must not include:

- Pause button.
- GPU acceleration indicator.
- Hardware encoder selector.

### 10.9 Footer

Footer contains:

- Assembly-derived application version (for example `v1.0.0`).
- One concise current-state message. Operation start replaces stale prior-action text immediately; completion, failure, and cancellation use their matching outcome. Detailed percentage, frame count, elapsed time, and ETA remain exclusively in the processing/result panel, and obsolete callbacks cannot restore an older footer state.
- About button.
- Settings button.

Footer does not contain:

- GPU text.
- Theme toggle.
- Hardware information.
- A permanent privacy/telemetry label; that explanation belongs in About.

### 10.10 About and Third-party Notices

About opens as a themed owner-modal dialog without replacing the current operation status. Header and About use the same BrandLogo template: a 54×54-DIP frame, 1-DIP border, 8-DIP corner radius and centered 38×38 vector artwork. Each in-app drawing keeps its own live resource context so accent preview/rollback updates it without recoloring neutral GF lettering. Main and Settings native window icons separately use ThemedWindowIcon to clone the shared vector per window, retaining DynamicResource expressions; this prevents stale accent brushes from shared Application-resource contexts. WPF refreshes its own icon handles on drawing changes. Native icon geometry and executable branding remain unchanged; no theme polling or manual native-handle lifecycle is added.

About shows GlyphFrame, the assembly-derived version, the concise product description, a Privacy section explaining local processing/no media uploads/no telemetry, and a Third-party software section naming FFmpeg and SkiaSharp without claiming a complete dependency inventory. Close remains reachable outside the vertically scrollable body, receives initial focus, and supports Escape. Natural height is capped to the owner monitor's usable work area; the body has no horizontal scrolling.

Third-party notices explicitly reads `THIRD_PARTY_NOTICES.md` from the running application's base directory. The owner-modal viewer displays the complete document as selectable read-only, wrapping local text. It does not execute Markdown, follow links or fetch external content. Unreadable/missing notices produce concise local guidance in About; closing the viewer returns focus to its action. The document is included in development and portable output.

Logo/placeholder and About/action-bar verification, including the distinction between fixtures and native checks, is recorded in [UI refinement history](UI_Refinement_History.md#recorded-verification).

## 11. UI States

### 11.1 State List

```text
Empty
LoadingMedia
LoadingVideoFrame
MediaReady
PreviewingFrame
FramePreviewReady
Rendering
RenderReady
RenderStale
Exporting
Exported
Cancelling
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

MediaReady:

- Original preview visible.
- Settings enabled.
- Image mode exposes Render Image without a separate preview action.
- Video mode exposes Preview Frame and Render Video when their independent prerequisites are valid.
- Export remains unavailable until a current render result exists.

PreviewingFrame:

- Applies only to video-frame preview.
- The active frame-preview request can be superseded by a newer position.
- Full render state and rendered-video identity remain independent.

FramePreviewReady:

- Symbolized video frame visible.
- Original and Symbolized tabs enabled.
- Full Render Video remains available.

Rendering:

- Render controls and settings that affect result identity are disabled.
- Export is unavailable.
- Processing panel shows truthful progress and Cancel.
- UI remains responsive.

RenderReady:

- Current image pixels or completed video artifact are reviewable in Symbolized.
- The persistent result panel exposes Export actions and result metrics; image format selection is available only in the native Save dialog opened by Export Image.
- The preview-panel media action row exposes the single Render Again action.
- Choosing an image format in Save does not make the image render stale or change the persisted default.

RenderStale:

- The previous result remains reviewable with an explicit stale warning.
- Export is disabled.
- The preview-panel Render Again action is the primary enabled action.

Exporting:

- Export button disabled.
- Current render result remains authoritative and is not regenerated.
- A destination-side partial is used before atomic move or replacement.
- UI remains responsive.

Exported:

- Success status shown.
- Open output location action available.
- Current render result remains available for repeated export.

Cancelling:

- Cancel cannot be repeated.
- Conflicting operations remain disabled until all active work releases resources.

Cancelled:

- Cancelled status shown.
- Partial render/export output is removed or clearly marked invalid.
- A previously current result remains intact unless it was explicitly replaced by the cancelled operation.

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
- Default image export format: initializes PNG/JPEG in Save, where an individual export can change it without changing this preference.
- Video audio mode.

The polarity is stored as a readable enum name. Existing settings files without the property use `LightOnDark`; invalid values normalize to that default. Reset to defaults includes `LightOnDark` in the editable draft; Save & Close applies that draft through the normal settings path without changing transient media ownership.

### 12.3 Application

The scrollable Application section includes:

- Selectable read-only application-data and logs-folder paths, with full-path help and standard keyboard copying.
- Open logs folder, including friendly launch-failure guidance.
- Bundled FFmpeg/ffprobe availability, versions and location, Refresh, and Show Details when a failed validation retains technical diagnostics.

### 12.4 Editing and Persistent Actions

The persistent bottom action bar has neutral `Reset to defaults` on the left and `Cancel` / primary `Save & Close` on the right. The right pair moves below only when its measured group budget cannot fit beside Reset; exceptionally small available widths allow further vertical reflow. Action heights follow text with their normal minimums; no action depends on scrolling to Application.

Reset loads the application's existing defaults into the editable draft and previews the accent; it does not save or publish settings. Its confirmation describes the draft and the need to Save. Cancel, Escape or window dismissal discards the draft and restores persisted preferences/accent. Save uses the normal persistence path; failure retains the draft and meaningful guidance for retry rather than closing or reporting success. Logs, media, cache and exports remain untouched.

The feedback row above the actions is absent when there is no meaningful message. Reset guidance, save failure and relevant action feedback appear there with wrapping only when present. No permanent introductory instruction or duplicate Reset block is reserved in Application.

## 13. Architecture

### 13.1 Solution Structure

The solution contains four production projects and two test projects:

```text
GlyphFrame.sln
├── src/
│   ├── GlyphFrame.App
│   ├── GlyphFrame.Core
│   ├── GlyphFrame.Rendering
│   └── GlyphFrame.Infrastructure
└── tests/
    ├── GlyphFrame.Tests
    └── GlyphFrame.App.Tests
```

Core is platform-neutral and defines contracts, models and policies. Rendering implements the shared symbolization/image pipeline with SkiaSharp. Infrastructure implements local files, settings and bundled-tool processes. App owns WPF presentation, playback and orchestration through dependency injection. The App test project targets Windows/WPF; the other test project targets platform-neutral code and includes real bundled-tool integration tests on the supported environment.

### 13.2 Folder Structure

Current source folders, excluding generated build output:

```text
src/
├── GlyphFrame.App/
│   ├── Behaviors/
│   ├── Presentation/
│   ├── Resources/
│   ├── Services/
│   ├── ViewModels/
│   └── Views/
├── GlyphFrame.Core/
│   ├── Enums/
│   ├── Imaging/
│   ├── Interfaces/
│   ├── Media/
│   ├── Models/
│   ├── Policies/
│   ├── Presets/
│   └── Settings/
├── GlyphFrame.Rendering/
│   ├── Conversion/
│   ├── Grid/
│   ├── Mapping/
│   └── Skia/
└── GlyphFrame.Infrastructure/
    ├── Ffmpeg/
    ├── Media/
    ├── Paths/
    ├── Properties/
    ├── Settings/
    └── Shell/
```

### 13.3 Core Components

The implementation separates input, rendering, artifact ownership, publication and presentation responsibilities:

| Responsibility | Implemented components |
| --- | --- |
| Input and metadata | Core `MediaTypeDetector`; Infrastructure `MediaInputService`, `MetadataExtractorImageMetadataReader`, `MetadataExtractorVideoMetadataReader`, `FfprobeVideoMetadataReader` |
| Shared image/frame symbolization | `ImagePipelineService`, `SkiaImageDecoder`, `OutputDimensionResolver`, `SkiaGlyphMeasurer`, `SymbolGridCalculator`, `BrightnessToSymbolMapper`, `FrameToSymbolConverter`, `SkiaSymbolFrameRenderer`, `SkiaImageExporter` |
| Tool execution and preview | `BundledFfmpegLocator`, `ProcessRunner`, `BinaryProcessRunner`, `FfmpegVideoFrameReader` |
| Streaming video and audio | `FfmpegSequentialVideoFrameReader`, `FfmpegVideoEncoder`, `StreamingVideoExportService`, `FfprobeAudioStreamProbe`, `Mp4AudioCompatibilityPolicy`, `FfmpegAudioMuxer` |
| Review-artifact ownership and publication | `ManagedVideoRenderService`, `ManagedVideoArtifactStore`, `ManagedVideoArtifactSession`, `VideoArtifactPublisher` |
| Playback | App-owned `OriginalVideoPlayer`, `RenderedVideoPlayer`, shared playback primitives and `VideoPlaybackCoordinator`; separate mutable transports |
| Settings, diagnostics and dialogs | `JsonSettingsService`, `ApplicationSettingsManager`, `AccentThemeService`, `LocalDiagnosticLogger`, `ErrorPresentationService`, `ErrorDetailsDialogService`, `ThirdPartyNoticesService` |

### 13.4 Key Interfaces

The linked declarations are authoritative for exact signatures, defaults and ownership. This index describes the boundaries without maintaining duplicate API declarations.

| Contract | Current boundary |
| --- | --- |
| [IMediaTypeDetector](../src/GlyphFrame.Core/Interfaces/IMediaTypeDetector.cs) | Detects `MediaSelectionKind`, including None. |
| [IImageMetadataReader](../src/GlyphFrame.Core/Interfaces/IImageMetadataReader.cs), [IVideoMetadataReader](../src/GlyphFrame.Core/Interfaces/IVideoMetadataReader.cs) | Asynchronous typed metadata reads with cancellation. |
| [IVideoFrameReader](../src/GlyphFrame.Core/Interfaces/IVideoFrameReader.cs) | Random-access normalized position plus optional metadata context; returns an owned frame/result. |
| [ICharacterPresetProvider](../src/GlyphFrame.Core/Interfaces/ICharacterPresetProvider.cs) | Lists `CharacterPresetOption` values and resolves a `CharacterPresetKind`. |
| [ISymbolGridCalculator](../src/GlyphFrame.Core/Interfaces/ISymbolGridCalculator.cs) | Calculates from output `PixelDimensions` and measured `GlyphMetrics`. |
| [IBrightnessToSymbolMapper](../src/GlyphFrame.Core/Interfaces/IBrightnessToSymbolMapper.cs) | Maps luminance through a character ramp and explicit polarity. |
| [IFrameToSymbolConverter](../src/GlyphFrame.Core/Interfaces/IFrameToSymbolConverter.cs) | Converts `DecodedImage` using grid, metrics, preset, polarity and cancellation. |
| [ISymbolFrameRenderer](../src/GlyphFrame.Core/Interfaces/ISymbolFrameRenderer.cs) | Renders a `SymbolFrame` to owned `RenderedImage` with cancellation. |
| [IImageExporter](../src/GlyphFrame.Core/Interfaces/IImageExporter.cs) | Borrows rendered pixels and encodes to a path with format, overwrite policy and cancellation; the caller retains ownership. |
| [ISequentialVideoFrameReader](../src/GlyphFrame.Core/Interfaces/ISequentialVideoFrameReader.cs) | Owns one sequential decoder for the operation rather than repeatedly invoking random-access preview extraction. |
| [IVideoEncoder](../src/GlyphFrame.Core/Interfaces/IVideoEncoder.cs) | Accepts an asynchronous rendered-frame stream, disposes each yielded frame and reports accepted-frame progress. |
| [IVideoExportService](../src/GlyphFrame.Core/Interfaces/IVideoExportService.cs) | Coordinates streaming rendering/encoding/audio through typed jobs, results and progress. |
| [IManagedVideoRenderService](../src/GlyphFrame.Core/Interfaces/IManagedVideoRenderService.cs), [IManagedVideoArtifactSession](../src/GlyphFrame.Core/Interfaces/IManagedVideoArtifactSession.cs) | Render candidates and accept/reject retained review artifacts under session ownership. |
| [IVideoArtifactPublisher](../src/GlyphFrame.Core/Interfaces/IVideoArtifactPublisher.cs) | Publishes the accepted artifact without symbolization or video re-encoding. |

FFmpeg paths, process arguments, binary pipes, exit codes and backend diagnostics remain in Infrastructure. Successful frame reads own exact straight-alpha RGBA8888 pixels independently of the completed process. Preview extraction and sequential decoding retain their distinct lifetime/cancellation contracts. Awaited encoder writes provide backpressure; Core contracts do not expose WPF, process handles or Skia native types.

## 14. Data Models

This section describes current model responsibilities. Exact declarations are linked rather than duplicated as an alternative schema.

### 14.1 MediaSelectionKind

[MediaSelectionKind](../src/GlyphFrame.Core/Enums/MediaSelectionKind.cs) distinguishes None, Image and Video.

### 14.2 ImageMetadata

[ImageMetadata](../src/GlyphFrame.Core/Models/ImageMetadata.cs) is an immutable record containing the path/name, encoded and display-oriented dimensions, format, byte size, normalized orientation and optional raw orientation value. `HasOrientationMetadata` is derived from that optional value; orientation is not reduced to a standalone Boolean.

### 14.3 VideoMetadata

[VideoMetadata](../src/GlyphFrame.Core/Models/VideoMetadata.cs) retains path/name, container, byte size, optional encoded dimensions, duration, FPS, frame count, codecs, audio presence/sample rate and rotation. Unknown values remain nullable; frame count is `long?`. Display dimensions apply normalized quarter-turn metadata without replacing encoded diagnostics.

### 14.4 VideoFrameReadResult

[VideoFrameReadResult](../src/GlyphFrame.Core/Models/VideoFrameReadResult.cs) carries status and a typed error category, an owned `DecodedImage` on success, requested/resolved normalized positions, optional timestamps/frame context, friendly guidance and separate technical detail. Cancellation is explicit; superseded cancellation is not a user-facing error. Unknown timing fields are not fabricated.

### 14.5 ImageRenderSettings

[ImageRenderSettings](../src/GlyphFrame.Core/Models/ImageRenderSettings.cs) captures font family, font size, `OutputResolution` and `SymbolPolarity`. Its default is Cascadia Mono, size 10, Same as source and Light on Dark.

Original Colors is the sole fixed product color policy. The [SymbolFrame](../src/GlyphFrame.Core/Models/SymbolFrame.cs) stores its resolved opaque background, so both the image and video pipelines render without consulting later mutable settings.

### 14.6 CharacterPresetOption

[CharacterPresetOption](../src/GlyphFrame.Core/Models/CharacterPresetOption.cs) is an immutable record with kind, name, exact ramp and Unicode flag. It defines the three built-in ramps and Detailed ASCII default; `CharacterPresetProvider` exposes those options.

### 14.7 Render and Publication Jobs

Rendering, managed review and destination publication use separate contracts:

- The image pipeline consumes source pixels, `ImageRenderSettings` and `CharacterPresetOption`; [ImagePipelineResult](../src/GlyphFrame.Core/Models/ImagePipelineResult.cs) retains rendered pixels, grid and glyph metrics. Image export borrows that result through `IImageExporter`.
- [VideoExportJob](../src/GlyphFrame.Core/Models/VideoExportModels.cs) captures input/output paths, metadata, render settings, preset, overwrite permission and audio mode. Separate sequential-decode and video-encoding jobs describe the pipe stages.
- [FullVideoRenderIdentity](../src/GlyphFrame.Core/Models/FullVideoRenderIdentity.cs) captures the accepted review identity; frame-slider position is excluded.
- [VideoArtifactPublicationJob](../src/GlyphFrame.Core/Models/VideoArtifactPublicationJob.cs) couples an accepted artifact to the original input, observed destination version and overwrite permission. Publication is distinct from rendering.
- App's [ImageSaveDialogResult](../src/GlyphFrame.App/Services/IImageSaveDialogService.cs) returns the accepted path and format; the saved preference only initializes that dialog.

### 14.8 VideoExportProgress

[VideoExportProgress](../src/GlyphFrame.Core/Models/VideoExportProgress.cs) is an immutable stage snapshot with a `long` processed-frame count, optional `long?` total, optional percentage/ETA, elapsed time, indeterminate flag and stage description. `VideoEncodingProgress` separately reports accepted encoder frames. Workflow state is not a substitute for pipeline-stage progress.

### 14.9 Managed Video Review Artifact

Phase 11B.2 adds platform-neutral `FullVideoRenderIdentity`, `ManagedVideoArtifact`, and `ManagedVideoRenderResult` models. The identity captures the media/source generation, render-settings generation, preset and exact ramp, font family and size, output resolution, polarity, and audio mode; video-frame slider position is deliberately excluded. Artifact metadata contains only the generated identifier and cache path, output dimensions, normalized FPS, accepted frame count, authoritative duration, captured audio mode and outcome, and file size. Video bytes remain on disk rather than in settings or memory.

The artifact store owns every physical file below its verified cache root. Normal candidate operations require a direct child of the current leased session with the canonical `artifact-{id}.mp4` or matching pending name; foreign-session, reparse-point, noncanonical, empty, and outside-root candidates are rejected. A completed render is a candidate until the workflow accepts or rejects it. Once accepted, other layers borrow the artifact while the session owns deletion. Replacement accepts the new candidate before attempting to delete the prior artifact; failed deletion is tracked without rolling back the new current artifact, then retried on later operations and shutdown. Exclusive lease probing makes later startup recovery safe when multiple GlyphFrame processes run concurrently.

Phase 11B.3 finalization uses a cancellable playback preflight before the session commit. Close the previous player handle but retain its owned file, open the exact completed candidate with the render token, and revalidate operation, media generation, render identity, and view lifetime. Failed/cancelled preflight rejects the candidate and restores the previous review. If no previous result exists, an unplayable completed candidate may remain owned as Review Unavailable; it must not show success. After valid preflight, disable Cancel and label the narrow acceptance stage as non-cancellable. Accept the captured candidate, record its exact identity, and revalidate after the await. Only a current, successfully opened and accepted result may select Symbolized and show Video Ready/100%. If settings change during commit, retain the matching accepted identity as stale, not as a current success. Shutdown or view invalidation must prevent late reopening or success presentation. Backend progress cannot overwrite this UI finalization stage.

Playback borrows the managed MP4 without transcoding or copying. Each opening creates an isolated WPF media transport and enables scrubbing, including after Close, so paused seeking is supported without resetting volume/mute preferences. Fullscreen is owned by the preview's window; Escape returns to the existing embedded session. Fit / Fill / 100% remains available during rendered-video review and applies to both surfaces. Unloading stops timers and closes media handles; reloading reopens the retained artifact paused at its saved position. Owner closure, replacement, and shutdown release playback before session deletion. A runtime playback failure updates all result indicators and commands and retains the artifact with Review Unavailable.

Artifact publication has an independent operation outcome: success records the user destination and shows Video Exported; failure/cancellation uses export-specific presentation without changing the retained review identity or deleting its MP4. Playback preferences never affect publication. Deterministic workflow and STA window/transport tests cover review preservation. The 2026-09-18 user-performed interactive pass completed the real paused-frame refresh, audio, fullscreen, scaling, and multi-monitor playback checks required for Phase 11B.3 closure.

View reload during active rendering/acceptance, frame preview, or artifact publication records deferred recovery rather than permanently skipping it. Every operation-completion path uses an awaitable completion boundary that releases operation resources before processing pending recovery. A shared serialized reopen path is used by this boundary, view loading, post-commit recovery, and restoration of a previous artifact. It validates media, operation, artifact, view generation, and cancellation before and after asynchronous work. Queued duplicate requests do not retry a failed recovery automatically; obsolete continuations never close a newer transport. A loaded view recovers the exact accepted artifact at its retained position without overwriting export success/failure/cancellation; an unloaded view remains suspended until a valid load, with no green Video Ready presentation. Only settings/audio changes affect artifact staleness, not view reloads. Image operations use the same completion boundary but cannot reopen video from unrelated media.

Retained playback availability and diagnostics are independent of the latest render-operation outcome. Starting, failing, or cancelling Render Again does not clear an existing review error or describe a closed player as playable. A successful valid open clears the retained playback error; unrelated media selection clears its entire review context. Render diagnostics remain separately available when restoration succeeds after a failed replacement. Success, warning, and failure indicators must not overlap. Core continues to describe the completed artifact identity; App additionally requires a successfully opened matching player before presenting Video Ready.

## 15. Enums

Current enum declarations are separate source files; UI labels are not additional enum values.

| Enum | Purpose / values |
| --- | --- |
| [MediaSelectionKind](../src/GlyphFrame.Core/Enums/MediaSelectionKind.cs) | None, Image, Video |
| [AudioExportMode](../src/GlyphFrame.Core/Enums/AudioExportMode.cs) | KeepAudio, RemoveAudio |
| [OutputResolution](../src/GlyphFrame.Core/Enums/OutputResolution.cs) | SameAsSource, Hd720, FullHd1080 |
| [ImageExportFormat](../src/GlyphFrame.Core/Enums/ImageExportFormat.cs) | Png, Jpeg |
| [SymbolPolarity](../src/GlyphFrame.Core/Enums/SymbolPolarity.cs) | LightOnDark, DarkOnLight |
| [CharacterPresetKind](../src/GlyphFrame.Core/Enums/CharacterPresetKind.cs) | Identifies the three built-in character presets. |
| [RenderWorkflowStatus](../src/GlyphFrame.Core/Enums/RenderWorkflowStatus.cs) | Durable media/render/review/publication state, including stale, cancelled and failed results. |
| [VideoExportStage](../src/GlyphFrame.Core/Enums/VideoExportStage.cs) | Preparing, ProcessingFrames, FinishingEncoding, InspectingAudio, MergingAudio, CopyingArtifact, Finalizing, Cancelling, Completed |
| [VideoExportStatus](../src/GlyphFrame.Core/Enums/VideoExportStatus.cs) | Terminal result status, separate from live stage progress. |

Original Colors remains the single fixed rendering policy rather than a configurable enum. Workflow state, live pipeline stage and terminal operation result are separate concepts. Additional typed input/process/frame/artifact error and lifetime enums remain in Core; this table is a navigation index, not an exhaustive replacement for their declarations.

## 16. Privacy and Local Data

GlyphFrame must follow these privacy rules:

- All processing is local.
- No file upload.
- No telemetry.
- No analytics.
- Phase 12C logs stable error identities and operation contexts locally after accepted recoverable failures.
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

Phase 12A implements one immutable application-level presentation contract for recoverable failures. It contains a stable error code, the affected operation context, a non-empty catalog-owned user message, and optional technical detail. Raw exception messages, stack traces, media paths, HRESULT values, FFmpeg/ffprobe stderr, and process diagnostics are retained only as technical detail and must never become the primary message. Mapping is centralized for media input, frame extraction and symbolization, image render/export, full-video rendering, reviewed-artifact publication, managed artifacts, Original and Symbolized playback, and bundled-tool validation.

Managed video-render results preserve an optional originating `VideoExportErrorCategory` only for failures returned by the video pipeline, allowing decoder, renderer, encoder, audio-probe, audio-mux, and finalization identities to survive the managed-artifact boundary. Publication classifies destination access denial, Windows disk-full conditions, destination changes after confirmation, and generic atomic publication/finalization failure while the exception type and native error code remain available; cache and artifact-lifecycle failures do not fabricate a pipeline category.

Error presentations remain independent by workflow context. In particular, media-input, frame-preview, retained playback/review, latest render, and publication failures do not overwrite one another. A recoverable failure must preserve any retained image or video result and its established retry, stale-result, generation, ownership, cleanup, and player-lifetime rules.

Cancellation, request supersession, view unload, and shutdown are expected workflow outcomes, not application errors. They retain their existing state-specific messages and do not create an error presentation. Unexpected exceptions at established recoverable operation boundaries receive safe catalog guidance while their complete exception diagnostics remain available separately. Process-corrupting exceptions such as `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException` must not be normalized or broadly swallowed.

Phase 12B keeps concise friendly text as the normal presentation and adds one reusable modal `Error Details` dialog. A context-local `Show Details` action is visible and executable only when the failure currently displayed at that location retains non-empty technical detail. It opens the same immutable `ErrorPresentation` that owns the friendly message; successful retry, replacement, cancellation, supersession, or context reset cannot expose an obsolete presentation, and an unrelated workflow error is never merged into it.

The dialog uses the application theme and active-window ownership, shows the stable application error code, a user-readable operation, friendly message, and scrollable monospaced technical detail, and supports Escape, Close, and explicit `Copy Details`. The copied plain-text report has deterministic code, context, message, and retained-detail fields. Copying is never automatic; expected clipboard contention leaves the dialog open with concise retry guidance and is never reported as success. Bundled-tool validation uses the same dialog through `FfmpegErrorPresentation`; successful tool/path/version information remains in Settings while failed-tool raw diagnostics are no longer exposed through a separate expander.

Phase 12B does not copy automatically, and Phase 12C adds no telemetry, upload, or crash reporting. Full technical details remain in memory for explicit Show Details use and are never persisted by the local logger.

For Phase 12C, local logs live only under `IApplicationPaths.LogsDirectory` (`%LocalAppData%\GlyphFrame\logs`). An individual log file must not exceed 1 MiB (1,048,576 bytes); rotate before an append that would cross that limit. A serialized UTF-8 record, including its line terminator, must not exceed 1 KiB (1,024 bytes); do not write a truncated or malformed record. Retain at most 10 completed files and 10 MiB (10,485,760 bytes) of completed logs, removing the oldest completed files first. Remove completed files whose last write was more than 14 days ago, measured in UTC, even when the count and size limits have not been reached. Apply these limits only to GlyphFrame-owned completed log files. Never delete or modify a file while any GlyphFrame instance is actively writing to it; concurrent active files may temporarily increase total storage beyond the completed-log limits and become eligible for cleanup after they close. Logging and cleanup failures must not change the originating workflow result.

The implemented record is one newline-terminated UTF-8 JSON object with exactly `TimestampUtc` (ISO-8601 UTC), `Code` (`ApplicationErrorCode`), and `Context` (`ErrorPresentationContext`). The application queues these three allowlisted values at explicit current-failure acceptance boundaries, not from status-message or property notifications; a new failed retry records a new event even when its code and context match the previous failure. Repeated notification of one failure, Show Details, cancellation, supersession, success, unload, and shutdown do not create failure entries. Startup and Settings use the same application-level FFmpeg validation observer: the validator's cached result identity is recorded at most once across consumers, while a fresh failed Refresh result or recoverable failed validation attempt records another event. Media-input, frame, image, full-video render, publication, managed-artifact, Original/Symbolized playback, and bundled-tool validation use these boundaries. No `TechnicalDetail`, arbitrary message, path, filename, exception, stack, FFmpeg output, settings value, clipboard content, or media content is serialized. Each process writes a unique `.active` file and renames it to an owned `.log` when complete. Recovery ignores locked live files and removes abandoned active files only when exclusive write access proves no writer remains. File work runs off the UI thread; missing or unwritable storage drops the logging attempt without affecting the original result or recursively logging the failure. Settings retains Open Logs Folder.

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
- Retain at most one completed full-video review artifact for the active media session, plus bounded in-progress partials.
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
- Image-only Same-as-source warning above the display-oriented 1080p boundary, without limiting render/export.
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
- Determinate and indeterminate export progress, accepted-frame counting, monotonic percentages, elapsed/ETA calculation, ordered stage transitions, terminal completion/cancellation, and stale-update rejection.
- Cancellation during decoding, rendering, encoder writes/finalization, audio probing/muxing, and the pre-publication boundary with process-tree, partial-file, destination, and file-handle verification.
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

Phase 15A implements reproducible packaging steps with `pwsh -NoProfile -File scripts/Build-PortablePackage.ps1` (Windows, PowerShell 7, .NET 10 SDK, Git and configured NuGet/runtime-pack access). The contract is Release / win-x64 / self-contained / folder-based (`PublishSingleFile=false`) / untrimmed (`PublishTrimmed=false`). One fresh publish reuses the existing required runtime/native/tool/attribution, checksum, outside-repository startup, short marked isolated-data-root, active PID-bound lease and exact-session shutdown-cleanup checks. The verifier remains independently runnable.

The package is `GlyphFrame-<version>-win-x64-portable.zip` with one `GlyphFrame-<version>-win-x64/` application directory and expected relative Tools layout, plus a SHA-256 checksum and machine-readable report in a unique ignored run directory. Version is read from the published application assembly using the same Major.Minor.Build convention as About, not a new hard-coded version. Reports record commit/dirty-tree provenance, SDK/tool versions, build options, excluded PDBs, staging file hashes and completed validation evidence. Archive entries are checked for required layout, safe relative paths, duplicate/case/file-directory conflicts, unwanted content and byte/hash agreement with validated staging. Generated data, tests/fixtures, repository/IDE files, logs/settings/cache, markers, media and smoke evidence are not distributed. Failure retains evidence and cannot report an incomplete package as successful. Reproducible commands/payload verification do not promise identical ZIP bytes across runs.

Portable deployment is extraction and launch without an installer, **not** beside-EXE application data: ordinary settings, privacy-safe logs and managed cache retain `%LocalAppData%\GlyphFrame`. The marked verification-data-root opt-in remains verification-only. The included user README documents extraction, directory preservation, uninstall by removing the closed application's folder, and compatibility limitations. Existing third-party notices/license/distribution information are preserved; their presence does not prove legal distribution compliance or supply an invented application license. Public release requires release-owner licensing and corresponding-source review.

Startup verification of the published folder is not interactive execution of the extracted ZIP. Independent Phase 15B native validation on 2026-10-07 used a fresh ZIP from clean commit `cdb1f8eadaeda3d831c203faf62d89ed56cd8297`, verified all 416 extracted payload hashes, and launched only the extracted EXE with isolated data and an observed production lease. Image/video rendering/publication, full output decoding, active video-render cancellation/retry, and normal exact-session cleanup passed. Repeated fullscreen entry failed time/frame/thumb agreement; returning to Original once reset its position to zero. These are observed defects, not fixes or a complete Phase 15B sign-off. Human audibility and active artifact-publication cancellation remain NOT TESTED; transient image Rendering layout was inconclusive. Phase 15C clean Windows without installed .NET/FFmpeg remains NOT TESTED. See [the dated packaging validation report](Phase_15_Packaging.md#phase-15b--independent-extracted-zip-validation-2026-10-07) for identities, reproduction, per-scenario methods and evidence. No product fix, release/installer/signing or Phase 16 publication was performed.

## 21. Roadmap

### Current roadmap status — 2026-10-25

Committed baseline `e5e743f` includes progress/result, warning, preview-caption and startup refinements after `803ab97` image navigation. Its October 24 clean-source A1 passed **1,257 Release tests (488 core + 769 App)**, a zero-warning/error forced Release build, formatting/diff and 35 separate helper checks. A2 passed for the [recorded October 24 clean-source portable](Phase_15_Packaging.md#recorded-october-24-clean-source-candidate), run `8399abc6`, including all 416 payload hashes and independent extracted startup/session verification. These are historical passes for that committed baseline, not for the newer working-tree correction.

The current uncommitted native-icon correction has **1,258 passing Release tests (488 core + 770 App)**, a zero-warning/error forced rebuild and successful formatting/diff verification, including a fresh precommit recheck. Its [latest provisional](Phase_15_Packaging.md#latest-recorded-candidate), run `2ef20d96`, records parent `e5e743f` with WorkingTreeDirty=true. The [A5 manual checklist](Phase_14_Coverage.md#user-reported-a5-retest--october-25) is USER-REPORTED PASS for the newest locally tested version on Windows 26H2 build 26300.9711, with display/text and second-monitor checks as recorded there; no exact tested ZIP hash is inferred. Next: commit the reviewed implementation/documentation, repeat affected clean-source A1/A2 and bind acceptance to the final candidate.

The [A4 record](Phase_14_Coverage.md#current-candidate-a4-verification--october-24) separately attributes positive ordinary-use, integration and agent-native outcomes; native Save failure/retry and stronger shell/copy-after-error cases remain open. Remaining A3/A6/A7, separate clean-Windows A8 and portfolio/distribution A9/A10 scope is maintained in the release checklist. Earlier `75aad73`/`1ddbab1` passes and the [focused footer retest](Phase_14_Coverage.md#user-reported-progress-footer-retest--october-24) retain their original attribution; none certifies every target environment.

| Phase | Current status | Remaining boundary |
| --- | --- | --- |
| 1 — Documentation | Maintained specification/reference exist; current acceptance summaries synchronized | Public-facing documentation and evidence presentation are part of Phase 16. |
| 2–12, including 11B | Core application, rendering/review/export, settings and error-handling workflows implemented | Historical representative checks do not replace current Phase 14/15 acceptance. |
| 13 — UI Polish | Implemented, including an uncommitted live native-icon follow-up; A5 manually passed for the user's newest local version | Preserve the supplied manual scope; final candidate binding and broader A3 acceptance remain open. |
| 14 — Testing | Current working-tree suite: 1,258 passed; historical clean A1 for `e5e743f`: 1,257 | Repeat affected A1 after the implementation commit; finish remaining A3/A4/A6/A7 scope in the [release acceptance checklist](Phase_14_Coverage.md#release-acceptance-checklist). |
| 15A — Packaging | Implemented; latest `2ef20d96` is provisional, 11 publish checks / 416 payloads; clean `8399abc6` A2 is historical | Build and independently verify the final clean committed-source candidate; preserve previous provenance. |
| 15B — Extracted-package validation | Partial/open; A4 evidence and A5 user-reported PASS separately attributed | Bind the human result to final candidate identity and finish remaining layout/dialog/audio/publication cases without treating scoped passes as blanket acceptance. |
| 15C — Clean Windows | NOT TESTED | Execute without separately installed .NET/FFmpeg and record the actual OS/media environment. |
| 16 — Portfolio Release | Not complete | Prepare public documentation, demo assets and architecture overview; resolve licensing/distribution decisions before release. |

Phase 15B follow-up (2026-10-08): the shared playback controller owns a paused-position snapshot. Seek updates it immediately; playing timer ticks, a playing-to-paused transition and natural completion sample the native clock. Surface creation and idempotent pause do not replace a paused seek with delayed native zero/end readback; queued paused ticks are ignored. Replay remains the explicit restart operation. Separate transports, generation checks, media processing, publication and fullscreen ownership are unchanged. Seven delayed-readback/real-surface regression cases and 1,014 solution tests pass. An independently extracted dirty-tree candidate passed three full completion→midpoint→fullscreen-reopen cycles for each role and repeated paused timestamp comparisons. The historical single B2 observation's exact native cause is not retroactively proven. Precise playing-switch tolerance, rapid native switching, active publication cancellation, native SymbolizedPlayback-error/publication interaction, human audibility, sustained minimum-size image Rendering, broader DPI/multi-monitor checks and final clean committed-package retest are not signed off. See the October 8 follow-up in `Phase_15_Packaging.md`; Phase 15C remains separate.

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
- The Phase 9 encoder remains the video-only foundation. Phase 10 muxes audio afterward without re-encoding video, and Phase 11 reports accepted-frame throughput plus every non-frame stage without changing either pipeline.

### Phase 10 - Audio

- Capture Keep Audio or Remove Audio when export begins.
- Confirm source audio with bundled ffprobe; uncertain probe results fail safely.
- Stream-copy AAC into MP4; transcode incompatible or unknown codecs to AAC at 192 kb/s, 48 kHz stereo.
- Keep the completed symbolized H.264 video unchanged with `-c:v copy`.
- Allow shorter audio to end naturally and cap longer audio at the authoritative rendered-video duration without `-shortest`.
- Publish a silent result successfully when the source has no audio or Remove Audio is selected.
- Use separate video-only and mux partials, atomically publish only complete output, and preserve existing destinations on failure or cancellation.

### Phase 11 - Progress and Cancellation

- Report Preparing, Processing Frames, Finishing Encoding, Inspecting Audio, Merging Audio, Finalizing, Cancelling, and Completed stages through a platform-neutral progress contract.
- Advance frame counts only after the encoder accepts a complete rendered frame through an awaited write.
- Use a trustworthy positive source frame count for determinate progress; otherwise show the accepted frame count with indeterminate progress.
- Keep percentages finite, clamped, and monotonic, cap incomplete work below 100%, and report 100% only after atomic output publication succeeds.
- Measure elapsed time from export start and estimate remaining time from completed-frame throughput only after at least two frames and one second of determinate progress.
- Hide frame-based ETA during encoder finalization, audio inspection/muxing, output finalization, and every indeterminate stage.
- Cancel immediately, disable repeat requests and conflicting actions, and retain the cancelling presentation until cleanup completes.
- Propagate the same token through decoding, symbol rendering, encoder writes/finalization, strict audio probing, audio muxing, and the pre-publication boundary.
- Terminate active process trees through bounded cleanup, remove video and audio partials, never publish incomplete output, and preserve an existing destination.
- Keep export-status motion independent from pipeline semantics: stop decorative clocks when hidden or unloaded, respect the Windows reduced-motion preference, and show the completion mark only after successful publication and resource cleanup.

### Phase 11B - Render, Review, and Export Workflow

Phase 11B.3 implements the `Render Video` / `Render Again` application workflow, latest-operation acceptance rules, Video Ready and stale/failure/cancelled result presentation, and in-app review of the exact accepted managed MP4. Player UX is covered by deterministic controller, view-contract, and STA layout/lifetime tests plus the later user-performed audiovisual and multi-monitor pass. Phase 11B.4 implements artifact-based destination publication and removes the transitional direct-export surface. Phase 11B.5A records the compatibility evidence, and the completed Phase 11B.5B blocks implement the Original transport foundation, runtime-gated Original-tab UI, orientation, fallback, fullscreen, single-audio switching, and deterministic paused timestamp comparison. The representative interactive validation is complete; broader clean-machine and unusual-codec coverage remains future compatibility testing.

- Replace image preview generation with an explicit `Render Image` action and retain one reusable rendered-image result.
- Keep `Preview Frame` only for video and maintain its identity independently from the full-video render result.
- Add `Render Video` as the primary full-video action and render a complete MP4 review artifact into an application-managed local cache.
- Include the captured Keep Audio or Remove Audio choice in the review artifact so in-app review matches later export.
- Show the current image or completed video in the existing Symbolized tab; give a completed full-video artifact priority over a frame preview.
- Add in-app playback for the controlled H.264/AAC-or-silent MP4 artifact with play/pause, seek, time, volume/mute, fit, and deterministic handle release.
- Replace the transient export-status card with a persistent processing/result panel that transitions through Rendering, Render Ready, Render Stale, Exporting, Exported, Cancelling, Cancelled, and Error.
- Remove the permanent right-column Output card. Request image format and destination in the native Save dialog only when Export is invoked, keep export/result actions below the preview, and retain Render / Render Again in the metadata/action row without duplication.
- Export only a current, non-stale rendered result. Image export encodes retained rendered pixels; video export publishes the reviewed artifact through a destination-side partial and atomic move or replace without rerunning symbolization or video encoding.
- Keep stale results reviewable but disable export until the preview-panel Render Again action succeeds. Render-setting and video-audio changes invalidate the corresponding full result; image format and destination changes do not. Video slider movement invalidates only frame preview.
- Use unique Unicode-safe artifact paths and clean player handles, render partials, destination partials, and superseded/orphaned artifacts after media replacement, rerender, cancellation, failure, application exit, and subsequent startup recovery.
- Cover state transitions, result reuse, invalidation boundaries, stale-callback rejection, player disposal, cache cleanup, repeated export, cancellation, Unicode paths, and existing-destination preservation with automated tests.

#### Phase 11B.3 completion — Player UX and Review Scaling

The following player UX is implemented with automated controller, WPF view-contract, layout, and lifetime coverage. A user-performed interactive pass on 2026-09-18 completed the real audiovisual and multi-monitor checks. It does not add original-video playback or change export semantics.

- Replace text playback controls with one Play/Pause icon button, current/total time and seek bar, a Mute/Unmute icon button, compact volume slider, review-scale selector, and fullscreen toggle. Provide tooltips, accessible names, keyboard operation, and visible focus states.
- Style timeline and volume sliders consistently with the existing dark/accent theme: thin tracks, accent-filled position, a compact round thumb, and clear hover, pressed, disabled, and focus states. Retain native keyboard commands, click-to-seek, and a larger hit area. Suppress clock-to-thumb updates while dragging without replacing the binding or feeding clock updates back as seeks. Do not introduce a separate visual theme.
- Remove the separate bottom-row Restart button. At natural playback completion, retain the last frame and show a centered replay icon button over a subtle dimming layer. Clicking it or the bottom Play button restarts playback from the beginning. Ordinary pause must not show the replay overlay; seeking back before the end hides it and leaves playback paused. Use a 160 ms opacity transition and honor reduced motion. Stop/remove animation clocks when hidden or unloaded. Only a natural end during playback sets replay state; paused end-position notifications do not. Keep the decoded final frame without an additional seek beyond it.
- Reuse the same playback session and control behavior in embedded and fullscreen review. Fullscreen exposes an explicit exit button and supports Escape; replay, seeking, volume, and mute remain available without a second independent player. Controls remain visible; automatic hiding is deferred.
- Make Fit and Fill effective on the applicable image/frame and video review surfaces. Fit preserves the entire frame and its aspect ratio; Fill preserves aspect ratio while filling and clipping to the viewport. 100% means one source/output pixel per physical display pixel: use the displayed frame/artifact pixel dimensions divided by the viewport's current monitor DPI scale. Use scrollbars for oversized content inside a clipped viewport. The application declares PerMonitorV2 DPI awareness and recalculates on viewport/DPI changes. No outer Viewbox may rescale the media; controls and overlays stay at normal UI scale. These are presentation settings only and never change render identity, output resolution, or export bytes.
- Original video uses the reusable player surface only after the current WPF generation reports MediaOpened and positive natural dimensions. While opening, the existing FFmpeg frame remains visible. On timeout, missing dimensions, failure, or a later playback error, Original returns to its labelled frame-position surface without showing nonfunctional transport controls; Preview Frame remains the explicit symbolization action.
- Add regression coverage for control/replay state and scaling behavior. The 2026-09-18 user-performed pass verified displayed paused frames, audio, fullscreen controls, resizing, and Windows DPI behavior.

#### Phase 11B.4 — Publish the Reviewed Video Artifact (Complete)

Implemented after Phase 11B.3. The application exports the exact accepted current managed MP4; it never invokes symbolization, video/audio encoding, or remuxing for publication. The streaming export service remains an internal dependency of managed rendering, not a second user export route.

- Move video export actions into the persistent result panel below the preview, matching the image workflow, and remove the permanent right-column Output card and transitional direct-render/export action.
- Request an MP4 destination when Export Video is invoked. Publish the exact accepted, current managed MP4 without repeating symbolization or video/audio encoding; source playback or review scaling must not change what is exported.
- Keep Render Video / Render Again solely in the preview-panel action row. A stale result remains reviewable but cannot be exported until rerendered.
- Preserve the reusable artifact after export. Show the destination, Open Folder, and an appropriate repeat-export action in the result panel.
- Use destination-side partial publication and atomic move/replace where supported; confirm overwrite, preserve existing destinations on failure/cancellation, and clean incomplete publication files. Reuse existing Unicode-path, ownership, and lifecycle safeguards.
- Verify repeated export without rerendering, byte-preserving publication, stale-result blocking, cancellation, destination failures, existing-file preservation, and retention of the reviewed artifact.

Implementation and safety policy:

- `IVideoArtifactPublisher` is the platform-neutral publication boundary. Infrastructure copies with a bounded 128 KiB asynchronous buffer to an exclusively created, unique `.partial` beside the destination, flushes it, then performs atomic `File.Move` or `File.Replace`. There is no destructive replacement fallback. Resolution, FPS, duration, frame count, and captured audio are preserved byte-for-byte.
- A managed-session borrow holds its existing lifecycle gate throughout publication. Clear, replacement, and disposal cannot delete the source until the borrower finishes. The callback requires no WPF dispatcher continuation. Media/artifact identity is captured before the modal save dialog, revalidated after selection and destination preparation, and validated again by the session. Settings/audio invalidation or unload requests cancellation; obsolete UI completions are ignored. Existing serialized deferred-review recovery remains shared by all operation endings.
- The save dialog is opened on each export. Cancellation leaves the prior result/outcome unchanged. Existing-file confirmation is tied to file identity, write time, and SHA-256 content, rechecked after copying while writes are denied. A newly appearing or changed destination is a conflict requiring selection again, not implicit overwrite permission. Reparse-point paths and hard-linked destination files are conservatively rejected; file IDs and resolved parent directories protect original/cache paths including supported Windows aliases. The managed MP4 is never moved out of the cache.
- Copy progress reports copied bytes and publication, with no invented frame throughput or ETA. The last cancellation check immediately precedes atomic commit. Only committed publication reaches 100%; cancellation arriving after commit cannot change success to cancellation. Failed/cancelled work removes only its owned partial, preserves any prior destination, and keeps the managed review. If partial cleanup itself fails, the result records the owned incomplete path and technical diagnostics and does not claim success.
- Automated coverage includes byte identity across captured audio outcomes, repeated export, confirmation/conflicts, protected paths, cancellation before/after commit, injected commit/cleanup failures, session disposal/clear while borrowed, UI identity changes, recovery, and real rotated Unicode-artifact publication. Live smoke checks exercised render/review, repeated Unicode exports, opening/playing the exported file in Windows Media Player, Open Folder, declined overwrite, stale blocking, and owned-session cleanup on close. A user-performed follow-up on 2026-09-20 confirmed cancellation during active reviewed-artifact publication, confirmed overwrite of an existing destination, retry/review preservation, and the exact minimum-size publication layout. These checks complete the representative Phase 11B.4 validation. The later Phase 11B.5 user-performed pass separately covered audibility and audiovisual/multi-monitor player behavior.

#### Phase 11B.5 — Original Video Playback and Tab Comparison (Complete)

This approved roadmap addition after Phase 11B.4 and before Phase 12 is implemented through the Phase 11B.5B Original-playback, fallback, and paused timestamp-comparison blocks. Automated, real-container, and user-performed interactive validation are complete for the representative matrix. The runtime eligibility gate remains environment-dependent and is not a promise to play every imported file.

Phase 11B.5A compatibility evaluation is recorded in `docs/Original_Video_Playback_Compatibility_Report.md`. On the evaluated Windows 25H2 x64 system, WPF rendered the tested H.264/AAC-or-silent MP4, MOV, and MKV inputs and MPEG-4 Part 2/MP3 AVI input, including a Unicode source path. These are environment-specific observations, not container-wide guarantees. A tested FFV1/PCM MKV was fully readable through GlyphFrame metadata and FFmpeg frame extraction and raised WPF `MediaOpened`, but exposed no natural video dimensions and produced no decoded picture. A rotated H.264 MOV opened in WPF at encoded 320×240 rather than its metadata-directed 240×320 display orientation. WPF natural duration also differed from authoritative metadata on several short fixtures. Therefore `MediaOpened` alone is not a playback-readiness contract, display rotation needs explicit handling, and FFmpeg frame preview must remain the fallback. The original component evaluation did not establish audibility; the 2026-09-18 user-performed follow-up confirmed audible playback controls, single-audio switching, live paused comparison, fullscreen, and multi-monitor behavior. The five Phase 11B.5A decisions are the implemented contract for Phase 11B.5B: use a dedicated Original-player facade over separate shared WPF playback primitives, apply runtime playback eligibility and view-level rotation, coordinate paused timestamp transfer between tabs, retain explicit FFmpeg frame fallback, and create no automatic proxy.

The first Phase 11B.5B foundation block adds neutral App-layer playback contracts with generation-tagged opened, ended, and failed events, natural video dimensions, transport state, audio preferences, and deterministic close/disposal. `WpfMediaPlaybackBackend` owns one WPF `MediaPlayer` and rejects native callbacks after close, replacement, or disposal. A shared controller owns open generations, a bounded injectable deadline, position clamping, play/pause/replay, timer state, and volume/mute behavior. Existing `IRenderedVideoPlayer` remains the Symbolized-review facade through a compatibility adapter. Independent `IOriginalVideoPlayer` construction receives a different backend, timer, generation, state, and fullscreen host.

The second Phase 11B.5B block integrates that facade after media input and metadata acceptance. The reusable `VideoReviewSurface` binds to either facade without sharing mutable transport state. A ready Original source shows transport, timeline, mute/volume, scaling, replay, and independently owned fullscreen controls; opening preserves any decoded FFmpeg frame; fallback hides playback-only controls and retains the frame-position surface and processing actions. Trusted quarter-turn metadata rotates only the WPF Original visual and uses `DisplayWidth`/`DisplayHeight` exactly once, including fullscreen; FFmpeg frames receive no second transform. Selecting another accepted medium, unloading, or disposing closes Original transport and fullscreen, while a failed replacement selection preserves the last accepted source. Player events and open completions are checked against both native generation and current media identity.

A small coordinator owns playback activation and comparison switching. It pauses the outgoing player before capturing its absolute time, resolves the target duration, clamps and seeks the incoming surface, then explicitly leaves both transports paused. Original and Symbolized keep independent volume, mute, timer, source, and fullscreen state; switching never transfers those settings or resumes playback. Original target duration uses accepted source metadata first and the WPF duration only as fallback. Symbolized target duration uses accepted managed-artifact metadata first and the rendered player duration only as fallback. An unavailable, non-finite, zero, or negative target duration transfers to zero. When target FPS is trustworthy, a transfer at or past the end is clamped one target frame before the end; comparison validation uses `max(250 ms, two target frame intervals)`. No numeric tolerance is asserted without trustworthy FPS.

If Original is still opening, the requested comparison timestamp is retained only with the current media, operation, view, and open generations. A ready result applies it only while Original remains selected; fallback maps it to the normalized FFmpeg selector and uses the existing latest-request-wins frame reader without symbolization or a hidden WPF player. Unload, disposal, media replacement, or a newer switch invalidates pending comparison work. Preview Frame remains explicit and uses the same safe FFmpeg extraction path. Representative real audiovisual and platform validation is complete; broader environment coverage remains a future compatibility concern.

- Add playback in the existing Original tab, before or after symbolized rendering, using the same visual control design as the result player. Keep the existing Original/Symbolized tab locations.
- Start compatibility validation with the existing input-container allowlist: MP4, MOV, MKV, and AVI. A file extension identifies a container, not a codec guarantee. Document tested video/audio codec combinations, relevant profiles and orientation, backend requirements, and the validation environment.
- Keep import/FFmpeg processing support distinct from original-player support. Do not label an entire accepted container unsupported merely because one codec combination fails to play. Do not expand the import allowlist implicitly.
- When original playback is unavailable, retain FFmpeg-based original-frame preview and Preview Frame, explain the playback limitation, and keep rendering/export available if the processing pipeline can handle the file. Do not show an enabled but nonfunctional Play button.
- Support play/pause, seeking while paused, current/total time, available source audio, mute/volume, replay, scaling, and fullscreen. Keep source playback independent of Keep Audio/Remove Audio, which controls the rendered artifact rather than mutating source media.
- On switching tabs, pause the outgoing player before activating another surface; never play both audio streams concurrently. Transfer the current absolute media time, clamped to the target duration, and keep the incoming player paused for comparison. Frame-exact synchronization and automatic resume behavior remain outside the approved contract and must not be implied by approximate time alignment.
- Preserve deterministic Preview Frame semantics: capture a defined original-media position and obtain the source frame through the existing decoder; continuous playback must not silently trigger symbolization. Seeking and tab changes must not stale the full-video artifact or start a full render.
- Keep source media read-only and all processing local. Do not silently create a full source copy or a transcoded playback proxy. Any proxy strategy requires explicit approval of its storage, cleanup, cost, and privacy implications before implementation.
- Apply bounded resource use, asynchronous opening/decoding, clear playback errors, obsolete-callback rejection, and deterministic release on media replacement, unload, fullscreen closure, and application shutdown. Do not delete or invalidate the managed symbolized artifact when original playback fails.
- Before phase closure, validate a representative container/codec matrix, Unicode paths, rotated and silent/audio inputs, seek/replay, tab changes, source failures with frame-preview fallback, and simultaneous-audio prevention. Include real interactive checks, not only fake-backend tests.

#### Approved Phase 11B.5 Decisions

- Original playback backend: implement a dedicated Original-player facade with a separate instance of shared WPF playback primitives. Do not share one mutable player between Original and Symbolized, and do not add a third-party playback dependency in Phase 11B.5B.
- Playback eligibility: require the current open generation to receive `MediaOpened` and positive natural video dimensions. Treat this as a minimum readiness gate rather than a guarantee of continued decoding; fall back on timeout, failure, missing dimensions, or later playback failure without marking the source unprocessable.
- Orientation: apply trusted GlyphFrame rotation metadata in the Original view without transcoding or mutating the source.
- Tab comparison: pause the outgoing player, transfer an absolute timestamp clamped to the incoming duration, and keep the incoming player paused. Simultaneous playback is excluded. Use `max(250 ms, two frame intervals)` as the initial comparison-validation threshold and refine it only from measured Phase 11B.5B evidence.
- Fallback and proxy: retain FFmpeg original-frame preview and the existing explicit `Preview Frame` semantics, including full-artifact presentation priority, and do not create an automatic playback proxy.
- Resolved for Phase 11B.3: 100% is one media pixel per physical display pixel, with DPI-aware dimensions and scrollbars for oversized content; it is not another fit-to-window mode.
- Resolved for Phase 11B.3: fullscreen controls remain visible. Automatic hiding is deferred and is not implemented.

### Phase 12 - Error Handling

- **Phase 12A — Error Presentation Foundation (implemented):** immutable error identity/context plus catalog-owned friendly messages and separate hidden technical diagnostics; centralized normalization for input, processing, playback, tool, managed-artifact, and publication failures; independent workflow error contexts; cancellation remains a non-error outcome.
- **Phase 12B — Show Details (implemented):** one themed, owner-modal details dialog exposes the current context's immutable code, operation, friendly message, and retained technical detail only after explicit user action; deterministic clipboard copying is also explicit, tested through an abstraction, and failure-safe. Error lifetimes and independent workflow contexts remain unchanged.
- **Phase 12C — Local Logging (implemented):** accepted recoverable failures enqueue only UTC timestamp, stable error code, and operation context for best-effort local logging under the specified 1 MiB per-file, 10 completed files/10 MiB total, 14-day retention, and 1 KiB per-record limits. Concurrent active writers remain protected; no media content, decoded frames, technical details, or fabricated events are logged.
- Existing overwrite confirmation remains part of safe publication rather than an error state. Destination conflicts, permission failures, insufficient space, encoder failures, and missing tools use the Phase 12A presentation policy.

### Phase 13 - UI Polish

- Shared buttons, tabs, segmented controls, and player icon buttons have distinct enabled hover/pressed/keyboard-focus/disabled presentation under the selected accent. Disabled controls no longer take on active hover styling; muted copy has improved contrast against dark cards.
- The existing empty-state media-selection guidance, compact viewport activity ring, detailed processing/result panel, reduced-motion behavior, and distinct ready/stale/cancelled/error states remain the canonical workflow presentation. The superseded permanent Output card is not restored.
- Settings preserves its persistence and validation behavior while giving keyboard focus to the accent selector, keeping the form vertically scrollable at its minimum size, and trimming long tool-version lines with full-value tooltips.
- The footer's About action opens a themed owner-modal dialog with the application assembly version, local-processing/no-telemetry information, accessible Close controls, and Escape support. Opening it does not replace the current processing status. Main and Settings native title bars request dark Windows caption styling where supported.
- Main-window minimum size remains 1024×720 DIPs. Initial Main and owner-modal Settings bounds are centered within the selected monitor's DPI-aware work area, retaining their preferred sizes where they fit and keeping the supported minimum where the work area permits. At constrained height, the left preview/result pane scrolls vertically independently of the right settings pane; the media action row, result card, and footer remain reachable. A failed replacement selection retains the previous accepted result and presents Show Details beside the current Input error when technical detail exists, without duplicating the clean-start error action. Existing Fit/Fill/100% media scaling, including one media pixel per physical display pixel at 100%, is unchanged. Automated WPF main-window and dialog layout checks cover minimum/default sizes, with synthetic 100%/125%/150% DPI work-area bounds. A user-performed single-monitor smoke retest on 2026-09-29 passed at actual 100%/125%/150% scaling: initial window placement at 150%, Settings from a minimum-size owner, image/video Ready actions and export controls at minimum size, and Show Details for a failed replacement after image export. Multi-monitor behavior was not tested in that dated retest; the user's October 5 cross-monitor follow-up is recorded under Phase 14.

### Phase 14 - Testing

Selected coverage gaps are implemented; Phase 14 as a whole remains open. See `docs/Phase_14_Coverage.md` for test names, commands, evidence, and limitations. The playable committed MP4/MOV/MKV/AVI fixtures now exercise production managed rendering, acceptance, decoded symbol-art verification, and two byte-identical Unicode publications; existing rotated-MOV and audio assertions remain. Above-1080p image-warning tests cover display-oriented boundaries, output selection, replacement/notifications, and unblocked render/export. A separate PowerShell package verifier publishes self-contained Windows x64 Release, checks runtime/native dependencies, tools, committed checksums/licenses/notices, outside-repository startup, and bounded shutdown. An explicit marked verification-data root isolates application data; default user-local behavior is unchanged. Isolation must positively observe the launched PID's canonical session and production exclusive lease under that unique root within a bounded timeout; only the specific sharing-violation result is accepted as lock evidence. Shutdown cleanup separately proves that observed session was removed and no session directories remain, with both evidence objects retained in the ignored report. Dependency-free PowerShell regressions exercise the same helpers without publishing. Generated packages/evidence are ignored. The separate October 1 D–G retest records successful published playback, repeated export, Remove Audio, chooser cancellation and overwrite decline, with human sound confirmation on October 2. That functional pass does not establish layout stability. Clean-machine execution without installed .NET remains Phase 15, not proven by a successful development-machine launch.

The package verifier's application data now resides under unique marked `%TEMP%\GFpkg\<run-id>` roots, not inside the deeply nested package/evidence directory. Before launch it checks completed/pending production artifact names (worst-case PID) and escaped absolute file URIs against conservative harness budgets of 220/240 characters. These budgets are verification safeguards, not universal Windows limits or product restrictions. The supplied September 30 probe demonstrated WPF playback failure at a 266-character path while the same MP4 opened at 125 characters; Remove Audio failed at candidate preflight and retained the prior Keep Audio artifact, rather than demonstrating a mux defect. Reports retain prospective/observed path lengths, PID-bound lease/cleanup evidence, and an exact short-root retest command; marked temporary data is retained for inspection/retesting, and verifier-owned working-directory cleanup follows successful session cleanup. Declined overwrite restores settled footer/workflow and prior publication diagnostics only for the current attempt. Review guidance and Show Details remain bound to the independent SymbolizedPlayback context through publication progress/outcomes, clearing on successful recovery or media replacement. October 2 geometry regressions use real ReviewViewport/VideoReviewSurface components in the same main-window layout across state transitions, with a 1-DIP rounding tolerance. The fresh published default-size retest covers video/image loaded, Ready and Exported plus fullscreen return. October 5 native follow-ups additionally passed minimum-size image/video state transitions and actual SymbolizedPlayback-error interaction with publication success, cancellation, declined overwrite, failure and retry. A current Release-build live 125% -> 150% -> 125% run passed review/fullscreen/render/export; its report explicitly records incomplete disk screenshot evidence. Windows scale was restored and exact-session cleanup verified. The user separately confirmed successful cross-monitor movement/fullscreen return and no recurrence of the historical fullscreen-completion timeline observation; this is user-performed validation, not an independent multi-monitor capture or proof of a separate timeline fix. The generic publication-failure message is destination-neutral, with regression coverage; the October 5 audited block passed 1,007 tests. This is a dated outcome, not the latest suite count; later verification is recorded in the UI refinement history. These remaining checks close the audited block's commit-readiness gaps, with no known reproducible blocking defect. Historical failed reports remain unchanged; broader Phase 14 coverage remains open and clean Windows without .NET remains Phase 15.

- Full media format matrix.
- Portrait and landscape tests.
- Audio tests.
- Cancellation tests.
- Error handling tests.
- Packaging smoke test.

### Phase 15 - Packaging

- **Phase 15A implemented:** reproducible self-contained Windows x64 folder publish, bundled runtime/native dependencies and FFmpeg, versioned portable ZIP/checksum, payload/archive verification and machine-readable provenance. Existing attribution preserved; public-distribution legal review remains separate.
- **Phase 15B partial/open:** the October 7 committed-source native run found fullscreen/tab-position defects; subsequent corrective implementation and dated retests remain in `docs/Phase_15_Packaging.md`. Fresh A2 on October 24 verified portable `8399abc6` and isolated extracted-EXE startup from clean `e5e743f`; October 22 `1ddbab1` remains historical. Later A4 user, integration and controlled native fault evidence has its own scope. These passes do not close the remaining A3–A7 current-candidate matrix.
- **Phase 15C planned:** clean Windows execution without separately installed .NET/FFmpeg.

### Phase 16 - Portfolio Release

- Screenshots.
- Sample input/output.
- Demo video.
- Architecture diagram.
- README.
- Known limitations.
- Public documentation must not rely on ignored local evidence being present in a clone; preserve historical outcomes while making unavailable evidence locators explicit.
- Decide the application license and review bundled FFmpeg corresponding-source/distribution obligations before public distribution; existing notices alone are not release approval.
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
- Product header shows the scalable GF/frame mark, `Video & Image to Symbol Converter` and its description; the single preset selector is in Characters.
- Left column contains the preview panel and the persistent processing/result panel.
- Preview tabs are ordinary Buttons whose selected appearance derives only from the ViewModel; repeating an active-tab click does not deselect, pause, seek, reopen, or render. The metadata row retains the media icon and finite-width ellipsized filename/full-name tooltip without repeating Image/Video (the Input card still identifies media type).
- Successful accepted-media replacement commits the new workflow before status notifications, removes the previous result composition before exposing planned output, and refreshes all video-artifact properties before asynchronous frame loading. Cancelled/failed replacement retains the accepted result; obsolete player/recovery completions cannot republish it after a successful replacement.
- The processing/result panel follows the measured content-driven compositions in section 10.8: spacious row, standalone Export shrinking before reflow, distinct exported paired/rail/compact actions, bounded metric groups and centered working indicator. It does not reserve a fixed state-independent height or use a prior-mode hysteresis band. Supported-size containment and native acceptance remain separate checks.
- Before rendering, the shell says `Ready to render` / `Review your settings, then render.` with a static outlined accent-circle glyph (no spinner/check/percentage), planned TARGET resolution and video-only AUDIO (`Keep audio` / `Remove audio`). TARGET uses display-oriented source dimensions and the existing output-resolution/yuv420 rules; unavailable dimensions say Unavailable. These values follow current settings, not an accepted artifact. No additional Render or enabled Export is introduced. Accepted result metrics continue to describe retained artifact/image data independently of planned settings.
- Meaningful operation/result content changes use a 180 ms opacity transition, not layout or player-size animation. Progress updates do not restart it; reduced motion applies immediately and hidden/unloaded content releases its animation clock. This presentation follow-up is not certification of native comparison timing/races or full Phase 15B closure.
- Right column contains compact Input and Characters plus a remaining-height Render card with persistent Video audio; it has no permanent Output card after Phase 11B.
- Preview panel hides tabs/metadata before accepted media, then uses equal-width selection-driven Original and Symbolized tabs and an aspect-preserving media stage.
- Loaded-image mode shows `Render Image` and no redundant image-preview action.
- Loaded-video mode asynchronously attempts Original playback after input and metadata acceptance. A ready source shows the shared playback-control design with Original-owned transport and fullscreen; opening preserves the decoded frame, and fallback shows the labelled frame-position slider plus a non-blocking limitation message with no nonfunctional playback controls. Secondary `Preview Frame` and primary `Render Video` remain available according to their workflow rules, and Preview Frame is hidden after an accepted full-video artifact takes presentation priority.
- Rendering mode shows a compact ring-only busy overlay over the media viewport and the detailed circular percentage indicator, progress bar, frame progress, elapsed time, remaining time, and Cancel action exclusively in the lower processing/result panel. Completion never leaves the preview covered.
- Render Ready mode persists result and export actions below the preview.
- Video Ready and Video Exported use accepted-artifact FILE SIZE, OUTPUT, and AUDIO metric cards; AUDIO reflects the artifact's stored mux outcome even after the audio selector makes it stale, while successful subtitles stay concise and state-specific stale, cancellation, failure, suspension, opening, and review-unavailable explanations remain truthful.
- Footer shows the assembly-derived version, concise status, About and Settings, without a permanent privacy label. About provides privacy information and explicit access to the installed notices as described in section 10.10.
- Settings uses the persistent Reset/Cancel/Save bar and draft/feedback rules in section 12.4.
- Image/video controls appear conditionally.
- At 100% Windows scaling, all labels and controls fit at the approved desktop viewport without overlap or clipping.
- Narrow layouts reflow without losing controls or introducing horizontal page scrolling.
- UI remains responsive during frame preview, full rendering, playback, and export.

### 22.3 Image Input and Render

- User can select PNG, JPG/JPEG, WebP, and BMP files.
- User can drag and drop supported image files.
- App shows original image preview.
- App shows image metadata.
- App hides video-only controls for images.
- App exposes Render Image without a separate image-preview command.
- Render Image creates one reusable symbolized result and selects the Symbolized tab.
- Render Image / Render Again stays in the preview-panel media action row throughout the image workflow and is not duplicated in the result panel.
- App preserves source aspect ratio.
- Light on Dark is the default and maps bright cells to denser symbols on a dark background.
- Dark on Light maps dark cells to denser symbols on a light background.
- Transparent pixels composite exactly once over the selected symbol background.

### 22.4 Image Export

- User can export symbolized PNG.
- User can export symbolized JPG/JPEG.
- PNG is the default image export format. The persisted Settings preference initializes the native Save dialog; choosing a per-export format or cancelling never changes that preference. The dialog offers only PNG (*.png) and JPEG (*.jpg;*.jpeg); its accepted filter controls the encoder and extension normalization before source/conflict checks and the existing single overwrite confirmation.
- Selected output resolution is respected.
- Selected symbol polarity is respected and matches the symbolized preview.
- PNG and JPEG outputs use the same polarity-dependent opaque background.
- Export uses the current non-stale rendered pixels and never reruns symbolization solely because destination or PNG/JPEG format changed.
- Export Image is unavailable before a current render exists.
- Export controls are hidden when no rendered image exists; a stale retained image keeps the existing result visible while Export remains disabled.
- Render and export failures and cancellations have distinct result-panel presentation. Export failure or cancellation preserves the retained pixels for another export attempt.
- Output overwrite requires confirmation.
- Unsupported or invalid images show friendly errors.

### 22.5 Video Input and Preview

- User can select MP4, MOV, MKV, and AVI files.
- User can drag and drop supported video files.
- App reads and displays video metadata.
- App shows original preview frame.
- User can select frame position with a slider.
- App generates symbolized preview frame.
- Preview Frame remains independent from full Render Video before artifact acceptance. It becomes non-executable once the accepted rendered-video player has presentation priority; returning to frame review later requires an explicit review-mode design.
- App shows video-only controls for videos.
- App automatically requests an original frame near the middle after metadata succeeds.
- Slider seeking remains responsive, reconciles to the nearest resolved frame, and cannot publish an obsolete result.
- Frame pixels retain correct red/blue channel order and remain valid after the FFmpeg process and pipes are disposed.
- Cyrillic, spaces, and mixed-language input paths decode directly without renaming or temporary copies.
- Source videos are not left locked after preview reads, failures, cancellation, or repeated seeking.
- The symbolized frame uses the exact currently displayed original frame and the shared image pipeline.
- Frame/media/render-setting changes mark the previous symbolized frame stale; output-format-only changes do not.
- Moving the frame slider invalidates only the frame preview and never a completed full-video artifact.
- Missing native dependencies, unsupported codecs, and frame failures remain non-fatal and user-friendly.

### 22.6 Video Export

- User can export MP4.
- User must render and review a complete MP4 artifact before Export Video becomes available.
- MP4 uses H.264.
- MP4 uses yuv420p pixel format.
- MP4 uses CRF 20.
- MP4 uses medium preset.
- Export uses normalized source FPS.
- Rendered frames are streamed to FFmpeg through stdin.
- The app does not create thousands of temporary PNG files.
- Export Video publishes the exact reviewed artifact without rerunning symbolization or H.264 encoding.
- Repeated export from one current artifact is supported and uses a destination-side partial before atomic publication.

### 22.7 Audio

- Keep Audio preserves audio when source audio exists.
- AAC source audio is stream-copied and the symbolized H.264 video is never re-encoded.
- Incompatible or unknown source audio falls back to AAC at 192 kb/s, 48 kHz stereo.
- Remove Audio produces an MP4 without audio.
- Source videos without audio export successfully.
- Keep Audio fails safely when bundled ffprobe cannot reliably determine audio presence.
- Shorter audio does not truncate video; longer audio is capped at the completed symbolized-video duration.
- Both video-only and mux partials are cleaned and an existing destination survives failure or cancellation.
- The selected audio mode is captured for Render Video and is reflected in both in-app review and exported output.

### 22.8 Progress and Cancel

- Render stage, accepted current frame, elapsed time, and determinate or indeterminate progress are visible.
- A trustworthy total frame count is shown when available; unavailable or invalid totals are never fabricated.
- Percentage is finite and monotonic, remains below 100% until review-artifact publication, and reaches 100% only for a complete reviewable result.
- Estimated remaining time is derived from accepted-frame throughput only when calculable and is hidden during indeterminate, audio, and finalization stages.
- Audio inspection, audio merging, encoder finalization, output finalization, cancellation cleanup, and completion have explicit presentation.
- Cancel responds immediately, cannot be repeated, and keeps conflicting actions disabled until cleanup finishes.
- Cancellation reaches every active decoding, rendering, encoding, probing, muxing, and pre-publication boundary.
- Process trees, streams, frames, handles, and temporary files are cleaned after success, failure, or cancel; incomplete output is not published and an existing destination is preserved.

### 22.9 Render, Review, and Export

- Original and Symbolized remain the canonical review tabs in their current location above the preview surface.
- A completed image automatically opens in Symbolized and remains available for comparison with Original.
- A completed video automatically opens in Symbolized and is playable through the in-app result player.
- The processing panel remains after rendering and exposes result summary plus Export, Open Folder and Copy path actions according to workflow availability. Image format selection belongs only to Save; Render / Render Again remains solely in the preview-panel media action row.
- Render-affecting changes produce an explicit stale state, keep the old result reviewable, and disable export until rerender succeeds.
- Image format and destination changes do not invalidate image rendering; video slider movement does not invalidate full-video rendering.
- Only one current full-video artifact is retained for the active media session, and superseded or orphaned artifacts are cleaned safely.
- Player handles are released before artifact deletion or replacement.
- The selected destination is requested at export time, not before rendering.
- Existing destinations are confirmed and preserved on publication failure or cancellation.

### 22.10 Settings and Persistence

- Accent color setting is persisted.
- Default character preset is persisted.
- Default font and font size are persisted.
- Default output resolution is persisted.
- Default symbol polarity is persisted, safely defaults when missing, and is loaded into the draft by Reset to defaults, then applied through Save & Close.
- Default image format is persisted.
- Default audio mode is persisted.
- Reset loads existing defaults into the draft only; it does not save or publish application-wide preferences. Accent preview rolls back and the draft is discarded on Cancel/Escape/close. Save & Close uses the normal persistence path, and failed Save retains the draft and truthful guidance for retry.
- Application-data and logs paths are read-only, selectable, keyboard-copyable fields with full accessible values.

### 22.11 Privacy

- No media files are uploaded.
- No telemetry is sent.
- No analytics are sent.
- Logs are local.
- Logs do not contain image pixels, video frames, or source media content.
- Completed review artifacts stay in the application-managed local cache only for the active workflow and are never uploaded.

### 22.12 Documentation

- `GlyphFrame Technical Specification v1.0` exists.
- `docs/design_reference/GlyphFrame_Design_Reference.png` exists.
- Acceptance criteria are included.
- Project name is updated to `GlyphFrame` in all project-created documentation.


### Image navigation evidence

Still-image viewing behavior is defined in section 10.6. The [UI history](UI_Refinement_History.md#image-navigation-october-22-23) distinguishes the former toolbar, gesture-only Fit floor and routed-wheel correction; [Phase 14](Phase_14_Coverage.md#image-navigation-current-coverage) tracks remaining native acceptance, and [Phase 15](Phase_15_Packaging.md#image-navigation-package-provenance) records historical package provenance. The [October 24 clean `e5e743f` A1/A2](Phase_15_Packaging.md#recorded-october-24-clean-source-candidate) includes the implemented navigation. The [latest candidate](Phase_15_Packaging.md#latest-recorded-candidate) is the later native-icon provisional, not another clean-source or blanket native-acceptance pass.
