# ADR 0002: Controlled Windows FFmpeg tools

Date: 2026-10-27; updated 2026-10-28. Status: **reviewed `9.0-glyphframe.2` integrated and verified through clean-source application builds/tests/packages at private commit `e43fada`; public distribution remains unapproved**.

## Context and decision

The private checkpoint `e75a0215c19a8d8324548230c62b8f9c2487f82c` used Gyan's FFmpeg 9.0 essentials build. Its configuration README did not establish reconstruction of those exact vendor binaries. We chose a controlled source/build/notice chain rather than treat a configuration listing as complete Corresponding Source evidence.

The corrected `.2` pair is now integrated locally. Acquisition, build/tests, pins, legal manifest and package checks changed together. The former vendor cache, installed app and old packages remain unchanged. Replacing today's tools does not clear historical Git objects, retrospectively approve old binaries, settle application/GPL scope or close Windows-native/media gates.

## Required application contract

GlyphFrame invokes separate CLI processes, not libav DLL APIs:

- `ffprobe`: JSON metadata, video display matrices and first-audio-stream inspection.
- Decode: local MP4/MOV/MKV/AVI, selected video stream, seek, autorotation and sequential RGBA through `pipe:1`.
- Encode: RGBA `rawvideo` through `pipe:0`, `libx264`, CRF 20 / medium, yuv420p and MP4 fast-start.
- Audio: copy compatible audio; otherwise native AAC with the existing duration/rate/channel policy; Remove Audio stays silent.
- Synthetic probes: lavfi `testsrc2`, `color`, `drawbox` and `sine`; ordinary tests do not regenerate committed fixtures.
- Native Windows execution without MSYS2, GCC or third-party runtime DLLs on the user's PATH.

The runtime locator/validator and rendering/audio product logic are unchanged. Maintained integration tests now assert exact controlled version **and both executable hashes**.

## Controlled build and pinned inputs

