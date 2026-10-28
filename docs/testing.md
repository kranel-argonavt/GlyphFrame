# Testing and verification

GlyphFrame distinguishes automated tests, real media integration, native UI inspection and user reports. A passing fixture is not a human playback test or clean-machine certification.

## Run the automated checks

Use Windows x64, PowerShell 7, the .NET 10 SDK and access to configured NuGet sources. The full suite requires the [exact locally reviewed video tools](../Tools/ffmpeg/README.md); missing tools do not silently skip integration tests. From the repository root:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -ArchivePath 'C:/GFInputs/GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip'
dotnet restore GlyphFrame.sln
dotnet build GlyphFrame.sln -c Release --no-restore
dotnet test GlyphFrame.sln -c Release --no-build --no-restore
dotnet format GlyphFrame.sln --verify-no-changes --no-restore
git diff --check
```

[GlyphFrame.Tests](../tests/GlyphFrame.Tests/GlyphFrame.Tests.csproj) covers domain/workflow, files, actual Skia output, media/audio integration and artifact publication. [GlyphFrame.App.Tests](../tests/GlyphFrame.App.Tests/GlyphFrame.App.Tests.csproj) covers commands, Settings drafts, clipboard feedback, WPF geometry/bindings and playback/window coordination. Dispatcher-sensitive tests use the established nonparallel WPF collection; a finite passing run does not prove every historical scheduling failure impossible.

Helper regressions are separate from xUnit. They use inert fixtures and do not acquire/compile FFmpeg, publish/install the app or declare release approval:

```powershell
pwsh -NoProfile -File scripts/Test-PortablePackage.ps1
pwsh -NoProfile -File scripts/Test-PackageVerificationSession.ps1
pwsh -NoProfile -File scripts/Test-InstallerPackage.ps1
pwsh -NoProfile -File scripts/Test-PackageLegal.ps1
pwsh -NoProfile -File scripts/Test-SourcePreparation.ps1
pwsh -NoProfile -File scripts/Test-ControlledFfmpeg.ps1
```

Actual publish/startup checks are a [separate packaging operation](packaging.md). A fresh image-only source build without FFmpeg is not a full-suite pass. Source-export path checks are not exhaustive secret/history or licensing audits.

## Coverage that matters

| Area | Representative assertions |
| --- | --- |
| Symbolization | Glyph-based grid, luminance/polarity, alpha compositing, deterministic output and orientation |
| Image workflow | Decoding, retained pixels, stale settings, PNG/JPEG export without rerendering, cancellation and replacement |
| Real video | MP4/MOV/MKV/AVI, rotated MOV, sequential frames, H.264 output, AAC copy/fallback/removal and duration |
| Ownership/files | Leased sessions, obsolete-result rejection, partial cleanup, atomic publication and retry |
| Presentation | Responsive/narrow layouts, long text, working/result transitions, focus and reduced motion |
| Settings/errors | Draft Reset/Cancel/Save, storage failure/retry, notices/paths, bounded logs and independent error contexts |
| Playback | Separate players, seek/end/fullscreen/tab transfer, stale events, audio ownership and cleanup |
| Clipboard | Exact destination copying, truthful failure and cancelled/replaced/late-feedback rejection |

Tests use synthetic media, not personal downloads. [Fixture provenance](../tests/GlyphFrame.Tests/TestInfrastructure/MediaFixtures.md) distinguishes metadata-only/playable samples and reproduction. Tiny clips test contracts, not production-scale throughput or stress performance.

## Recorded verification snapshot

As of 2026-10-28. Reading this document executes no checks. These results describe local work based on private parent `e75a021` with uncommitted integration changes, not a clean-source release. The precommit rerun reconfirmed the full suite, helpers, build and formatting; overlapping earlier runs are not additive.

| Check | Result | Scope |
| --- | --- | --- |
| Release xUnit | **1,288 passed: 488 core/infrastructure/rendering + 800 App**, zero failures/skips | Exact controlled `.2` version and both executable hashes; includes missing/altered notices and manifest-binding regressions |
| Helper regressions | **141 passed: 36 controlled + 18 source + 25 legal + 20 portable + 27 installer + 15 session** | Synthetic policy/integrity checks, not native acceptance |
| Build and formatting | Release build/rebuild: **0 warnings/errors**; formatting and diff checks passed | .NET application build, not a claim that upstream FFmpeg compilation was warning-free |
| Source-only build | **459-file** hash-verified fresh snapshot built without a tool cache or FFmpeg EXEs in output; 0 warnings/errors | Run `321899bb23bd4c6797367e7aa9982375`; build-only, no new public history or full suite without tools |
| Current package | **445 payloads / 31 legal documents / 9 dependency bindings**; exact tools/source-material/twelve-notice binding; published startup, exclusive session, normal exit and cleanup passed | [Exact ZIP/Setup identities](packaging.md#current-local-controlled-tool-candidate); unsigned Setup built but not installed/run |

### Current extracted-ZIP native smoke

A fresh extraction and separate marked profile passed About/notices, image/video render and native Save. The viewer exposes the summary plus all **30 appended document headers**, the controlled version and IJG credit. Actual saved outputs fully decoded: one 320×240 PNG and two six-second 640×360/180-frame MP4s. Keep Audio preserved **283 AAC packet payloads**; Remove Audio produced no audio stream.

Rendered playback/end/seek/fullscreen/Escape, stale export blocking after audio changes, cancelled/successful replacement and active 900-frame-render cancellation passed. Only the lease remained after Cancel. Normal close returned zero, removed the owned session and preserved all 445 payloads, ordinary settings and installed EXE/tool hashes.

The normal-name app started but the computer-use helper twice misbound its window to the installed app identity. Only that empty owned test process was terminated; its nonzero exit/retained session is diagnostic evidence, not a cleanup pass. Native interaction used an additional **byte-identical renamed apphost** in a separate extraction. This is a targeting workaround, not a product fix or a normal-name helper pass. No human listening, renewed native PCM fallback, fresh DPI matrix, Setup lifecycle or clean-PC acceptance is inferred.

### Earlier evidence

[ADR 0002](decisions/0002-controlled-ffmpeg-build.md#verification-milestones) summarizes the controlled-build progression, reproduced VVC defects, corrected replay and scoped engineering review. Earlier isolated native checks included PCM-to-AAC fallback; their source-snapshot scope is not transferred to the current package. Superseded legal collections and installed candidates retain their [historical packaging records](packaging.md#historical-candidate-records). Full iteration journals, failed attempts, original TRX/captures and immutable hashes remain local, not public-document dependencies.

## Manual observations and attribution

The following are **USER-REPORTED PASS**, distinct from agent-native captures and automated fixtures:

- **Responsive UI:** minimum/smooth-resize, working/cancelling/Ready/Exported, centering/actions/long-text/scroll checks were reported working. Exact per-state dimensions and executed bytes were not restated.
- **Settings/appearance:** all six accents, Header/About, Cancel/Escape/title-close, Reset, Save/restart and paths/notices were reported working at 100/125/150% display scale, with positive text-size and second-monitor/fullscreen feedback. Exact accessibility text percentages and immutable build identity were not supplied for every report.
- **Installed branding:** About 1.0.0 / Rose, logos, Save/restart and the corrected desktop icon were confirmed after an earlier Setup update. No target package checksum or new DPI matrix was supplied.
- **Playback/audio:** both players, roughly ten end/seek/fullscreen repetitions, tab transfer, audible volume/mute/single-audio ownership, Remove Audio/external exports and rapid replacement were reported working on the latest committed local build. Clip: 10 seconds / 30 FPS / audio; no independently identified portable hash.
- **Clean PC:** startup/general functionality were reported working using the last supplied ZIP and the same Windows build as the main PC, previously identified as Windows 11 26H2 / 26300.9711. No prerequisite installation was reported, but target checksum, pre-existing runtime inventory, edition/media inventory and per-feature logs were not supplied. This is not a Windows 10/all-edition or current-`.2` package pass.

Accept positive reports within their scope; do not repeat ordinary checklists merely because agent targeting was inconclusive. A failed timing attempt is not retroactively passed by general feedback.

## Native retest checklist

Use the exact independently extracted candidate and record package/source hash, Windows build, display/text scale and media parameters. Use marked synthetic fixtures for failure tests, never personal destinations or OS security changes.

1. Check landscape/portrait image Ctrl+wheel Fit floor, drag pan and ordinary-wheel handoff; tab/rerender framing, cancelled replacement and successful reset.
2. Resize through working, Ready, stale/error and exported transitions in both directions; check actions, long text and constrained-height scrolling.
3. Check Settings draft Reset/Cancel/Escape/title-close, Save/restart, selectable paths and About/notices. Distinguish actual storage failure from a controlled dependency exception.
4. Repeat Original/Symbolized end → embedded seek → fullscreen; compare paused/playing time/frame/thumb and tab transfer. Listen for mute/volume/single-audio ownership; externally verify silent output.
5. Cancel rendering while active. Separately cancel publication during actual copy, not Save/overwrite confirmation; verify retained hashes and owned partial cleanup.
6. Decline overwrite, induce a safe owned-destination write/replace failure and retry without rerendering. Keep playback guidance/Details independent of export feedback.
7. Close normally; verify the positively observed session and owned media processes/partials disappear, with input/package hashes unchanged.

Finishing before Cancel input is **inconclusive**, not a cancellation pass. Clipboard doubles do not prove OS clipboard denial or a deliberately forced native late-feedback race.

## Remaining release boundaries

- Available reports do not certify every window/state/DPI/mixed-monitor combination or native fault branch.
- Bind affected checks to final clean-source artifacts; old local-build/installed/user results do not automatically cover new bytes.
- Current Setup lifecycle and clean-PC acceptance remain open. Earlier clean-PC feedback is not an audited absence-of-runtime environment; Windows/codec compatibility stays bounded.
- [Media provenance](screenshots/README.md), [licensing/distribution](licensing.md) and [history preparation](portfolio.md) remain separate from successful tests. No test authorizes a tag, upload or public release.

Current product behavior is in [product-spec.md](product-spec.md); ownership decisions are in [architecture.md](architecture.md). Detailed local evidence and superseded artifacts are retained rather than embedded in public guides.
