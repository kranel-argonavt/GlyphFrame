# Original Video Playback Compatibility Report

## Status and scope

This report records the Phase 11B.5A evaluation performed on 2026-09-09. Its five architecture and behavior decisions were approved on 2026-09-09 as the implementation contract for Phase 11B.5B. This approval does not add Original-tab playback, approve a new dependency, or expand GlyphFrame's import allowlist.

The evaluation asked whether the WPF `MediaPlayer` stack already used for managed rendered artifacts can safely provide Original-tab playback while the bundled-FFmpeg frame reader remains the fallback. All media stayed local. No proxy, source copy for playback, or third-party playback package was added.

## Evaluation environment

| Item | Observed value |
|---|---|
| Windows | Display version 25H2, build 26200.9168; the legacy registry product label reports Windows 10 Home |
| OS/process architecture | x64 / x64 |
| Runtime | .NET 10.0.11, `net10.0-windows` WPF |
| WPF playback API | `System.Windows.Media.MediaPlayer` through `WpfRenderedVideoPlaybackBackend` |
| FFmpeg | 9.0 `essentials_build-www.gyan.dev`, GCC 16.1.0, bundled x64 executable |
| ffprobe | 9.0 `essentials_build-www.gyan.dev`, bundled x64 executable |
| Additional media components | A user-scoped AppX query returned no matching HEVC, VP9, AV1, Web Media, MPEG, or Media Extension packages. The Windows `MediaPlayback` optional-feature query required elevation and was not performed. No third-party codec pack was identified or assumed. |

WPF `MediaPlayer` uses Windows media components and therefore remains dependent on the codecs available on the target Windows installation. A pass on this machine is evidence for this environment, not a portable compatibility guarantee.

## Architecture after the first Phase 11B.5B foundation block

- Neutral App-layer playback contracts represent generation-tagged native events, visual source, natural dimensions, duration/position, volume/mute, transport operations, and position timing.
- `WpfMediaPlaybackBackend` owns one WPF `MediaPlayer`, exposes its `VideoDrawing`, recreates the transport for each open, rejects native events from inactive/replaced players, enables paused scrubbing, and releases the player on close or disposal.
- A shared media-playback controller owns open generations, the bounded injectable open deadline, common transport state, position clamping, play/pause/replay behavior, and deterministic event/timer disposal.
- `RenderedVideoPlayer` preserves its existing public Symbolized-review contract through an adapter over those shared primitives; `WpfRenderedVideoPlaybackBackend` retains only rendered-review fullscreen ownership and messages around the neutral transport.
- `OriginalVideoPlayer` is a separate high-level facade with its own backend, timer, generation, state, source path, and Original-specific readiness/fallback results. It is registered for later ViewModel integration but is not presented in the Original tab yet.
- The rendered-video player is coupled to managed-artifact review semantics and user messages. It borrows the artifact; the managed session remains the file owner.
- Original-frame preview is independent. `FfprobeVideoMetadataReader` obtains authoritative metadata and `FfmpegVideoFrameReader` extracts a display-oriented owned RGBA frame with bundled FFmpeg. This path supports Unicode paths and applies rotation metadata.
- `MainWindowViewModel` still owns only the rendered-artifact player. The Original tab intentionally remains a frame-position surface until the next integration block.

Reusing the transport technology does not require sharing one player instance. Original media and the accepted symbolized artifact have different ownership, failure, duration, and lifecycle rules.

## Method and evidence levels

The temporary diagnostic harness referenced the real App and Infrastructure projects and exercised the production metadata reader, production FFmpeg frame reader, and production WPF playback backend. It was not added to the tracked test suite because its WPF result depends on machine codec availability.

For each case, the harness:

1. ran GlyphFrame media-kind detection and metadata probing;
2. extracted a frame at normalized position 0.5 through bundled FFmpeg;
3. opened the source through WPF and recorded `MediaOpened` or `MediaFailed`;
4. recorded WPF natural duration and natural video dimensions;
5. paused and sought to approximately 25% and 75%;
6. rendered the real WPF `VideoDrawing` into a `RenderTargetBitmap` and checked for visible pixels and frame changes;
7. closed the backend and opened the source with exclusive `FileShare.None` access.

