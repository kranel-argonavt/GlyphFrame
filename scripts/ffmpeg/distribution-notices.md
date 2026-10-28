# Controlled FFmpeg distribution preparation

Reviewed 2026-10-28 for source profile `windows-x64-static-gpl3-experimental-2`, FFmpeg `d32b387f2b0a484599d4587d651891f0c63c4238` plus the separately pinned downstream patch. The local `.2` integration now copies the reviewed texts and credit into the application payload's legal collection. This document is **not** provenance for older Gyan packages, an end-user agreement, or public-distribution approval.

## Tool licensing and attribution

- The controlled FFmpeg/ffprobe tools are configured with GPL and version 3 enabled, including static libx264. Retain FFmpeg's full `COPYING.GPLv3`, `LICENSE.md`, applicable upstream copyright/license notices, and the exact corresponding source/build materials. Do not label these tools MIT or apply FFmpeg's LGPL-only linking checklist to this GPL configuration. [FFmpeg licensing guidance](https://ffmpeg.org/legal.html).
- Static x264 retains its own `COPYING` and copyright/attribution headers in the exact pinned source/archive and upstream history. Static zlib retains its own `LICENSE` and source notices. Neither library is relicensed by the project MIT license.
- The libgcc/MinGW/winpthreads material is additional, not the complete FFmpeg notice set. Retain the six exact texts and reviewed object/package mapping in [runtime material](runtime-notices.md); no pthread DLL import does not mean no static pthread code.
- FFmpeg's pinned `LICENSE.md` also calls out the Independent JPEG Group-derived `libavcodec/jfdctfst.c`, `jfdctint_template.c` and `jrevdct.c`. The accompanying future binary documentation must retain this credit:

  > This software is based in part on the work of the Independent JPEG Group.

  Their complete embedded copyright, no-warranty and permission notices remain in the exact FFmpeg source archive; the upstream terms must not be reduced to the credit sentence alone. GlyphFrame applies **no additional modifications to those three files relative to the pinned FFmpeg revision**. This does not claim FFmpeg's files are pristine IJG originals. The sole GlyphFrame downstream change is the [dated weighted-VVC patch](patches/README.md), outside those files.

The JPEG attribution is required by the pinned source's own notices, not by an assumption that every source file in FFmpeg has identical GPL terms. The local integration carries this credit and the complete retained notices in `Tools/ffmpeg/DISTRIBUTION_README.txt` and `licenses/FFmpeg/`, with hashes and a separate controlled-tool binding in the manifest/About collection. It does not update an already installed application or rewrite old packages.

## Corresponding Source checklist

The material ZIP currently retains exact FFmpeg/x264/zlib archives, the upstream x264 history needed for version reconstruction, the dated downstream patch and original regression, executable build-control scripts, source/toolchain/runtime locks, full upstream license material inside those archives and the additional runtime texts. The wrapper checks patch scope/preimage/postimage and source hashes; a functional replay uses separate source/work/prefix trees on the same pinned host.

The [GPLv3 definition](https://www.gnu.org/licenses/gpl-3.0.html#section1) includes source and scripts needed to generate/install/run/modify the covered work, while excluding qualifying System Libraries and unmodified general-purpose tools; automatically regenerable files need not be duplicated. The actual static runtime inventory and conditional [GCC exception](https://www.gnu.org/licenses/gcc-exception-3.1.html) must therefore be assessed by their terms, not by treating every compiler object as a new multimedia dependency or omitting all static runtime notices. This is an engineering mapping, not a legal opinion that every exception automatically applies.

Before a binary release:

1. Bind the final tool hashes, configure/build records, source/patch/material manifest and reviewed dependency/runtime notices together. A link to a moving upstream branch or a different `.1` map is insufficient.
2. Assemble the actual binary notice collection and manifest, including the IJG credit above, without changing the original application's MIT grant or imposing new restrictions on GPL components.
3. Make the exact reviewed Corresponding Source available through an appropriate GPLv3 section 6 route when conveying the binaries. For an online release, a neighboring, clearly linked downloadable source/material archive is a possible route; it is not necessary to install all sources with the app. No source hosting or written source offer is created by this document.
4. Verify the final package's offline notices and source/download links. Keep application/GPL scope, Windows-native deployment terms, media provenance and historical-binary handling as their own decisions.

`WarningReviewComplete`, `CorrespondingSourceApproved` and `PublicDistributionApproved` remain false in this experimental workflow. A successful source ZIP integrity check is not approval of distribution or a claim of bit-identical/independent-host rebuilding.
