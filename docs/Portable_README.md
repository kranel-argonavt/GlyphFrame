# GlyphFrame {{VERSION}} — Windows x64 portable application

Extract the **entire ZIP** before launching. Open `GlyphFrame.App.exe` inside the extracted application directory. Keep all bundled files and the `Tools/ffmpeg/` structure together; do not run directly inside the ZIP or move only the EXE.

The folder contains a self-contained .NET/WPF runtime, Skia dependencies, FFmpeg and ffprobe. A separately installed .NET runtime or FFmpeg is **not intended to be required**. Actual clean-Windows execution without those installations remains Phase 15C validation; structural packaging checks alone do not prove it.

Portable means **deployment without an installer**, not beside-EXE application data. Normal settings, privacy-safe logs and managed review cache remain under `%LocalAppData%\GlyphFrame`. Media stays local; no telemetry or upload is added. `--verification-data-root` is a marked, explicit test-only opt-in, not normal usage.

To remove the application, close it and delete its extracted directory. That does not remove user-local preferences/logs or exports saved elsewhere. Do not delete another running instance's cache. No PATH or Windows installation changes are required by this package.

Supported target: Windows 10/11, x64. Original playback depends on Windows media components/codecs; import or FFmpeg decoding support does not guarantee continuous WPF playback. Unsupported playback retains frame-preview/rendering fallback. Long media/cache paths can limit WPF playback; use a reasonably short extraction path. Unusual codecs, clean-machine behavior and the extracted ZIP's interactive workflow still require validation.

See `THIRD_PARTY_NOTICES.md`, `Tools/ffmpeg/LICENSE.txt`, `Tools/ffmpeg/DISTRIBUTION_README.txt` and `Tools/ffmpeg/SHA256SUMS.txt` for attribution and bundled-tool provenance. This provisional package is not a public release or a claim of legal distribution compliance. Application licensing and FFmpeg corresponding-source/distribution obligations require release-owner review before public distribution; no application license is invented here.
