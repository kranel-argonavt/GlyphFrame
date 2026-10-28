/* GlyphFrame regression harness (MIT); links separately against GPL FFmpeg.
 * Test only the declared DSP interface: no argument compensation or patched oracle.
 * Integer positions/unit spatial ratio must match ordinary weighted prediction.
 */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "libavcodec/vvc/dsp.h"
#include "libavcodec/vvc/data.h"
#include "libavutil/cpu.h"

enum { SIDE = 272, GUARD = 64, CAPACITY = 128 * (128 * 2 + 32) + 2 * GUARD };

int main(void)
{
    VVCDSPContext *d = calloc(1, sizeof(*d));
    uint8_t *src = malloc(SIDE * SIDE * 2);
    uint8_t *actual = malloc(CAPACITY), *reference = malloc(CAPACITY);
    int cases = 0, result = 1;
    if (!d || !src || !actual || !reference) goto done;
    av_force_cpu_flags(0);
    for (int depth = 8; depth <= 12; depth += 2) {
        int bytes = depth == 8 ? 1 : 2, maximum = (1 << depth) - 1;
        for (int i = 0; i < SIDE * SIDE; i++) {
            int value = (31 + 37 * i + 11 * (i / SIDE)) & maximum;
            if (bytes == 1) src[i] = value;
            else ((uint16_t *)src)[i] = value;
        }
        ff_vvc_dsp_init(d, depth);
        for (int chroma = 0; chroma < 2; chroma++) {
            const int8_t *f = chroma ? &ff_vvc_inter_chroma_filters[0][0][0] : &ff_vvc_inter_luma_filters[0][0][0];
            for (int log_width = 1; log_width <= 7; log_width++) {
                int width = 1 << log_width, stride = width * bytes + 32;
                const int heights[] = { 1, 8, 128 };
                for (int hi = 0; hi < 3; hi++) {
                    int height = heights[hi];
                    for (int denom = 0; denom <= 7; denom++) {
                        const int weights[] = { 1 << denom, (1 << denom) + 1, -1 };
                        const int offsets[] = { -8, 0, 5 };
                        for (int wi = 0; wi < 3; wi++) for (int oi = 0; oi < 3; oi++) {
                            int weight = weights[wi], offset = offsets[oi];
                            const uint8_t *base = src + (8 * SIDE + 8) * bytes;
                            memset(actual, 0xa5, CAPACITY); memset(reference, 0xa5, CAPACITY);
                            d->inter.put_uni_w_scaled[chroma][log_width - 1](actual + GUARD, stride, base,
                                SIDE * bytes, height, 0, 0, 1024, 1024, height, denom, weight, offset, f, f, width);
                            d->inter.put_uni_w[chroma][log_width - 1][0][0](reference + GUARD, stride, base,
                                SIDE * bytes, height, denom, weight, offset, f, f, width);
                            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
                                int value = bytes == 1 ? base[y * SIDE + x] : ((const uint16_t *)base)[y * SIDE + x];
                                int expected = ((value * weight + (denom ? (1 << (denom - 1)) : 0)) >> denom) + offset * (1 << (depth - 8));
                                if (expected < 0) expected = 0;
                                if (expected > maximum) expected = maximum;
                                const uint8_t *a = actual + GUARD + y * stride;
                                const uint8_t *r = reference + GUARD + y * stride;
                                int got = bytes == 1 ? a[x] : ((const uint16_t *)a)[x];
                                int ordinary = bytes == 1 ? r[x] : ((const uint16_t *)r)[x];
                                if (got != expected || ordinary != expected) {
                                    fprintf(stderr, "FAIL depth=%d chroma=%d w=%d h=%d denom=%d weight=%d offset=%d x=%d y=%d got=%d ordinary=%d expected=%d\n",
                                        depth, chroma, width, height, denom, weight, offset, x, y, got, ordinary, expected);
                                    goto done;
                                }
                            }
                            /* Check prefix, per-row padding, and all bytes beyond the logical output. */
                            for (int i = 0; i < CAPACITY; i++) {
                                int relative = i - GUARD;
                                int writable = relative >= 0 && relative / stride < height && relative % stride < width * bytes;
                                if (!writable && (actual[i] != 0xa5 || reference[i] != 0xa5)) {
                                    fprintf(stderr, "FAIL output guard index=%d case=%d\n", i, cases); goto done;
                                }
                            }
                            cases++;
                        }
                    }
                }
            }
        }
    }
    if (cases != 9072) goto done;
    printf("PASS %d weighted scaled cases: declared interface, independent formula, ordinary callback, output guards\n", cases);
    result = 0;
done:
    free(d); free(src); free(actual); free(reference);
    return result;
}
