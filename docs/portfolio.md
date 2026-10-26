# Portfolio source preparation

## Publication scope

Publish the original application source, tests and synthetic fixtures, project-owned branding/design reference, maintained documentation, MIT license and third-party notices. Keep installer and packaging scripts as engineering work. Do not include downloaded tools, restored dependencies, generated application/runtime binaries, Setup/ZIP outputs, logs, personal settings, screenshots of unrelated personal media, or private iteration journals.

The root [README](../README.md) explains image-only builds and explicit local video-tool initialization. [Licensing](licensing.md) records unresolved binary-distribution issues without pretending that source-only means dependency-free or that MIT relicenses upstream components. Public screenshots/demo material are still a separate preparation step.

## Preserve private history; export current source

The original development Git history contains FFmpeg executables. Removing them from the current tree or adding `.gitignore` does not remove historical objects. Do not make that repository public, mirror all refs, or push its history to the portfolio repository without a separately reviewed history decision.

From the private development repository, after reviewing current changes:

```powershell
pwsh -NoProfile -File scripts/Export-PortfolioSource.ps1
```

The script creates a fresh ignored `artifacts/portfolio-source/<run-id>/GlyphFrame/` containing current tracked/nonignored source files, including uncommitted edits, but no `.git`. It rejects unexpected root paths, executables, archives, generated/private directories, linked files and unreviewed media locations. It verifies every copied SHA-256 and rechecks source/status stability. Only the two deliberately removed tracked tool EXEs may be absent. Unknown/missing content fails rather than being silently omitted.

Sibling `source-manifest.json` and `report.json` are local evidence, **not part of the public source folder**. They record the parent revision and dirty state; a snapshot is not a clean commit, Git clone or binary release. This is a conservative path policy, not a complete content/secret scanner. Review text, asset rights, links and the manifest before publication.

## Verify before creating the public repository

1. Build a fresh snapshot without its own ignored cache: `dotnet restore GlyphFrame.sln`, then `dotnet build GlyphFrame.sln -c Release --no-restore`. Confirm the missing-video-tools guidance and absence of FFmpeg/ffprobe in output; do not rely on stale `bin/` contents from an earlier tool-enabled build.
2. For full video/tests, explicitly run `scripts/Initialize-LocalTools.ps1 -Download` or the pinned offline `-ArchivePath` option, rebuild and run the full [test commands](testing.md). Build/acquisition do not imply binary-distribution approval.
3. Review public text/assets for personal paths, credentials and unrelated media. Preserve the MIT license, legal texts, exact pins, synthetic fixture provenance and unresolved dependency findings.
4. Only after owner approval, create a new public Git history from the reviewed **source folder**, not from the private development `.git` or the snapshot's evidence parent. Do not copy old refs/remotes/objects or attach local Setup/ZIP files. No script here initializes Git, stages, commits, pushes, tags or publishes.

The private development repository, ignored local tools, backups and existing candidate packages stay available. The installer was not removed or rolled back; future binary publication requires closing its separate [licensing and distribution gates](licensing.md#open-binary-distribution-gates).
