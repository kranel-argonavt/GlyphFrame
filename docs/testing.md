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

Recorded on 2026-10-25; this is evidence for the listed revisions/conditions, not an execution triggered by opening this document.

| Check | Recorded result | Boundary |
| --- | --- | --- |
| Clean automated baseline | `f3004e7`: **1,258 Release tests**, 488 core/infrastructure/rendering + 770 App; zero failures/skips; forced rebuild zero warnings/errors; format/diff passed | First-invocation post-commit pass. Earlier failed attempts and test-isolation investigation are retained locally. |
| Packaging helpers | **20 portable + 15 session checks** passed | Separate helper cases, not additions to the xUnit count. |
| Portable construction/extraction | 11 publish checks, all **416 payloads**, 9 independent extracted checks | Exact retained candidate/source/hash are in [packaging](packaging.md#recorded-candidate). Startup is not complete interactive acceptance. |
| Settings/native recovery | Real Save failure/retry, notices/log-directory recovery and normal session cleanup passed; separate candidate-assembly probe passed **21 assertions**, with 25 focused regressions | Native Save used an unchanged draft. Edited-draft preservation/controlled Win32 branch have separate integration and later user evidence; actual shell-launch failure after directory creation was not natively induced. |
| Operation/publication boundaries | **499 focused tests** (372 App + 127 core) and **21 actual-publisher assertions** passed; native real file failures/retries, rerender cancellation/retention and playback-error/publication separation passed | These overlap the full suite or are separate probe assertions, not 520 new unit tests. Native sustained image-busy/live-copy timing was inconclusive in the agent run; the user subsequently reported the discussed manual remainder working. |
| Integrity/cleanup | Native operation checks preserved input hashes, all extracted payload hashes and the ZIP; observed sessions removed on normal close | Scoped isolated checks, not proof of every external storage/process condition. |

## Manual observations and attribution

- **Responsive UI:** user reported the supplied minimum/smooth-resize, working/cancelling/Ready/Exported, centering/actions/long-text/scroll checklist working. Exact dimensions, per-state/media results and executed bytes were not restated; this is not a complete measured DPI matrix.
- **Settings/appearance:** ordinary and recovery checks were reported working, including all six accents, Header/About, Cancel/Escape/title-close, Reset, Save/restart and paths/notices. Reported display scales were 100/125/150%, with positive independent accessibility text-size feedback and second-monitor/fullscreen use. Exact text percentage and immutable local-build identity were not supplied for every report.
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
