# Controlled FFmpeg: standalone source and rebuild guide

This companion accompanies **9.0-glyphframe.2**. It clarifies the frozen source archive without editing that archive or claiming another compilation. It is usable independently of a GlyphFrame checkout, application execution or Microsoft-component acknowledgement. Current assets remain local review candidates; this guide is not a public source offer or legal-completeness certificate.

## Exact materials

| Input or output | SHA-256 |
| --- | --- |
| `GlyphFrame-ffmpeg-9.0-glyphframe.2-build-materials.zip` | `c2172927ec454ead9eed21cdc6d9be1904816fef085eb539492c3d54a035f5aa` |
| `GlyphFrame-ffmpeg-9.0-glyphframe.2-win-x64.zip` | `cc5fea33bd2a9c7656b2b6ae17b66417c2c6268b62b8b43e323d5748d98980cb` |
| Recorded `ffmpeg.exe` | `a01e45417cfa050c11151a2bf609e983408b8fa688e9d6ab66cb223eba9e7dbe` |
| Recorded `ffprobe.exe` | `e9fde8cdd23dd3534d15ff49b6788f80328f916634ae81bf7f8b9331da4ee2a9` |

The source ZIP has **27 files**, including its `materials-manifest.json`. Its `inputs/` directory contains the exact FFmpeg, x264 and zlib source archives plus an **upstream x264** Git bundle used to reconstruct version ancestry. That bundle is not GlyphFrame's private Git history. Source archives retain their own upstream copyright and license files. Original project scripts/regression harness have the included MIT license; the FFmpeg patch retains the applicable upstream terms.

Verify the source ZIP with `Get-FileHash -Algorithm SHA256 -LiteralPath <downloaded-file>` before extracting it. Compare the result with the exact value above, not merely the filename. Extract the **entire** ZIP into a new directory. Do not execute an unverified or incomplete download. Checksums identify bytes; they are not signatures or evidence of legal approval.

## Clarification of the frozen internal guide

The archived `scripts/ffmpeg/README.md` describes an earlier preparation stage. Its Gyan acquisition statement is historical: the current application's selected tools are the controlled `.2` pair above, not Gyan. Its optional `Export-ControlledFfmpegBuildMaterials.ps1` example belongs to the full project and is not included or needed in the standalone ZIP. **Rebuild with the included `scripts/Build-ControlledFfmpeg.ps1`, not the exporter.**

Frozen review/approval flags describe engineering workflow state, not a restriction on independently licensed source rights. They must not be silently relabeled as approval of the original artifacts. This companion is supplied beside the immutable source ZIP in the newer delivery candidate; do not substitute a different archive under the same checksum/name or infer that a documentation clarification rebuilds the tools.

## Host prerequisites

Use Windows x64, PowerShell 7, Windows `tar.exe`, and a separately prepared **MSYS2/UCRT64** environment matching the included `scripts/ffmpeg/toolchain.lock.json` and `runtime.lock.json`. The locks record the base MSYS2 archive, exact 150-package inventory, critical tools and eleven runtime-object identities. An arbitrary current compiler installation is not the recorded host. GCC is `16.2.0-6`; MinGW CRT/winpthreads are `14.0.0.r569.g34eeafb7d-1`.

Compiler binaries/packages are not in this source ZIP. The build command does not install/update packages, fetch sources or change system PATH. The locks are an integrity/replay policy, not a complete hermetic-host image or a promise that external package repositories retain every old package indefinitely. If a required host package is unavailable, stop the exact replay; do not disable checks and call a substitute host verified. Record the missing package and arrange a reviewed matching environment or a separately identified toolchain update. Ordinary application users do not need this development environment.

## Rebuild from the extracted archive

Choose a fresh, short build destination **without spaces**, outside both the source-input and MSYS2 trees. Existing/partial build directories are not reused, overwritten or deleted. Example paths must be adapted deliberately:

```powershell
$materialRoot = 'C:\GFSource\ffmpeg-materials'
$msysRoot = 'C:\GFBuild\msys64'
$freshBuildRoot = 'C:\GFBuild\new-run'

pwsh -NoProfile -File "$materialRoot\scripts\Build-ControlledFfmpeg.ps1" `
  -SourceDirectory "$materialRoot\inputs" `
  -MsysRoot $msysRoot `
  -BuildRoot $freshBuildRoot `
  -Jobs 8
```