Use Windows x64 MSYS2/UCRT64 in a separate short build root, with process-local PATH and baseline x86-64/runtime CPU dispatch. [FFmpeg Windows guidance](https://ffmpeg.org/platform.html) and [MSYS2 environments](https://www.msys2.org/docs/environments/) describe the toolchain route. The workflow does not install WSL/Docker, change global PATH or automatically update packages.

| Component | Pinned identity |
| --- | --- |
| FFmpeg | [`d32b387f2b0a484599d4587d651891f0c63c4238`](https://github.com/FFmpeg/FFmpeg/commit/d32b387f2b0a484599d4587d651891f0c63c4238), source RELEASE 9.0 |
| x264 | `0480cb05fa188d37ae87e8f4fd8f1aea3711f7ee` from [VideoLAN upstream](https://code.videolan.org/videolan/x264); retained ancestry reconstructs its clean version |
| zlib | [`da607da739fa6047df13e66a2af6b8bec7c2a498`](https://github.com/madler/zlib/commit/da607da739fa6047df13e66a2af6b8bec7c2a498), v1.3.2 |
| Host | MSYS2/UCRT64, GCC 16.2.0, NASM 3.02; exact base/package/tool/runtime hashes in the locks |

The [offline recipe and material guide](../../scripts/ffmpeg/README.md) is the executable workflow reference. Source archives, upstream x264 bundle, one explicit patch, critical tools, 150-package inventory, eleven runtime objects and six notice texts are pinned. Canonical LF source handling prevents an artificial x264 modified-version suffix. Inputs, patch scope and host identities are checked before/after work; failed roots are retained, not reused.

Static x264/zlib selection is explicit. External autodetection, network, ffplay, hardware acceleration and capture/output devices are disabled, except lavfi synthetic input. No nonfree or host-CPU-only build is enabled. [Enabling libx264 requires GPL FFmpeg](https://ffmpeg.org/general.html#x264); this tool profile is not an automatic relicense of original GlyphFrame code. Static compiler/runtime ingredients mean x264 and zlib are not the whole notice/source inventory.

## Downstream VVC correction

Actual-library differential probes found two defects in the preserved unpatched `.1` candidate:

1. The weighted scaled wrappers interpreted `height, denom, wx, ox` in the wrong order, including valid calls producing no output.
2. The scaled weighted shift omitted `denom`, disagreeing with ordinary weighted prediction at integer positions/unit spatial ratio.

The [minimal downstream patch](../../scripts/ffmpeg/patches/README.md) changes only those wrapper argument lists and shift in `libavcodec/vvc/inter_template.c`. Source schema/profile 2 binds the exact pristine preimage, postimage and patch bytes; all 10,421 FFmpeg source files are checked for unlisted changes. The upstream archive remains pristine. This is a declared GlyphFrame downstream correction, not an accepted upstream patch.

The original declared-interface regression fails on the unpatched library and an order-only mutant, then passes **9,072 cases on each corrected build**: independent pixel formula, ordinary weighted callback and output guards across 8/10/12-bit luma/chroma, widths/heights, denominators, weights and offsets. Scope is integer positions/unit spatial ratio, not complete interpolation/RPR or malformed-input coverage. Do not activate the defective `.1` pair or describe diagnostic caller compensation as a fix.

## Verification milestones

These are distinct historical stages, not additive test counts or claims about untested artifacts. Full journals, captures, failures, source/output hashes and superseded archives are retained locally.

| Stage | Evidence and boundary |
| --- | --- |
| Initial `.1` preparation | Pristine source comparison, offline replay, zlib/x264 checks, CLI/codec probes and an isolated application suite/native smoke passed. Later direct DSP probes still exposed the two defects above; earlier successes did not overrule them. |
| Corrected `.2` build/replay | First build passed **2,885 configured sample-free FATE tests**. An extracted 26-file material ZIP rebuilt using its own scripts on the same pinned host. Each corrected pair passed the 9,072-case regression, zlib/x264 checks, **19 CLI probes, 12 H.264/AAC cases and six selected VVC sample/thread CRC comparisons**. The replay was not independent-host or bit-identical. |
| Isolated corrected application | Exact-version/two-hash adaptation passed the then-current **1,283-test** suite without skips. Separate native checks covered PCM fallback, AAC-copy/silent exports, PNG, playback/fullscreen, replacement and active Cancel. This was a framework-dependent snapshot, not the final self-contained package. |
| Engineering/material review | Corrected objects/EXEs received matched-section stack/linker audits and full notice/source-material integrity checks; details below. The 27-file documentation refresh was not another literal rebuild. |
| Local rollout and committed verification | Initial dirty-source integration/native checks were followed by fresh independent builds, the full suite/helpers and clean-source ZIP/Setup from private commit `e43fada`. Independently extracted original-name startup/session cleanup passed. See the single current [verification summary](../testing.md#recorded-verification-snapshot) and [package identities](../packaging.md#current-local-controlled-tool-candidate); earlier native workflows are not relabeled as a repeat on the new bytes. |

Official external VVC coverage used only `CodingToolsSets_A_2`, `RPR_A_4` and `SCALING_A_1` from the [FATE sample collection](https://fate-suite.ffmpeg.org/vvc-conformance/), at one/four threads with pinned upstream CRC references. Neither those six runs nor sample-free FATE is the complete external-sample corpus. Downloaded samples remain local, not public source/build material.

## Final `.2` engineering and material review

Compiler warnings remain visible: **57 x264 / 82 FFmpeg warning lines**, including repeated bit-depth instantiations. No blanket suppression, codec removal or thread-stack increase was used to claim a warning-free build.

Source-contract review and focused probes addressed unused/fallthrough, bounded formatting, x264 alignment/lifetime/two-pass and FFmpeg string/array-bound families. Finite index models were distinguished from actual decoder execution. The review retained the original valid-interface failures and corrected harness retries; no reproduced functional defect was waived as a warning. Related [AAC PR 25478](https://code.ffmpeg.org/FFmpeg/FFmpeg/pulls/25478) and [VVC PR 25480](https://code.ffmpeg.org/FFmpeg/FFmpeg/pulls/25480) were recorded open/unmerged at that historical review; no proposed patch was imported as an accepted fix.

On the **actual corrected build**, nine audit-recompiled units matched their original code/unwind sections and reported **347 static frames**. The largest measured frame was 328,368 bytes; the selected inter/thread/executor subtotal added 8,296 bytes, not a complete program/CRT/OS bound. Four observed 2 MiB workers passed **288 normal-interface DSP calls**, including live 1 MiB caller padding, without reproduced stack exhaustion. Earlier compensated weighted `.1` calls were diagnostic only and are not counted as production-interface passes.

Audit-only relinks of both tools matched five original code/data/unwind sections each and selected the same seventeen archive names, including static winpthreads/libgcc/MinGW material. [Compiler frame output](https://gcc.gnu.org/onlinedocs/gcc/Developer-Options.html) and [Windows stack defaults](https://learn.microsoft.com/en-us/windows/win32/ProcThread/thread-stack-size) informed the scoped assessment. Residual complete-program stack, arbitrary malformed-input, sanitizer and untested interpolation uncertainty remains; selected passing probes are not a blanket safety certificate.

## Source/build material boundary

The immutable **27-file** material archive is bound in [distribution provenance](../../Tools/ffmpeg/DISTRIBUTION_README.txt), the legal manifest and package checks:

```text
GlyphFrame-ffmpeg-9.0-glyphframe.2-build-materials.zip
SHA-256 c2172927ec454ead9eed21cdc6d9be1904816fef085eb539492c3d54a035f5aa
```

It retains exact FFmpeg/x264/zlib archives, upstream x264 ancestry, patch, regression, recipe/helpers, locked inventory, original-script license and full runtime notices. It excludes application/toolchain executables and private GlyphFrame history. All streams were verified; all **15 executable recipe/input/lock/patch/regression files** match the successfully replayed archive. The final documentation/export-scope refresh did **not** receive another literal compilation. Later application helper/doc edits do not relabel this archive or earlier binaries.

[Runtime material](../../scripts/ffmpeg/runtime-notices.md) binds six full GCC/Runtime Exception/MinGW/winpthreads texts and eleven objects. Static pthread code was observed despite FFmpeg `--disable-pthreads`; absence of a pthread DLL does not prove absence of static code. The [GCC exception FAQ](https://www.gnu.org/licenses/gcc-exception-3.1-faq.html.en) describes conditional eligible-compilation permissions, not a reason to omit notices. MinGW's [COPYING](https://github.com/mingw-w64/mingw-w64/blob/master/COPYING) distinguishes its components' terms.

[Distribution preparation](../../scripts/ffmpeg/distribution-notices.md) retains the required IJG credit and complete pinned JPEG source-header notices. GlyphFrame makes no additional changes to those three JPEG files. Full reviewed texts now ship in `licenses/FFmpeg/`, indexed by the manifest and About viewer; this notice rollout is completed locally, not merely planned.

## Local integration and remaining gates

The exact first corrected `.2` pair is selected from ignored `artifacts/local-runtime/controlled-9.0-glyphframe.2/`. [Initialization](../../Tools/ffmpeg/README.md) accepts only the pinned two-file local archive; no public download/vendor fallback or arbitrary-tool switch is configured. Different replay EXE hashes require new review/pins, not silent substitution.

Current tests and package checks bind actual tool bytes to version, source-material identity and twelve required notices. Clean-source commit `e43fada` now has new hash-bound ZIP/Setup candidates, with direct original-name extracted startup/session cleanup verified. The earlier dirty-source native workflow used a byte-identical renamed apphost because of a computer-use helper identity collision; failed targeting/termination remains separate from normal-close cleanup, and that helper was not exercised by the new probe. [Testing](../testing.md#current-extracted-zip-native-smoke) owns current and earlier scope; this ADR does not transfer older native/installed/user acceptance.

Local integration is complete within that scope. Remaining work is explicit:

- Review source delivery/hosting, final application/GPL scope and Windows-native protective terms; retain all notice/source identities.
- Retain the completed private clean-source binding and identify artifacts for remaining affected native/final-publication checks. Current unsigned Setup was not installed/run; current clean-PC and other limits remain in [testing](../testing.md#remaining-release-boundaries).
- Prepare a separately audited [history-preserving publication copy](../portfolio.md#planned-route-preserve-development-history-in-a-reviewed-copy); retain the private baseline/backups and remove historical executables from intended public history.
- Obtain owner authorization before tagging, uploading or publishing.

The experimental builder itself does not activate tools and still rejects true `WarningReviewComplete`, `CorrespondingSourceApproved` and `PublicDistributionApproved` flags. Scoped engineering success and local integration do not silently approve complete Corresponding Source, every warning or public distribution.
