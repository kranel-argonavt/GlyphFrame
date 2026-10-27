# Demonstration media

These are local portfolio-presentation assets, not test fixtures or evidence of an exhaustive native/compatibility acceptance pass. The main README shows the white project logo, the startup screen, one image comparison and a linked video result; three additional screenshots are collapsed. No screen-recorded application walkthrough is required.

## README logo

`logo.svg` uses the exact frame/GF geometry and 2-unit frame stroke from `GlyphFrameWindowIcon` in the application's `Components.xaml`, the same paths used by `scripts/Build-ShellIcon.ps1` for the white Windows shortcut icon. The white mark is placed on a dark rounded tile with outer padding so it remains visible on light and dark documentation backgrounds. It contains no external image/font references or scripts. The application geometry, shell ICO, accent-aware in-app logos and installer assets are unchanged.

## Application screenshots

The project author supplied these screenshots after receiving the Release build from commit `ed62872`, without the earlier development overlay. This is user-supplied capture provenance, not an independently embedded binary/source attestation.

- `startup.png`: initial media-selection screen, with no media selected, used as the README hero. At the author's request, only the 38-pixel Windows title bar was cropped off: 1634 x 1017 became 1634 x 979, with no scaling or change to the retained pixels. The full original remains in local artifacts.
- `application.png`: rendered-video review, playback controls, settings and export readiness, retained inside the additional screenshots.
- `image-ready.png`: retained image result and image export action.
- `compact-export.png`: narrower layout with the three post-export actions.

Apart from that exact title-bar crop, screenshots are copied unchanged, with no retouching, fabricated interface state or replacement of visible filenames. The crop used ordinary PNG imaging tools, not AI editing; decoded-pixel hashes confirmed that the entire retained area is identical to the original. The media pictured inside them retains its own provenance below; branding ownership does not grant rights to arbitrary depicted input media.

## Car image

- `car-original.jpg`: the author reports that this image was AI-generated. The generator/model/prompt and any generator-specific terms were not supplied; no photographer/stock attribution or real-world endorsement is claimed.
- `car-symbolized.png`: the author's supplied GlyphFrame output of that image, 1620 x 1080 pixels.

Both files are byte-identical copies of the supplied image attachments under descriptive names. They are not retouched, resized or recompressed. The original input is retained at its supplied resolution, so opening the full image downloads a relatively large JPEG. The application screenshot still displays the former input filename; it has not been edited to simulate a renamed input.

## Puppy video

- `puppy-symbolized.mp4`: a web-preview derivative of the author's supplied `puppy_symbolized.mp4`, reported to be GlyphFrame output from Pixabay footage. It is approximately 5.07 seconds, 1920 x 1080 at 30 FPS, H.264 video with the supplied AAC audio stream preserved. For file size only, the video was re-encoded using libx264, preset `slow`, CRF 26, yuv420p and fast-start MP4; it is not the byte-identical application export. No brightness/style/content edits were applied.
- `puppy-poster.jpg`: an actual frame at 1 second from the supplied rendered video, extracted at 1280 x 720 for the README's linked preview. It is not a generated concept or an original-footage still.

The original rendered MP4 and preparation trials are retained locally outside the source files. Raw Pixabay footage is not included. The author supplied the [Pixabay item page](https://pixabay.com/videos/ai-generated-puppy-pet-dog-animal-371301/) and reported that the footage has no sound; no separately added audio was reported. Local analysis of the supplied render found an encoded AAC track with zero-valued samples (silent), not an absent audio stream. The contributor and original download/date have not been independently verified; automated retrieval of the item page returned HTTP 403. The older screenshot input name `377924.mp4` is not treated as an independently verified match to item `371301`.

Source attribution is therefore **Pixabay — owner-reported**, not original footage authored by GlyphFrame's developer. See the official [Content License Summary](https://pixabay.com/service/license-summary/) and [full terms, section 5](https://pixabay.com/service/terms/), reviewed on 2026-10-26. The full terms distinguish new creative works from Standalone distribution and explicitly include a filter-only transformation among Standalone examples. Merely changing a file is not a blanket permission determination. Whether this particular symbolization qualifies for separate redistribution has **not** been independently established; the remaining item-level verification and that assessment stay open before public publication of the MP4. The supplied URL and silent audio do not by themselves close that review.

These notes record facts and review limits, not legal advice, a recovered license grant or a declaration that the clip is prohibited. No third-party material is relicensed under GlyphFrame's original-code MIT license, and no public upload/release is performed by preparing the local gallery.
