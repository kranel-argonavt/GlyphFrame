# Portfolio source preparation

## Publication scope

Publish the original application source, tests and synthetic fixtures, project-owned branding/design reference, maintained documentation, MIT license and third-party notices. Keep installer and packaging scripts as engineering work. Do not include downloaded tools, restored dependencies, generated application/runtime binaries, Setup/ZIP outputs, logs, personal settings, screenshots of unrelated personal media, or private iteration journals.

The root [README](../README.md) explains image-only builds and explicit local video-tool initialization. [Licensing](licensing.md) records unresolved binary-distribution issues without pretending that source-only means dependency-free or that MIT relicenses upstream components. The local gallery opens with the existing white GF branding and the startup screen, followed by an AI-generated car input/result pair and a short symbolized video with an on-demand preview; three additional application screenshots are collapsed and no walkthrough is required. [Media notes](screenshots/README.md) record supplied provenance and the unresolved standalone-video permission assessment. Local preparation is not publication approval.

## Planned route: preserve development history in a reviewed copy

The original development Git history contains FFmpeg executables. Removing them from the current tree or adding `.gitignore` does not remove historical objects. Do not make that repository public, mirror all refs, or push its history to the portfolio repository without a separately reviewed history decision.

The owner has chosen to retain the development story rather than start a new public history. The following sequence is agreed but **not completed**:

1. Review and commit the current notices/gallery/documentation baseline in the private repository. A verified private backup of reachable history, uncommitted working files and current tools has been created; keep its bundle, mirror, metadata and diagnostic reports outside public source. The owner creates the checkpoint commit separately.
2. Prepare a controlled FFmpeg build in the current development repository, with exact upstream/dependency revisions, build scripts/configuration, toolchain identity, hashes, licenses and complete Corresponding Source evidence for the resulting tools. Replacing the tools does not clear the old Gyan binaries in history or the separate Windows-native/media gates. No replacement has been built or accepted yet; current acquisition/pins and recorded tests still refer to the existing tools.
3. After replacement verification, prepare a separate history-preserving copy that removes the two historical FFmpeg/ffprobe executable paths and audits all refs/objects intended for publication for other private or unreviewed content. Keep the original repository and recovery copy private. Preserve commit messages/authorship and legitimate development changes where possible; affected commit IDs and descendant IDs will change. This is not a promise to preserve old hashes or publish private app refs.
4. Verify the copied history and a fresh checkout/build/test, review assets/notices/publication scope, and obtain owner approval before changing remotes or pushing. Any later Setup/ZIP additionally needs its own clean-source artifact identity and closed binary-distribution gates.

No history filtering, public push or controlled-tool build is performed by documenting this plan. Private backup locations and evidence dumps are deliberately not public documentation dependencies.

## Available fallback: export current source without history

The existing snapshot workflow remains available if the owner chooses a history-free publication instead. It is not the implementation of the planned history-preserving route. From the private development repository, after reviewing current changes:

```powershell
pwsh -NoProfile -File scripts/Export-PortfolioSource.ps1
```

The script creates a fresh ignored `artifacts/portfolio-source/<run-id>/GlyphFrame/` containing current tracked/nonignored source files, including uncommitted edits, but no `.git`. It rejects unexpected root paths, executables, archives, generated/private directories, linked files and unreviewed media locations. It verifies every copied SHA-256 and rechecks source/status stability. Only the two deliberately removed tracked tool EXEs may be absent. Unknown/missing content fails rather than being silently omitted.

Sibling `source-manifest.json` and `report.json` are local evidence, **not part of the public source folder**. They record the parent revision and dirty state; a snapshot is not a clean commit, Git clone or binary release. This is a conservative path policy, not a complete content/secret scanner. Review text, asset rights, links and the manifest before publication.

## Verify a history-free snapshot before publication

1. Build a fresh snapshot without its own ignored cache: `dotnet restore GlyphFrame.sln`, then `dotnet build GlyphFrame.sln -c Release --no-restore`. Confirm the missing-video-tools guidance and absence of FFmpeg/ffprobe in output; do not rely on stale `bin/` contents from an earlier tool-enabled build.
2. For full video/tests, explicitly run `scripts/Initialize-LocalTools.ps1 -Download` or the pinned offline `-ArchivePath` option, rebuild and run the full [test commands](testing.md). Build/acquisition do not imply binary-distribution approval.
3. Review public text/assets for personal paths, credentials and unrelated media. Preserve the MIT license, legal texts, exact pins, synthetic fixture provenance and unresolved dependency findings.
4. For this fallback only, and after owner approval, create a new public Git history from the reviewed **source folder**, not from the private development `.git` or the snapshot's evidence parent. Do not copy old refs/remotes/objects or attach local Setup/ZIP files. No script here initializes Git, stages, commits, pushes, tags or publishes.

The private development repository, ignored local tools, backups and existing candidate packages stay available. The installer was not removed or rolled back; future binary publication requires closing its separate [licensing and distribution gates](licensing.md#open-binary-distribution-gates).
