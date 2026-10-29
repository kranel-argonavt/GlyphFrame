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
pwsh -NoProfile -File scripts/Test-NativeTerms.ps1
pwsh -NoProfile -File scripts/Test-PackageLegal.ps1
pwsh -NoProfile -File scripts/Test-SourcePreparation.ps1
pwsh -NoProfile -File scripts/Test-ControlledFfmpeg.ps1
```

Actual publish/startup checks are a [separate packaging operation](packaging.md). A fresh image-only source build without FFmpeg is not a full-suite pass. Source-export path checks are not exhaustive secret/history or licensing audits.

The 2026-10-29 source-delivery follow-up on parent `9229401` with helper/documentation edits passed **155 helper checks** (37 controlled, 18 source, 25 legal, 20 portable, 30 installer, 15 session, 10 native terms). The added controlled case first reproduced a missing application-native helper in the standalone source dependency chain, then passed after imports were moved to their actual consumers. The frozen archive's own helpers, source/patch locks and six runtime notices were also loaded/validated after extraction without compiling or running an input executable. The [delivery revision 2 checks](packaging.md#controlled-ffmpeg-delivery-candidate) do not rerun the full application suite, prior functional FFmpeg replay or native acceptance. Earlier totals below retain their original artifact scope.

## Filtered-history clean-checkout verification

On 2026-10-29, the 49-commit development baseline was preserved in a separate reviewed repository and filtered to `cef9f3e`. Both legacy FFmpeg/ffprobe executable paths were removed from every snapshot, and one exact personal temporary-root prefix was replaced in a historical document version. A bijective commit/parent mapping preserved author/committer identity, dates and message bytes; all expected historical file entries were compared. Current product files, demo media, contracts and component texts stayed unchanged. Only `main` remains, with no remotes, replace/backup refs or unreachable objects in that reviewed baseline.

A new full-history checkout of `cef9f3e`, with no inherited project `bin`, `obj` or tool cache, passed:

- Missing-tool verification rejected the absent pair as expected; a Release build succeeded with the documented image-only guidance and no FFmpeg/ffprobe in application output.
- Explicit acquisition from the exact local `.2` initializer archive, archive/tool hash checks, and a forced Release rebuild. Both builds had **zero warnings/errors**.
- **1,327 Release tests**: 488 domain/integration and 839 app/UI, zero failures or skips.
- **175 inert helper checks**: 20 portable, 15 session, 30 installer, 30 native terms, 25 legal, 18 source preparation and 37 controlled FFmpeg.
- Formatting, diff checks, unchanged tracked files and a clean working tree. The tested source identity was `cef9f3e`, not the earlier private checkpoint or a later documentation edit.

The host used Windows, .NET SDK **10.0.401**, the existing NuGet cache/configuration and the reviewed local tool archive; this is a fresh source checkout, not a clean-machine or offline-dependency certification. The initial sandboxed clone was blocked by Git/MSYS process permissions before building; a separately located unrestricted retry passed. No application test failure required a code change. Native interaction, installing/updating Setup, operative agreement, new portable/Setup creation and public source availability were not exercised. Historical package identities and their native/user evidence below retain their original scope; they are not relabeled to the filtered commits.

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

## Adopted component-terms 1.0 verification

The owner explicitly adopted the [exact edition 1.0](licensing.md#prepared-release-edition-10) on 2026-10-29. The initial edition-1.0 private working-tree verification on parent `6d443a0` passed **1,321 Release tests** (488 domain/integration + 833 app/UI), including **33 focused native-terms cases**, plus **162 helpers** (37 controlled + 18 source + 25 legal + 20 portable + 30 installer + 15 session + 17 native-terms). Formatting and the forced zero-warning/error rebuild passed. All eight substantive sections and the reviewed runtime/reference scope are unchanged.

Positive protocol tests use inert synthetic components/terms, not acceptance of the actual package. Regressions cover rejected old preview purposes, changed editions/scopes, unadopted manifests, draft substitution, decline preservation, persistence failures and unchecked compiled dialogs. A constrained-height/enlarged-text failure led to an explicit readable terms minimum; an old draft-label assertion and a renamed Windows-native open-issue label were corrected. The initial restricted .NET attempt could not complete; the unrestricted reruns passed. Full attempts remain in local diagnostics, not as extra public journals.

The current package verifier cannot synthesize acceptance. For the [private edition-1.0 pair](packaging.md#private-edition-10-candidate), package/tool integrity and responsive fresh pending/decline passed, without acknowledgement/cache. The owner's subsequent scoped retest is recorded below, separately from automation. Instrumented session/lease cleanup, installed/portable operative-record interoperability and the new Setup's installation lifecycle are not established by that retest. Earlier clean-PC and preview checks retain their original scope. No public distribution approval follows from these results.

### Owner-reported edition-1.0 retest

The owner identified **`cfc94837 / ZIP 02389d19`**, matching the recorded private edition-1.0 pair, and reported all five requested checks **USER-REPORTED PASS**, with no problems or omitted checklist items: readable terms/reference viewers; Decline/Escape/title-close; explicit agreement and restart without a repeated prompt; image/video render and export; and the supplied Setup's terms page and Cancel before Install.

This closes the requested short manual retest within that scope. It is a trusted owner report, not agent-observed native interaction, an independent full-hash download verification or authenticated proof of acceptance. No screenshots/state snapshots, new Windows/build/DPI details or instrumented lease evidence were supplied for this run. The requested isolated-profile workflow was provided; actual record bytes were not independently captured. Automated verifier reports remain unchanged: they did not create operative agreement.

**Not exercised by this checklist:** installing/updating/uninstalling the new Setup, installed/portable record sharing or operative silent-mode behavior. The Setup check stopped at Cancel, not Install. Do not reopen every earlier functional test merely because these affected checks are now complete; current public-delivery/history gates remain separate.


### Profile-validation parity follow-up

Precommit review reproduced three malformed but hash-rebound profiles accepted by PowerShell and rejected by the application: string schema, numeric edition and an extra profile framework. The helper now uses the same typed `System.Text.Json` getters, exact framework/collection shape and bounded strict-UTF-8 text reading as the native reader, rather than PowerShell coercion. Six malformed-profile cases are covered in both C# and helper tests. An invalid-scope dialog has a distinct failure title; the verifier's tested pending-state assertion rejects that title, unrelated windows, existing acknowledgement and media state. It never accepts the actual terms.

After an expected red helper regression, **1,327 Release tests** passed (488 domain/integration + 839 app/UI), including **39 focused native-terms cases**, plus **175 helpers** (37 controlled + 18 source + 25 legal + 20 portable + 30 installer + 15 session + 30 native-terms). The three original probes now reject all malformed inputs; formatting, zero-warning/error forced rebuild and document/hash checks passed. The unchanged `cfc94837` payload also passed the stricter checker. The [corrected private pair](packaging.md#corrected-profile-validation-candidate) passed 447-payload packaging and valid pending/decline. A separate malformed copy showed the expected failure title and was rejected by the pending classifier, then closed with exit zero and no acknowledgement/cache; this was script-observed, not visual/native DPI acceptance. These results do not retest every native workflow or replace the owner's separately recorded package identity. Terms/profile hashes and all substantive clauses remain unchanged; no synthetic operative acknowledgement or public approval is created.

## Windows-native preview verification

The later implementation-preview work is separate from the clean-source baseline below. On private parent `66e8d99` with uncommitted changes, **1,315 Release tests** passed (488 domain/integration + 827 app/UI); 27 cover the new bundle/record/compiled-dialog paths. Initial restricted testhost connection failures were followed by an unrestricted run that exposed and corrected exception handling/test expectations. WPF fixtures cover unchecked/default/invalid-scope behavior, write failure/decline and constrained-height/enlarged-text reachability; these are not native DPI acceptance.

The fresh preview portable verified **447 payloads, 32 legal documents and 9 dependency bindings**, including the nonoperative terms/profile. All **154 helper checks** passed (36 controlled + 18 source + 25 legal + 20 portable + 30 installer + 15 session + 10 native-terms); an initial AppContainer file-move denial was rerun outside that restriction. Fresh pending/decline produced no acknowledgement/media cache. A separately labeled synthetic nonoperative profile entered the workspace, held the observed exclusive session and exited with cleanup. The first accepted-profile probe lacked a complete window observation and failed graceful-close verification; the corrected probe passed. This is not human/legal assent. Setup compilation succeeded after recorded Inno syntax/API corrections; compilation and static policy assertions are not an installed round-trip, silent-mode native pass, downgrade or uninstall acceptance.

The initial agent-assisted normal-name native app inspection remained blocked after one refreshed retry by the computer-use helper's installed-app identity collision. Its owned empty process was closed, no session/record remained, and ordinary installed-app/settings/acknowledgement state was unchanged. The Setup preview page was inspected natively: full text, initially negative choice/disabled Next, optional notices with Escape, affirmative review enabling Next and transition to Tasks before Install without a record. The owner authorized Cancel/Yes; Setup logged normal deinitialization without installing or changing ordinary state. That initial check covered page/decline behavior, not installation or silent-scope acceptance. An AppContainer-redirected ordinary-profile comparison was discarded and repeated against the actual baseline profile. Detailed attempts remain local; the subsequent human-run protocol below is separate evidence, not a claim that agent targeting was fixed. Operative wording, author obligations and public approval remain [open](licensing.md#decisions-before-operative-distribution).

### Other-PC manual preview protocol

On 2026-10-29, the owner completed **A0–A6 and B0–B4** using the [exact working-tree preview pair](packaging.md#manually-tested-preview-candidate) on another AMD64 PC: Windows 11 Home/Core, 25H2, **26200.8246**. The agent reviewed 19 state snapshots and two silent-result summaries; UI behavior is **USER-REPORTED PASS**, not agent-observed interaction. Screenshots were not supplied. Baseline observations showed no registered/default installation, acknowledgement, settings or session directories. `dotnet` was already found, so this is not a proven runtime-free machine. Display/text scales were not recorded for this run.

| Scenario | Result and evidence |
| --- | --- |
| A0–A2: fresh profile, silent refusal, interactive cancellation | Silent exit **1** plus the exact refusal marker, not merely a nonzero exit; record/settings/default installation remained absent. The owner confirmed initial negative choice, disabled Next, text viewers and Cancel/Escape/title-close. |
| A3–A4: installed and portable first use | Owner confirmed no duplicate same-scope prompt, About/notices, saved accent/restart, synthetic-video render/export and draft Reset/Cancel. Snapshots bind the expected installed apphost and unchanged shared record/settings/export after portable use. |
| A5–A6: same-version repair, uninstall/reinstall | Acknowledged silent repair exited **0**. Default apphost/registration disappeared after uninstall and returned after reinstall; record/settings/export hashes remained identical. Owner confirmed retained accent and portable/installed startup. This is not a different-version upgrade. |
| B0–B2: missing, corrupt and foreign records | Owner confirmed preview without workspace and successful decline paths. Missing record stayed absent; both negative record hashes exactly match the supplied fixtures after decline. |
| B3: persistence failure and recovery | Two snapshots identify a directory obstacle instead of a record. Owner confirmed app/Setup refusal, successful app retry after removing only the empty obstacle, restart and App-to-Setup reuse. The new record hash is intentional; settings/export remained unchanged. |
| B4: invalid profile in a separate portable copy | Owner confirmed disabled affirmative controls/no workspace for the damaged copy and normal original/installed startup. Final shared-state hashes remained unchanged. Damaged-copy contents/UI were not independently captured. |

All final observations show zero media-session directories. This does not independently prove exclusive-lease ownership or process cleanup; earlier positively observed lease checks retain their separate scope. The foreign zero-hash record and invalid profile are negative fixtures, not a real valid changed-runtime upgrade. Raw reports, backups and diagnostics remain local; no acknowledgement/settings file or test kit belongs in Git. These results do not close operative terms, Publisher obligations, the DPI/OS matrix or a future clean-source/publication-copy release check.

### Owner clean-PC Setup confirmation

On 2026-10-29, the owner explicitly confirmed having performed the clean-PC Setup check successfully, with behavior consistent with the earlier completed protocol. Record this as **USER-REPORTED PASS**, not as a wholly untested scenario. The existing hash-bound A/B evidence above remains valid within its original scope. The additional confirmation did not identify another Setup checksum, Windows build or prerequisite inventory, so it is not assigned to a different package or used to certify a runtime-free machine. No repeat of the completed checklist is required merely because screenshots or agent-native interaction were not supplied. A later materially changed/final package still needs its own appropriate artifact binding.

## Recorded verification snapshot

The subsequent clean-source preview verification of **`9229401`** passed **1,315 Release tests and 154 helpers**, with all 471 checkout files unchanged, a source-only build before explicit tool initialization, zero-warning/error rebuild and formatting/diff checks. Its independently extracted normal-name app passed pending/decline and a separate synthetic nonoperative workspace/observed-lease cleanup check. [Exact new ZIP/Setup identities and limits](packaging.md#clean-source-preview-9229401) are separate from the owner's earlier A/B run and the older baseline below. The later source-delivery follow-up's 155 helpers are a separate run, not additive to those 154.

Historical clean-source baseline as of 2026-10-28. Reading this document executes no checks. That run used a fresh independent checkout of private commit `e43fadad905deafeb636be1e9a7aec830984f918`, with `WorkingTreeDirty=false` throughout build/tests/packages. The full suite, helpers, formatting and packaging were rerun; overlapping precommit results are not additive. Later implementation/documentation work does not relabel these artifacts. This single-commit verification clone is not the planned history-preserving publication copy or a public release.

| Check | Result | Scope |
| --- | --- | --- |
| Release xUnit | **1,288 passed: 488 core/infrastructure/rendering + 800 App**, zero failures/skips | Exact controlled `.2` version and both executable hashes; includes missing/altered notices and manifest-binding regressions |
| Helper regressions | **141 passed: 36 controlled + 18 source + 25 legal + 20 portable + 27 installer + 15 session** | Synthetic policy/integrity checks, not native acceptance |
| Build and formatting | Release build/rebuild: **0 warnings/errors**; formatting and diff checks passed | .NET application build, not a claim that upstream FFmpeg compilation was warning-free |
| Source-only build | **459-file** hash-verified fresh checkout built without a tool cache or FFmpeg EXEs in output; 0 warnings/errors and missing-tool guidance observed | Built before explicit tool initialization; build-only, not full runtime/image-only or clean-machine acceptance |
| Current package | **445 payloads / 31 legal documents / 9 dependency bindings**; exact tools/source-material/twelve-notice binding; published startup, exclusive session, normal exit and cleanup passed | [Exact ZIP/Setup identities](packaging.md#current-local-controlled-tool-candidate); unsigned Setup built but not installed/run |

### Current extracted-ZIP native smoke

The clean-source ZIP passed independent extraction and all **445 path/length/hash** comparisons before and after startup. Direct process control launched the original `GlyphFrame.App.exe`, without an alias, using a separate marked profile. A responsive window and exclusive production-session lease were observed; normal close returned zero and removed the observed session, leaving no session directories. The extracted legal collection and controlled-tool binding passed again. Ordinary settings and installed app/tool hashes remained unchanged.

This is an extracted startup/session smoke, not a repeat of native render/export, playback/listening, PCM fallback, DPI, Setup lifecycle or clean-PC acceptance. It did not exercise the computer-use helper, so the earlier identity collision is not declared fixed. See [the clean-source candidate](packaging.md#current-local-controlled-tool-candidate) for exact identities.

### Earlier controlled-tool native smoke

The preceding dirty-source candidate's fresh extraction and separate marked profile passed About/notices, image/video render and native Save. The viewer exposes the summary plus all **30 appended document headers**, the controlled version and IJG credit. Actual saved outputs fully decoded: one 320×240 PNG and two six-second 640×360/180-frame MP4s. Keep Audio preserved **283 AAC packet payloads**; Remove Audio produced no audio stream.

Rendered playback/end/seek/fullscreen/Escape, stale export blocking after audio changes, cancelled/successful replacement and active 900-frame-render cancellation passed. Only the lease remained after Cancel. Normal close returned zero, removed the owned session and preserved all 445 payloads, ordinary settings and installed EXE/tool hashes.

The normal-name app started but the computer-use helper twice misbound its window to the installed app identity. Only that empty owned test process was terminated; its nonzero exit/retained session is diagnostic evidence, not a cleanup pass. Native interaction used an additional **byte-identical renamed apphost** in a separate extraction. This is a targeting workaround, not a product fix or a normal-name helper pass. No human listening, renewed native PCM fallback, fresh DPI matrix, Setup lifecycle or clean-PC acceptance is inferred. These observations remain bound to the [initial controlled-tool rollout](packaging.md#initial-controlled-tool-rollout), not the new clean-source package.

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
- The selected private commit now has a clean-source build/test/package binding and fresh extracted startup. Remaining affected native checks must still identify their actual artifact; older local-build/installed/user results do not automatically cover new bytes or a later publication-copy rebuild.
- The preview pair's scoped other-PC lifecycle protocol is complete as recorded above, not a general release certification. Different-version upgrade/downgrade, a real valid changed-runtime scope and native DPI/enlarged-text checks are not covered by that run. No current report proves absence of preinstalled runtimes; Windows/codec compatibility stays bounded.
- [Media provenance](screenshots/README.md), [licensing/distribution](licensing.md) and [history preparation](portfolio.md) remain separate from successful tests. No test authorizes a tag, upload or public release.

Current product behavior is in [product-spec.md](product-spec.md); ownership decisions are in [architecture.md](architecture.md). Detailed local evidence and superseded artifacts are retained rather than embedded in public guides.
