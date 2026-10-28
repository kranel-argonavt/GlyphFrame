# Controlled local video tools

The source repository contains provenance/notices and exact pins, **not executables**. Local builds now use the reviewed Windows x64 `9.0-glyphframe.2` pair, including the [tracked weighted-VVC correction](../../scripts/ffmpeg/patches/README.md), instead of Gyan's earlier build.

These preparation commands are for **developers building from Git**, not end users. The agreed release format is self-contained Setup/portable with the pair already bundled, plus separately available matching source/build materials and notices. The [history-preserving repository and GitHub Releases plan](../../docs/portfolio.md#publication-scope) is the target; absence of binaries in Git is not a source-only product restriction or a new history-free repository.

## Prepare exact reviewed tools

PowerShell 7 on Windows:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -ArchivePath 'C:\GFInputs\GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip'
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -VerifyOnly
```

Obtain the **exact locally reviewed archive** from the maintainer. No public download is configured yet, and `-Download` is intentionally unsupported; there is no automatic vendor fallback or network acquisition. The archive contains only `Tools/ffmpeg/ffmpeg.exe` and `ffprobe.exe`; the whole archive and both files must match `SHA256SUMS.txt`. A different build with the same version is not accepted silently.

The local delivery candidate wraps this unchanged two-file initializer archive with the frozen source/build archive, complete tool notices and explicit integrity/source directions. It is a preparation artifact, not an uploaded release or a cleared EXE-only download. Unpack that delivery folder first, then use its inner tool ZIP as `-ArchivePath`; the existing archive contract and pins do not change. Current preparation status and limits are in [packaging](../../docs/packaging.md#controlled-ffmpeg-delivery-candidate).

The initializer never executes the tools, installs software, changes PATH, overwrites a cache, or repairs a partial/mismatched directory. It accepts the verified pair into ignored `artifacts/local-runtime/controlled-9.0-glyphframe.2/Tools/ffmpeg/`. The previous unversioned Gyan cache is left untouched; old installed apps/packages are not updated.

Builds copy the pair beside the app into `Tools/ffmpeg/`, preserving the runtime locator. Real-tool tests use the same versioned cache and assert exact version plus both executable hashes. Image workflows can build/run without video tools; full integration/package verification requires this explicit preparation. NuGet dependencies are separate.

## Rebuild and distribution boundary

The [offline recipe/material workflow](../../scripts/ffmpeg/README.md) retains exact upstream inputs, runtime identities, downstream patch and regressions. Functional replay on the pinned host is not bit-identical output: rebuilding in a different root/host may produce different EXE hashes, which require an explicit new review/pin update rather than an arbitrary-tools switch.

`DISTRIBUTION_README.txt` records the exact binary/source/material identities and preparation limits. `LICENSE.txt` retains GPLv3; the additional full x264/zlib/runtime/JPEG notices live in `licenses/FFmpeg/`, are hash-bound by the legal manifest and displayed in About. Package verification binds the actual pair to all twelve required tool documents.

This is **local integration, not an approved public binary release**. Source delivery/hosting, application/GPL and Windows-native terms, final artifact acceptance, media and historical binaries retain their [separate gates](../../docs/licensing.md). Never commit EXEs, archives or caches. Preserve failed acquisition runs and old packages; do not weaken pins to hide an integrity failure.
