# Testing and verification

GlyphFrame separates deterministic logic/component tests, real media/file integration, native application checks and human observations. A passing fixture or test double is not presented as a human playback test or clean-machine certification.

## Run the automated checks

Use Windows x64 with the .NET 10 SDK and access to the configured NuGet sources. From the repository root:

```powershell
dotnet restore GlyphFrame.sln
dotnet build GlyphFrame.sln -c Release --no-restore
dotnet test GlyphFrame.sln -c Release --no-build --no-restore
dotnet format GlyphFrame.sln --verify-no-changes --no-restore
git diff --check
```

The two test projects have different responsibilities:

- [GlyphFrame.Tests](../tests/GlyphFrame.Tests/GlyphFrame.Tests.csproj): domain/workflow, settings/files, actual Skia output, FFmpeg/container/audio integration and managed-artifact publication.
- [GlyphFrame.App.Tests](../tests/GlyphFrame.App.Tests/GlyphFrame.App.Tests.csproj): ViewModel commands, independent error contexts, Settings drafts, clipboard feedback, WPF bindings/geometry, playback coordination and native-window components.

Machine-dependent native windows/Dispatcher tests use the established nonparallel WPF collection where required. The committed isolation correction changes test scheduling, not production behavior or assertions. A finite passing sample does not prove every historical low-level Dispatcher failure impossible.

PowerShell 7 helper regressions are separate from xUnit and do not publish or launch the app:

```powershell
pwsh -NoProfile -File scripts/Test-PortablePackage.ps1
pwsh -NoProfile -File scripts/Test-PackageVerificationSession.ps1
pwsh -NoProfile -File scripts/Test-InstallerPackage.ps1
```

Publishing/startup validation is an explicit separate operation; see [packaging](packaging.md).

## Coverage that matters

| Area | Representative assertions |
| --- | --- |
| Symbolization | Automatic glyph-based grid, luminance/polarity, alpha compositing, deterministic output and orientation |
| Image workflow | Valid decoding, retained pixels, stale settings, PNG/JPEG export without rerendering, cancellation and replacement |
| Real video | MP4/MOV/MKV/AVI, rotated MOV, sequential frames, valid H.264 output, stream-copy/AAC/silent audio and repeated publication |
| Ownership and files | Leased sessions, obsolete candidate rejection, source preservation, partial cleanup, atomic destination replacement and retry |
| Presentation | Populated responsive layouts, narrow/long text, render/result transitions, focus/action reachability and reduced-motion logic |
| Settings and errors | Draft Reset/Cancel/Save, storage failure/retry, notices/log paths, safe error details and bounded logging |
| Playback | Independent players, paused seek/fullscreen/tab transfer, stale event rejection, audio coordination and handle cleanup |
| Clipboard | Explicit exact-path copying, truthful failure, cancelled/replaced destination and late-feedback rejection |

Tests use locally generated synthetic media, not personal downloads. [MediaFixtures.md](../tests/GlyphFrame.Tests/TestInfrastructure/MediaFixtures.md) describes committed fixture provenance, metadata-only versus playable samples and reproduction. Tiny integration clips prove specific behavior, not production-scale throughput or stress performance.

## Recorded verification snapshot

Current summary as of 2026-10-26; opening this document does not execute tests. Full attempts, previous baselines and superseded package identities remain in local archives.

