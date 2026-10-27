# Testing and verification

GlyphFrame separates deterministic logic/component tests, real media/file integration, native application checks and human observations. A passing fixture or test double is not presented as a human playback test or clean-machine certification.

## Run the automated checks

Use Windows x64 with the .NET 10 SDK and access to the configured NuGet sources. The full suite includes real FFmpeg integration: initialize [pinned local tools](../Tools/ffmpeg/README.md) explicitly first. These tests are not silently skipped when tools are absent. A fresh image-only source build is a separate check, not a full-suite pass. From the repository root:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -Download
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
pwsh -NoProfile -File scripts/Test-PackageLegal.ps1
pwsh -NoProfile -File scripts/Test-SourcePreparation.ps1
```

Publishing/startup validation is an explicit separate operation; see [packaging](packaging.md). Source-preparation helpers use inert synthetic ZIP entries, check pinned hashes/path policy and refuse overwrite; they do not download or execute FFmpeg. Source snapshot verification excludes Git history and runtime payloads; it does not certify secret absence or binary-release licensing.

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

Current summary as of 2026-10-27; opening this document does not execute tests. Full attempts, previous baselines and superseded package identities remain in local archives.

| Check | Recorded result | Boundary |
| --- | --- | --- |
| Automated implementation | **1,283 Release tests**, 488 core/infrastructure/rendering + 795 App; zero failures/skips; Release build with zero warnings/errors; format passed | Rerun for the XmpCore notice correction on parent `ed62872`; dirty source, not clean-source release acceptance. Five additional cases cover exact XmpCore document binding, preserved unresolved gates and missing/changed BSD text or attributions. Earlier 1,278-test source-preparation baseline is retained in local evidence. |
| PowerShell helpers | **27 installer + 20 portable + 15 session + 17 legal + 13 source checks** passed | 92 separate checks, not additions to xUnit. Source checks cover inert extraction, altered archive/tool bytes, traversal/duplicates, missing pairs and no-overwrite/path policy; no helper-test app launch/installation. |
| Source-only preparation | Fresh history-free snapshot restored/built with no FFmpeg tools or stale output; 0 warnings/errors. Explicit fixed-archive acquisition, repeat verification and rebuilt output tool hashes passed | Local working-source snapshot, not a new Git commit/public repository or native clean-machine test. Original private Git history remains unchanged and contains older executables. |
| Local publish after tool relocation | Self-contained Windows x64 output preserved pinned tools and verified **19 legal documents / 9 dependency bindings** | Fresh local publish only; no Setup/ZIP release, native application launch or legal approval inferred. |
| Package integrity | Latest portable from clean `ed62872`: all **433 payloads**, 19 legal documents/9 dependency bindings, FFmpeg/ffprobe execution and isolated published startup/shutdown/session cleanup passed | [Latest portable identity](packaging.md#latest-portable-snapshot). No Setup built for this run; public binary licensing remains incomplete. Earlier extracted-ZIP/installed-copy observations are not transferred to these bytes. |
| XmpCore correction | **21 legal documents / 9 exact dependency bindings** verified in fresh self-contained publish; **18 focused About/notices tests** passed, overlapping the full suite | [Correction verification](packaging.md#xmpcore-collection-verification). Existing published-startup/tool/session checks passed; no new Setup/ZIP or manual notices interaction. Public-distribution approval remains false with FFmpeg and Windows-native gates open. |
| Installer lifecycle | Earlier candidates passed first install, English wizard/task defaults, registration, running-app refusals, same-version repair, native uninstall/data retention and reinstall | The earlier installed icon candidate separately passed update, all installed payload hashes, retained settings/exports and native Rose startup. The preceding paired licensing Setup was not installed, and the latest portable has no matching new Setup; [scope](packaging.md#verification-status). Not a different-version upgrade or clean-PC Setup pass. |
| Installed branding | Nine EXE/Setup frames checked; installed Shell index zero returned white 20/40-pixel icons. User confirmed Rose Save/restart/About and the visible desktop icon | **USER-REPORTED PASS** for the human observations, not an agent-observed desktop screenshot or a new DPI matrix. |
| Recovery, publication and cleanup | Real storage/publication failures and retries, render cancellation/retention, playback-error separation and owned-session cleanup exercised | Focused tests overlap the full suite; probes/native observations are separate methods. Some timing-dependent branches remained inconclusive for the agent and have scoped later user feedback, not exhaustive OS-fault certification. |

The current source collection has **21 documents / 9 dependency bindings**, including the recovered XmpCore BSD-3-Clause text and Adobe/.NET port attributions. The text was compared with all six archived Adobe legal paragraphs, and both retained source-header forms were checked against the exact .NET source tag. The existing offline notices tests verify complete display and reject missing/altered XmpCore documents. The full suite and all 92 helper checks were rerun for this correction. An initial sandbox build returned failure without diagnostics; retry with normal .NET cache access passed. The session helper's initial sandbox TEMP exceeded its deliberate path budget; normal Windows TEMP passed without relaxing limits.

The private pre-checkpoint review on 2026-10-27 repeated the Release build, all **1,283 tests / 92 helper checks**, formatting and diff checks successfully on parent `ed62872` with the pending notices/gallery changes. The private recovery bundle was restored to an isolated mirror, checked with full Git integrity verification and compared against all backed-up refs; working-file/tool copies were hash-verified. This establishes a recovery baseline, not a new commit, fresh native acceptance or a tested controlled FFmpeg build. The [planned replacement and history-preserving publication copy](portfolio.md#planned-route-preserve-development-history-in-a-reviewed-copy) remain pending.

The [latest portable ZIP](packaging.md#latest-portable-snapshot) remains the immutable 433-file/19-document candidate from clean `ed62872`, not a package of these later changes. The [previous paired 430-file/16-document candidate](packaging.md#previous-paired-candidate) is older still. Their recorded checks are not new installer/clean-PC/public-distribution acceptance for the corrected collection; any later package needs fresh source/hash binding.

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
- The portfolio gallery is prepared locally; [media provenance](screenshots/README.md) and release-owner licensing/distribution review remain separate from application tests. Screenshots and example outputs are presentation assets, not new native acceptance or performance evidence. Successful tests do not authorize tagging or publishing.

Detailed attempt journals, TRX, native captures, hashes and provisional package histories are retained in local archives rather than shipped as public documentation dependencies. Current product behavior is in [product-spec.md](product-spec.md); ownership decisions are in [architecture.md](architecture.md).
