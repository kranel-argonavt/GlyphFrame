# Phase 15 — Windows x64 portable packaging and validation

Current status (A1/A2 evidence recorded 2026-10-22): Phase 15A packaging and A2 committed-source/extracted-startup verification passed for clean `1ddbab1`; interactive Phase 15B acceptance remains partial/open and Phase 15C clean-machine execution is NOT TESTED. Earlier UI verification is consolidated in [UI refinement history](UI_Refinement_History.md); the original October 7/8 extracted-package findings below retain their attribution. Current outstanding work is tracked in the [release acceptance checklist](Phase_14_Coverage.md#release-acceptance-checklist).

The pre-image-navigation UI implementation is committed in `75aad73`, with aligned documentation in `1ddbab1`. The fresh A2 package below was actually built and independently extracted/launched from clean `1ddbab1` before recording these results. No native visual/media-workflow or clean-machine pass is inferred; the older candidate remains a separate historical identity.

## Latest recorded candidate

The latest provisional image-navigation correction is run **`fbd50cd47e264d57b0c515ec2ae6071e`**, Release/win-x64/self-contained 1.0.0, recorded under [image-navigation package provenance](#image-navigation-package-provenance). Its source parent is `1ddbab13f354914d7951aa81ed819a1ffc051aca` with **WorkingTreeDirty=true**; that commit does not contain these changes. It is separate from the clean A2 package below; startup/payload verification and interactive native acceptance have different scopes. Final committed-source packaging/retest remains necessary before release acceptance.

## Recorded A2 committed-source candidate

This identifies the October 22 A2 package, not a public release. Detailed local run IDs, absolute paths, process/session evidence and logs remain in ignored artifacts and are not dependencies of a public clone.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained / untrimmed folder publish |
| Source commit | `1ddbab13f354914d7951aa81ed819a1ffc051aca`; clean working tree/index at build and extracted validation |
| ZIP | `GlyphFrame-1.0.0-win-x64-portable.zip`, 148,737,136 bytes |
| ZIP SHA-256 | `2fb83a3e3adf3ee3ff9d4a6041a14d98273ab7cb92f99561e7ba3d355b1f981b` |
| A1 baseline | 1,177 tests passed, zero failures/skips; Release build zero warnings/errors; format/diff and 35 separate helper checks passed after retained environment retries |
| Package checks | 11 publish structural/tool/startup/session checks; 416 staged/archive payload paths/lengths/hashes verified; PDBs excluded |
| Independent extracted verification | 9 checks passed: actual ZIP checksum/archive recheck, all 416 extracted files, version/README, exact extracted EXE, responsive window/3-second observation, positively observed PID-bound exclusive lease, normal exit 0, exact session removal/zero remaining sessions, unchanged ZIP/extracted hashes and clean source |
| Retention | ZIP/checksum, manifests/reports, fresh extracted folder and marked isolated data retained locally; no old run reused or broad cleanup |
| Acceptance boundary | A2 PASS; native visual/media workflows A3–A7 and clean Windows A8 remain open; no publication or licensing sign-off |

The application was launched with a hidden-window request only for bounded automated startup; no screenshot, audible playback, rendering/export interaction, resize or DPI acceptance is claimed. The verifier closed only its own process, without forced termination. Normal user-local preferences/logs/cache were not used. No failed product check occurred in A2. The recording changes to this document and the checklist were made afterward and do not relabel the package as built from a later documentation revision.

### Historical October 21 candidate — superseded

The previous latest-candidate record is retained below with its original source identity, hash, checks and limitations. It is not the October 22 A2 package.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained |
| Run | `fb87586d2b6d48da829c8ea46c3e7b60` (October 21 notices-button follow-up) |
| Base commit | `5fb66ace4378a5818104be95f1ea08a9bdfecd91` — dirty-tree build; the base does not contain the UI changes |
| ZIP SHA-256 | `A50BF71B056B6D3432EC0D0227CC843400E42145724FC9BA3819AAD41086F4FA` |
| Local package directory | `artifacts/portable-packages/fb87586d2b6d48da829c8ea46c3e7b60/` (ignored; may be absent in a clone) |
| Package checks | 11 structural/tool/startup/session checks and 416 payload paths/lengths/hashes passed |
| Focused validation | 13 About/notices/logo regressions, Release compilation, formatting and diff checks passed |
| Native limitation | A separately extracted instance exited before targeting; cause/exit code unestablished. No native visual pass for this style-only candidate. |

The later independent repository audit reran all 1,177 tests successfully, but did not regenerate this package or certify its native UI. Earlier About/footer/action-bar native evidence belongs to the preceding candidate, not automatically to these latest bytes. Detailed local provenance remains under `artifacts/notices-button-20261021/` and the run's package/publish reports. No release acceptance is declared.

Phase 15A implements distribution construction and structural verification only. It does not publish a release, prove extracted-ZIP interaction (15B), prove clean-machine compatibility (15C), or close broader Phase 14 testing. The initial repository was clean at `540ae20` (`test: expand verification and fix review UI regressions`). Implementation packages are explicitly dirty-tree builds; that commit does not cover their uncommitted changes.

## Command and prerequisites

Windows, PowerShell **7**, .NET **10 SDK**, Git, bundled committed tools, and access to configured NuGet sources/runtime packs are required for building:

```powershell
pwsh -NoProfile -File scripts/Build-PortablePackage.ps1
```

From outside the repository, supply the absolute script path. All source/output paths resolve from `$PSScriptRoot`, not the caller's directory. No system PATH, installed software, Windows settings or normal GlyphFrame profile is modified. Application startup uses the existing short, marked `%TEMP%\GFpkg\<run-id>` data root, outside-repository working directory and bounded owned-process lifecycle.

## Extracted-ZIP validation rules

Phase 15B must launch the EXE from a freshly extracted, checksum-verified ZIP, not from `bin/`, publish/staging, or an older package. Validate every extracted payload path, byte length and hash against the package manifest before launch. Use a fresh run-owned working directory outside the repository and an explicit isolated marked verification data root:

| Purpose | Path template / rule |
| --- | --- |
| Working directory | `%TEMP%\GF15b\<run-id>`, outside the repository |
| Extracted application root | `%TEMP%\GF15b\<run-id>\zip\GlyphFrame-<version>-win-x64` |
| Application to launch | Extracted application root + `\GlyphFrame.App.exe` |
| Verification data root | `%TEMP%\GFpkg\<run-id>`, with the required `.glyphframe-package-verification` marker |
| Launch isolation | Pass the marked absolute data root through `--verification-data-root`; do not use the normal user profile |
| Session verification | Positively observe the launched PID's production session and exclusive lease; after normal close, verify that exact session was removed |

`<run-id>` and `<version>` are placeholders, not literal directory names. The harness creates and validates fresh directories and the required marker before launch; the marker alone does not replace its path/URI safety checks. Never reuse an old run or recursively remove a broad temp/user-data root.

This document keeps reusable rules, source revisions, dated outcomes and limitations. Exact run GUIDs, absolute paths, PIDs, session names, EXE/DLL/ZIP hashes and detailed capture metadata belong in the run's `identity.json`, packaging reports and smoke evidence under ignored `artifacts/`. Dated local evidence links below are retained for traceability; those files are not shipped in Git and may be absent in a fresh clone. Simplifying this document does not modify or delete the original evidence.

## Contract and output

One fresh publish of `src/GlyphFrame.App/GlyphFrame.App.csproj`: Release, `win-x64`, self-contained, `PublishSingleFile=false`, `PublishTrimmed=false`. `Verify-PublishPackage.ps1` remains independently runnable; the packaging command invokes it once with a fresh run GUID and links its report. A shared helper supplies the required-file list rather than duplicating dependencies. Existing CLR/WPF/Skia, committed FFmpeg/checksum/attribution, tool-version execution, active production lease, path/URI budget and graceful session-removal checks remain mandatory.

```text
artifacts/portable-packages/<run-id>/
├── .glyphframe-portable-package-run
├── GlyphFrame-<version>-win-x64/        validated staging application folder
│   ├── GlyphFrame.App.exe
│   ├── README.md                      concise distribution instructions
│   ├── THIRD_PARTY_NOTICES.md
│   ├── [application and runtime files]
│   └── Tools/ffmpeg/[tools, hashes, license, distribution information]
├── GlyphFrame-<version>-win-x64-portable.zip
├── GlyphFrame-<version>-win-x64-portable.zip.sha256
├── payload-manifest.json
└── report.json
```

The same run ID's fresh publish/startup evidence is under `artifacts/package-verification/<run-id>/`. Both output roots are already ignored by `.gitignore`. Existing runs are never reused. The command stages only freshly published files, excluding **all PDB debug symbols** (including Skia's large native PDB), and adds the version-substituted `docs/Portable_README.md` as `README.md`. It never archives bin/, the repository, or all artifacts/. Unknown development or runtime-data content fails the policy rather than being silently included.

Version is the published `GlyphFrame.App.dll` assembly's Major.Minor.Build, matching `AboutDialogService.FormatVersion`. Currently the SDK's existing default assembly version is 1.0.0.0, shown as v1.0.0; no new version property or override is introduced. Full assembly version and convention are recorded. A future product-version decision remains owned by the application, not the packaging script.

Portable describes **deployment**, not data storage. Users extract the entire archive, launch GlyphFrame.App.exe and keep the directory structure. Normal settings/logs/cache remain user-local under `%LocalAppData%\GlyphFrame`. Removing the extracted application after closing it does not remove preferences, logs or separately saved exports. The verification-data-root switch is explicit, marked and test-only.

## Verification, provenance and failures

`PortablePackage.Helpers.ps1` validates required files, safe fresh run scope, canonical Windows-relative paths and content policy. Symlinks/reparse points are rejected before traversal. Logs/settings/cache, media/fixtures, tests, source/build/IDE/repository files, verification markers and smoke evidence are forbidden; OpenCV/OpenCvSharp assets remain rejected. Existing FFmpeg attribution is copied unchanged and verified by the existing verifier. No application license or legal-compliance assurance is invented. Release-owner application licensing and complete corresponding-source/GPL distribution review remain required before Phase 16 public distribution.

The payload manifest records every staged relative path, byte length and SHA-256. The ZIP uses exactly one application-directory prefix; the actual ZIP is reopened and every entry is validated for paths, unwanted content, duplicates/case aliases, file-directory conflicts, lengths and hashes. Staging is rechecked for modification. Only then is the operation-owned `.zip.incomplete` promoted to the final ZIP and its SHA-256 checksum written. No ZIP extraction is needed for these structural checks, and no extracted application is launched. File I/O is stream-based; the archive is not loaded into one memory buffer.

`report.json` records success/failure, failed or final step, only completed checks, package/assembly version, commit SHA, dirty status and status entries, Release/runtime/options, SDK/PowerShell/Windows architecture, bundled tool version lines, ZIP/checksum paths/hash/size, excluded symbols, archive-validation evidence and a pointer plus embedded copy of publish verification. Full publish tool-version output stays in that linked evidence directory. A source commit is never represented as covering uncommitted changes. Reports are evidence, not an attestation that source was immutable during an externally modified build.

Failures return a nonzero script error, keep staging/incomplete archive/reports for inspection, and do not announce a successful package. No recursive cleanup is performed by the build command. The verifier's existing bounded process cleanup and checked empty working-directory removal remain; marked data is retained after its exact production session has been verified removed. Never delete a broad artifact/temp/user-data root to hide a failed check.

Reproducibility means documented commands, configuration, fresh inputs and verified payload—not byte-identical ZIPs. Entries are ordered, but ZIP timestamps/compressor/tool versions are not normalized; hashes may differ between valid runs.

## Regressions and validation

```powershell
dotnet build GlyphFrame.sln -c Release --no-restore
dotnet test GlyphFrame.sln -c Release --no-build --no-restore
dotnet format GlyphFrame.sln --verify-no-changes --no-restore
git diff --check
pwsh -NoProfile -File scripts/Test-PackageVerificationSession.ps1
pwsh -NoProfile -File scripts/Test-PortablePackage.ps1
pwsh -NoProfile -File scripts/Build-PortablePackage.ps1
```

Packaging regressions use temporary synthetic payloads and real ZipArchive/stream hash APIs. They exercise missing/empty requirements, version derivation, marked fresh output/refused reuse, forbidden/unsafe paths and content, valid ZIP layout/hashes, missing/modified/duplicate/case-conflicting/extra entries, checksum syntax/overwrite protection and truthful failed/unfinished reporting. No publish, application launch or machine codec dependency is introduced into ordinary xUnit execution.

### Implementation verification — 2026-10-06

- Release build: zero warnings and zero errors.
- Complete solution tests: **1,007 passed**, zero failed/skipped (488 GlyphFrame.Tests and 519 GlyphFrame.App.Tests).
- Formatting verification and `git diff --check`: passed.
- Existing session-helper regressions: **15 passed**. New portable-package helper regressions: **20 passed**.
- Real portable command: passed all **11** published-folder checks, including responsive startup, positive PID-bound active lease observation and graceful exact-session removal. The actual ZIP passed path/content validation and hash comparison for **416 files**. An independent reopening of the final ZIP and checksum comparison also passed.

Verified version **1.0.0** (assembly 1.0.0.0), dirty-tree source identity `540ae20c807e473b3fff6159a3632dc9f5618ba8`. SDK 10.0.401, PowerShell 7.6.5, Windows build 26300, x64; bundled FFmpeg/ffprobe 9.0 essentials build. Exact package identity and hashes remain in the linked local evidence.

- [ZIP](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/GlyphFrame-1.0.0-win-x64-portable.zip) and [SHA-256 file](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/GlyphFrame-1.0.0-win-x64-portable.zip.sha256).
- [Packaging report](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/report.json) and [payload manifest](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/payload-manifest.json).
- [Published-folder startup/session evidence](../artifacts/package-verification/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/report.json).

These artifacts are ignored, local implementation evidence, not committed distribution assets. Startup used a marked short data root following `%TEMP%\GFpkg\<run-id>`; its owned session was removed, while verifier evidence/data-root markers were retained. No live interaction with an extracted ZIP, audiovisual validation or clean-machine check was performed.

## Phase 15B — independent extracted-ZIP validation, 2026-10-07

**Outcome: FAIL / incomplete sign-off.** The clean committed-source package passed structural verification and the main image/video workflows, but native testing reproduced a fullscreen timeline mismatch and observed a tab-transfer position reset. No product code or packaging script was changed. Active artifact-publication cancellation and human audibility remain unverified. Phase 15C was not performed.

### Provenance and isolation

HEAD was independently checked as `cdb1f8eadaeda3d831c203faf62d89ed56cd8297` (`feat(packaging): implement portable Windows x64 distribution`). `git status --short` was empty before building, before extraction, and after interaction/cleanup before these documentation edits. No unrelated changes required preservation. The older dirty-tree implementation package above was not used for this run.

The exact command `pwsh -NoProfile -File scripts/Build-PortablePackage.ps1` succeeded on the host with all **11** publish-verification checks and **416** ZIP payload files verified. An initial sandbox invocation failed the prospective temporary-path budget before publish; its [failure report](../artifacts/portable-packages/66e28e4d08a24bc79596bf0c2b3cea23/report.json) is retained. Repeating the same command outside sandbox temp redirection produced the successful package below; no script was altered.

| Verification | Recorded outcome |
| --- | --- |
| [Packaging report](../artifacts/portable-packages/281c25924e604b41b9f0184aea574705/report.json) | success; linked [publish/startup report](../artifacts/package-verification/281c25924e604b41b9f0184aea574705/report.json) |
| [ZIP](../artifacts/portable-packages/281c25924e604b41b9f0184aea574705/GlyphFrame-1.0.0-win-x64-portable.zip) | checksum and all 416 extracted payload files verified; exact hashes in local reports |
| Version/configuration | 1.0.0 / assembly 1.0.0.0; Release, win-x64, self-contained, folder publish, untrimmed |
| Host | Windows NT 10.0.26300.0 x64; .NET SDK 10.0.401; PowerShell 7.6.5 |
| Bundled tools | FFmpeg and ffprobe `9.0-essentials_build-www.gyan.dev` |
| Interactive process/isolation | only the extracted EXE, outside-repository working directory and marked data root; exact identity in `identity.json` |

The actual checksum was checked before extraction. Every archive entry was validated for safe canonical paths, one application-directory prefix, conflicts and layout before any extraction write. Extraction used a fresh run-owned directory, not publish/staging. All extracted relative paths, file count, lengths and SHA-256 values matched the [packaging manifest](../artifacts/portable-packages/281c25924e604b41b9f0184aea574705/payload-manifest.json). [Identity and extraction evidence](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/identity.json) records full absolute paths, source copies, process identity and the path plan; [setup script](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/setup.ps1) preserves the procedure.

Only that extracted EXE was launched for the interactive run, with `--verification-data-root` and a marked absolute directory following `%TEMP%\GFpkg\<run-id>`. The working directory followed `%TEMP%\GF15b\<run-id>`, outside the repository. There were no competing GlyphFrame processes at launch. The launched PID's real production session was positively observed under its own cache, and an existing-file exclusive-open probe returned the required sharing violation for `.session.lease`. Completed/pending paths and their escaped URIs were within the existing 220/240 helper budgets; exact paths and lengths remain in `identity.json`. The lease was neither synthesized nor inferred from directory existence.

Native interaction used the current computer-use skill, its required references, and `@oai/sky` through the persistent Node REPL. No component renders or custom native UI helpers were substituted. Scale remained **125%** throughout; no Windows, audio-device, PATH or normal-profile configuration was changed. Default capture size was 1267×814 logical pixels. Native system-menu Size, an edge-selection key, then moving that edge below the supported bound established the actual 1024×720-DIP minimum (bounded screenshot **1011×714**, excluding invisible non-client borders). Earlier unchanged drag/resize attempts are explicitly not minimum evidence.

### Fixtures, artifacts and methods

Sources were valid originals copied to Unicode long filenames under the run's `Медіа` directory; the original files were only read. The PNG was **640×360** and the H.264/AAC MP4 **320×180, 30 FPS, 600 frames, 20 seconds**, with moving frame/time markings and mono 48 kHz audio. These were not symbolized exports or metadata-only fixtures. Their original and copied hashes are in `identity.json`. A run-owned text `invalid.png` exercised invalid input. A separate valid **FFV1/PCM MKV, 10 seconds**, derived from the original through the extracted FFmpeg, exercised runtime fallback; [fixture provenance](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/fallback-fixture.json) identifies its bytes. A 300-second stream-copy fixture was also created for possible cancellation testing, but was **not loaded**; cancellation was instead made observable by selecting the normal 1080p render workload. Its unused provenance remains [recorded](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/cancellation-fixture.json).

All publication destinations were new or explicitly replaceable run-owned Unicode files under `Експорт`. The Keep Audio accepted artifact's identity and hash were recorded while it still existed. Both first exports matched it exactly; both fully decoded with the extracted FFmpeg (exit 0), and ffprobe reported H.264 High/yuv420p, 320×180, 30 FPS/600 frames, duration 20 seconds, and AAC mono 48 kHz. Exact file identities, byte lengths and hashes remain in [accepted evidence](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/keep-accepted.json) and [image/video export validation](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/export-media-validation.json).

Remove Audio succeeded as a **new** accepted artifact. Its export matched that artifact, fully decoded, and had **zero audio streams**: [accepted evidence](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/removed-accepted.json), [export validation](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/remove-export-validation.json). The later cancellation retry accepted a complete 1920×1080/600-frame Removed-audio artifact and fully decoded it; the larger export matched the accepted hash. This larger publication also finished before an active Cancel action could be observed. Exact artifact identities, lengths and hashes remain in [retry evidence](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/cancel-retry-accepted.json) and [larger publication](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/larger-publication-validation.json).

### Scenario results

Every row below belongs to the same baseline ZIP and extracted process recorded in `identity.json`; there was no modified-package fault-injection process. **N** = native interactive observation/screenshot, **A** = automated read-only file/probe/decode/helper validation, **U** = user confirmation. Evidence names are relative to [this run's ignored evidence directory](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/); the screenshot index maps names to full paths, capture time and hashes. PASS is limited to the behavior stated in the row, not the whole audiovisual matrix.

| Scenario | Result | Expected → actual behavior | Method / evidence |
| --- | --- | --- | --- |
| Clean-source package construction | PASS | Fresh committed-source build and mandatory verification → clean HEAD and successful package, 11 checks/416 files | A; packaging report, `identity.json` |
| Actual ZIP extraction | PASS | Safe fresh extraction matching manifest → every path, length and hash matched | A; `identity.json`, `baseline-payload.json` |
| Isolated process/session/lease | PASS | Extracted process owns real leased session → launched PID, exact session and sharing violation observed | A; `identity.json` |
| Startup/version/responsiveness | PASS | No unhandled exception; correct product/version → responsive native application and v1.0.0 | N; `01-startup-default`, `04-about-v1`; isolated log contains only exercised recoverable errors |
| Settings/tools/About | PASS | Open, inspect, close → correct extracted tool paths and versions, isolated data root, both dialogs closed | N+A; `02-settings-open`, `03-settings-tools-isolated`, `04-about-v1`, `91-settings-from-minimum-owner`, `93-settings-closed-via-close` |
| Default layout | PASS | Reachable controls/actions, independent scrolling, stable allocation → observed image/video loaded, ready/exported and active video rendering without overlap | N; `08`, `09`, `13`, `14`, `24`, `30`, `32`, `51` captures |
| Actual minimum layout | PASS for observed states | Reachable preview/media/result/settings controls and pane scrolling → real minimum established; video Loaded (fallback), Rendering, Ready, Exported and image Loaded/Ready/Exported usable | N; `74`, `76`, `77`, `78`, `79`, `80`, `84`, `85`, `88`, `90`, `91` captures |
| Transient image Rendering layout | INCONCLUSIVE | Capture busy image state → image finished before returned capture; Ready transition stable, busy layout not independently captured | N; `85-minimum-image-render-outcome` (Ready, not Rendering) |
| Image load/Original/render/metrics | PASS | Decode original then reusable Symbolized result → original 640×360; initial grid 80×22/output 640×360; accepted metrics visible | N; `08-image-original-loaded`, `09-image-ready-top`, `13-image-ready-metrics` |
| Image tabs/Fit/Fill/100% | PASS | Correct view/scale without rerender → Original switching and three scale views observed; 100% physical-pixel size consistent with 125% scale | N; `10-image-fill`, `11-image-100percent`, `12-image-original-switch` |
| Image new/repeated export/decode | PASS | New run-owned outputs retain result → two identical PNG exports, both fully decode to 640×360 | N+A; `14`, `15`; `export-media-validation.json` |
| Image Export Again/Open Folder reachability | PASS | Accessible retained-result actions → enabled and reachable at default/minimum | N; `16-image-export-actions-reachable`, `90-minimum-image-export-again-folder` (Open Folder not invoked) |
| Image chooser cancellation | PASS | Preserve settled workflow/review → Image Exported and prior outcome retained | N; `17-image-chooser-cancelled` |
| Image declined overwrite | PASS | Preserve existing export bytes → cancellation guidance, retained result and identical before/after bytes | N+A; `18`, `19`; `image-overwrite-preservation.json` |
| Image setting/stale/rerender | PASS | Old result truthful, export blocked until rerender → font 10→12 showed Render Stale; rerender recovered to grid 64×18/output 640×360 | N; `20-image-stale`, `21-image-rerender-recovered` |
| Image minimum export/decode | PASS | New output agrees with accepted resolution → 1920×1080 PNG fully decoded; retained result/actions reachable | N+A; `89`, `90`; `minimum-image-validation.json` |
| Original input metadata/frame | PASS | Trusted metadata and actual frame → H.264/AAC 20-second original, 30 FPS/320×180 and visible frame/time marks | N+A; `24-video-original-ready`, `identity.json` |
| Original continuous Play/Pause | PASS visually | Continuous WPF frame advance and updated pause → actual moving frame, pause at ~15 seconds | N; `25-original-continuous-playing`, `26-original-paused`; audibility excluded |
| Original click seek/thumb drag/scales | PASS | Paused frame/time follows input; Fit/Fill/100% available → ~10.2s click and ~7.5s drag visibly changed frame; scale views observed | N; `27`, `28`, `29`, prior 100% Original capture |
| Keep Audio full render/accepted metrics | PASS | Complete playable artifact, accepted values → Video Ready, 600 frames, 320×180, Audio Original; H.264/AAC confirmed | N+A; `32-video-ready-metrics`, `keep-accepted.json` |
| Symbolized Play/Pause/seek/replay/scales | PASS visually | Review accepted video and seek/replay → changing frames, paused middle seek, replay; Fill/Fit/100% inspected, oversize scrolling at 1080p | N; `35`, `38`, `47`, `70`, `71`; audibility excluded |
| Tab switch stops outgoing playback | PASS visually | Both transports remain paused → Original active→Symbolized paused→Original paused; no automatic playback restart observed | N; `63`, `64`, `65`; absence of overlapping audio requires U |
| Tab timestamp transfer | FAIL | Retain/clamp outgoing timestamp → Symbolized showed ~0:15 paused, returning Original showed 0:00/frame 0 | N; `64-tab-switch-paused-symbolized`, `65-original-remained-paused`; defect B2 |
| Mute/unmute/volume UI | PASS for UI response | Controls toggle and volume thumb follows drag → mute/unmute actions and increased volume displayed | N; `48`, `49`, `50`; actual sound requires U |
| Fullscreen/Escape, first completion | PASS | Middle state agrees, natural end retained → 0:10/frame/thumb/fill aligned, end 0:20/Replay, Escape retained final embedded state | N; `35`, `37`, `39`, `40` |
| Repeated historical fullscreen scenario | FAIL | Repeat middle entry and completion/return consistently → second entry retained midpoint frame but reset time/thumb/fill to zero; later natural end/embedded return visually consistent | N; `41`, `42`, `43`, `44`, `46`; defect B1; not reported fixed |
| Two Keep Audio Unicode publications | PASS | Copy accepted bytes twice without render → both SHA-256 hashes equal accepted artifact while it still existed | N+A; `51`, `52`; `export-media-validation.json` |
| Codec/dimensions/duration/audio/full decode | PASS | Complete correct output → H.264 320×180/20s + AAC, both exports full-decode exit 0 | A; `export-media-validation.json` |
| Video chooser cancellation | PASS | Prior review/export outcome preserved → Video Exported retained, settled footer | N; `53-video-chooser-cancelled` |
| Video declined overwrite | PASS | Existing bytes unchanged, review retained → same exported bytes, settled retained review | N+A; `54`, `55`; `video-declined-overwrite.json` |
| Video confirmed overwrite | PASS | Replace only own test file with reviewed bytes → confirmed replacement, exact accepted bytes, retained review | N+A; `56`; `video-confirmed-overwrite.json` |
| Remove Audio stale/new render/export | PASS for artifact/visual review | New accepted Removed result and no audio → stale old metric remained Original until success; new result Removed, matching silent-stream export | N+A; `57`, `59`, `60`, `61`; `remove-export-validation.json` |
| Initial short render cancellation attempt | INCONCLUSIVE | Cancel active full render → workload completed before Cancel could take effect; no cancellation PASS assigned | N; `33-rerender-active`, `34-cancel-outcome`; first indexed Cancel action also unavailable in capture cache |
| Active full-video cancellation | PASS | Expected cancellation, responsive app, retained file and cleanup → 1080p workload showed frame 27/600; Cancel yielded Video Rendering Cancelled, unchanged prior artifact, zero pending files | N+A; `66`, `67`; `render-cancellation-files.json` |
| Retry after render cancellation | PASS | Successful same-workload retry → new Video Ready 1920×1080/600 frames/Removed; complete FFmpeg decode | N+A; `68`, `69`; `cancel-retry-accepted.json` |
| Active artifact-publication cancellation | NOT TESTED | Observe and cancel a running copy → 1.96 MB and later 36.25 MB publications finished before an active Cancel action was observable; chooser cancel is separate | N; `51`, `77`; larger attempt inconclusive, no cancellation/partial-cleanup success claimed |
| Invalid input from clean workflow | PASS | Recoverable matching input error/details → InvalidImage / Media input, dialog closed, app survived | N; `06-invalid-clean-start`, `07-invalid-details` |
| Invalid replacement after success | PASS | Retain accepted result plus matching current details → image result/Export Image remained; InvalidImage / Media input details closed normally | N; `22-invalid-replacement-retained`, `23-invalid-replacement-details` |
| Original runtime fallback | PASS | Unsupported native playback leaves frames/processing usable → valid FFV1 timed out; fallback seek changed burnt frame; Preview Frame/Render Video enabled | N+A; `82`, `83`; `fallback-fixture.json`; not continuous FFV1 playback evidence |
| Symbolized playback-error/publication interaction | NOT TESTED | Safely reproduce accepted-review playback error and publish → no natural Symbolized failure observed; accepted artifact was not corrupted/deleted, no unrelated error substituted | N; baseline remained intact; Original fallback is a different context |
| Human audibility | NOT TESTED | U confirms Keep Audio, mute/unmute/volume, Remove Audio silence and no overlap → confirmation requested during run, no response available | U unavailable; no historical confirmation reused |
| Normal close/exact cleanup | PASS | Only test process closes and exact observed session disappears → normal native close, process exited, exact session removed, zero remaining session directories | N+A; `native-normal-close.json`, `cleanup-preservation.json` |
| Package/source preservation | PASS | Baseline and original media unchanged → 416/416 payload paths/lengths/hashes unchanged; ZIP and originals/copies unchanged; generated fixtures also unchanged | A; `cleanup-preservation.json`, `fixture-preservation.json` |
| Clean-machine execution | NOT TESTED | Separate Phase 15C Windows without installed .NET/FFmpeg → outside this run; no software uninstalled | No 15C evidence |

### Defects and follow-up

**B1 — P2, fullscreen timeline/frame disagreement; blocks Phase 15B sign-off.** Reproduction on the baseline extracted PID: render the 20-second original with Keep Audio; pause/seek near 10 seconds; enter fullscreen; play to natural completion; Escape; click the embedded timeline near its middle (frame visibly updates, time ~0:10); enter fullscreen again. The second fullscreen entry displays the same midpoint frame but **0:00**, thumb at start, and empty filled track. The mismatch remained on a second observation. [First correct midpoint](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/37-fullscreen-midpoint-controls.png), [reproduced mismatch](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/42-second-fullscreen-timeline-mismatch.png), [matching zero-position accessibility capture](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/42-second-fullscreen-timeline-mismatch.json). Playing afterward visually reached 0:20/Replay and Escape returned a consistent final state. This later recovery does not turn the failed entry into PASS or establish a fix. Required follow-up: repair position/frame synchronization across completed→seek→fullscreen reopen, then independently repeat the full cycle on a new committed-source package.

**B2 — P2, tab timestamp transfer resets Original; separate observed transport symptom.** After Remove Audio review, switch to Original, seek to ~7 seconds, start playback, switch to Symbolized, then back to Original. The saved Original playing capture shows ~7.4 seconds; the next Symbolized capture is paused at 15.246 seconds. Returning Original remained paused but showed **0:00/frame 0**, contrary to transfer from the outgoing Symbolized position. The exact Original position at the intervening tab click was not captured. [Original playing](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/63-original-playing-before-tab-switch.png), [outgoing/target state](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/64-tab-switch-paused-symbolized.png), [reset Original](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/65-original-remained-paused.png). This was observed once; its reproducibility/root cause and relationship to B1 are not established. Required follow-up: investigate reopened Original position restoration, then repeat switching before/after natural completion. Neither defect was fixed during validation.

No safe natural Symbolized playback failure was reproduced. Missing-tool/modified-package injection was not used. An observable long artifact-copy cancellation, its retained-review/retry/partial cleanup, and human sound confirmation remain follow-up requirements. Image Rendering was too brief for independent busy-state layout capture. Cross-monitor and other scale settings were not exercised or changed. These limitations are not replaced by historical tests or automated product-unit coverage.

### Evidence integrity, cleanup and readiness

[Screenshot index](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/screenshot-index.json): **102 native PNGs, 97 distinct hashes**, each saved from the explicit current returned WindowState rather than a closure over the initial screenshot. Saved defect/minimum captures were reopened and visually matched the returned state. Duplicate settled completion/mismatch frames are identified, not used as independent scenario proof. Capture 36 is a main-window crop under fullscreen; capture 37 targets the actual fullscreen window. Some accessibility text lagged screenshots (notably 44/45); final completion/Replay is a visual observation there. Capture 80/81 still shows opening, so only the later 82/83 fallback/error captures establish fallback. Capture 92 was renamed to `92-settings-escape-attempt-still-open` after inspection: Escape did not close Settings, whereas its native Close control did (93). It is not counted as close evidence.

[Cleanup/preservation evidence](../artifacts/smoke-phase15b-2026-10-07-a49df0cb/cleanup-preservation.json) records normal close, PID exit, removal of the exact positively observed production session and **zero** remaining session directories. The test session cache was removed by the application; no broad root was recursively deleted. All **416** baseline extracted files and the ZIP remained identical. Original media and their Unicode copies retained their initial hashes. Seven run-owned exports remain, with no destination partial files observed; the canceled render retained only the prior artifact plus lease, with zero pending outputs. Packages, reports, marked data roots, fixtures, exports and useful failure evidence are preserved. The isolated log contains only the two exercised InvalidImage events and the FFV1 OriginalPlaybackUnavailable event. No unrelated process was terminated or source/user file modified.

The package remains structurally suitable for a **separate** Phase 15C compatibility experiment, but Phase 15B is **not complete and not a readiness sign-off** because B1/B2 and the explicit remaining checks are unresolved. A development-machine extracted launch does not prove clean Windows compatibility. No commit, tag, push, release, installer, deployment, software uninstall or product fix was performed.

## Pending validation and release boundaries

### Phase 15B corrective follow-up — 2026-10-08

The October 7 outcomes above and original PNG/JSON evidence are preserved; exact run identities remain in the local evidence rather than being repeated in this document. New [native report](../artifacts/smoke-phase15b-fixes-2026-10-08-c2fe7496/smoke-report.md), [identity](../artifacts/smoke-phase15b-fixes-2026-10-08-c2fe7496/identity.json), [capture index](../artifacts/smoke-phase15b-fixes-2026-10-08-c2fe7496/screenshot-index.json) and [shutdown evidence](../artifacts/smoke-phase15b-fixes-2026-10-08-c2fe7496/shutdown.json) are separate ignored artifacts. The requested historical standalone smoke-report file was absent; the existing dated document and PNG/JSON evidence were used instead.

**B1 controller mechanism:** native seek readback can lag the displayed paused frame. Previously every position getter read that clock, so creating another binding could expose end/zero despite a completed midpoint seek. The shared controller now retains commanded paused position, samples native progress while playing/on actual playing-to-pause/natural-end transitions, and ignores paused queued ticks. Idempotent pause preserves an existing paused seek. Surface recreation performs no seek; Replay intentionally restarts. No backend, slider, layout, ownership or export change was required.

**B2 corrective protection:** the same stale paused getter could feed zero into the next outgoing timestamp capture. Deterministic delayed-readback comparison tests now preserve the transferred timestamp in both directions and stop both timers. This is a proven controller failure mechanism, not a claim that the one October 7 native observation has been independently root-caused. New native repeated paused comparisons and post-render switching did not reproduce its reset. Exact playing-switch tolerance is still inconclusive because a screenshot before a click is not the instantaneous outgoing pause position.

New `PlaybackPositionSynchronizationTests` uses independent real player facades, a backend with separately scheduled native readback, actual `VideoReviewSurface` bindings and the production fullscreen host. Its **7 cases failed before correction and pass afterward**: three end→seek→fullscreen recreations per role, paused/playing comparison round trips, multiple surfaces/role changes without initialization seeks, drag completion/live binding, and queued paused timer suppression. Focused synchronization/player/seek selection: **52 passed**. Release: **0 warnings / 0 errors**. Complete suite: **1,014 passed (488 main + 526 App), zero failed/skipped**. Session-helper checks **15/15** and portable-helper checks **20/20** passed.

The real portable command produced a version **1.0.0** candidate with explicitly **dirty-tree** provenance and parent `cdb1f8eadaeda3d831c203faf62d89ed56cd8297`; that commit does not contain these fixes. All **416** archive/extracted paths, lengths and hashes matched. Only the extracted EXE was launched from a fresh outside-repository working directory following `%TEMP%\GF15b\<run-id>`, with short marked data root `%TEMP%\GFpkg\<run-id>`; its own production PID-bound lease was positively observed. Prospective absolute-path/URI budgets passed. Exact package/run identifiers, absolute paths, process identity, EXE/DLL/ZIP hashes and the launch command remain in the new report/identity.

Native candidate results: **three complete B1 cycles passed for each player** (natural end, Escape, paused embedded midpoint, fullscreen reopen); frame/time/thumb/fill/Replay agreed. Repeated paused tab comparisons passed; Original displayed frame 10.067 at transferred 10.1502145 (83 ms, within the trustworthy 30 FPS target's 250 ms comparison tolerance). Switching initiated after playing and after a new Remove Audio artifact produced nonzero paused targets, but exact playing-transfer timing and rapid-race certification remain unverified. Click/drag seeking, Original Replay, visual independent mute, Original scale selection, Keep Audio rendering/repeated byte-identical Unicode exports, Remove Audio replacement/export with no audio stream, chooser cancellation/overwrite decline and retained input error/Details→image recovery were observed. Default-size Image Ready/export actions were reachable with deliberate vertical scrolling. Native audibility, volume response, full scaling matrix and minimum-size/DPI validation were not inferred from controls or probes.

Shutdown passed: exact session removed, no remaining sessions/owned playback or FFmpeg processes/operation partials, all extracted payload and original/copied fixture hashes unchanged. All saved screenshot hashes were checked; saved 12/22/59 were reopened and inspected. Returned screenshot bounds were 1267×814 main and 1536×960 fullscreen, not an independently certified DIP/DPI measurement. Useful isolated evidence/data/exports remain; no broad cleanup was performed.

Final formatter verification and `git diff --check` exited **0**. One repeated suite run aborted the App host after 475 passes with an unhandled WPF Dispatcher exception; the diagnostic crash-sequence run and subsequent exact normal command both passed **488+526**. It was not reproduced or attributed to a specific test, and remains an unexplained intermittent test-host risk. No exception suppression or assertion weakening was used; the extracted candidate did not crash during its native retest.

**Still required:** active artifact-publication cancellation was **INCONCLUSIVE** (small copy completed before active Cancel observation); native SymbolizedPlayback-error/publication interaction and human audibility are **NOT TESTED**. A small image completed too fast to observe sustained Rendering, and actual minimum-window resizing was not established in this run. Complete those checks, precise/rapid native switching, broader DPI/multi-monitor checks and a new clean-source package retest **after the user commits**. The new report gives bounded, isolated retest instructions. No commit, tag, release or push was created; Phase 15B is not signed off and Phase 15C remains separate.

- **15B performed but not signed off:** the dated extracted-ZIP report above records native passes, B1/B2 transport defects, unverified active publication cancellation and audibility, and inconclusive transient image busy-state capture. The earlier 15A implementation reports remain structural evidence only. Retest repaired committed-source bytes and finish those checks before declaring 15B complete.
- **15C NOT TESTED:** run on clean supported Windows x64 without .NET or FFmpeg installed; record OS/media components and runtime/native availability. A development-machine published-folder launch is not this proof.
- **16 planned:** portfolio/release materials, product/application license decision, FFmpeg corresponding-source/distribution review and any release publication. No tag, installer, signing workflow, upload or deployment is added.
- Historical Phase 14 successful and failed evidence remains unchanged. WPF codec/long-path compatibility limitations remain product/runtime considerations, not resolved by making a ZIP.

## UI follow-ups — October 11–21

The [consolidated UI history](UI_Refinement_History.md) replaces repeated per-iteration candidate summaries. It retains changes, superseded decisions, dated automated/native outcomes, failed attempts and open acceptance. Current layouts and dialog behavior are specified in the technical specification, not in historical shell-height tables. Full local handoffs and pre-consolidation Phase 14/15 snapshots remain in the ignored archive described by that history; they are not required in a clean clone.


## Image navigation package provenance

These historical provisionals were built from dirty trees based on `1ddbab13f354914d7951aa81ed819a1ffc051aca`; that parent commit does not contain their changes. None replaces clean A2 run `c5bee19a51c542728228225d06838a04` or its SHA-256 `2fb83a3e3adf3ee3ff9d4a6041a14d98273ab7cb92f99561e7ba3d355b1f981b`.

| Run / scope | ZIP SHA-256 | Verification boundary |
| --- | --- | --- |
| October 22 first provisional `227a60121fed49e5a43c50448c4621ff` | `34e86d6a123e41bed7b985fd028d51d9441fdcf876bd432dfadbe3bcb0f19cd7` | Native drag exposed a routing defect; diagnostic history, not corrected acceptance. |
| October 22 corrected toolbar `aefbca00a8b14f508cc4e263b0255ced` | `e7d38091ae68865e492fb75f032440ceef07faef74ae16352587cbeea40149ea` | 11 publish checks, 416 payloads; bounded extracted native image pan/buttons/Fit/tab/restore and basic video observations only. |
| October 23 gesture-only/Fit-floor `f36d3fe8ab0545969ea10ad771535f48` | `73108e0222e453a5f9176dd39ac3f2ffd27d15d74f0b055c54ea4d1f45c6b459` | 11 publish checks, 416 payloads; changed bytes NOT natively accepted. Later audit reproduced swallowed ordinary wheel. |
| October 23 routed-wheel correction `fbd50cd47e264d57b0c515ec2ae6071e` | `1124a8045b9411fc67afa1c97a6de7fbf93329c98e3265acacdfdcab62978521` | 11 publish checks, 416 payloads; independent extraction hashes/startup/normal close/session removal passed. Native Fit/no-toolbar and non-zoom ordinary wheel only; constrained outer fallback and Ctrl+wheel NOT TESTED. |

The correction passed **1,212 full / 359 focused tests**, 35 separate packaging/session helper checks and Release/format/diff validation. Its package report preserves the actual dirty source status; final evidence-only documentation was completed after packaging. Package/probe/native reports stay under the local ignored `artifacts/image-navigation-correction-20261023-9e70/` and the package run directories. Historical image-navigation results and complete local archive identities are summarized in [UI history](UI_Refinement_History.md#image-navigation-october-22-23). Old package identities never imply these fixes, and their hashes were rechecked unchanged. Phase 15B remains partial/open; Phase 15C clean Windows remains NOT TESTED.
