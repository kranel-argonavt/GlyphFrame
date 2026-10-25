# Changelog

This file summarizes user-visible work. It is not a release/tag history or a replacement for the scoped [verification record](docs/testing.md).

## Unreleased

### Added

- Local image/video conversion to colored symbol art with a shared glyph-based rendering pipeline.
- Separate Render → Review → Export workflows, retained image results and managed full-video review artifacts.
- PNG/JPEG image export and byte-copy publication of accepted H.264 MP4 output, with Keep/Remove Audio.
- Independent Original/Symbolized playback, comparison, fullscreen, seek and single-audio coordination.
- Responsive processing/result layouts, gesture-only image zoom/pan, live-accent branding and reduced-motion behavior.
- Draft-based settings with explicit Save/Cancel/Reset, local notices, recovery guidance and optional technical error details.
- Privacy-safe local diagnostics, synthetic media/integration tests and verified self-contained portable packaging.

### Refined

- Image zoom stops at centered Fit; ordinary wheel movement follows pan/outer-scroll boundaries without an image scaling toolbar.
- Result metrics/actions remain bounded and adapt to measured space; progress guidance remains readable when narrow.
- Source replacement, failed/cancelled retries and publication retain the correct accepted result and independent diagnostics.
- Playback seek/fullscreen synchronization and native icon theme refresh have dedicated regression coverage.
- Public documentation is organized around product behavior, architecture, testing, packaging and a playback ADR rather than development-phase journals.

No public release is declared. Final artifact/evidence binding, portfolio assets and release-owner licensing/distribution decisions remain open; see [testing](docs/testing.md#remaining-release-boundaries) and [packaging](docs/packaging.md#before-public-distribution).
