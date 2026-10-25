# Portable packaging

GlyphFrame uses a self-contained Windows x64 folder distribution. The build and verification scripts create a local candidate; they do not sign, tag, upload or publish a release.

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

This is a retained local candidate, not a downloadable/public release. Record date: 2026-10-25.

| Field | Recorded value |
| --- | --- |
| Version / target | 1.0.0 / Release / win-x64 / self-contained folder |
| Build source | `d2851e10a13c57c8e6e4adfcc5ec6dd56c235829`, clean at build |
| Run identity | `c168d65e421c469a97207c714ef6338d` |
| ZIP | `GlyphFrame-1.0.0-win-x64-portable.zip`, 148,743,809 bytes |
| ZIP SHA-256 | `733699c2bbed5d8d98987f57a786387a1bd9c6aae6b2d9adb150c0f614ee4d46` |
| Structural/extracted checks | 11 publish checks, all 416 payloads, 9 independent extraction/startup/session checks passed |
| Later source distinction | `f3004e7` commits WPF test isolation and has a clean 1,258-test baseline; it did not rebuild or relabel these application bytes |
| Human clean-PC check | User confirmed the last supplied ZIP, startup/general functionality and same reported Windows 11 26H2 / 26300.9711 build; runtime inventory/target checksum were not independently supplied |

Native recovery/publication results and local-build versus ZIP-bound human reports retain their separate [testing scope](testing.md#manual-observations-and-attribution). Earlier aborts, retries and superseded candidates are retained locally; their detailed chronological logs are not required to use this repository.

Repository documentation has since changed, including the portable README template. Those edits are not inside this immutable ZIP. A future archive containing changed text needs its own manifest/hash verification; do not rewrite old reports or automatically transfer every local-build observation to new bytes. Documentation-only changes do not justify rerunning every unchanged feature, but final artifact/evidence binding must remain explicit.

## Running and removing the application

Extract the entire ZIP and launch `GlyphFrame.App.exe` inside the application directory. Keep all runtime/native files and `Tools/ffmpeg/` together. Do not launch from inside the ZIP or copy only the EXE.

Portable means no installer, not beside-EXE data. Normal settings, privacy-safe logs and managed review cache stay under `%LocalAppData%\GlyphFrame`. Closing/removing the application folder does not remove settings or exports saved elsewhere. The verification-data-root switch is test-only.

The target is Windows 10/11 x64. Native playback depends on Windows media components/codecs and can fail on long media/cache paths even when FFmpeg can process a file. The reported clean-PC pass covers supplied Windows 11 conditions, not all Windows editions or an independently audited absence of runtimes. See [ADR 0001](decisions/0001-native-video-playback.md).

## Before public distribution

- Select final source/artifact identity and carry forward or repeat affected verification honestly.
- Keep [third-party notices](../THIRD_PARTY_NOTICES.md), bundled [FFmpeg license](../Tools/ffmpeg/LICENSE.txt), [distribution provenance](../Tools/ffmpeg/DISTRIBUTION_README.txt) and [checksums](../Tools/ffmpeg/SHA256SUMS.txt).
- Resolve the application's license and bundled FFmpeg corresponding-source/distribution obligations with the release owner. Attribution/checksum verification is not a legal compliance sign-off.
- Prepare portfolio screenshots, owned/generated sample media and a demo; no personal media or local evidence dumps belong in the package.
- Publish/tag/upload only with explicit authorization. No public release or license choice is created by this document or the packaging command.
