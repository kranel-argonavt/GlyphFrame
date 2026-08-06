# Phase 5 media fixtures

The committed files under `Fixtures/` are tiny, deterministic, copyright-free container headers for the automated tests. They contain only the minimum metadata structures required to exercise PNG, JPEG/EXIF, WebP, BMP, MP4, MOV, AVI, and MKV readers; they contain no decoded image pixels, playable frames, or user media.

Tests copy the committed fixtures into an isolated `%TEMP%\GlyphFrame.Tests` directory and delete that test-owned directory afterward. They do not generate or download media, do not require FFmpeg, and never access the user's media or `%LocalAppData%\GlyphFrame` directories.

Phase 6 pixel-pipeline tests create valid, tiny raster images with deterministic colors directly inside the same isolated test directory. These purpose-built images exercise real PNG, JPEG, WebP, and BMP decoding, orientation transforms, transparency, symbol rendering, and export without relying on personal or copyrighted media.