The bitmap checks are component-level visual evidence: decoded WPF pixels were inspected programmatically, not watched in a live application window. No case was listened to, so audible playback, mute, channel correctness, and audio/video synchronization remain unverified. Desktop automation was unavailable in this session, so no live interactive playback claim is made.

## Fixtures

Committed deterministic fixtures were reused for the H.264 MP4, silent MP4, rotated MOV, and corrupt MKV cases. Small temporary files were generated with the bundled FFmpeg for missing combinations and removed after evaluation.

| Temporary fixture | Size | Purpose |
|---|---:|---|
| `h264-aac.mov` | 19,421 bytes | H.264/AAC MOV remux |
| `h264-aac.mkv` | 18,815 bytes | H.264/AAC Matroska remux |
| `mpeg4-mp3.avi` | 50,738 bytes | MPEG-4 Part 2/MP3 legacy AVI |
| `ffv1-pcm.mkv` | 158,080 bytes | FFV1/PCM file that FFmpeg reads but WPF does not render on this machine |
| `Юнікод テスト/відео з аудіо.mp4` | 19,370 bytes | Unicode-path copy of the committed H.264/AAC fixture |

Equivalent reproducible generation commands, run from the repository root, are:

```powershell
$ffmpeg = '.\Tools\ffmpeg\ffmpeg.exe'
$source = '.\tests\GlyphFrame.Tests\Fixtures\video-audio-aac.mp4'

& $ffmpeg -y -i $source -map 0:v:0 -map 0:a:0 -c copy -f mov h264-aac.mov
& $ffmpeg -y -i $source -map 0:v:0 -map 0:a:0 -c copy -f matroska h264-aac.mkv

& $ffmpeg -y `
  -f lavfi -i 'testsrc2=size=160x90:rate=10:duration=1.2' `
  -f lavfi -i 'sine=frequency=880:sample_rate=44100:duration=1.2' `
  -c:v mpeg4 -q:v 5 -c:a libmp3lame -b:a 64k -ac 1 -shortest mpeg4-mp3.avi

& $ffmpeg -y `
  -f lavfi -i 'testsrc2=size=160x90:rate=10:duration=1.2' `
  -f lavfi -i 'sine=frequency=660:sample_rate=48000:duration=1.2' `
  -c:v ffv1 -level 3 -c:a pcm_s16le -ac 1 -shortest ffv1-pcm.mkv
```

The Unicode case is a byte-for-byte copy into a mixed Cyrillic/Japanese path; copying rather than transcoding isolates path handling from codec behavior.

## Compatibility matrix

`GF duration` is authoritative metadata returned by GlyphFrame. `WPF duration` is `MediaPlayer.NaturalDuration`; the difference matters for tab-position transfer. “Visible” means the WPF drawing produced non-black decoded pixels. “Changed” means the two paused seek captures differed; the rotated fixture is intentionally static.

