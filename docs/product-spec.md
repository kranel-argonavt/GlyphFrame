# Product specification

GlyphFrame converts local images and videos into colored symbol art on Windows x64. This document describes current behavior, not a development task list or a promise of future features.

## Workflow and formats

The workflow is **Select → Render → Review → Export**. Rendering and saving a destination are separate operations.

| Media | Input | Output |
| --- | --- | --- |
| Image | PNG, JPG/JPEG, WebP, BMP | PNG or JPEG |
| Video | MP4, MOV, MKV, AVI | MP4 with H.264 video; retained/copied or AAC audio, or silent |

Exactly one readable local file is accepted at a time. Metadata loading is asynchronous; a newer selection supersedes an older request. A failed or cancelled replacement preserves the previous valid selection. File extensions are an import allowlist, not a guarantee that Windows can play every codec in that container.

Source media is opened read-only and is not renamed, modified or deleted. Selected paths are transient and are not saved in preferences.

## Symbol rendering

Images and decoded video frames use the same conversion core:

1. Normalize image orientation or obtain a display-oriented video frame.
2. Resolve output dimensions while preserving aspect ratio.
3. Measure the selected font and derive the symbol grid automatically.
4. Sample cell colors, calculate Rec. 709 luminance and map it to the selected ramp/polarity.
5. Render colored glyphs with SkiaSharp onto an opaque background.

Transparent pixels are composited over that background before sampling. The user does not edit columns/rows independently; smaller font sizes produce denser art.

| Control | Choices |
| --- | --- |
| Character preset | Simple ASCII, Detailed ASCII, Blocks |
| Font family | Cascadia Mono, Consolas, Courier New, Lucida Console |
| Font size | 9, 10, 12 |
| Resolution | Same as source, 720p, 1080p |
| Symbol polarity | Light on Dark, Dark on Light |
| Video audio | Keep Audio, Remove Audio |

The ramps remain ordered from sparse to dense; polarity changes the luminance mapping and background, not the ramp order. Unavailable requested fonts use the implemented fallback path with a warning rather than a silent claim that the requested font was used. Original sampled colors are retained; monochrome/color-mode editing is not exposed.

Defaults are Detailed ASCII, Cascadia Mono size 10, same-as-source resolution, Light on Dark, PNG export and Keep Audio. The settings catalog is the authoritative list of options: [AppSettingsCatalog.cs](../src/GlyphFrame.Core/Settings/AppSettingsCatalog.cs).

## Image rendering and export

Render Image produces and retains one full-resolution symbolized result for review. Export Image uses those retained pixels; choosing a different filename or PNG/JPEG format does not rerender.

PNG and JPEG are offered together in the native Save dialog. The accepted format controls the encoder and normalized file extension; the saved default initializes that choice but does not constrain an individual export. There is no image-format dropdown in the result panel.

Changes affecting pixels make a retained result stale and disable its export until rerendering. View-only navigation and export-format/destination changes do not make it stale. A failed or cancelled rerender preserves the prior retained result; export availability still depends on its identity matching current settings.

Above-1080p display-oriented images with Same as source show a warning, not an automatic render/export prohibition.

## Video rendering and export

Original initially has a bundled-FFmpeg frame preview. Continuous Original playback is enabled only when the Windows player becomes usable; otherwise the frame preview remains available. Preview Frame symbolizes the currently displayed owned frame and is independent of full-video rendering.

Render Video streams decoded frames through the shared symbol renderer and H.264 encoder. It does not create a directory of frame images. A completed candidate becomes the accepted managed review MP4 only after current operation/source/settings checks. A failed, cancelled or obsolete candidate must not replace a previously accepted result.

Export Video copies the exact accepted, current MP4 to the selected destination. It does not decode, symbolize or encode it again. Publication writes an owned partial file and commits only after validation; failure/cancellation before commit preserves an existing destination. Cancellation arriving after a successful commit must not misreport the export as cancelled.

Keep Audio copies compatible source audio or uses AAC fallback; Remove Audio produces a silent result. A video with no source audio remains silent. Audio selection is part of the render identity, so changing it requires rerendering. Completed AUDIO metrics describe the actual artifact, not the current selector.

## Review and navigation

