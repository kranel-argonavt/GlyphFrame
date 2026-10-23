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
| October 21: notices-button style | Existing neutral button surface replaces the transparent text style without changing commands or placement. This was a style-only packaged follow-up, not a release. |

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

## Image navigation October 22-23

October 22 introduced transient still-image navigation using the existing visual: cursor-anchored Ctrl+wheel, bounded drag, accepted-media reset versus cancelled/failed retention, normalized framing across Original/Symbolized and rerender, and DPI-aware scale. That version had − / percentage / + / Fit controls. October 23 deliberately removed the toolbar and its height/margins, set zoom-out's floor to centered Fit, and restored Fit tracking when resize overtakes manual scale. Video Fit/Fill/100% remains independent; render identity, pixels, export and preferences are unchanged by viewing.

| Recorded implementation | Measured verification | Native scope |
| --- | --- | --- |
| October 22 corrected toolbar | 1,196 solution tests (488 + 708), 131 focused; Release/format/diff passed; 20 packaging + 15 session helper checks. | Extracted corrected portable: Fit/100%, drag, zoom buttons, ordinary wheel pan, equal-raster Original/Symbolized, maximize/restore and basic video playback/completion. Ctrl+wheel, mixed DPI, full minimum/resize matrix, different-raster native review and full A3 were NOT TESTED. |
| October 23 gesture-only/Fit floor | 1,200 solution tests (488 + 712), 347 focused; Release/format/diff passed at that run. | New bytes NOT natively accepted. Fixture text-size checks were targeted to the removed row; unrelated fixed-height clipping remained visible at larger text. |
| Independent October 23 review | Forced Release rebuild, 1,200 tests and formatting passed; diff check failed on newly observed `.gitignore` trailing whitespace. | Synthetic populated WPF probe confirmed swallowed ordinary wheel: Fit outer offset 100→100 versus direct outer wheel 100→148. This is routed-event evidence, not native input acceptance. |

Failed attempts are preserved: initial Canvas/import and synchronous-command test compilation errors; missing fixture bindings (115/129 passing) and encoder overwrite arguments; intermediate 1,194/1,195 totals; initially invalid overlay fixtures, then regenerated; first provisional native drag failed because ScrollViewer consumed the bubbling button event. Moving capture to the preview tunnel had an intentionally failing old-handler regression, followed by one stale incremental binary run (130 passed/1 failed) after timestamp restoration; forced rebuild passed 131/1,196. The gesture-only follow-up first passed 343/347: three fixtures selected video viewports too, and one exposed tiny-extent center cancellation; selecting the image instance and retaining exact centered-axis 0.5 fixed those without relaxed geometry assertions. An initial sandbox SDK call stalled, and a stale-text patch matched nothing; corrected retries are recorded locally. Historical passes do not erase these failures or accept later bytes.

### Routed-wheel review correction

The ready-image internal scroller now bypasses its native wheel handler. The original bubbling event reaches ReviewViewport, which consumes an actual image pan or lets the same event reach its enclosing pane. Ctrl+wheel remains local, including limits. No recursive forwarding, cloned input, global hook or unrelated-scroller search is used. Directional tests also exposed reversed image-wheel pan, corrected to the normal scroll direction. Pan rejects sub-micro-DIP clamped-boundary noise without publishing geometry. Unused toolbar commands/percentage formatter were removed; tests call the retained geometry operations directly. Legacy video retains native internal scrolling.

The correction passed a forced nonincremental Release rebuild (zero warnings/errors), **1,212 solution tests (488 core + 724 App), 359 focused**, 20 package helpers, 15 session helpers, formatting and diff checks. The unchanged audit probe now moves the outer offset **100→148 at both Fit and the zoomed lower boundary**, matching direct outer input; inner scrollable height is 0 and outer scrollable height 938.4. Actual Image routed-event tests cover directional boundaries, single delivery/scroll owner, local Ctrl dispatch/limits and legacy video scrolling. Ctrl tests deterministically dispatch the production modifier branch through the real routed hierarchy; they do not inject an OS-held Ctrl key.

This run's first isolated probe build accidentally globbed archived C# snapshots; limiting its compile items to the original Program.cs fixed the diagnostic project. The first focused run passed 340/359: 15 exact-type assertions assumed the child could not derive from ScrollViewer, and four directional cases exposed reversed wheel-pan. Tests now accept the intentional ScrollViewer subtype while retaining all style, extent, geometry and native-scroll assertions; the pan direction was corrected, not the directional expectations. One local-link check found three anchors using a dash inconsistent with the heading; normalized heading spelling passed the repeat. The session helpers ran separately because the package-helper script exits its shell normally. Full failed logs and retries remain local; no exceptions were suppressed.

The extracted new provisional was opened with an isolated profile and a synthetic image. Toolbar absence, centered Fit and ordinary wheel without zoom were observed, but the outer pane did not overflow. Resize targeting was inconclusive, so native constrained-height fallback is **NOT TESTED**; held-Ctrl scrolling is **NOT TESTED** because the available UI tool cannot hold a modifier during scroll. Both screenshots are explicitly native, bounded observations, distinct from the WPF probe. The owned process closed normally, its session was removed and all 416 extracted payload hashes remained unchanged. Package identity and remaining native retest are maintained in [Phase 14](Phase_14_Coverage.md#image-navigation-current-coverage) and [Phase 15](Phase_15_Packaging.md#image-navigation-package-provenance). No full A3, mixed-DPI, clean-Windows or release acceptance follows from these results.

### Additional local evidence preservation

The complete pending October 22/23 journals were copied without rewriting into the ignored `artifacts/image-navigation-correction-20261023-9e70/archive/docs/` before their public copies were removed. `archive-manifest.json` in that run records absolute original/archive paths and equal SHA-256 values. The archive was created exclusively and did not overwrite an earlier archive.

| Original repository path / archived basename | SHA-256 of original and archive |
| --- | --- |
| `docs/Image_Navigation_2026-10-22.md` / `archive/docs/Image_Navigation_2026-10-22.md` | `6ee7bdf384632031feb5dc3fe3adcf9c0fb110cf7364fc33cfe35c73bbef29bb` |
| `docs/Image_Fit_Floor_2026-10-23.md` / `archive/docs/Image_Fit_Floor_2026-10-23.md` | `552eba6f5e1b81dcb7a7a050fab946b8b700682c7d61eda520080e9eb3757c42` |

These paths identify recoverable local history, not public links or clean-clone dependencies. Resolve original journal links using their recorded original `docs/` base. Earlier October 21 archives, A1/A2 outcomes and ZIP identities are unchanged. Maintained specification/history/coverage/provenance and source/tests belong in the public candidate; complete journals, probes, logs, TRX, captures, manifests and portable packages stay local and ignored.