| Container and streams | GlyphFrame import / metadata | FFmpeg frame | WPF result | Duration: GF / WPF | Paused seeking and displayed frame | Audio evidence | Handle release |
|---|---|---|---|---|---|---|---|
| MP4, H.264 + AAC, 64×48, 10 fps | Video; success | 64×48; success | `MediaOpened`, natural 64×48 | 1.2 s / 1.0 s | 0.25 s and 0.75 s; visible and changed | AAC detected; audibility not observed | Exclusive open succeeded after close |
| MP4, H.264, silent, 64×48, 10 fps | Video; success; no audio | 64×48; success | `MediaOpened`, natural 64×48 | 1.2 s / 1.0 s | 0.25 s and 0.751 s; visible and changed | Confirmed silent by ffprobe | Exclusive open succeeded after close |
| MOV, H.264 + AAC, 64×48, 10 fps | Video; success | 64×48; success | `MediaOpened`, natural 64×48 | 1.2 s / 1.0 s | 0.25 s and 0.75 s; visible and changed | AAC detected; audibility not observed | Exclusive open succeeded after close |
| MKV, H.264 + AAC, 64×48, 10 fps | Video; success | 64×48; success | `MediaOpened`, natural 64×48 | 1.221 s / 1.0 s | 0.25 s and 0.75 s; visible and changed | AAC detected; audibility not observed | Exclusive open succeeded after close |
| AVI, MPEG-4 Part 2 + MP3 mono, 160×90, 10 fps | Video; success | 160×90; success | `MediaOpened`, natural 160×90 | 1.3 s / 1.3 s | 0.325 s and 0.975 s; visible and changed | MP3 detected; audibility not observed | Exclusive open succeeded after close |
| MOV, H.264, silent, encoded 320×240 with 90° rotation | Video; success; display 240×320 | 240×320 display-oriented; success | `MediaOpened`, **natural 320×240** | 1.2 s / 1.0 s | Visible static color quadrants; WPF showed encoded orientation and ignored rotation metadata | Confirmed silent by ffprobe | Exclusive open succeeded after close |
| MP4, H.264 + AAC under mixed Cyrillic/Japanese path | Video; success | 64×48; success | `MediaOpened`, natural 64×48 | 1.2 s / 1.0 s | 0.25 s and 0.75 s; visible and changed | AAC detected; audibility not observed | Exclusive open succeeded after close |
| MKV, FFV1 + PCM S16LE mono, 160×90, 10 fps | Video; success | 160×90; success | **`MediaOpened`, but no natural video dimensions and no decoded picture** | 1.2 s / 1.0 s | Position changed; both captures remained black | PCM detected; audibility not observed | Exclusive open succeeded after close |
| Corrupt MKV | Extension recognized as Video; metadata failed on truncated EBML | Not attempted because required metadata failed | `MediaFailed`, COM `0xC00D1197` | unavailable | unavailable | unavailable | Exclusive open succeeded after close/failure |

### Verified conclusions

- The tested WPF stack rendered H.264 in MP4, MOV, and MKV, and MPEG-4 Part 2 in AVI on this machine. H.264/AAC and silent Unicode-path sources also opened.
- Compatibility is not implied by the import extension. FFV1/PCM MKV passed GlyphFrame probing and FFmpeg extraction, and even raised WPF `MediaOpened`, but WPF supplied no natural video dimensions and no picture.
- `MediaOpened` alone is therefore an unsafe playback-success criterion. A positive natural video size is a minimum additional gate; runtime `MediaFailed` and open timeout handling remain required.
- WPF did not apply the rotated MOV's 90° display transform. Original playback must apply trusted GlyphFrame rotation metadata in the view or fall back to the display-oriented FFmpeg frame.
- WPF natural duration was 1.0 s for multiple 1.2–1.221 s sources. Position transfer must clamp against the actual target and must not promise frame-exact synchronization from these duration values.
- Paused position changes rendered a different frame for every dynamic supported case. This is component evidence for scrubbing, not a human observation of the screen.
- `Close` released the source handle for every success, false-positive open, and failure case tested.
- Actual audible playback and audio/video synchronization were not verified.

## Backend options

### Option A — Generalize and reuse the current rendered player directly

Advantages:

- no new deployment dependency or native payload;
- proven generation, timeout, paused scrubbing, fullscreen, and handle-release behavior;
- existing player-control design can be reused.

Risks:

- the current interfaces, failure messages, and lifecycle assumptions are specifically about a managed rendered artifact;
- one shared controller cannot safely represent two sources with independent durations, failures, positions, and ownership;
- modifying the managed-artifact controller directly increases regression risk for replacement, deferred recovery, and cache deletion.

Using the same single instance for Original and Symbolized is not recommended.

### Option B — Dedicated Original-player facade over shared WPF primitives

Create an App-layer `IOriginalVideoPlayer`/controller with a separate backend instance. Extract only genuinely common WPF transport primitives from the current backend and coordinate the two controllers at the ViewModel or a small comparison coordinator.

