# Publication preparation

## Publication scope

The agreed target is **the existing project with its reviewed development history and ready-to-run downloads through GitHub Releases**. It is not a new root commit, a shallow public history or a permanent source-only distribution. Work in a separate reviewed copy to protect the private original; that copy preserves the development story rather than replaces it.

| Publication channel | Intended contents |
| --- | --- |
| Git repository and reviewed history | Original application code, tests/synthetic fixtures, project-owned assets, maintained docs, build/installer scripts, MIT and third-party notices; preserve legitimate development commits, messages and authorship |
| GitHub Release assets | Self-contained Setup and portable ZIP bundling the controlled tools/runtime, plus exact FFmpeg tool/source-build materials, complete notices and checksums through the reviewed delivery route |

Generated EXEs, archives, restored packages, caches, personal settings, unrelated media and private journals do not belong in Git. Release assets are not committed blobs, and a draft/provisional file is not automatically approved for upload. End users should launch the installed/extracted application without separately installing .NET or FFmpeg; explicit pinned tool preparation is a developer build step.

The root [README](../README.md) explains source builds; [licensing](licensing.md) records the separate repository and binary-release gates. The local gallery uses project branding, a startup-screen hero, an AI-generated car comparison and a linked symbolized video; additional screenshots are collapsed. [Media notes](screenshots/README.md) retain the remaining standalone-video permission assessment. Local preparation is not publication approval.

## Planned route: preserve development history in a reviewed copy

The original development Git history contains FFmpeg executables. Removing them from the current tree or adding `.gitignore` does not remove historical objects. Do not make that repository public, mirror all refs, or push its history to the portfolio repository without a separately reviewed history decision.

The owner has chosen to retain the development story rather than start a new public history. The sequence is agreed and **partially completed**; no public-history copy has been prepared:

1. **Private baseline checkpoint completed.** The owner committed the notices/gallery/documentation baseline as `e75a021`. Its verified private backup of reachable history, then-uncommitted files and tools remains separate; keep the bundle, mirror, metadata and diagnostics outside public source.
2. **Controlled integration and private clean-source verification completed.** Commit `e43fada` contains the `.2` recipe, exact inputs/patch/runtime identities, notices and application integration. A fresh checkout passed build/tests and produced clean-source ZIP/Setup, plus independent extracted startup/session cleanup; see [verification scope](testing.md#recorded-verification-snapshot). The separate versioned cache preserves the old Gyan cache. Source hosting/public distribution remains open. Replacing current tools does not clear historical executable objects or Windows-native/media gates; earlier artifact identities remain immutable.
3. **Local FFmpeg delivery assembly completed; distribution review pending.** The unchanged inner tool ZIP and frozen source/build ZIP are grouped with all twelve required documents, checksums and draft relative source directions. [Candidate verification](packaging.md#controlled-ffmpeg-delivery-candidate) covers actual archive/extraction integrity, not final Corresponding Source approval or hosting. No public URL/upload exists.
4. **Review remaining release terms and assets.** Decide the scoped Windows-native distribution-terms mechanism and final application/GPL scope without replacing the original MIT grant; settle media review. These remain owner/reviewer decisions, not implied agreement from packaging. Actual hosting/source availability and final artifact acceptance also remain open.
5. **Prepare the history-preserving publication copy.** Remove the two historical FFmpeg/ffprobe executable paths and audit all refs/objects intended for publication for other private/unreviewed content. Keep the original repository/recovery copy private. Preserve legitimate development commits, messages and authorship where possible; affected commit IDs and descendants change. Do not create a fresh public root or promise old hashes/private app refs remain public.
6. **Verify and authorize publication.** Test the copied history/fresh checkout and bind final packages/source assets to that copy's actual identities. Review scope and close relevant binary gates, then obtain owner approval before changing remotes, pushing, tagging or uploading. Repository and Releases remain separate channels within the same agreed target, not a switch back to a source-only plan.

No history filtering, public push or controlled-tool build is performed by documenting this plan. Private backup locations and evidence dumps are deliberately not public documentation dependencies.

The one-commit shallow clone used for clean-source verification is not the publication-copy step and must not replace the agreed development history. After filtering, recheck that copy under its changed commit identities; do not relabel private `e43fada` artifacts as built from future IDs. The Git channel needs history/asset/content review; the planned bundled Release assets additionally need the separate [distribution decisions](licensing.md#next-distribution-decisions). Neither channel is uploaded implicitly.

The [local FFmpeg delivery candidate](packaging.md#controlled-ffmpeg-delivery-candidate) supports those planned Release assets; it is not a replacement application repository or an instruction for end users to assemble their own tools. GitHub's autogenerated project-source archive must not be substituted for the reviewed FFmpeg material archive: exact upstream input archives are not tracked in Git.

## Available fallback: export current source without history

The existing snapshot workflow is **not selected for publication**. It remains a backup/inspection option and could become a publication alternative only after an explicit owner decision to abandon the chosen history-preserving route. Do not use its history-free output as the agreed public repository. From the private development repository, after reviewing current changes:

```powershell
pwsh -NoProfile -File scripts/Export-PortfolioSource.ps1
```

The script creates a fresh ignored `artifacts/portfolio-source/<run-id>/GlyphFrame/` containing current tracked/nonignored source files, including uncommitted edits, but no `.git`. It rejects unexpected root paths, executables, archives, generated/private directories, linked files and unreviewed media locations. It verifies every copied SHA-256 and rechecks source/status stability. Only the two deliberately removed tracked tool EXEs may be absent. Unknown/missing content fails rather than being silently omitted.

Sibling `source-manifest.json` and `report.json` are local evidence, **not part of the public source folder**. They record the parent revision and dirty state; a snapshot is not a clean commit, Git clone or binary release. This is a conservative path policy, not a complete content/secret scanner. Review text, asset rights, links and the manifest before publication.

## Verify a history-free snapshot before publication

1. Build a fresh snapshot without its own ignored cache: `dotnet restore GlyphFrame.sln`, then `dotnet build GlyphFrame.sln -c Release --no-restore`. Confirm the missing-video-tools guidance and absence of FFmpeg/ffprobe in output; do not rely on stale `bin/` contents from an earlier tool-enabled build.
2. For full video/tests, explicitly use `scripts/Initialize-LocalTools.ps1 -ArchivePath` with the exact reviewed `.2` archive, rebuild and run the full [test commands](testing.md). No public download or `-Download` mode is configured yet; see [tool preparation](../Tools/ffmpeg/README.md). Build/acquisition do not imply binary-distribution approval.
3. Review public text/assets for personal paths, credentials and unrelated media. Preserve the MIT license, legal texts, exact pins, synthetic fixture provenance and unresolved dependency findings.
4. For this fallback only, and after owner approval, create a new public Git history from the reviewed **source folder**, not from the private development `.git` or the snapshot's evidence parent. Do not copy old refs/remotes/objects or attach local Setup/ZIP files. No script here initializes Git, stages, commits, pushes, tags or publishes.

The private development repository, ignored tools, backups and candidates remain available. The installer was not removed or rolled back; the planned ready-to-run Releases require closing their separate [licensing and distribution gates](licensing.md#open-binary-distribution-gates).
