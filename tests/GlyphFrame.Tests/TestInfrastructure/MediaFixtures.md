# Phase 5 media fixtures

The Phase 5 `sample.*`, `silent.mkv`, `rotated.jpeg`, landscape/portrait, and broken files under `Fixtures/` are tiny, deterministic, copyright-free container headers. They contain only the minimum structures required to exercise PNG, JPEG/EXIF, WebP, BMP, MP4, MOV, AVI, and MKV metadata readers; those Phase 5 video fixtures contain no playable frames or user media.

Tests copy the committed fixtures into an isolated `%TEMP%\GlyphFrame.Tests` directory and delete that test-owned directory afterward. They do not generate or download media, do not require FFmpeg, and never access the user's media or `%LocalAppData%\GlyphFrame` directories.

Phase 6 pixel-pipeline tests create valid, tiny raster images with deterministic colors directly inside the same isolated test directory. These purpose-built images exercise real PNG, JPEG, WebP, and BMP decoding, orientation transforms, transparency, symbol rendering, and export without relying on personal or copyrighted media.

Phase 8 adds `video-preview.mp4`, `video-preview.mov`, `video-preview.mkv`, and `video-preview.avi`: 64 x 48, 1.2-second, copyright-free fixtures generated locally with the repository's bundled FFmpeg. MP4, MOV, and MKV contain H.264; AVI contains MPEG-4 Part 2 for broad container compatibility. Their first half is solid red and their second half is solid blue. The deliberate color and time transition verifies bundled-FFmpeg frame extraction, Unicode-safe random access, RGBA channel order, managed frame ownership, and file-handle release without downloading media during tests.

`video-preview-rotated.mov` is a 2,067-byte, copyright-free H.264 MOV generated locally with the same bundled FFmpeg. Its encoded stream is 320 x 240, 1.2 seconds, 10 FPS, and 12 frames. A genuine MOV display matrix reports a 90-degree counter-clockwise rotation, so ffprobe retains the encoded dimensions while GlyphFrame resolves 240 x 320 display dimensions. The encoded landscape source contains red, green, blue, and yellow quadrants; after FFmpeg autorotation, the portrait frame's top-left, top-right, bottom-left, and bottom-right regions are green, yellow, red, and blue respectively. The integration test samples those regions to prove the real rotation direction, RGBA channel order, and opaque alpha rather than checking dimensions alone.

The fixture can be reproduced without external media using two bundled-FFmpeg steps. First, generate the 320 x 240 landscape source from the `color` source plus four `drawbox` filters and encode it as H.264/yuv420p. Then stream-copy it to MOV while applying `-display_rotation 90` before the input. Only the final rotated MOV is retained; no generated source or raw-frame file belongs in the repository.

Phase 10 adds five locally generated audio fixtures and reuses `video-preview.mp4` as the confirmed no-audio case. All audio fixtures use a deterministic 64 x 48 `testsrc2` video at 10 FPS for 1.2 seconds and synthetic sine tones—no downloaded, personal, or copyrighted media:

- `video-audio-aac.mp4`: H.264/yuv420p plus mono AAC at 48 kHz, 1.2 seconds. It exercises MP4-compatible audio stream copy.
- `video-audio-pcm.mov`: H.264/yuv420p plus mono PCM signed 16-bit little-endian at 44.1 kHz, 1.2 seconds. It exercises AAC fallback.
- `video-audio-short.mp4`: 1.2-second video plus 0.6-second AAC, proving shorter audio never cuts the completed symbolized video.
- `video-audio-long.mp4`: 1.2-second video plus 1.8-second AAC, proving longer audio is trimmed to the authoritative symbolized-video duration.
- `video-audio-offset.mp4`: 1.2-second video plus 0.8-second AAC beginning near 0.279 seconds, proving normal source audio start timing survives muxing.

The compatible fixture is reproducible from the repository root with the bundled tools using this command shape (change the tone frequency and audio duration for the other AAC cases):

```powershell
Tools/ffmpeg/ffmpeg.exe -f lavfi -i "testsrc2=size=64x48:rate=10:duration=1.2" -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=1.2" -map 0:v:0 -map 1:a:0 -c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 96k -ac 1 -f mp4 -y tests/GlyphFrame.Tests/Fixtures/video-audio-aac.mp4
```

The incompatible fixture uses the same video source with a 44.1 kHz sine tone and `-c:a pcm_s16le -ac 1 -f mov`. The committed files were verified with bundled ffprobe for codec, sample rate, channel count, duration, and frame count.
