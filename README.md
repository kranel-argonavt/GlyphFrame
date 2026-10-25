# GlyphFrame

GlyphFrame is a Windows desktop application for converting local videos and images into colored symbol art.

## Status

The Render → Review → Export workflow is implemented for images and full videos. Image export encodes the retained result as PNG or JPEG; video export copies the accepted rendered MP4 without rendering or encoding it again. Original and Symbolized video review use independent players with shared playback controls. Recoverable errors have concise guidance, explicit technical details, and bounded privacy-safe local logging.

The current UI includes measured responsive result layouts, compact Input/Characters cards, a finite-height Render settings area, draft-only Settings editing, and an About dialog with local third-party notices. These features are implemented; implementation and automated coverage do not imply complete native or release acceptance.

Broader Phase 14 coverage remains open. Phase 15A portable packaging is implemented, Phase 15B extracted-package acceptance remains partial/open, and Phase 15C clean-Windows validation without separately installed .NET/FFmpeg remains untested. Current verification, dated native outcomes and limitations are recorded in [Phase 14 coverage](docs/Phase_14_Coverage.md) and [Phase 15 packaging](docs/Phase_15_Packaging.md). The [release acceptance checklist](docs/Phase_14_Coverage.md#release-acceptance-checklist) consolidates outstanding verification and portfolio-release work. Historical passes or failures apply to the builds and conditions recorded there.

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

## Interface

The left pane contains Original/Symbolized review and one persistent processing/result panel. The status panel's height follows visible content; the preview receives the remaining pane height, with deliberate vertical scrolling when the combined minimum content no longer fits.

Result layouts are chosen from actual inner width and visible controls: a spacious single row, a two-row layout, an exported-action column, or a centered compact stack. Standalone Export can shrink before moving below the metrics; an exported action group instead changes composition while keeping its buttons aligned. There is no hysteresis band in the current layout selector. Detailed geometry and state rules are in the [technical specification](docs/GlyphFrame_Technical_Specification_v1.0.md#108-processing-and-result-panel).

On the right, compact Input and Characters cards sit above Render. Input shows readiness followed by wrapping metadata. Render pins its heading and Video audio section around the parameter viewport; a subdued Coming soon message appears only in genuine spare space and never creates scrolling or reserves hidden space. Unavailable Audio is passive information; temporary operation locks preserve the selected audio mode.

The main footer contains the assembly-derived version, concise operation status, About and Settings. About uses the same live-accent vector logo as the header and contains the product description, privacy explanation and an explicit Third-party notices action. The latter opens the complete installed document as selectable read-only local text, without interpreting Markdown or following links. Privacy information is not a permanent footer label.

## Local settings

Preferences are stored for the current Windows user under `%LocalAppData%\GlyphFrame`:

```text
%LocalAppData%\GlyphFrame\
├── settings.json
└── logs\
```

Phase 12C writes newline-delimited UTF-8 JSON records containing only `TimestampUtc`, `Code`, and `Context` at accepted failure-attempt boundaries, so a new failed retry is recorded even when its context is unchanged. Startup and Settings share one FFmpeg validation observer: repeated reads of the same cached failed result do not create another event, while a new failed Refresh does. No paths, diagnostic text, FFmpeg output, settings, or media content enter the log. Writing happens off the UI thread and is best-effort: storage failure leaves the original workflow and friendly message unchanged. Files rotate before exceeding 1 MiB; each complete record is at most 1 KiB including its newline. At most 10 completed files and 10 MiB of completed logs are retained, and completed files older than 14 UTC days are removed. Each instance uses a distinct active file; live writers are protected, while abandoned active files are removed when they can be opened exclusively. Active files may temporarily exceed the completed-log limits. Settings → Open Logs Folder remains available. The persisted defaults are Detailed ASCII, Cascadia Mono at size 10, same-as-source resolution, Light on Dark symbol polarity, PNG image export, and Keep Audio for video. Available accents are Violet, Purple, Blue, Cyan, Emerald, and Rose, with Violet as the default.

Settings has a persistent action bar: Reset to defaults on the left, Cancel and Save & Close on the right. Reset changes only the editable draft; Save persists/applies it, while Cancel, Escape or closing discards it and restores the saved accent. Meaningful feedback, such as pending defaults or a failed Save, appears above the actions only when present. Application-data/logs paths are selectable, read-only fields.

Default image export format initializes the PNG/JPEG choice in the native Save dialog; it can be changed for an individual export without changing that preference or rerendering. Settings are written through a temporary file before replacing `settings.json`. Missing and partial settings use default settings, invalid values are normalized, and malformed JSON is preserved as `settings.invalid.json` (or the next numbered name) before recovery.

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

`Preview Frame` passes one explicitly selected owned source frame to the same grid, luminance, character-preset, polarity, and SkiaSharp renderer used for images. When continuous Original playback is ready, the action first pauses it and maps its current timestamp through authoritative source duration before FFmpeg extracts that frame; in fallback mode it retains the normalized frame-position slider. Moving the frame position, selecting other media, or changing a render setting makes the old symbolized frame stale; output-format-only changes do not. Continuous Original playback never triggers symbolization.

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

Phase 11B.1 completes the image half of this workflow. Image mode has no separate symbolized-preview command: one stable preview-panel action starts as `Render Image` and becomes `Render Again` after a result exists. Rendering creates one reusable full image result, selects the existing Symbolized tab, and enables `Export Image` in the persistent result panel. A stale result remains reviewable until that single `Render Again` action safely replaces it, and successful export retains it for further PNG/JPEG exports. Neither media mode now has a permanent Output card.

Phase 11B.2 added the managed backend for full rendering. It composes the proven sequential FFmpeg decoder, shared symbolization and SkiaSharp renderer, H.264 encoder, and Phase 10 audio path to create a completed MP4 under `%LocalAppData%\GlyphFrame\Cache\VideoArtifacts\<session>`. Candidate files are promoted only after successful validation. Each running application session holds an exclusive filesystem lease, so startup recovery preserves every active GlyphFrame instance and removes only stale session directories. One accepted artifact may be retained; a replacement is accepted before cleanup of the previous file, and failed cleanup is tracked and retried at later lifecycle boundaries. All candidate and cleanup operations remain restricted to canonical files in the owning session.

Phase 11B.3 connects that backend to the editor. `Render Video` captures immutable media, symbolization, output, and audio settings. Candidate opening remains cancellable: the previous player handle closes, but its artifact remains owned until the replacement opens successfully and its operation/identity is revalidated. Failed or cancelled preflight rejects the candidate and restores the previous review. Acceptance then enters an explicitly labelled, non-cancellable commit with Cancel disabled. After acceptance, the intended artifact identity is recorded and revalidated before automatic Symbolized selection, Video Ready, or 100%. Disposal and invalidation suppress late success; a setting change during commit records the accepted artifact as stale. An unplayable first result remains owned with Review Unavailable instead of success.

The exact completed H.264/AAC-or-silent MP4 is opened by WPF without copying, transcoding, or on-the-fly symbolization. Embedded and fullscreen surfaces share the same transport and icon control row: Play/Pause, current time, themed seek slider, duration, Mute/Unmute, volume, scale, and fullscreen entry/exit. Controls stay visible (auto-hide is deferred). Natural completion pauses on the final frame and displays a centered circular replay button with a reduced-motion-aware 160 ms fade; the bottom Play button also restarts. Ordinary pause and paused seeking do not show replay, and seeking back after completion leaves the player paused. Keyboard commands and accessible action names remain available; clock updates cannot move a thumb during a drag. Scrubbing is enabled on every opening for paused-frame seeking. Fit / Fill / 100% remains available for original video frame previews, Original continuous playback, and rendered-video review. Still-image Original/Symbolized use cursor-anchored Ctrl+wheel zoom and bounded left-drag pan, without a scaling toolbar. Zooming out stops at centered Fit; it cannot shrink the image below its complete-image viewport fit. Image viewing state is transient and independent of rendering/export; successful replacement starts in Fit, while failed/cancelled replacement and same-source review retain meaningful framing. Fit preserves the whole frame; Fill preserves aspect ratio and clips excess content. **100% maps one media pixel to one physical display pixel** using pixel dimensions divided by the viewport's current DPI scale. Scrollbars expose oversized content; no outer Viewbox rescales it or the controls. A per-monitor DPI manifest and viewport DPI-change handling preserve that rule when moving between monitors. Rendered video uses the accepted artifact's output dimensions, while Original playback uses trusted display-oriented source dimensions plus a view-only quarter-turn. Scaling changes never alter render identity, output pixels, or export. Fullscreen is owned by the main window; Escape closes only the active fullscreen window. View unloading stops both players' timers and closes their playback, while reloading reopens retained sources paused at saved positions. Replacement and shutdown release player handles before artifact cleanup. Runtime rendered-review failures refresh the whole result panel and retain the file; Original failures return to the FFmpeg frame fallback without invalidating processing. Automated tests cover replay/seek state, independent fullscreen controls and ownership, themed minimum-width layout, scroll/clipping and rotation behavior, DPI calculations, manifest embedding, and existing recovery regressions. A user-performed interactive pass on 2026-09-18 additionally confirmed displayed paused frames and tab alignment, final-frame/replay behavior, audible playback with mute/volume, prevention of overlapping Original and Symbolized audio, interactive fullscreen, Fit/Fill/100%, multi-monitor DPI behavior, runtime fallback, and main-window/fullscreen cleanup.

A full artifact takes priority over frame preview, remains playable when stale, and is not invalidated by movement of the frame-preview slider. Publication has an independent export outcome: success shows Video Exported, the selected destination, Export Again, and Open Folder; failure or cancellation retains the review and allows retry. A stale artifact cannot be exported until Render Again succeeds.

Phase 11B.4 publishes through `IVideoArtifactPublisher`, using a bounded 128 KiB asynchronous copy into a unique destination-side partial file, followed by atomic move/replace. It does not move the cache artifact or call FFmpeg. Playback, volume, mute, scaling, and fullscreen cannot change the copied bytes. The managed session lends the exact current artifact under its lifecycle gate until publication finishes, so replacement and shutdown cannot delete a borrowed file. Save-dialog cancellation preserves the previous outcome. Settings changes or view unload cancel an active copy; shared deferred recovery restores review afterward.

The first Phase 11B.5B block extracts neutral WPF media transport, position-timer, generation-event, timeout, and controller primitives while retaining `IRenderedVideoPlayer` as the existing Symbolized-review API. A separately constructed `IOriginalVideoPlayer` owns its own WPF `MediaPlayer`, timer, open generation, state, source lifetime, and fullscreen host. The second block connects that facade only after video input and metadata are accepted. Original readiness still requires the current generation's `MediaOpened` event plus positive natural video dimensions. Ready sources use the same reusable player-control surface as Symbolized; timeout, cancellation, synchronous/native failure, missing dimensions, or later playback failure instead retain the FFmpeg frame surface and its frame-position controls without disabling Preview Frame, Render Video, or artifact export.

Original playback applies trusted 0°/90°/180°/270° metadata only to the WPF visual, using display-oriented dimensions for Fit, Fill, natural-size scrolling, and fullscreen. FFmpeg frames remain unrotated by this view because the decoder already returns display-oriented pixels. A tab change pauses the outgoing transport before reading its absolute timestamp, clamps that timestamp against the target's authoritative duration, seeks the incoming surface, and leaves both players paused. Source metadata duration takes precedence for Original; accepted-artifact duration takes precedence for Symbolized. With a trustworthy target FPS, an end-bound transfer backs away by one target frame; unavailable or non-positive duration transfers to zero. Starting either transport still pauses the other first, so both audio streams cannot remain active. Volume and mute remain independent, and switching never resumes playback automatically. In Original fallback, the same timestamp is mapped to the normalized FFmpeg selector and loaded through the generation-checked frame reader without symbolization or a hidden WPF transport. A new accepted media selection supersedes the old Original generation, images close it, failed replacement selection preserves it, and unload/disposal closes its timer, fullscreen, and native source deterministically.

Existing destinations require confirmation tied to a file-identity/content snapshot, rechecked before commit. Newly appearing or changed destinations require selection again. Source/cache destinations, reparse-point paths, and hard-linked destination files are rejected; Windows file identity and resolved directory paths also protect against supported path aliases. Atomic replacement failure has no destructive fallback. The final cancellation check is immediately before publication: a successful atomic commit is always reported as success, even if cancellation arrives afterward. Progress describes byte copying and publication, not rendering or frame throughput. Failed/cancelled copies remove only their owned partial; an undeletable partial is reported with its ownership path in technical diagnostics rather than hidden as success.

Phase 11B.4 live smoke checks exercised rendering a local audio fixture, Symbolized review, two Unicode-path exports, opening/playing the exported MP4 in Windows Media Player, Open Folder, declined overwrite, and stale-result blocking. Both exports were byte-identical to the accepted artifact; closing GlyphFrame removed its owned session. A user-performed follow-up on 2026-09-20 additionally confirmed cancellation during active reviewed-artifact publication, confirmed replacement of an existing destination, retry/review preservation, and the exact minimum-window publication layout. Deterministic automated tests cover publication cancellation, replacement, conflicts, cleanup, and lifecycle races. The later Phase 11B.5 interactive pass separately covered playback audibility and multi-monitor behavior.

Review recovery is deferred when a load occurs during rendering, acceptance, frame preview, or artifact publication. All operation-completion paths await the same serialized, generation-checked recovery after releasing their operation resources. It resumes the exact accepted MP4 at the retained position if the view is loaded, or on its next valid load, without changing the separate export outcome. Unloaded review is presented as suspended, never as a playable Video Ready result. View lifetime changes do not stale the artifact; render/audio changes still do. Playback-error diagnostics survive failed or cancelled render retries until a valid replacement or retained-artifact recovery actually opens successfully. Latest render failures remain separate from retained playback failures, with mutually exclusive result indicators.

The panel below the preview is the canonical processing/result surface for both media modes. Image results expose `Export Image` / `Export Again` and `Open Folder`, with PNG/JPEG chosen only in the native Save dialog; video results expose `Export Video` / `Export Again` and `Open Folder` without a format selector. It does not duplicate the preview-panel render action. Failed or cancelled export retains the rendered pixels or managed MP4 for retry, and repeated export never reruns symbolization.

The preview busy overlay is deliberately compact: while an operation is active it dims only the media viewport and shows one segmented activity ring without a number. Operation title, percentage, stage, linear progress, frame count, elapsed/remaining time, and Cancel appear only in the processing/result panel. The scrim disappears as soon as work and cleanup finish, so the lower panel's completion animation never covers the finished preview. The footer changes immediately to one concise operation-specific message and does not duplicate detailed progress; image success says `Image ready for review and export.` while retained symbol-grid/output metrics and any font-fallback warning remain in the result panel. Generation checks prevent obsolete callbacks from restoring an older message.

Accepted video results expose artifact-based `FILE SIZE`, `OUTPUT`, and `AUDIO` metrics. AUDIO describes the retained artifact's actual mux outcome (`Original`, `AAC`, `Removed`, or `No source`), not the currently selected audio setting. Ready and Exported summaries stay concise, while stale, failure, cancellation and review-unavailable guidance remains state-specific. Copy path explicitly copies the last successful destination, and Open Folder remains separate.

Image and video results use the measured compositions described under [Interface](#interface); height follows populated content, with no fixed state-independent shell or prior-mode hysteresis band. Metrics remain available for accepted retained artifacts, including stale or temporarily unavailable review. `Preview Frame` is available only before the first full-video artifact is accepted; afterward it is hidden because the rendered video has review priority. An explicit frame-preview-versus-rendered-video switch remains outside the current workflow.

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

GlyphFrame resolves these tools relative to the running application directory, never the current working directory. It validates each executable with `-version` asynchronously at startup and caches the result for the session. Settings → Application shows availability, separate FFmpeg and ffprobe versions, the resolved folder, a Refresh action, and `Show Details` only when a failed validation retains diagnostics. Missing or invalid tools do not prevent the main window from opening. Users do not need to install FFmpeg or modify `PATH`.

## Error presentation

Successful image/video destinations are available through a keyboard-accessible **Copy path** button instead of an inline path or filename summary. Its tooltip contains the full destination; explicit activation copies Unicode text through the existing clipboard service. The fixed-width button briefly says **Copied!**, or **Copy failed** with retry guidance if the clipboard is unavailable, without changing export outcome or layout. Failure/cancellation keeps the previous successful destination associated with the retained result; a new accepted result or media replacement removes it. Transient feedback is cancelled on invalidation/replacement/disposal and rejects obsolete callbacks. Open Folder remains separate. Successful footers say `Image exported successfully.` or `Video exported successfully.`; no path is copied automatically or logged.

Phase 12A gives recoverable media-input, frame-preview, image render/export, video render/publication, managed-artifact, playback, and bundled-tool failures one application-level presentation policy. Every presentation keeps a stable error code and affected operation, uses catalog-owned user guidance in the existing UI locations, and retains lower-level exception or process diagnostics separately. Raw paths, stack traces, HRESULT values, and FFmpeg output are never used as the primary message.

Managed full-video failures retain their originating decoder, renderer, encoder, audio-probe, audio-mux, or finalization category through the review-artifact layer. Reviewed-video publication likewise preserves destination access denial, reliably identified disk-full conditions, destination changes, and generic atomic-publication failures before the application converts them to friendly guidance.

Error state remains scoped to the workflow it affects: an export failure does not replace a retained playback failure, a render retry does not erase publication history, and obsolete operation callbacks cannot overwrite a newer context. Ordinary user cancellation, supersession, unload, and shutdown remain expected workflow outcomes rather than errors.

Phase 12B adds one reusable modal `Error Details` dialog to the existing failure locations. `Show Details` appears only for the currently visible failure when its immutable presentation retains non-empty technical detail; retry, replacement, or context reset removes the corresponding action without disturbing unrelated errors. The dialog shows the stable code, operation, friendly message, and scrollable technical detail. Copying produces a deterministic plain-text report only after the user chooses `Copy Details`; clipboard contention is reported inside the open dialog and never as false success. Full technical details are never copied automatically, uploaded, or written to disk. Phase 12C logs only stable code and operation context; opening Show Details does not create a log entry.

## Documentation

- [GlyphFrame Technical Specification v1.0](docs/GlyphFrame_Technical_Specification_v1.0.md)
- [GlyphFrame Design Reference](docs/design_reference/GlyphFrame_Design_Reference.png)
- [Original Video Playback Compatibility Report](docs/Original_Video_Playback_Compatibility_Report.md)
- [Phase 14 coverage and provisional package verification](docs/Phase_14_Coverage.md)
- [Phase 15 portable packaging and validation boundaries](docs/Phase_15_Packaging.md)

- [Consolidated UI refinement history, verification and open acceptance](docs/UI_Refinement_History.md)

Detailed local iteration logs are archived outside Git. The history distinguishes current behavior from superseded layouts, fixture evidence, native observations and remaining release gates.

Selected Phase 14 gaps now have real MP4/MOV/MKV/AVI managed-render and repeated-publication integration coverage, display-oriented above-1080p image-warning tests, and an independently runnable package verifier: `pwsh -NoProfile -File scripts/Verify-PublishPackage.ps1`. The verifier creates an ignored self-contained Windows x64 Release package, validates runtime/native/tool/license assets and committed FFmpeg checksums, and checks outside-repository startup and owned-process shutdown with isolated application data. Isolation requires observing the launched application's actively held production session lease under the unique data root; cleanup separately verifies removal of that exact session and zero remaining session directories. `pwsh -NoProfile -File scripts/Test-PackageVerificationSession.ps1` tests these actual helpers without publishing. The separate October 1 published-package D–G report records successful playback/audio/publication checks, with human audibility confirmation on October 2. Clean-machine-without-.NET validation remains pending. This block does not close all of Phase 14 or Phase 15.

The verification harness now keeps marked application data under short `%TEMP%\GFpkg\<run-id>` roots, separate from ignored package/evidence output. It rejects prospective completed/pending artifact paths above 220 characters or absolute file URIs above 240 before launch; these are harness headroom budgets, not universal Windows or product limits. The September 30 package smoke exposed WPF failure on a deeply nested artifact path, not an established audio-muxing defect. Declining overwrite now restores settled workflow/footer presentation, and retained review guidance is bound to its own playback error instead of export status. The October 2 layout correction gives the preview a finite pane-derived allocation, with results scrolling below it and a permanent outer scrollbar gutter; retained internal media extents no longer request parent space. Same-instance real WPF component regressions cover minimum/default/larger sizes and Fit/Fill/100%. October 5 native follow-ups passed minimum-size image/video transitions, actual playback-error/publication interaction, and live 125% -> 150% -> 125% scaling with review/fullscreen/render/export. The user separately confirmed cross-monitor movement/fullscreen return and no recurrence of the historical fullscreen-completion timeline observation. This is attributed manual evidence, not an independently captured multi-monitor run or proof of a separate timeline fix. The generic publication-failure wording is now destination-neutral. The October 5 audit passed 1,007 tests and found no known reproducible blocking defect in that audited block; this is historical evidence, not the latest suite count or a current commit/release declaration. See the coverage document for later verification, provenance, screenshot-capture limitations and remaining scope. Historical failed smoke evidence remains unchanged; broader Phase 14 coverage and clean-machine Phase 15 verification are not declared complete.

## Portable packaging — Phase 15A

Phase 15A is implemented. On Windows with PowerShell 7, the .NET 10 SDK, Git and access to the configured NuGet/runtime-pack sources, run:

```powershell
pwsh -NoProfile -File scripts/Build-PortablePackage.ps1
```

The command resolves repository paths independently of the working directory and performs one fresh Release / win-x64 / self-contained, folder-based, untrimmed publish through the existing verifier. It emits `GlyphFrame-<version>-win-x64-portable.zip`, a `.zip.sha256` checksum, `payload-manifest.json` and `report.json` under a unique ignored `artifacts/portable-packages/<run-id>/`. Version comes from the published application assembly using the same Major.Minor.Build convention as About; no product version is changed. Reports identify the source commit **and dirty-tree state**, SDK/tool versions, options, completed checks and linked startup/session evidence. Every archived file is checked against the staging manifest, including hashes; paths, duplicates/conflicts and forbidden development/user-data content are rejected. PDBs are explicitly excluded. Steps are reproducible; byte-identical ZIPs across runs are not promised.

Extract the whole ZIP and launch `GlyphFrame.App.exe` inside its single application directory, retaining `Tools/ffmpeg/`. Portable deployment does **not** mean portable application data: normal settings, logs and cache stay under `%LocalAppData%\GlyphFrame`; the marked verification switch is test-only. The ZIP includes its own concise user README and existing third-party attribution. No installer, signing, tag, upload or release is created. Public-distribution licensing/corresponding-source review remains required; attribution checks alone do not establish legal compliance.

The verifier's isolated startup/shutdown check runs the freshly published folder, **not the extracted ZIP**. The separate October 7 Phase 15B native run tested the actual extracted ZIP and recorded transport defects and remaining checks; it is not complete. Phase 15C clean Windows without separately installed .NET/FFmpeg remains **NOT TESTED**. See the [packaging document](docs/Phase_15_Packaging.md) for commands, provenance, scenario results and limitations. Historical Phase 14 evidence and its broader open scope are unchanged.

The October 8 Phase 15B corrective block retains the commanded paused playback timestamp rather than rereading a delayed native clock during fullscreen binding or tab comparison. Seven new deterministic cases and the complete 1,014-test suite pass. A fresh **dirty-tree** extracted candidate passed three end→midpoint→fullscreen-reopen cycles per player and paused comparison round trips; Keep/Remove Audio exports, input-error recovery and exact-session cleanup were observed. Active publication cancellation, native SymbolizedPlayback-error/publication interaction, human audibility, sustained minimum-size image Rendering and final clean committed-package validation remain pending. This is not Phase 15B closure.

## Upcoming phases

- Broader Phase 14 testing remains separate. Complete the current-candidate Phase 15B acceptance matrix and retest a package built from the eventual clean committed revision. Earlier transport corrections and dated retests are recorded in the packaging document; they do not close all remaining checks. Phase 15C clean-machine execution and Phase 16 portfolio materials/release publication remain planned.

The technical specification records the detailed phase boundaries. Natural-size review is one media pixel per physical display pixel with scrolling; fullscreen controls stay visible and auto-hide remains deferred. The Original-player backend, runtime fallback, view-level orientation, independent fullscreen, single-audio coordination, paused timestamp comparison, no-proxy decisions, and representative interactive validation are complete. Broader clean-machine and unusual-codec coverage remains future compatibility testing rather than an unfulfilled Phase 11B.5 implementation requirement.

## Privacy

GlyphFrame is local-only software. It does not upload media and includes no telemetry or analytics. Media input is opened read-only and is never copied, renamed, modified, or deleted. Image pixels, preview frames, and sequential video-render frames are processed only in bounded memory; exports write only to the explicitly selected destination. Phase 11B permits one generated review MP4 in an application-managed local cache for the active workflow, with deterministic cleanup and no source-media copy. Settings remain on the current device under the user-local application-data directory; media paths, decoded frames, and media content are not stored in preferences.
