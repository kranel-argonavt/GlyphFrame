# UI refinement history and acceptance

Consolidated on 2026-10-21 from the October 11–21 iteration records. This is a maintained change/verification summary, not a new test run or release sign-off. Historical observations apply only to their recorded builds and conditions.

The [technical specification](GlyphFrame_Technical_Specification_v1.0.md#108-processing-and-result-panel) owns current behavior and geometry. [Phase 14](Phase_14_Coverage.md) owns broader coverage and the consolidated release acceptance checklist; [Phase 15](Phase_15_Packaging.md) owns packaging rules, the latest recorded candidate and dated extracted-package outcomes. Do not use superseded iteration measurements as current requirements.

## Changes and superseded decisions

| Iteration | Retained outcome / historical distinction |
| --- | --- |
| October 11–13: preview/status and Copy path | Selection-driven tabs, neutral planned output, opacity-only transitions and explicit successful-destination copying. Early fixed-height shells, future-action reservations and hysteresis were intermediate designs, replaced on October 19. |
| October 14–18: settings composition and visual polish | Compact Input/Characters, finite Render parameters with persistent Video audio, accepted-artifact FILE SIZE rather than completed FRAMES, local vectors, passive unavailable Audio and stable Copy feedback. Earlier shell heights/action budgets are not current policy. |
| October 19: size-matrix refinement | Status measures populated content first; preview receives the remaining bounded height. Centered compact envelopes, shared edges and equal/orphan metric rows replace the old fixed shells and internal status scrolling. |
| October 19: progress and spacious mode | Readable 54-DIP progress host, stationary percentage, centered whole action groups and a measured single-row result when all groups fit. |
| October 19: working center and Save | Circle centers against the complete progress body; stacked Cancel is excluded. Image format selector and its invisible width budget are removed. Native Save offers PNG/JPEG together; the accepted filter controls encoding and extension normalization. Settings supplies only the initial choice. |
| October 19: replacement and sizing | Accepted replacement prepares workflow/ownership before notifying media/status bindings, preventing old result and new planned cards appearing together. Cancelled/failed replacement retains the accepted result. Cards use bounded nominal widths with readability exceptions; standalone Export shrinks before reflow, while exported groups change paired/rail/compact composition. |
| October 20: Input and draft Settings | Removed Formats pseudo-control; readiness precedes wrapping metadata. Reset loads the draft only; Cancel/dismissal rolls back and Save applies. Paths are selectable read-only fields. The Render placeholder was removed in this iteration, then deliberately restored in the next one. |
| October 20: live logo and placeholder | Per-image cloned vector drawings retain live accent resources. The restored noninteractive Coming soon overlay appears only in measured spare space, without increasing desired height or scroll extent. |
| October 21: About/footer/actions | Shared Header/About logo template; product, Privacy and Third-party software content; explicit inert local notices viewer. Privacy text leaves the main footer. Reset moves into the persistent Settings action bar with conditional feedback. |
| October 21: notices-button style | Existing neutral button surface replaces the transparent text style without changing commands or placement. This is the latest recorded packaged UI change, not a new release. |

## Recorded verification

These are dated final suite outcomes, not cumulative counts and not newly executed by this documentation cleanup:

| Focused iteration | Full Release tests passed | Native evidence scope |
| --- | ---: | --- |
| Size-matrix refinement | 1,111 (488 core + 623 App) | No native screenshots: approval timed out; WPF captures are fixtures only. |
| Progress/spacious correction | 1,118 (488 + 630) | Partial ordinary/maximized/restored workflows, cancellation, retained export failure/Details/retry. |
| Working center / image Save | 1,128 (488 + 640) | Partial progress centering; real PNG/JPEG outputs from one render, filter switching and cancelled Save retention. |
| Replacement/sizing | 1,146 (488 + 658) | Accepted A/B replacements, failed/cancelled retention, exports and ordinary/maximized/restored layouts. |
| Input/Settings polish | 1,166 (488 + 678) | Input/replacement, draft Reset, Cancel/title-close/Save and ordinary About; native Escape/copy targeting was unreliable. |
| Logo/placeholder | 1,168 (488 + 680) | Emerald/Violet preview via Reset, Cancel/Save/restart and placeholder maximize/restore; not direct six-preset native editing. |
| About/footer/actions | 1,177 (488 + 689) | Shared logos, About/notices/focus return, Settings actions, draft Cancel and save failure/retry/restart. |

The notices-button-only follow-up passed 13 focused regressions and package verification; it did not itself rerun the full suite. The later independent repository audit did rerun the complete 1,177 tests successfully, with zero failures/skips, a zero-warning/error Release build, formatting verification, 20 portable-helper checks and 15 session-helper checks. That audit did not rebuild a package or perform native UI acceptance.

Recorded package checks verify structural/tool/startup/session behavior and payload hashes, not every extracted-window workflow. The latest candidate identity and its separate native limitation are maintained only in [Phase 15](Phase_15_Packaging.md#latest-recorded-candidate).

Native screenshots, WPF-rendered fixtures and user reports are distinct evidence. Screenshot tool bounds are not independently measured WPF DIPs. The user's earlier successful Settings Escape, path copying and sizing/DPI checks are attributed to the user's unspecified tested conditions; exact build, scales and monitors were not supplied and are not invented.

## Failures, retries and unresolved risks

- Earlier October 11–15 suite repetitions aborted with an unhandled WPF Dispatcher signature while other complete runs passed. The root cause was not proven; no exception suppression or native-product-crash conclusion follows from these records.
- A Copy-feedback geometry test failed at an immediate asynchronous-completion assertion after cancellation, with preceding geometry assertions passing. Subsequent passing repeats did not eliminate that ordering flaw. The October 21 test-only correction now injects a controlled hold, explicitly verifies reset/cancellation and unchanged geometry while the command is still pending, then awaits delayed cancellation completion on its STA Dispatcher with a bounded timeout. All six widths and both feedback outcomes remain covered; ten independent corrected-test runs passed 60/60 cases. This resolves the identified completion-order assumption, not every historical Dispatcher-host risk, and changes no production behavior.
- Test-development failures included obsolete layout assumptions, floating-point/device-rounding comparisons, missing imports, loose-resource/capture setup and one dispatcher wait. Corrected final runs are the table's outcomes; failed attempts are not relabeled as passes.
- SDK/sandbox restrictions, a helper-name error, an overlapping build/test DLL lock and a verification-root path-budget failure required corrected or ordinary-environment retries. These are not unqualified product failures or passes.
- Several native resize, modal keyboard and dropdown targeting attempts did not exercise the requested case reliably. The latest style-only extracted instance exited before targeting, with no established cause/exit code, despite passing package startup. It has no native visual pass.

## Remaining acceptance

Phase 13 UI is implemented with these follow-ups. Broader Phase 14 remains open; Phase 15A packaging is implemented; Phase 15B is partial/open; Phase 15C clean-Windows execution without separately installed .NET/FFmpeg is NOT TESTED.

The [release acceptance checklist](Phase_14_Coverage.md#release-acceptance-checklist) is the single current worklist for native layouts, dialogs, theme/DPI, playback/audio, operation boundaries, committed-source packaging, clean Windows and portfolio/distribution readiness. It supersedes the duplicated worklist previously kept here, without changing the historical observations above. Commit `75aad73` now contains these UI refinements; no fresh package or additional acceptance is implied by that commit. The identified Copy-feedback completion-order assumption is resolved as recorded above, while unattributed historical Dispatcher-host risk is not relabeled as resolved.

## Evidence retention and repository policy

Seven detailed October 19–21 handoffs and pre-consolidation snapshots of Phase 14/15 were preserved byte-for-byte in the local ignored `artifacts/iteration-journal-archive-20261021/`, with a SHA-256 manifest and original repository paths. Build logs, TRX, captures, packages, identities, failed attempts and older smoke reports remain in their existing ignored artifact directories; no evidence payload was deleted.

The local archive is not a Git/public documentation dependency and may be absent in a clean clone. Original archived Markdown keeps its original bytes and original `docs/` relative-link base; use the manifest's source path when resolving those historical links. Only this summary and maintained documentation belong in the proposed public change. Original reports are recoverable locally, but their run-specific values must not be treated as the current specification.