The driver verifies source/patch/toolchain/runtime identities, creates fresh source trees, reconstructs x264 ancestry offline and builds zlib, x264 and FFmpeg. It applies only the declared downstream patch to `libavcodec/vvc/inter_template.c`, verifies its pre/postimage and rejects unlisted tree changes. The patch is not represented as upstream accepted. Its regression driver/source is included.

The recipe runs zlib checks, x264 checkasm8/checkasm10 and the 9,072-case weighted VVC regression. Add `-RunFate` for the configured sample-free FATE run; external conformance samples are not included. Neither these checks nor successful compilation certify every decoder, hostile input, warning or memory/resource path.

Successful tools are under `BuildRoot/prefix/bin/`; `report.json` and per-step logs remain in the chosen build root. Check the report's completion status and errors, execute trusted newly built tools with `-version`, and perform your own intended-use tests. The wrapper copies no output into GlyphFrame and creates no app installer. Different build roots/hosts may change output hashes; successful functional replay is not byte-identical reproducibility certification. GlyphFrame's initializer deliberately rejects a newly rebuilt pair unless its exact hashes receive separate review/pins.

## Source/dependency scope

| Material | Retained source or evidence | Boundary |
| --- | --- | --- |
| FFmpeg | Exact upstream source archive and declared VVC patch/regression | Full source notices retained; no moving upstream branch substituted |
| x264 | Canonical source archive plus upstream ancestry bundle | Version reconstruction is offline; no project-private Git history needed |
| zlib | Exact source archive | Library source/license included, not only its EXE dependency name |
| Build/install/use control | PowerShell driver/helpers, shell recipe, locks, this guide and runtime instructions | The exporter is not needed; tools are CLI programs, not an installable service |
| libgcc/startup objects | Exact compiler/runtime object mapping and complete GPL/Runtime Exception texts | A compiler/System Library or eligible-compilation analysis is component-specific, not an inference from static linkage alone |
| MinGW CRT/imports/winpthreads | Exact package/object inventory and all installed grants/notices | Not reconstructed merely from a short upstream revision; Windows import libraries are distinct from Windows DLL implementations |
| Other notices | Complete FFmpeg/x264/zlib/GCC/MinGW/winpthreads/IJG copies accompany the delivery kit | Preserve the IJG binary-documentation credit and original embedded source notices |

GPLv3's [source definitions and exclusions](https://www.gnu.org/licenses/gpl-3.0.en.html#section1) distinguish required program sources from eligible System Libraries and general-purpose tools. The [GCC Runtime Library Exception](https://www.gnu.org/licenses/gcc-exception-3.1.en.html) has file/compilation conditions; it is not a blanket exemption for all GNU or statically linked code. The frozen archive's `scripts/ffmpeg/runtime-notices.md` and runtime lock record the material mapping and conservative notice collection. Their earlier audit wording does not erase the later `.2` engineering review recorded in the full project.

The custom program inputs, patch and build drivers are retained and had an earlier functional replay. This guide does not declare every runtime-source exclusion legally settled. Final release review must record the actual exclusion/exception basis or supply any additional required exact package sources/recipes. A provenance URL alone is not a substitute where source must accompany the binary. No source-approval flag is changed here.

## Modifying or using the tools

To use a trusted build independently, keep `ffmpeg.exe` and `ffprobe.exe` in your chosen tool directory and invoke them by explicit path. No Windows service, registry entry or global PATH change is required by this build workflow. Preserve applicable notices and source-delivery obligations when conveying binaries; the inner two-EXE initializer ZIP is not a cleared standalone release by itself.

Source licensing permits modifications under the applicable licenses; the recorded locks and exact application pins are engineering safeguards, not a ban on modifying source. Work in a separate copy, retain modifications/notices, and distinguish changed inputs/recipe/output from the recorded `.2` artifacts. Updating a lock to accept different bytes is not evidence that those bytes produced the original tools.

## Before online distribution

The selected route is direct, matching source/build access beside binaries on the same versioned GitHub Release page, following the reviewed [GPLv3 section 6(d) route](https://www.gnu.org/licenses/gpl-3.0.en.html#section6). No public repository/tag/asset URL is configured by this guide. Do not substitute GitHub's autogenerated application-source ZIP: it omits these upstream inputs.

Before making a release public, bind actual source/guide/notices/Setup/portable hashes, approve remaining scope decisions, verify the real anonymous source download and links, and keep matching source available with related binaries. Source access must not require launching GlyphFrame, accepting unrelated Microsoft-component terms, payment or a personal request. Hosting availability and any fallback remain the distributor's responsibility. This describes the intended online route, not a new written source offer or authorization to upload.