- Original and Symbolized are separate video players with independent state and source ownership.
- Video controls include Play/Pause, seek, elapsed media position/duration, mute, volume, Fit/Fill/100% and fullscreen. Natural completion pauses on the final frame and offers replay.
- Tab comparison pauses playback and transfers a clamped timestamp. The two players must not produce overlapping audio.
- Video 100% means one media pixel per physical display pixel; DPI and view-only orientation are accounted for. Fullscreen controls stay visible; auto-hide is deferred.
- Image navigation uses cursor-anchored **Ctrl + wheel** zoom and bounded left-drag panning, with no scaling toolbar. Zooming out stops at centered Fit. Ordinary wheel movement pans when possible and otherwise follows the enclosing scroll route.
- Same-source tab switching/rerendering retains meaningful image framing. Successful source replacement resets to Fit; failed/cancelled replacement does not.
- Preview navigation never changes output pixels, render settings or export identity.

Windows codec/long-path limitations and the choice of playback backend are documented in [ADR 0001](decisions/0001-native-video-playback.md).

## Interface and settings

The left pane contains review and a content-driven processing/result panel. Layout adapts to actual available inner width and populated controls: a spacious row, two-row composition, action rail or compact stack. Controls remain reachable through deliberate vertical overflow rather than horizontal clipping. Metric cards and actions have bounded sizes.

During processing, the header carries the operation title and video frame count when available. A progress bar is followed by stage guidance and estimated remaining time; narrow layouts stack text without dropping it. Elapsed processing time is not a visible footer field. Cancel and the progress indicator align with the complete processing body.

Input metadata begins with readiness; Render keeps its heading and Video audio section around a finite settings viewport. A future-settings placeholder appears only in genuine spare space and does not create scrolling.

Settings supports six accent presets and reduced-motion behavior. Its persistent action bar provides draft-only Reset, Cancel and Save & Close. Cancel, Escape or title-close discards the draft and restores saved appearance; Save applies/persists it. Save failure leaves the dialog/draft available for recovery. Application/log paths are selectable read-only fields.

Header and About share live-accent vector branding. About explains local processing/privacy and opens the installed third-party notices as selectable read-only text. Technical error details are explicit actions, not automatic clipboard operations or external uploads.

Windows Explorer/Start Menu/desktop shortcuts and Setup use a static white mark with transparency, derived from the same frame/GF geometry. This shell icon is embedded in the EXE; it does not follow the accent preset. The white mark has no added dark tile, so contrast depends on the shell background.

The [design reference](design_reference/GlyphFrame_Design_Reference.png) is a historical visual concept, not a current application screenshot or an exact geometry contract.

## Data, errors and cleanup

Normal application data lives under `%LocalAppData%\GlyphFrame`: settings, bounded privacy-safe logs and managed video cache. Portable deployment does not move these beside the EXE.

Settings use normalized defaults and temporary-file replacement; malformed settings are preserved before recovery. Logs contain only UTC timestamp, stable error code and operation context, not media paths/content or raw technical diagnostics.

Errors show concise recovery guidance, with optional code/operation/technical Details. Playback and publication errors remain separate contexts. Failed retries retain useful prior results/history; obsolete callbacks cannot overwrite current state. Copy path appears for a successful destination and reports clipboard failure truthfully, without changing the export outcome.

Each managed-video session holds an exclusive lease. Cleanup only targets owned cache paths, releases player handles before deleting artifacts and skips another live session. A stale result may remain reviewable even when its export is disabled. A review-open failure retains a valid artifact for export rather than claiming playback succeeded.

## Deliberate limits

There is no batch conversion, realtime symbolized video playback, GPU/hardware-encoder UI, arbitrary/custom ramp editor, GIF/WebM/image-sequence export, render pause, light theme or telemetry. These are scope limits, not unfinished controls to expose in the current UI.

Deployment supports the self-contained portable ZIP and an English per-user x64 Setup. Setup requests no elevation, defaults to `%LocalAppData%\Programs\GlyphFrame`, creates a Start Menu shortcut and makes the desktop shortcut optional. It keeps a stable installation identity for updates, blocks replacement/removal while that installed executable is running and rejects a version downgrade. Uninstall removes owned program files/shortcuts, not settings or user exports. Neither distribution provides automatic updates or file associations. See [packaging](packaging.md#installer) for build and acceptance boundaries.

See [architecture](architecture.md) for ownership boundaries, [testing](testing.md) for evidence and remaining limits, and [packaging](packaging.md) for deployment requirements.
