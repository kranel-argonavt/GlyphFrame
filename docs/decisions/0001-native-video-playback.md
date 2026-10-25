# ADR 0001: independent video players over shared WPF transport

Status: accepted and implemented. Original decision: 2026-09-09. This condensed record retains the engineering decision and compatibility limits rather than the complete evaluation journal.

## Context

GlyphFrame needs Original playback for comparison with a fully rendered Symbolized MP4. It already uses WPF for managed-artifact review and bundled FFmpeg for probing, frame extraction, encoding and audio muxing.

Original input and generated review output have different ownership: input is borrowed read-only, while the managed artifact is owned and replaced/cleaned by the application. Sharing one player instance would also couple their timing, failure and lifecycle state.

## Decision

Use **separate Original and Rendered player facades/backend instances** over common WPF transport primitives. Share reusable transport/generation/timeout/seek/lifetime logic, not a single source/player state. Coordinate comparison and audio ownership at the application layer.

- Enable Original continuous playback only after runtime readiness; keep the bundled-FFmpeg display-oriented frame preview as fallback.
- Do not treat `MediaOpened` alone as proof of usable video: require usable natural dimensions and handle open timeout/runtime failure.
- Apply trusted rotation metadata as a view-only transform because Windows playback may ignore the container display matrix; otherwise retain the oriented fallback.
- Pause both players for tab comparison, transfer/clamp a timestamp against the target and leave both paused. Do not promise frame-exact synchronization across native clocks/durations.
- Keep paused-position ownership independent of delayed native clock notifications; surfaces/fullscreen must not overwrite an intentional seek with an older value.
- Release playback/timers before media replacement, artifact deletion and shutdown. Reject stale native callbacks by generation.
- Do not generate a source proxy/copy just to enable playback, and do not introduce an additional bundled player dependency for the current scope.

The relevant implementation is [MediaPlaybackController](../../src/GlyphFrame.App/Services/MediaPlaybackController.cs), [OriginalVideoPlayer](../../src/GlyphFrame.App/Services/OriginalVideoPlayer.cs), [RenderedVideoPlayer](../../src/GlyphFrame.App/Services/RenderedVideoPlayer.cs) and [VideoPlaybackCoordinator](../../src/GlyphFrame.App/Services/VideoPlaybackCoordinator.cs).

## Alternatives considered

| Option | Assessment |
| --- | --- |
| Reuse one managed-artifact player directly | Avoids another dependency but conflates original/managed ownership, durations, failures and cleanup; rejected as the shared-instance design. |
| Independent facades over common WPF primitives | Reuses existing technology while keeping state/ownership separate; selected. |
| Add another media backend/runtime | Could widen codec support but adds deployment/native payload, maintenance and compatibility scope; deferred unless concrete requirements justify it. |
| Transcode a proxy for Original | Can normalize playback but copies/transcodes user content, consumes storage/time and complicates identity/cleanup; not part of this product. |

## Evidence behind the decision

The 2026-09-09 component evaluation used Windows display version 25H2 / build 26200.9168, x64, .NET 10 WPF and bundled FFmpeg/ffprobe 9.0. The legacy registry product label was not treated as authoritative Windows-edition evidence. Optional media-component inventory was incomplete; no codec-pack absence or universal compatibility was assumed.

| Representative input | Observed result in that environment |
| --- | --- |
| H.264 MP4/MOV/MKV, MPEG-4 Part 2 AVI | WPF produced visible video; FFmpeg metadata/frame extraction also succeeded. |
| Supported H.264 media under mixed Cyrillic/Japanese paths | Opened successfully in the checked cases. |
| Rotated H.264 MOV | WPF showed encoded orientation; the separate FFmpeg/metadata path supplied correct display orientation. |
| FFV1/PCM MKV | FFmpeg processing succeeded and WPF raised MediaOpened, but WPF supplied no usable natural video dimensions/picture. |
| Corrupt MKV | Validation/open failed; source handles were released. |

WPF natural duration differed from probed duration for some small fixtures. Paused seek changed rendered pictures in dynamic supported cases, but this is component evidence, not a frame-perfect or audible native-playback guarantee. Exclusive source access after close confirmed handle release in the evaluated cases.

Later user observations confirmed ordinary playback, volume/mute, single-audio tab switching and fullscreen behavior within their reported conditions. Current regression/manual scope is maintained in [testing](../testing.md), not retroactively assigned to the initial codec evaluation.

## Consequences and limits

- No additional playback package or source proxy is required by this decision.
- Import/FFmpeg support and Windows continuous playback remain separate capabilities. A recognized container extension is not a codec guarantee.
- Original failure falls back without invalidating otherwise processable media. A valid rendered artifact that Windows cannot open remains retained/exportable.
- Native clock differences, long paths, Windows media components and untested codecs remain real compatibility boundaries.
- Deterministic controller tests, rendered WPF fixtures, native screen checks and human audio observations prove different things and must remain distinguishable.

See [architecture](../architecture.md) for ownership boundaries and [product scope](../product-spec.md) for user-visible behavior. The full dated evaluation and unsuccessful attempts are preserved in local archives; public readers do not need those files to understand the decision.
