# Phase 5 media fixtures

The Phase 5 `sample.*`, `silent.mkv`, `rotated.jpeg`, landscape/portrait, and broken files under `Fixtures/` are tiny, deterministic, copyright-free container headers. They contain only the minimum structures required to exercise PNG, JPEG/EXIF, WebP, BMP, MP4, MOV, AVI, and MKV metadata readers; those Phase 5 video fixtures contain no playable frames or user media.

Tests copy the committed fixtures into an isolated `%TEMP%\GlyphFrame.Tests` directory and delete that test-owned directory afterward. They do not generate or download media, do not require FFmpeg, and never access the user's media or `%LocalAppData%\GlyphFrame` directories.

Phase 6 pixel-pipeline tests create valid, tiny raster images with deterministic colors directly inside the same isolated test directory. These purpose-built images exercise real PNG, JPEG, WebP, and BMP decoding, orientation transforms, transparency, symbol rendering, and export without relying on personal or copyrighted media.

Phase 8 adds `video-preview.mp4`, `video-preview.mov`, `video-preview.mkv`, and `video-preview.avi`: 64 x 48, 1.2-second, copyright-free fixtures generated locally with the repository's bundled FFmpeg. MP4, MOV, and MKV contain H.264; AVI contains MPEG-4 Part 2 for broad container compatibility. Their first half is solid red and their second half is solid blue. The deliberate color and time transition verifies bundled-FFmpeg frame extraction, Unicode-safe random access, RGBA channel order, managed frame ownership, and file-handle release without downloading media during tests.