| Check | Recorded result | Boundary |
| --- | --- | --- |
| Automated implementation | **1,274 Release tests**, 488 core/infrastructure/rendering + 786 App; zero failures/skips; build zero warnings/errors; format/diff passed | Implementation based on `9e9a8b3` plus uncommitted changes, not final clean-source release acceptance. Includes 6 lifetime-marker and 10 icon cases. |
| PowerShell helpers | **27 installer + 20 portable + 15 session checks** passed | 62 separate checks, not additions to xUnit. Includes trailing-separator package identity and invalid/failed-run rejection; no app launch or installation. |
| Package integrity | Current paired candidate: all **416 payloads**, published startup/session cleanup and embedded shell icons passed | [Current artifact identity](packaging.md#recorded-candidate). Earlier independent extracted-ZIP checks are retained locally and are not silently transferred to new bytes. |
| Installer lifecycle | First install, English wizard/task defaults, registration, running-app refusals, same-version repair, native uninstall/data retention and reinstall passed | Initial native/CLI checks used an earlier Setup. Current Setup separately passed installed update, all payload hashes, retained settings/exports and native Rose startup; [scope](packaging.md#verification-status). Not a different-version upgrade or clean-PC Setup pass. |
| Installed branding | Nine EXE/Setup frames checked; installed Shell index zero returned white 20/40-pixel icons. User confirmed Rose Save/restart/About and the visible desktop icon | **USER-REPORTED PASS** for the human observations, not an agent-observed desktop screenshot or a new DPI matrix. |
| Recovery, publication and cleanup | Real storage/publication failures and retries, render cancellation/retention, playback-error separation and owned-session cleanup exercised | Focused tests overlap the full suite; probes/native observations are separate methods. Some timing-dependent branches remained inconclusive for the agent and have scoped later user feedback, not exhaustive OS-fault certification. |

## Manual observations and attribution

- **Responsive UI:** user reported the supplied minimum/smooth-resize, working/cancelling/Ready/Exported, centering/actions/long-text/scroll checklist working. Exact dimensions, per-state/media results and executed bytes were not restated; this is not a complete measured DPI matrix.
- **Settings/appearance:** ordinary and recovery checks were reported working, including all six accents, Header/About, Cancel/Escape/title-close, Reset, Save/restart and paths/notices. Reported display scales were 100/125/150%, with positive independent accessibility text-size feedback and second-monitor/fullscreen use. Exact text percentage and immutable local-build identity were not supplied for every report.
- **Installed branding (2026-10-26):** user explicitly reported About 1.0.0, new Rose colors/logos, Save & Close, full restart and About all OK, then confirmed the visible desktop icon after the corrected Setup was applied. Record **USER-REPORTED PASS** for both cases; no package checksum was restated by the user and no broader DPI claim is implied.
- **Playback/audio:** all seven supplied items were reported passed on the latest committed local build: both players, roughly ten completion/seek/fullscreen repetitions in total, paused/playing comparison, audible Keep Audio/volume/mute/single ownership, Remove Audio/external exports and rapid transitions/replacement. Test clip: 10 seconds, 30 FPS, audio. The user clarified local-build provenance, not an independently identified portable hash.
- **Clean PC:** user reported successful startup/general functionality on a clean computer, confirmed the last supplied ZIP and the same Windows build as their main PC, previously reported as Windows 11 26H2 / 26300.9711. No prerequisite installation was reported. Record **USER-REPORTED PASS** for those conditions; a target checksum, pre-existing runtime/tool inventory, Windows edition/media inventory and per-feature logs were not supplied. This is not a separate Windows 10 or all-edition pass.

Accept these positive reports within scope; do not repeat identical ordinary checklists merely because agent targeting was inconclusive. Native screenshots, rendered WPF fixtures, controlled collaborators and human listening remain different evidence methods.

## Native retest checklist

Use the exact independently extracted candidate and record its hash/source, Windows build, display/text scale and media parameters. For potentially destructive failures use only marked synthetic fixtures, never personal destinations or changed OS security/associations.

1. Load landscape/portrait images; check Ctrl+wheel Fit floor, drag pan and ordinary-wheel handoff at boundaries. Verify tab/rerender framing, cancelled replacement and successful replacement reset.
2. Resize around actual layout transitions in both directions through working, Ready, stale/error and exported states. Check all actions, long guidance and constrained-height scrolling.
3. Exercise Settings draft Reset/Cancel/Escape/title-close, Save/restart, selectable paths and About/notices. Distinguish real storage failure from a controlled dependency exception.
4. For Original/Symbolized, repeat end → embedded seek → fullscreen reopen; compare time/frame/thumb, paused/playing tab transfer, mute/volume and single-audio ownership. Listen to Keep Audio and externally verify Remove Audio output.
5. Cancel rendering while actually active, not after completion. Separately cancel publication during actual copy, not Save/overwrite confirmation. Check retained artifact/destination hashes and owned partial cleanup.
6. Decline overwrite, induce a safe owned-destination write/replace failure, retry without rerendering, and ensure playback guidance/Details remain independent of publication feedback.
7. Close normally; confirm the positively observed session is removed, no owned media processes/partial files remain and input/package hashes are unchanged.

An operation finishing before input is an inconclusive timing attempt, not a cancellation pass. A clipboard-service double does not prove system clipboard denial was induced; general manual feedback does not establish a deliberately forced late-feedback race.

## Remaining release boundaries

- A full measured window/state/DPI matrix, unreported mixed-DPI combinations and all native fault branches are not certified by the available reports.
- Some human checks identify the latest local implementation rather than immutable final-distribution bytes; bind relevant evidence to the final artifact before publication.
- The clean-PC report is positive human evidence, not an independently audited absence-of-runtime environment. Broader Windows/codec compatibility remains bounded by tested conditions.
- Portfolio screenshots, owned/generated samples, demo material and release-owner licensing/distribution review remain separate work. Successful tests do not authorize tagging or publishing.

Detailed attempt journals, TRX, native captures, hashes and provisional package histories are retained in local archives rather than shipped as public documentation dependencies. Current product behavior is in [product-spec.md](product-spec.md); ownership decisions are in [architecture.md](architecture.md).
