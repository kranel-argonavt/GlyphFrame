# Local FFmpeg weighted-scaling correction

`0001-vvc-weighted-scaled-contract.patch` is a GlyphFrame downstream correction against FFmpeg revision `d32b387f2b0a484599d4587d651891f0c63c4238`, prepared 2026-10-28 by Nazar Khozin's GlyphFrame project. It is **not represented as an accepted upstream patch**. It changes only `libavcodec/vvc/inter_template.c`:

- Match the two weighted wrappers to the existing callback/caller order `height, denom, wx, ox`.
- Include the weight denominator in the scaled weighted output shift.

The upstream archive remains pristine. `sources.lock.json` separately records the patch's raw LF SHA-256 and the exact source preimage/postimage. The wrapper applies it only to a newly extracted tree, checks every source file for unlisted changes, and retains patch logs. Existing candidates/material ZIPs are not relabeled or overwritten. Candidate `.2` is distinct from the no-patch `.1` baseline.

The actual-library regression checks the declared callback interface at integer positions/unit spatial ratio against both an independent weighted-pixel formula and the ordinary weighted callback. It covers 8/10/12-bit luma/chroma, seven widths, three heights, eight denominators, three weights and three offsets, including negative weights, clipping and output guards: 9,072 combinations. It must fail against the original source and pass against the corrected source. This is not complete RPR/interpolation-phase, malformed-input or sanitizer coverage.

The diff modifies FFmpeg source; FFmpeg's upstream copyright/license terms remain applicable. It is not relicensed by the repository's original-script MIT license. The original regression harness/driver are MIT; binaries linked with FFmpeg are separate GPL tool-test artifacts and remain local. No upstream submission or public-distribution approval is implied.
