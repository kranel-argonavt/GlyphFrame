# Controlled-tool runtime material

This collection belongs to the **controlled FFmpeg workflow**, not the earlier Gyan pair. The application now retains verified copies of these six texts under `licenses/FFmpeg/`, indexed by its legal manifest; the source collection/locks remain experimental and do not approve public distribution. Review date: 2026-10-28.

## What is retained

`runtime.lock.json` maps three exact MSYS2 package versions to eleven static/import/startup object hashes and six **byte-for-byte installed notice texts**. The recipe checks the host objects and the retained texts before execution and after building. The material exporter verifies and includes the texts and this mapping without copying the compiler or runtime object binaries. Git attributes prevent line-ending conversion for these six texts. A changed package, object or notice must fail rather than silently inherit this review.

| Material | Package / source identity | Treatment |
| --- | --- | --- |
| libgcc, crtbegin/crtend | `mingw-w64-ucrt-x86_64-gcc 16.2.0-6`; upstream GCC release `16.2.0` | Preserve [GPLv3](notices/gcc/COPYING3.txt) and [Runtime Library Exception 3.1](notices/gcc/COPYING.RUNTIME.txt). The exception is conditional on covered files and eligible compilation, not a blanket waiver for unrelated code. |
| MinGW CRT, startup, Windows import libraries | `mingw-w64-ucrt-x86_64-crt 14.0.0.r569.g34eeafb7d-1`; upstream revision prefix `34eeafb7d` | Preserve the entire installed [general grant](notices/mingw/COPYING.txt), [runtime notices](notices/mingw/COPYING.MinGW-w64-runtime.txt) and [scope explanation](notices/mingw/COPYING.MinGW-w64.txt). Do not prune attributions based on an incomplete per-object inference. |
| Static winpthreads | `mingw-w64-ucrt-x86_64-winpthreads 14.0.0.r569.g34eeafb7d-1`; same upstream revision prefix | Preserve the **whole** [MIT/Lockless BSD notice](notices/winpthreads/COPYING.txt). Static linkage remains relevant even when no pthread DLL is imported. |

The identities are the installed package evidence, not a claim that an upstream tag alone reconstructs MSYS2's patched package. The pinned 150-package host inventory remains in `toolchain.lock.json`; individual static objects receive additional raw hashes here. This is not a complete hermetic toolchain image.

MinGW's own scope document recommends retaining its complete runtime notice set. Some sections describe optional parts and are not assertions that all those parts occur in the tools. The compiler/MinGW sources and package recipes are independently available from [GCC](https://gcc.gnu.org/git.html), [MinGW-w64](https://github.com/mingw-w64/mingw-w64) and [MSYS2 MINGW-packages](https://github.com/msys2/MINGW-packages); links are provenance, **not a replacement for Corresponding Source where required**. Compiler binaries are development tools and are not included in the material ZIP or proposed application payload.

## Linking and compilation boundary

The reviewed recipe compiles ordinary C and assembly using GCC/NASM and links static libraries, without proprietary GCC-IR optimization plugins or an LTO pipeline. The [GCC Runtime Library Exception](https://www.gnu.org/licenses/gcc-exception-3.1.en.html) permits eligible target-code combinations for covered runtime files; this engineering assessment does not relicense original GlyphFrame code or certify every dependency's terms. Preserve the exact exception, compiler identity and recipe evidence rather than extending the exception to all GNU programs.

The current linker-map audit identifies FFmpeg's seven libav libraries, x264, zlib, libgcc, MinGW CRT/import libraries and static `libpthread.a`, plus CRT startup objects. A `-latomic` option in a command is not proof that any libatomic member was included: selected members in the map must be distinguished from command-line library searches. Imported Windows DLL names do not mean the DLL implementations are bundled. The public runtime lock contains no developer absolute paths.

FFmpeg/x264/zlib retain their own complete license/copyright material in the pinned source archives; these six texts are **additional runtime notices**, not a substitute. The [distribution preparation](distribution-notices.md) also retains the IJG credit explicitly required by FFmpeg's embedded JPEG notices for future binary documentation. These materials are deliberately separate from the root MIT license. The linker-map audit above describes the unpatched `.1` baseline. Source profile `.2` subsequently adds the separately documented [downstream VVC correction](patches/README.md), with the same pinned host/runtime objects and notices; no unmerged upstream patch is represented as accepted. This clarification does not itself assert a new `.2` linker-map audit or change runtime permissions.

## Before isolated application testing

Require exact candidate/output hashes, native contract probes, runtime object/notice verification and a recorded compiler-warning disposition. Isolated testing can investigate unresolved warnings with trusted synthetic media; it is **not release acceptance**. Do not feed untrusted media into an experimental candidate merely because smoke tests passed. Do not remove builtin decoders, suppress diagnostics, or change the active cache to make this gate appear green.

Memory/resource-sensitive diagnostics remain a separate release gate. The available FATE run excludes external samples; checkasm and successful H.264/AAC round trips do not certify all decoders, malformed-input safety, every two-pass path, or worst-case thread-stack usage. `WarningReviewComplete`, `CorrespondingSourceApproved` and `PublicDistributionApproved` remain false until their own evidence is reviewed. Full GlyphFrame integration, package/native acceptance, Windows-native distribution terms and publication-history review are still separate tasks.
