GlyphFrame controlled FFmpeg tools - local integration candidate
Version: 9.0-glyphframe.2 (Windows x64; GPL version 3 or later)
Prepared: 2026-10-28, Nazar Khozin (kranel-argonavt)

This collection describes the controlled tools, NOT Gyan's earlier build.
It does not approve public binary distribution or settle application/GPL,
Windows-native deployment, media or historical-repository questions.

Binary identities (raw SHA-256):
ffmpeg.exe  a01e45417cfa050c11151a2bf609e983408b8fa688e9d6ab66cb223eba9e7dbe
ffprobe.exe e9fde8cdd23dd3534d15ff49b6788f80328f916634ae81bf7f8b9331da4ee2a9

Source and changes:
FFmpeg d32b387f2b0a484599d4587d651891f0c63c4238, upstream RELEASE 9.0.
https://github.com/FFmpeg/FFmpeg/commit/d32b387f2b0a484599d4587d651891f0c63c4238
x264 0480cb05fa188d37ae87e8f4fd8f1aea3711f7ee (GPLv2 or later).
https://code.videolan.org/videolan/x264
zlib da607da739fa6047df13e66a2af6b8bec7c2a498 (v1.3.2; zlib license).
https://github.com/madler/zlib/commit/da607da739fa6047df13e66a2af6b8bec7c2a498

One GlyphFrame downstream patch, prepared 2026-10-28:
scripts/ffmpeg/patches/0001-vvc-weighted-scaled-contract.patch
SHA-256 668e3930142242aba07f58dbf624f2adc95f5551da479e5a06c54b995f057cd0
Only libavcodec/vvc/inter_template.c changes: weighted callback argument
ordering and denominator-aware shift. This is not an accepted upstream patch.
The source archive remains pristine; the patch is applied to a fresh extracted
tree with exact preimage/postimage and full-scope checks.

Build: pinned MSYS2/UCRT64, GCC 16.2.0, NASM 3.02; static x264/zlib;
--enable-gpl --enable-version3 --disable-autodetect --disable-network;
baseline x86-64 with runtime CPU dispatch, no host-only target or nonfree parts.
The full offline recipe, configure flags, inputs and host/runtime inventory are
retained in scripts/ffmpeg and the exact build-material archive. Static libgcc,
MinGW CRT/import/startup objects and winpthreads are included; absence of a
pthread DLL does not mean absence of static pthread code.

Source/build materials (local, NOT a published source offer):
GlyphFrame-ffmpeg-9.0-glyphframe.2-build-materials.zip
SHA-256 c2172927ec454ead9eed21cdc6d9be1904816fef085eb539492c3d54a035f5aa
27 entries; source archives, upstream x264 history, patch, original regression,
scripts, locks and notices. Executable build inputs match the independently
replayed material ZIP; the latest documentation-only refresh was not literally
rebuilt again. Functional replay is not bit-identical/independent-host proof.
No public download/source-hosting location is configured. A binary release needs
final source/hash binding and an appropriate GPLv3 source-delivery route.

Notices:
Tools/ffmpeg/LICENSE.txt: full GPLv3 text.
licenses/FFmpeg/: FFmpeg license overview, x264 GPL text, zlib text, full
libgcc GPL/Runtime Library Exception, MinGW and winpthreads notices, and complete
pinned JPEG-source header notices. Original source copyright/permission notices
remain in the exact upstream archives; nothing is relicensed as MIT.

This software is based in part on the work of the Independent JPEG Group.
GlyphFrame makes no additional changes to FFmpeg's jfdctfst.c,
jfdctint_template.c or jrevdct.c relative to the pinned FFmpeg revision.
This does not claim those upstream files are pristine IJG originals.

Original GlyphFrame application/scripts: MIT; copyright 2026 Nazar Khozin.
FFmpeg/x264 and other third parties retain their own licenses. Separate CLI
execution alone does not determine aggregate-versus-combined-work status.
Public-distribution approval remains false. These records document provenance
and integrity, not legal advice or a compliance certificate.
