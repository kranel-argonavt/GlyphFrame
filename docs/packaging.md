# Packaging

GlyphFrame uses a self-contained Windows x64 payload, distributed as a portable ZIP or per-user Setup. The build and verification scripts create local candidates; they do not sign, tag, upload or publish a release.

## Build a candidate

Build prerequisites: Windows x64, PowerShell 7, .NET 10 SDK, Git, committed bundled tools and access to configured NuGet sources/runtime packs. These are developer requirements, not separate runtime prerequisites for the self-contained package.

From the repository root:

```powershell
pwsh -NoProfile -File scripts/Build-PortablePackage.ps1
```

Paths resolve from the script location, not the caller's current directory. Each invocation creates a fresh ignored run under `artifacts/portable-packages/<run-id>/` and invokes the existing publish verifier once. The application is published Release / `win-x64` / self-contained, with `PublishSingleFile=false` and `PublishTrimmed=false`.

```text
artifacts/portable-packages/<run-id>/
├── GlyphFrame-<version>-win-x64/          staged application folder
│   ├── GlyphFrame.App.exe
│   ├── README.md
│   ├── THIRD_PARTY_NOTICES.md
│   ├── [application, runtime and native files]
│   └── Tools/ffmpeg/[tools, checksums and attribution]
├── GlyphFrame-<version>-win-x64-portable.zip
├── GlyphFrame-<version>-win-x64-portable.zip.sha256
├── payload-manifest.json
└── report.json
```

The version comes from the published assembly's Major.Minor.Build, matching About. The [portable README template](Portable_README.md) is read by the build script and its `{{VERSION}}` token is substituted. Do not rename/remove that template without updating the script.

## What verification establishes

The publish verifier checks required application/CLR/WPF/Skia assets, bundled FFmpeg/ffprobe, committed tool checksums, attribution, tool execution and outside-repository startup. It positively observes the launched PID's exclusive production-session lease and verifies removal of that exact session after normal close; an absent cache alone is not cleanup proof.

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

## Recorded candidate

Current paired local candidate, not a downloadable/public release. Record date: 2026-10-26.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained folder |
| Build source | `9e9a8b3e104adca6d4bff7aaa64edd16517d4846` plus uncommitted installer/icon changes; dirty at build |
| Run identity | `d7e5c68fceb14da7951ee98240fd6bb7` for both formats |
| ZIP | `GlyphFrame-1.0.0-win-x64-portable.zip` |
| ZIP SHA-256 | `45d9d0b6890ace57c6a386f4d5d826cc8dff22df68c6d6045825519c76287ea1` |
| Setup | `GlyphFrame-1.0.0-win-x64-Setup.exe`, unsigned |
| Setup SHA-256 | `cfd06f3a45bc0548ed71dbe54c27acdcf974e2f69afe81f6b489aaed79129a17` |
| Artifact checks | All 416 payload paths/lengths/hashes, published startup/session cleanup and embedded EXE/Setup icon frames passed |
| Installed-copy checks | Same-version update matched all 416 hashes and preserved settings/exports; native Rose startup passed |

Earlier extracted-ZIP and clean-PC feedback applies to retained earlier candidates, not automatically to this Setup. Native/user observations retain their separate [testing scope](testing.md#manual-observations-and-attribution). Superseded identities, full reports, failures and retries remain in local archives; the public docs do not depend on them.

Later helper/documentation cleanup does not change or relabel these immutable bytes. Final clean-source packaging after commit needs its own manifest/checksums and explicit evidence binding; do not silently transfer every previous observation to a new artifact.

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
- Publisher `kranel-argonavt`, with the author's GitHub profile URL. This display metadata is **not a verified signing identity**: Setup is unsigned. Windows may show an unknown-publisher or reputation warning; no certificate, security-setting change or bypass is part of this workflow.
- Default program directory `%LocalAppData%\Programs\GlyphFrame`; stable AppId `9A07BED0-6720-4A06-8C95-317B14C580A0`. Later versions must retain this identity. Start Menu shortcut; desktop shortcut unchecked by default and its choice retained on update. Optional launch after install is unchecked.
- Shell branding is a checked-in white/transparent ICO with 16/20/24/32/40/48/64/128/256-pixel frames, embedded through `ApplicationIcon` and `SetupIconFile`. Both shortcuts explicitly use the installed EXE's icon index zero. `pwsh -NoProfile -STA -File scripts/Build-ShellIcon.ps1` regenerates it from the existing WPF frame/GF geometry without changing the in-app theme. The builder checks every expected PNG frame in both EXE and Setup native resources and rejects an older payload without them. No global Windows icon-cache deletion or Explorer restart is performed.
- Install over the existing copy while closed. The application holds a path-specific lifetime marker until process termination; Setup/uninstall check it and do not forcibly close/restart the application. Other portable paths and multiple running app instances remain supported. Setup rejects installing a lower application version over a higher one.
- Uninstall removes installer-owned program files and shortcuts, **not** `%LocalAppData%\GlyphFrame`, source media, exports or arbitrary files in the installation folder. No blanket directory deletion, file associations, autorun or auto-updater.

Compiler signature/version validation and payload checks do not establish native wizard usability or lifecycle acceptance. Record actual first install, startup, running-app refusal, closed-app repair/version-changing update, downgrade refusal, uninstall/data retention and reinstall separately. Same-version repair is not evidence for a real version upgrade. A new installer also needs clean-machine execution and explicit final source/hash binding before release. Earlier portable human feedback is not silently transferred to Setup.

### Verification status

Installer checks cover first install, non-admin HKCU registration, shortcut defaults/opt-in, running-app update/uninstall refusal, same-version repair, native uninstall/data retention and reinstall. Native application smoke checks cover image/PNG and silent-video/MP4 rendering/export. Those initial lifecycle checks used an earlier installer candidate; the [current candidate](#recorded-candidate) separately passed same-version installed update, complete installed payload comparison, retained settings/exports and native Rose startup. Neither check proves a different-version upgrade.

The user reported **1.0.0 / Rose** colors/logos, Save & Close, full restart and About all OK, then explicitly confirmed the corrected desktop icon after the current Setup was applied. Record these as **USER-REPORTED PASS**, distinct from agent-native inspection and read-only Shell icon extraction; no new measured DPI matrix is implied.

True version-changing upgrade/downgrade, clean-PC Setup, reputation prompts on downloaded unsigned bytes and the optional post-install launch action remain unverified. No security warning was bypassed. Complete attempt logs and prior candidate identities are retained locally, not repeated in this public guide.

## Before public distribution

- Select final source/artifact identity and carry forward or repeat affected verification honestly.
- Keep [third-party notices](../THIRD_PARTY_NOTICES.md), bundled [FFmpeg license](../Tools/ffmpeg/LICENSE.txt), [distribution provenance](../Tools/ffmpeg/DISTRIBUTION_README.txt) and [checksums](../Tools/ffmpeg/SHA256SUMS.txt).
- Resolve the application's license and bundled FFmpeg corresponding-source/distribution obligations with the release owner. Attribution/checksum verification is not a legal compliance sign-off.
- Prepare portfolio screenshots, owned/generated sample media and a demo; no personal media or local evidence dumps belong in the package.
- Publish/tag/upload only with explicit authorization. No public release or license choice is created by this document or the packaging command.