Advantages:

- no additional package or deployed binary;
- source-file ownership, original-specific compatibility/fallback state, and user messages remain separate from managed-artifact ownership;
- independent duration and position state is explicit;
- the coordinator can guarantee pause-before-switch and single-audio ownership;
- generic transport fixes can still be shared without coupling both workflows to one mutable player.

Risks:

- playback remains dependent on Windows codecs;
- WPF needs a natural-dimension readiness gate and runtime fallback;
- rotation must be applied explicitly;
- two transports require strict close/dispose ordering on replacement, unload, and shutdown.

**Approved direction:** Option B is the smallest safe Phase 11B.5B architecture and is the required basis for implementation.

### Option C — Bundled third-party backend

A bundled native player could provide a broader and more consistent codec matrix and better control over audio/video synchronization. It would also add a significant native deployment payload, license-compliance work, architecture-specific binaries, security and update maintenance, packaging complexity, and a new playback lifecycle. Exact size and license obligations depend on the selected product and build configuration.

This evaluation does not select, install, or approve a third-party backend. It should be reconsidered only if the system-player-plus-fallback contract is unacceptable.

## Approved compatibility and fallback contract

1. Continue to validate media through the existing GlyphFrame import and metadata pipeline. Importability means “processable by bundled FFmpeg,” not “playable by WPF.”
2. Attempt system playback for the existing MP4, MOV, MKV, and AVI allowlist. Do not hard-code an extension-wide guarantee.
3. Treat Original playback as ready only after the current open generation receives `MediaOpened` **and** reports positive natural video dimensions. A timeout, `MediaFailed`, missing natural dimensions, or later playback failure switches that media generation to fallback presentation.
4. Apply the trusted metadata rotation to the WPF visual without transcoding. If that cannot be done without incorrect orientation, retain the display-oriented FFmpeg frame fallback.
5. In fallback mode, show the current Original frame, the labelled frame-position selector, and `Preview Frame`; hide playback-only controls rather than leaving enabled no-op actions. Explain that animated source playback is unavailable while rendering and artifact export remain usable.
6. Preserve the existing `Preview Frame` action as the deterministic FFmpeg still-frame/symbolization path before a complete artifact exists and whenever Original playback falls back. Preserve the current rule that an accepted full artifact takes Symbolized presentation priority and disables an ineffective new frame preview. Continuous source playback must never trigger symbolization.
7. A playback failure never marks the source unprocessable and never deletes or invalidates the accepted symbolized artifact.
8. Do not create a proxy automatically. Unsupported sources remain usable through frame preview and full rendering if the FFmpeg pipeline accepts them.

## Approved tab-switching contract

- Maintain separate Original and Symbolized player controllers. They may preserve independent transport state, but a coordinator is the only component allowed to activate playback.
- Before a tab change, pause the outgoing player and confirm its timer is stopped. This prevents simultaneous audio.
- Transfer the outgoing absolute timestamp to the incoming player. Clamp it to the incoming target's usable duration; if that duration is unavailable or non-positive, use zero and show duration as unavailable rather than inventing one.
- Keep the incoming tab paused after the seek. Do not automatically resume playback.
- Define acceptable comparison alignment as `max(250 ms, two frame intervals)` using a trustworthy target FPS. This is a UI comparison tolerance, not a promise of frame-exact decoding. Report no numeric tolerance when target FPS is unavailable.
- If the target is shorter, clamp before its end by one trustworthy frame interval where possible so the paused surface does not immediately enter replay/end state.
- Keep Audio/Remove Audio never changes Original playback. It only changes the rendered artifact identity.
- If Original playback is unavailable, tab switching transfers the timestamp to the FFmpeg frame-position selector and obtains a new frame only through the existing explicit frame-loading path. It does not start a hidden player.
- On media replacement, close both player handles before accepting the new source state. A failed selection preserves the old state. On unload or shutdown, stop timers, close fullscreen, detach events, and close both transports before managed-artifact/session cleanup.

