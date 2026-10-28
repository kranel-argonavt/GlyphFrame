# Experimental controlled FFmpeg rebuild

This remains an **experimental offline rebuild workflow**, not a cleared public release. The application now locally integrates the exact reviewed `.2` pair in a versioned cache; the locks here still deliberately reject approval claims. ADR 0002 in the full repository records the application contract and open review; that context is not required to execute the standalone material package.

## Required inputs

- Windows x64, PowerShell 7 and Windows `tar.exe`.
- A separately installed MSYS2/UCRT64 environment matching `toolchain.lock.json` and `runtime.lock.json`. They record the actual 150-package environment, critical tool identities and eleven static/import/startup object hashes used for the experiment; this is not a complete hermetic-host attestation. The immutable base archive identity is also recorded. Ordinary builds never install, update or downgrade packages, change global PATH, or download anything.
- The four local files in `sources.lock.json`: pinned FFmpeg, x264 and zlib archives plus the **upstream x264** Git bundle required to reconstruct its version. The bundle is not GlyphFrame's private history. Source hashes/revisions and all unapproved status flags are explicit. Local source/package caches are not repository additions.
- The single [downstream VVC correction](patches/README.md), its exact preimage/postimage hashes and the included original regression source. Source profile/schema 2 produces `9.0-glyphframe.2`; the unchanged runtime/toolchain locks describe the same host ingredients, not a new runtime rebuild.

The archives are retained in the owner's private build inputs. Initial acquisition came from the linked upstream repositories with canonical LF handling; a future public source location must be reviewed and published separately. Do not substitute a similarly named download or change a hash merely to make the build pass. The current material package supplies the exact pinned inputs for offline replay; it does not silently fetch them from a moving branch.

## Rebuild

From the repository or extracted build-material root:

```powershell
pwsh -NoProfile -File scripts/Build-ControlledFfmpeg.ps1 `
  -SourceDirectory 'C:\GFInputs' `
  -MsysRoot 'C:\GFBuild\msys64' `
  -BuildRoot 'C:\GFBuild\fresh-run' `
  -Jobs 8
```

`BuildRoot` must be a new, short directory without spaces, outside/independent of the source-input and MSYS2 directories. Existing or partial outputs are never reused/overwritten/deleted. The recipe validates input/patch hashes, critical executable identities and the installed package inventory; copies/extracts fresh source trees; applies the locked patch using pinned Git outside a repository; checks the exact pre/postimage and all source files for unlisted changes; reconstructs x264 offline with LF and disabled Git user/system config/hooks; compares it with the canonical archive; builds zlib/x264/FFmpeg and retains per-step logs. The shell uses a process-local compiler PATH and does not load login/startup profiles or inherit arbitrary compiler flags. zlib tests and x264 checkasm8/checkasm10 use the recipe; `-RunFate` additionally runs configured FFmpeg FATE without an external sample directory. After building, the wrapper compiles/runs 9,072 actual-library weighted VVC regression cases and rejects any failure. These are not native app acceptance or complete VVC conformance coverage.

Outputs are under `BuildRoot/prefix/bin/`. The wrapper checks native version execution with a Windows-system-only PATH. `report.json` records recipe/source/toolchain/runtime lock identities, output hashes, warnings and completed/failed status. Host runtime objects and the six retained notice texts are checked before and after building. The compiler retains its diagnostics; success means **BuiltExperimental**, never warning-free, fully accepted or legally approved. This rebuild command itself copies no output into the application cache and builds no package/installer. The separate reviewed initializer accepts only the already pinned local tool archive. Retain failed runs for diagnosis.

The recipe sets `SOURCE_DATE_EPOCH`, but path/configuration/compiler/linker details still matter. Repeatable steps and successful functional replay are not a claim of byte-identical output across roots or hosts.

## Assemble build materials

```powershell
pwsh -NoProfile -File scripts/Export-ControlledFfmpegBuildMaterials.ps1 `
  -SourceDirectory 'C:\GFInputs'
```

This creates a new ignored `artifacts/ffmpeg-build-materials/<run-id>/` with a source/build ZIP, manifest and ZIP SHA-256. Every archived file is rechecked. It includes the exact upstream source inputs, offline recipe/helpers, locks, downstream patch/documentation, original regression source/driver, this guide, the [runtime material assessment](runtime-notices.md), [distribution preparation and IJG credit](distribution-notices.md), six unchanged runtime notices and the original-script MIT license. It excludes GlyphFrame's private history, app/FFmpeg executables, toolchain binary packages, user data and test media. The exporter itself does not execute an input or publish anything. `RuntimeNoticeIntegrityVerified=true` means those exact texts passed integrity verification, **not** that the entire source/distribution review was approved.

Extract the **whole** ZIP into a new folder, verify its checksum/manifest and use its `inputs/` directory as `SourceDirectory` for a fresh rebuild with the pinned toolchain. A successful independent replay must be recorded against the ZIP and output hashes, not inferred from an existing source/build tree.

## Licensing and remaining work

The root MIT license covers original GlyphFrame scripts and the original regression harness. The downstream diff modifies FFmpeg and retains its applicable upstream terms, not a new MIT label. FFmpeg, x264 and zlib retain the license/copyright files inside their exact source archives; none is relicensed as MIT. The explicit GPL/version3 profile is a tool-build choice, not an automatic license change to GlyphFrame's original code. The additional libgcc/MinGW/winpthreads inventory, exact notices and conditional exception assessment are recorded separately in [runtime material](runtime-notices.md); they do not settle application deployment terms.

**The ZIP is experimental build material, not an approved complete Corresponding Source package.** Warning review, relevant runtime/source/notices completeness, independent replay, full GlyphFrame/tool/package/native acceptance and the other distribution gates documented in the full repository remain separate. `CorrespondingSourceApproved`, `WarningReviewComplete` and `PublicDistributionApproved` stay false. Do not upload provisional binaries or make a written source promise based on this workflow.
