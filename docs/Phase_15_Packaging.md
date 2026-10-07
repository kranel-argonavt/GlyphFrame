# Phase 15A — reproducible Windows x64 portable packaging

Phase 15A implements distribution construction and structural verification only. It does not publish a release, prove extracted-ZIP interaction (15B), prove clean-machine compatibility (15C), or close broader Phase 14 testing. The initial repository was clean at `540ae20` (`test: expand verification and fix review UI regressions`). Implementation packages are explicitly dirty-tree builds; that commit does not cover their uncommitted changes.

## Command and prerequisites

Windows, PowerShell **7**, .NET **10 SDK**, Git, bundled committed tools, and access to configured NuGet sources/runtime packs are required for building:

```powershell
pwsh -NoProfile -File scripts/Build-PortablePackage.ps1
```

From outside the repository, supply the absolute script path. All source/output paths resolve from `$PSScriptRoot`, not the caller's directory. No system PATH, installed software, Windows settings or normal GlyphFrame profile is modified. Application startup uses the existing short, marked `%TEMP%\GFpkg\<run-id>` data root, outside-repository working directory and bounded owned-process lifecycle.

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

Final run: `9bd1c7d2b98c4b12ab8cdfd0b50bc1a2`, version **1.0.0** (assembly 1.0.0.0), dirty-tree source identity `540ae20c807e473b3fff6159a3632dc9f5618ba8`. SDK 10.0.401, PowerShell 7.6.5, Windows build 26300, x64; bundled FFmpeg/ffprobe 9.0 essentials build.

- [ZIP](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/GlyphFrame-1.0.0-win-x64-portable.zip): **148,718,654 bytes**.
- [SHA-256 file](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/GlyphFrame-1.0.0-win-x64-portable.zip.sha256): `0491e80ca0404cb67acaac875d10491c7465a62556721aa13bf9b4cebca5c369`.
- [Packaging report](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/report.json) and [payload manifest](../artifacts/portable-packages/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/payload-manifest.json).
- [Published-folder startup/session evidence](../artifacts/package-verification/9bd1c7d2b98c4b12ab8cdfd0b50bc1a2/report.json).

These artifacts are ignored, local implementation evidence, not committed distribution assets. Startup used the marked short data root `%TEMP%\GFpkg\9bd1c7d2b98c4b12ab8cdfd0b50bc1a2`; its owned session was removed, while verifier evidence/data-root markers were retained. No live interaction with an extracted ZIP, audiovisual validation or clean-machine check was performed.

## Pending validation and release boundaries

- **15B NOT TESTED here:** extract the completed ZIP to a new short folder, inspect startup/Settings/tools, render/review/export images and original videos, repeat export, playback/audio/fullscreen/fallback, errors/details, cancellation and graceful session cleanup. Use a separately marked short verification data root for test isolation, never omit the switch during automated verification. Retain comparable evidence and prove which extracted bytes were exercised.
- **15C NOT TESTED:** run on clean supported Windows x64 without .NET or FFmpeg installed; record OS/media components and runtime/native availability. A development-machine published-folder launch is not this proof.
- **16 planned:** portfolio/release materials, product/application license decision, FFmpeg corresponding-source/distribution review and any release publication. No tag, installer, signing workflow, upload or deployment is added.
- Historical Phase 14 successful and failed evidence remains unchanged. WPF codec/long-path compatibility limitations remain product/runtime considerations, not resolved by making a ZIP.