## Proxy strategy

A proxy is not necessary for the approved Phase 11B.5B fallback contract and must not be implemented implicitly. If future product requirements demand animated playback for every FFmpeg-readable source, a proxy decision would need explicit approval for:

- an application-owned cache root and session lease;
- disk-space estimation and a size ceiling;
- bounded transcoding and progress/cancellation semantics;
- unique partial-to-completed promotion and startup orphan recovery;
- source identity, invalidation, and privacy documentation;
- whether audio is copied or transcoded;
- cleanup on media replacement, shutdown, crash recovery, and user-data removal;
- startup latency and CPU cost.

## Risks and mitigations

| Risk | Approved mitigation for Phase 11B.5B |
|---|---|
| OS-dependent codec availability | Runtime readiness gate and FFmpeg frame fallback; no container-wide promise |
| False-positive `MediaOpened` | Require positive natural video dimensions; retain timeout and later failure handling |
| Ignored rotation metadata | Apply trusted GlyphFrame rotation in the view and test asymmetric rotated media |
| Natural-duration disagreement | Use absolute-time transfer, clamp to target, paused incoming state, documented tolerance |
| Two audio streams | Central coordinator pauses outgoing before activating incoming; never auto-resume |
| Stale WPF callbacks | Independent generation tokens per controller plus media-generation validation |
| Source file locked during replacement | Close Original transport before committing media replacement; retain deterministic exclusive-open tests |
| Regression to managed-artifact ownership | Keep Original facade separate; dispose player handles before session cleanup |
| Unsupported source blocks rendering | Playback failure is presentation-only; FFmpeg frame/render paths remain authoritative |

## Approved decisions

1. Use Option B: a dedicated Original-player facade using a separate instance of shared WPF playback primitives.
2. Determine runtime eligibility from `MediaOpened` plus positive natural video dimensions, with FFmpeg frame fallback on timeout, failure, or missing dimensions, rather than promising playback by extension. Positive dimensions are a minimum readiness gate, not a guarantee that every later frame or audio sample will decode.
3. Apply trusted rotation metadata in the Original view without transcoding.
4. Pause before switching tabs, transfer an absolute timestamp, and keep the incoming player paused. Use `max(250 ms, two frame intervals)` as an initial comparison-validation threshold and refine it only if Phase 11B.5B measurements justify a stricter contract.
5. Retain `Preview Frame` as the explicit FFmpeg fallback and still-symbolization action without creating a playback proxy. Preserve the existing full-artifact presentation-priority rule: after a complete video artifact is accepted, frame preview remains disabled unless a future explicit review mode is approved.

## Approved Phase 11B.5B implementation boundaries

Phase 11B.5B should implement:

- a dedicated original-player facade and small two-player coordinator in the App layer;
- runtime readiness/fallback state without changing Core's media processing contracts;
- Original-tab transport controls only when playback is ready;
- orientation-correct Original presentation;
- deterministic tab timestamp transfer with the incoming tab paused;
- single-audio enforcement and lifecycle cleanup;
- fallback to the current FFmpeg frame surface and `Preview Frame`;
- deterministic unit/STA tests plus a documented manual audiovisual matrix.

It should not add a third-party backend, proxy generation, new import containers, realtime symbolization, artifact publication changes, or Phase 12 logging/error-detail UI.

## Remaining evidence gaps

- No audible playback, mute, volume, channel, or audio/video synchronization check was performed.
- No live human-observed paused-seek, replay, fullscreen, focus, keyboard, minimum-window, or multi-monitor DPI check was performed.
- The compatibility matrix represents one Windows installation. Clean Windows 10 and Windows 11 machines, N/KN editions, and systems with different media-feature/codec installations remain untested.
- H.264 profiles/levels beyond these tiny fixtures, variable frame rate, unusual timestamps/start offsets, interlaced video, and damaged-but-partially-decodable sources remain untested.
- The positive-natural-dimensions gate eliminates the observed FFV1 false positive but is not proof that every subsequent frame or audio sample will decode.
