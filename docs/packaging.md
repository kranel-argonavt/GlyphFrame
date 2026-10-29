# Packaging

GlyphFrame's target publication format is a **public repository retaining its reviewed development history, plus separate GitHub Releases for self-contained Windows x64 portable ZIP and per-user Setup**. Generated tools/packages do not belong in Git; that is a storage boundary, not a decision to abandon binary releases. End-user packages bundle the controlled FFmpeg/ffprobe pair and .NET/WPF runtime rather than require manual tool installation. Current outputs remain local provisional candidates until the release gates close. Build/verification scripts do not sign, tag, upload or publish. See [publication preparation](portfolio.md) for the chosen history-preserving route; the history-free export is only a fallback. Current commands use `.2` pins/versioned cache; old candidate identities remain immutable.

## Build a candidate

Build prerequisites: Windows x64, PowerShell 7, .NET 10 SDK, Git, explicitly initialized local FFmpeg tools and access to configured NuGet sources/runtime packs. These are developer requirements, not separate runtime prerequisites for the self-contained package. Executables are not tracked in the source snapshot; [local acquisition](../Tools/ffmpeg/README.md) verifies the fixed archive and executable hashes. Packaging refuses missing/altered tools rather than silently producing an image-only ZIP or downloading them itself.

From the repository root:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -ArchivePath 'C:\GFInputs\GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip'
pwsh -NoProfile -File scripts/Build-PortablePackage.ps1
```

Paths resolve from the script location, not the caller's current directory. Each invocation creates a fresh ignored run under `artifacts/portable-packages/<run-id>/` and invokes the existing publish verifier once. The application is published Release / `win-x64` / self-contained, with `PublishSingleFile=false` and `PublishTrimmed=false`.

```text
artifacts/portable-packages/<run-id>/
├── GlyphFrame-<version>-win-x64/          staged application folder
│   ├── GlyphFrame.App.exe
│   ├── README.md
│   ├── THIRD_PARTY_NOTICES.md
│   ├── LICENSE
│   ├── licenses/[versioned manifest, texts and provenance]
│   ├── [application, runtime and native files]
│   └── Tools/ffmpeg/[tools, checksums and attribution]
├── GlyphFrame-<version>-win-x64-portable.zip
├── GlyphFrame-<version>-win-x64-portable.zip.sha256
├── payload-manifest.json
└── report.json
```

The version comes from the published assembly's Major.Minor.Build, matching About. The [portable README template](Portable_README.md) is read by the build script and its `{{VERSION}}` token is substituted. Do not rename/remove that template without updating the script.

## Controlled FFmpeg delivery candidate

The local-only delivery layout groups the **unchanged pinned two-file initializer ZIP**, the **frozen 27-file source/build ZIP**, all twelve required tool documents and explicit checksum/source directions. The wrapper keeps both inner archive identities; it does not regenerate sources from a moving branch or change the initializer contract.

```text
GlyphFrame-ffmpeg-9.0-glyphframe.2-delivery-candidate/
├── README.md
├── RELEASE-NOTES-DRAFT.md
├── manifest.json
├── SHA256SUMS.txt
├── tools/GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip
├── sources/GlyphFrame-ffmpeg-9.0-glyphframe.2-build-materials.zip
└── notices/[complete tool GPL/license/provenance texts]
```

The candidate's relative source links work after extraction; they are **not live GitHub download URLs**. No upload, written source offer, final Corresponding Source approval or agreement is created. Source completeness/scope, hosting and availability verification remain release gates. The EXE-only initializer ZIP must not be presented as a cleared standalone release without its accompanying notices/source-delivery route. End-user Setup/portable still bundle the tools; this separate delivery kit serves development and source-distribution preparation, not manual end-user installation.

On 2026-10-28, the local candidate passed **18 outer file/hash checks, a separate fresh extraction, 17 checksum entries, all 27 frozen source entries, both exact tool entries, twelve complete notice copies and thirteen relative source/notice links**. Nine additional inert verifier probes exercised malformed/tampered archive rejection. No input executable or compiler was run. The candidate ZIP SHA-256 is `99d2660dc91434daceca2e86c4f36f4aee8defa05d1e81647ab6d90820d4551a`; preparation used private `e43fada` plus documentation edits (`WorkingTreeDirty=true`), without changing inner archive bytes or approval flags. This is delivery assembly/integrity evidence, not a newly compiled or legally approved tool build.

The final release must identify its actual application/tool/source bytes and provide reviewed source directions beside the relevant download. Preserve complete notices with conveyed tools; do not treat a link alone as a replacement for a license copy. Local archive integrity checks establish bytes/layout, not legal clearance. Windows-native runtime terms and other application release gates remain separate from this FFmpeg-only candidate.

## Windows-native terms preview

The current implementation is **ImplementationPreview**, not an operative agreement or cleared release. It retains the unchanged nonoperative 0.2 draft, complete Microsoft reference texts and all other indexed notices. `licenses/WindowsNative/profile.json`, itself hash-bound by the legal manifest, identifies the reviewed text, two included runtime versions and five exact covered DLL bytes. A changed runtime/document scope requires a new identity; absent, partial, mixed or modified scope blocks self-contained startup before media work. Framework-dependent developer builds with none of those bundled DLLs do not use this bundle-specific gate.

Setup shows the complete draft/references before installation, with an initially negative choice and an optional local notices viewer. Only an explicit acknowledged Install attempt can persist new state, after the running-app/downgrade guards. Reading or cancelling earlier writes no acknowledgement. A new/unacknowledged silent scope refuses. The application first-use screen uses the same independent record and skips only an identical current profile; Decline/Escape/close exits. Persistence errors remain failures with retry/decline, not success. The installer/app never accept a Publisher indemnity on the owner's behalf.

The local `%LocalAppData%\GlyphFrame\native-terms-acknowledgement.txt` protocol is four UTF-8 ASCII lines: versioned header, `Purpose=ImplementationPreview`, `Bundle=<canonical profile SHA-256>`, and `AcknowledgedUtc=<UTC yyyy-MM-ddTHH:mm:ss.fffZ>`. Readers bound input to 1 KiB; writes use an owned sibling temporary file and atomic replacement. It is user-writable convenience state, not tamper-proof identity or legal evidence. Settings Reset/Save/Cancel and uninstall do not grant or erase this separate choice. No acknowledgement belongs in a packaged payload. A future operative purpose/scope must not reuse preview records.

Package verification now first observes a fresh pending preview, no media cache/record, and normal decline. It then creates an explicitly labeled **synthetic nonoperative fixture only in that run's marked disposable data root**, relaunches, positively observes the production session and checks graceful cleanup. There is no general product auto-consent switch, no human/legal assent and no implication that the runtime has not executed before the WPF dialog. Automated verification does not establish native installer lifecycle; the later scoped manual run is recorded below. Future operative recipient/distributor coverage remains a separate gate; see [licensing](licensing.md#windows-terms-implementation-preview).

### Manually tested preview candidate

The owner completed the [other-PC manual protocol](testing.md#other-pc-manual-preview-protocol) on 2026-10-29 against this exact pair. These are **dirty-source nonoperative previews**, not replacements for the historical clean-source baseline's identity or public releases. Subsequent documentation edits do not change the packages.

| Field | Recorded value |
| --- | --- |
| Source parent / state | `66e8d9955f294feaaec4f4c79e479793c86924d5` / `WorkingTreeDirty=true` |
| Portable ZIP SHA-256 | `5791165eb64d903a891884a9d3ed891672b5528e113e7fd323078adc3f4559d4` |
| Setup SHA-256 | `8cb8ac08a48d0a56d8b0d1e420f001ac040fba11ff5e5ceb1239001bc41d198a` |
| Canonical preview profile SHA-256 | `a7f3a765f0ea0f29e0593027172ab0c837de8758ecf1e24da92b918b1b4fcf13` |
| Package integrity | **447 payloads / 32 legal documents / 9 dependency bindings**, verified during packaging; this is distinct from the later snapshots' installed apphost comparison |
| Manual scope | Windows 11 Home 25H2 / 26200.8246; A0–A6 and B0–B4 user-reported UI behavior plus reviewed snapshots/silent summaries; install/first-use, portable sharing, same-version repair, retained-data uninstall/reinstall and negative record/profile/write cases |
| Still outside this run | Runtime-free environment, different-version upgrade/downgrade, real valid changed-runtime scope, native DPI/text-scale matrix, operative/legal approval and final clean-source/publication-copy acceptance |

## What verification establishes

The publish verifier checks required application/CLR/WPF/Skia assets, bundled FFmpeg/ffprobe, committed tool checksums, attribution, tool execution and outside-repository startup. It verifies canonical legal-text hashes and exact non-project dependency versions against `licenses/manifest.json`; changed dependencies or missing/altered texts fail. This confirms collection integrity, **not legal completeness**: reports explicitly retain the [open licensing gates](licensing.md). It positively observes the launched PID's exclusive production-session lease and verifies removal of that exact session after normal close; an absent cache alone is not cleanup proof.

Archive validation compares every payload path, byte length and SHA-256 against the staging manifest. Duplicate/conflicting/unsafe paths and forbidden development/user-data content are rejected; PDBs are excluded. Staging is rechecked before completion. The ZIP checksum identifies that particular archive; reproducible steps do not promise byte-identical ZIPs across separate builds.

`report.json` records source commit **and working-tree dirty state**, SDK/tool versions, publish settings, completed checks, archive identity and linked startup/session evidence. A parent commit alone does not contain uncommitted changes. A failed or unfinished step is not package success.

The publish verifier's launch is from the published folder, not the ZIP. Independently extracted execution is a separate check.

## Validate the actual extracted ZIP

1. Verify the ZIP SHA-256 against its checksum.
2. Extract the entire application into a fresh outside-repository directory; compare every extracted path/length/hash with `payload-manifest.json`.
3. Launch that exact extracted EXE, not a Debug/Release `bin` copy or older candidate.
4. For isolated verification use a fresh short data root with the required `.glyphframe-package-verification` marker and explicit `--verification-data-root`. The existing helpers create/validate this root; do not redirect a normal personal profile or reuse an old run.
5. Positively observe the launched PID's production session/active lease. After ordinary closure verify that exact session disappeared and no session directories remain; retain the evidence and input/package hashes.
6. Perform the applicable native/human checklist in [testing.md](testing.md#native-retest-checklist). Startup alone is not layout, playback/audio, rendering or export acceptance.

Verification data normally uses `%TEMP%\GFpkg\<run-id>`; extracted checks use a separate short `%TEMP%\GF15b\<run-id>` location. Prospective completed/pending artifact paths and absolute escaped file URIs must fit the existing harness budgets: 220 filesystem characters / 240 URI characters. These are conservative test-harness safeguards, not universal Windows or supported-product path limits.

Never recursively clean a broad temp/user-data directory. Test lifecycle/cleanup operations must stay within an identified marked run and exact launched process; do not delete another instance's live cache. Evidence and failed runs remain available for diagnosis.

The reusable verifier can also run independently:

```powershell
pwsh -NoProfile -File scripts/Verify-PublishPackage.ps1
```

Helper-only regressions are documented in [testing.md](testing.md#run-the-automated-checks).

## Current local controlled-tool candidate

These are the last **clean-source baseline** artifacts, predating the [nonoperative working-tree preview](#windows-native-terms-preview). New preview builds do not inherit their clean-source identity or native acceptance; their detailed run identities remain in local evidence.

On 2026-10-28, a fresh independent checkout of **`e43fadad905deafeb636be1e9a7aec830984f918`** passed source-only build, exact controlled-tool initialization, full Release tests/helpers, formatting and packaging. Both new candidates record **`WorkingTreeDirty=false`**. Later documentation edits do not relabel their source or payloads; this private verification clone is not a sanitized public-history copy.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained folder |
| Portable run | `7977ff5567ca4cc196eb36b44ce79ab6` |
| ZIP SHA-256 | `16fcde50181fa91321b726e06608bf3093aa4a2232b8535dff1d0689c87a7238` |
| Installer run | `ec03236eb523403d9000f44d54cb0336` |
| Setup SHA-256 | `13d4f75cdc96da4302acccd0385224cff929140e42fc232f0cc5925dafbc8e8b` |
| Artifact checks | All **445 payload paths/lengths/hashes**, **31 legal documents / 9 dependency bindings**, exact controlled tools/source-material identity and twelve required notices passed |
| Startup checks | Published and independently extracted original-name EXE: responsive window, positively observed exclusive production session, graceful exit zero and owned-session cleanup passed |
| Setup scope | Unsigned, Inno Setup 7.1.0; built from exactly that verified portable payload; shell icon/version/Publisher checks passed; **not installed or run** |
| Native scope | Fresh extracted startup/session/integrity only; no repeated native rendering/export, playback/listening, PCM fallback, DPI or clean-PC matrix |
| Distribution | Local provisional candidates; source delivery, GPL scope and Windows-native terms remain open |

The fresh suite passed **1,288 Release tests + 141 separate helper checks**. All 459 checkout files remained unchanged; installed app/tool/settings hashes and previous artifacts were preserved. Compared with the prior dirty-source candidate, five application binaries now identify the committed source revision, while 31 text-file raw differences are verified CRLF/LF-only. Exact FFmpeg/ffprobe bytes are unchanged; legal canonical hashes pass. These are not byte-identical application packages or a transfer of all earlier native acceptance.

The extracted probe used direct process control, not the computer-use helper; its earlier identity collision is not declared fixed. See [testing](testing.md#recorded-verification-snapshot) for current and historical evidence boundaries. Complete generated reports and hash-verified retained copies remain local; no public download location or release is created here.

## Historical candidate records

These are immutable local verification records from 2026-10-26 through 2026-10-28, not downloadable/public releases or the current clean-source candidate. Their byte identities and acceptance scope remain separate.

### Initial controlled-tool rollout

On 2026-10-28, the reviewed `9.0-glyphframe.2` tools were integrated through a separate versioned cache and exact offline archive pins. These initial candidates were built from parent `e75a0215c19a8d8324548230c62b8f9c2487f82c` **with uncommitted changes** (`WorkingTreeDirty=true`); the parent commit does not contain this integration. They are superseded locally by the clean-source candidate above, not relabeled as clean-source outputs.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained folder |
| Portable run | `84c4f67b6bfb4f8599731195c5781f5e` |
| ZIP SHA-256 | `25d024084e8aab997968be31af18a61a996576267c7f7e0d84315da5ad9a407d` |
| Installer run | `50710030f8a44da2a1339d5388347566` |
| Setup SHA-256 | `e8a6c03ebf04e20fda94e75980b5b25645adde533d3eb9a3da47862de0a507f3` |
| Artifact checks | All **445 payload paths/lengths/hashes**, **31 legal documents / 9 dependency bindings**, exact controlled tools/source-material identity and 12 required tool notices passed |
| Startup checks | Published tools execute; isolated published app startup, exclusive production session, graceful exit zero and owned-session cleanup passed |
| Setup scope | Unsigned; built from that exact verified portable payload; shell icon/version checks passed; **not installed or run** |
| Native scope | Extracted-ZIP About/notices, PNG/MP4 render/export, playback/end/seek/fullscreen/Escape, stale-audio export blocking, replacement and active Cancel passed on this host through a byte-identical renamed apphost; clean-PC/Setup acceptance **not tested** |
| Distribution | Local provisional candidates only; source hosting, GPL/distribution scope and Windows-native protective-term review remain open |

That integration suite passed **1,288 Release tests**, plus **141 separate helper checks**, not additive to the later clean-source rerun. Native exports fully decode: two 640×360/180-frame MP4s (AAC-copy and silent) and one 320×240 PNG. All 283 AAC packet payloads match the source; the silent MP4 contains no audio. The native notices text exposes all 30 appended document headers plus the summary/controlled version/IJG credit. Normal close returned zero, removed the owned session and preserved all 445 extracted payloads, ordinary settings and installed app/tool hashes.

The normal-name apphost started, but the computer-use helper twice misbound its window to the installed application's identity. That own empty test process was terminated; its nonzero exit and retained marked session are diagnostic evidence, not a cleanup pass. Native input used an additional byte-identical `GlyphFrame.PackageSmoke.exe` copy in a separate fresh extraction; the 445 original files were not changed. No runtime/product fix or ordinary installed update was needed. This alias-based smoke does not certify normal-name helper targeting, human listening, PCM fallback again, a new DPI matrix, clean-PC or Setup lifecycle. Earlier installed-copy/user acceptance is not transferred. Old vendor cache, installed application and previous packages remain unchanged.

### Latest portable snapshot

Historical vendor-tool snapshot (heading retained for existing evidence links); superseded locally by the controlled-tool candidate above.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained folder |
| Build source | `ed628727cb4a7cf8385dacd9d032000a524d9732`; clean working tree at build (`WorkingTreeDirty=false`) |
| Run identity | `8542ec30764d419c8af0d6cf25cc25b0` |
| ZIP | `GlyphFrame-1.0.0-win-x64-portable.zip` |
| ZIP SHA-256 | `7fb33e65f98e17776c0dd992a746bbe707434b258ab08ba489b575adb5d9c4f7` |
| Artifact checks | All 433 payload paths/lengths/hashes and 19 legal documents/9 exact dependency bindings passed; FFmpeg/ffprobe execution, isolated published startup, normal shutdown and observed-session cleanup passed |
| Setup / installed copy | No Setup built for this run; installed copy unchanged |
| Scope | Local Release supplied for screenshot capture, not a new clean-PC or complete native workflow acceptance pass |
| Licensing | Public binary-distribution gates remain incomplete; collection integrity is not legal clearance |

Later gallery/documentation edits and the recovered XmpCore license/attributions do not change or relabel this ZIP: its 19-document collection predates the 21-document follow-up and current 31-document collection. User-supplied screenshots are recorded separately in the [media notes](screenshots/README.md), not treated as independently bound acceptance evidence. Any later final binary candidate needs its own source/hash binding.

### Previous paired candidate

The preceding paired Setup/ZIP run has a 16-document legal inventory that predates the follow-up Windows-native terms and historical XMP conflict findings. It is superseded for licensing review; its immutable identities remain below rather than being relabeled as the latest portable.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained folder |
| Build source | `c07025993bc81544be1403acca92f18de1b1cb9d` plus uncommitted licensing/About changes; dirty at build |
| Run identity | `d32c941154ba41c6a467ca3c7fa98e66` for both formats |
| ZIP | `GlyphFrame-1.0.0-win-x64-portable.zip` |
| ZIP SHA-256 | `05a7afd489f1267f3bd0a595446a212563a6ce502fc57d2eae5ec26168448e7b` |
| Setup | `GlyphFrame-1.0.0-win-x64-Setup.exe`, unsigned |
| Setup SHA-256 | `3b29405e01e586750732877a2dfcce9adc69446aae7113e7d72a12332c99bde7` |
| Artifact checks | All 430 payload paths/lengths/hashes, 16 legal texts/9 exact dependency bindings, published startup/session cleanup and embedded EXE/Setup icon frames passed |
| Licensing | Original code MIT; third-party public-distribution gates explicitly incomplete |
| Installed-copy checks | Not repeated for these bytes; existing installed copy was not replaced |

Earlier extracted-ZIP and clean-PC feedback applies to retained earlier candidates, not automatically to this Setup. The previous `d7e5c68fceb14da7951ee98240fd6bb7` candidate passed same-version installed update, all 416 installed payload hashes, preserved settings/exports and native Rose startup; those observations are not relabeled for this licensing candidate. Native/user observations retain their separate [testing scope](testing.md#manual-observations-and-attribution). Superseded identities, full reports, failures and retries remain in local archives; the public docs do not depend on them.

Later helper/documentation cleanup does not change or relabel these immutable bytes. The later clean-source portable above has its own manifest/checksum and startup evidence; previous installed/native observations are not silently transferred to it.

### XmpCore collection verification

On 2026-10-26, run `9d955d256dde4d5cafdf68b3d8506124` verified a fresh self-contained publish from parent `ed62872` plus the uncommitted XmpCore correction and existing gallery/documentation work. All **21 legal documents / 9 exact dependency bindings**, pinned tool execution, isolated responsive startup, observed production-session lease, normal shutdown and cleanup passed. The recovered BSD text and attributions are present in the published `licenses/XmpCore/` directory. No new Setup/ZIP was produced and the installed copy was unchanged. This is dirty-source local verification, not a replacement clean-source candidate or public approval; the existing ZIP identities above remain immutable.

## Running and removing the application

Extract the entire ZIP and launch `GlyphFrame.App.exe` inside the application directory. Keep all runtime/native files and `Tools/ffmpeg/` together. Do not launch from inside the ZIP or copy only the EXE.

Portable means no installer, not beside-EXE data. Normal settings, privacy-safe logs and managed review cache stay under `%LocalAppData%\GlyphFrame`. Closing/removing the application folder does not remove settings or exports saved elsewhere. The verification-data-root switch is test-only.

The target is Windows 10/11 x64. Native playback depends on Windows media components/codecs and can fail on long media/cache paths even when FFmpeg can process a file. The reported clean-PC pass covers supplied Windows 11 conditions, not all Windows editions or an independently audited absence of runtimes. See [ADR 0001](decisions/0001-native-video-playback.md).

## Installer

Install Inno Setup 7 explicitly on the build machine; it is not a runtime dependency for users. The builder never downloads or installs a compiler. Review the compiler's own licensing terms for your use.

```powershell
pwsh -NoProfile -File scripts/Build-InstallerPackage.ps1
# Or reuse one completed, marked portable run (hashes and compatibility rechecked):
pwsh -NoProfile -File scripts/Build-InstallerPackage.ps1 -PortablePackageDirectory <absolute-run-directory>
```

By default the command first creates a fresh verified portable candidate. `-CompilerPath` can select an explicitly installed Inno Setup 7 `ISCC.exe`. A reused package directory may have a trailing `\` or `/`; canonical run identity and every verification check still apply. Setup contains exactly the manifest's payload files, plus Inno's installer/uninstaller machinery; there is no second application publish with different settings. The builder rejects pre-protocol application binaries, altered payloads/archives and incomplete portable reports. It writes a fresh ignored `artifacts/installer-packages/<run-id>/` directory containing Setup, checksum, generated explicit file list and report. Failed outputs remain diagnostic evidence, not accepted candidates.

The installation contract is:

- English; Windows x64 only; per-user without requesting administrator elevation. Installer OS floor is Windows 10 build 19041, not certification of every Windows edition/runtime-support combination. Existing Windows codec/compatibility limits still apply; current human evidence is Windows 11-specific.
- Publisher `Nazar Khozin (kranel-argonavt)`, with the author's GitHub profile URL. This display metadata is **not a verified signing identity**: Setup is unsigned. The display-name change preserves the stable AppId and installation/update path. Windows may show an unknown-publisher or reputation warning; no certificate, security-setting change or bypass is part of this workflow.
- Default program directory `%LocalAppData%\Programs\GlyphFrame`; stable AppId `9A07BED0-6720-4A06-8C95-317B14C580A0`. Later versions must retain this identity. Start Menu shortcut; desktop shortcut unchecked by default and its choice retained on update. Optional launch after install is unchecked.
- Shell branding is a checked-in white/transparent ICO with 16/20/24/32/40/48/64/128/256-pixel frames, embedded through `ApplicationIcon` and `SetupIconFile`. Both shortcuts explicitly use the installed EXE's icon index zero. `pwsh -NoProfile -STA -File scripts/Build-ShellIcon.ps1` regenerates it from the existing WPF frame/GF geometry without changing the in-app theme. The builder checks every expected PNG frame in both EXE and Setup native resources and rejects an older payload without them. No global Windows icon-cache deletion or Explorer restart is performed.
- Install over the existing copy while closed. The application holds a path-specific lifetime marker until process termination; Setup/uninstall check it and do not forcibly close/restart the application. Other portable paths and multiple running app instances remain supported. Setup rejects installing a lower application version over a higher one.
- Uninstall removes installer-owned program files and shortcuts, **not** `%LocalAppData%\GlyphFrame`, source media, exports or arbitrary files in the installation folder. No blanket directory deletion, file associations, autorun or auto-updater.

Compiler signature/version validation and payload checks do not establish native wizard usability or lifecycle acceptance. Record actual first install, startup, running-app refusal, closed-app repair/version-changing update, downgrade refusal, uninstall/data retention and reinstall separately. Same-version repair is not evidence for a real version upgrade. A new installer also needs clean-machine execution and explicit final source/hash binding before release. Earlier portable human feedback is not silently transferred to Setup.

### Verification status

Earlier installer checks cover first install, non-admin HKCU registration, shortcut defaults/opt-in, running-app update/uninstall refusal, same-version repair, native uninstall/data retention and reinstall. Native application smoke checks cover image/PNG and silent-video/MP4 rendering/export. The previous installed icon candidate separately passed same-version update, complete installed payload comparison, retained settings/exports and native Rose startup. The [previous paired licensing Setup](#previous-paired-candidate) has not been installed; its About/notices checks are automated WPF fixtures, not native acceptance. Neither the initial controlled-tool Setup nor the historical clean-source baseline Setup was installed or run. The later [preview pair](#manually-tested-preview-candidate) has its own owner-run lifecycle evidence. None of these checks proves a different-version upgrade.

The user reported **1.0.0 / Rose** colors/logos, Save & Close, full restart and About all OK, then explicitly confirmed the corrected desktop icon after the earlier icon Setup was applied. Record these as **USER-REPORTED PASS**, distinct from agent-native inspection and read-only Shell icon extraction; no new measured DPI matrix is implied.

True version-changing upgrade/downgrade, clean-PC Setup, reputation prompts on downloaded unsigned bytes and the optional post-install launch action remain unverified. No security warning was bypassed. Complete attempt logs and prior candidate identities are retained locally, not repeated in this public guide.

## Before public distribution

- Select final source/artifact identity and carry forward or repeat affected verification honestly.
- Keep [third-party notices](../THIRD_PARTY_NOTICES.md), bundled [FFmpeg license](../Tools/ffmpeg/LICENSE.txt), [distribution provenance](../Tools/ffmpeg/DISTRIBUTION_README.txt) and [checksums](../Tools/ffmpeg/SHA256SUMS.txt).
- Original GlyphFrame code is MIT-licensed. XmpCore's historical BSD text/attributions are now recorded; resolve the remaining [FFmpeg complete Corresponding Source and Windows-native end-user/distributor requirements](licensing.md) before public binary distribution. Original private Git history still contains executable payloads. The controlled replacement is integrated locally; the separately sanitized history-preserving copy remains pending. The history-free [source snapshot](portfolio.md) remains a fallback, not approval to push the current history. Current project visual provenance is owner-confirmed; future imported media needs separate review. Attribution/checksum verification is not a legal compliance sign-off.
- The portfolio gallery is prepared locally; review its [media provenance](screenshots/README.md) before publication. An application walkthrough/screencast is not required. Personal media and local evidence dumps do not belong in the package.
- Publish/tag/upload only with explicit authorization. No public release or license choice is created by this document or the packaging command.
