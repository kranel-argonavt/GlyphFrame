# Local video tools — no executables in the source distribution

The source repository retains version/checksum/license provenance, **not ffmpeg.exe or ffprobe.exe**. The pinned upstream build is Gyan's FFmpeg 9.0 Windows x64 essentials build. Its GPLv3 terms remain relevant to anyone redistributing those binaries; local acquisition does not certify a future GlyphFrame binary release.

On Windows with PowerShell 7, explicitly acquire the fixed upstream archive:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -Download
```

Or download [the exact 9.0 archive](https://github.com/GyanD/codexffmpeg/releases/download/9.0/ffmpeg-9.0-essentials_build.zip) yourself, then supply it without network access:

```powershell
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -ArchivePath 'C:\Downloads\ffmpeg-9.0-essentials_build.zip'
pwsh -NoProfile -File scripts/Initialize-LocalTools.ps1 -VerifyOnly
```

The script validates the whole archive SHA-256 from `SHA256SUMS.txt`, reads only safe ZIP entries, extracts just the two tools and validates both executable hashes before accepting the pair. It does not execute the tools, install software, change PATH, elevate, auto-update or overwrite an existing/partial cache. A correct existing pair is verified without downloading again. Failures/downloads remain under ignored `artifacts/tool-acquisition/` for inspection.

The accepted files live under ignored `artifacts/local-runtime/Tools/ffmpeg/`. Rebuilding copies them beside the application at `Tools/ffmpeg/`, preserving the existing runtime locator. Integration tests use the local cache directly. Do not commit EXEs, archives or the local cache.

A fresh source checkout can build/run image workflows without the tools; video processing and real-FFmpeg integration tests require the explicit preparation above. Local portable/Setup verification intentionally still requires a complete verified pair and rejects incomplete payloads. Application/installer functionality has not been removed.

If a partial/mismatched cache is detected, preserve and inspect that exact cache rather than deleting broad directories or replacing pins to make verification pass. Updating tool versions requires joint checksum, documentation, license/source and functional review. Public binary releases remain subject to [distribution gates](../../docs/licensing.md).
