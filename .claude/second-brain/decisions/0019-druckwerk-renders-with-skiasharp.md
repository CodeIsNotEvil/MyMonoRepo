---
tags: [decision, druckwerk, rendering, skiasharp]
created: 2026-10-16
updated: 2026-10-16
status: active
supersedes:
---
# 0019: Druckwerk renders with SkiaSharp and fills only the gaps itself

**Context.** [[druckwerk]] needs text layout, rasterizing, PNG/JPEG/WebP/TIFF, PDF (including PDF/A)
and SVG on Windows, Linux and in Blazor WebAssembly. An earlier revision of the plan proposed our
own managed rasterizer and PDF writer, so output would be byte-identical on every platform (cheaper
MDR validation). The owner chose (2026-10-16): use SkiaSharp wherever it can do the job, and write our
own code only for what it can't.

**Decision.** `Druckwerk.Rendering` builds on SkiaSharp 4.x (MIT, Skia BSD-3) and SkiaSharp.HarfBuzz
(MIT) for:
- text shaping and layout (which brings complex scripts for free);
- font loading and system fonts;
- rasterizing;
- PNG, JPEG and WebP encoding, and decoding input images;
- PDF through `SKDocument` (subsetted fonts, `PdfA` metadata for PDF/A-2b);
- SVG through `SKSvgCanvas`.

It runs in the browser through `SkiaSharp.NativeAssets.WebAssembly`.

Gaps we fill ourselves (checked against SkiaSharp 4.153.1, the current version on 2026-10-16):
- **Barcodes.** Skia draws rectangles. Encoding and dot snapping are ours.
- **TIFF.** Skia has no TIFF encoder. We write our own: baseline, PackBits, Deflate and CCITT G4 for
  1-bit. BitMiracle.LibTiff.NET (BSD-3) is the fallback if G4 costs too much.
- **1-bit output and dithering for printers.** Skia renders 8-bit. Thresholding, Floyd–Steinberg,
  Atkinson and ordered dithering, plus 1-bit PNG/BMP/PBM writers, are ours.
- **Physical size in SVG.** `SKSvgCanvas` writes pixel units. We rewrite the root element to mm with
  a `viewBox`. Our own minimal SVG writer is the fallback if its output proves unusable.
- **PDF/A-3 and checks.** Skia can't embed files (e-receipts, ZUGFeRD later). PDFsharp 6.x (MIT) adds
  the attachment and its PDF/A-3 metadata afterwards. veraPDF checks every PDF/A in CI, and any
  conformance gap Skia leaves gets fixed in the same PDFsharp step.
- **Printer languages, transports, templates, units:** none of this is Skia's domain anyway.

**Alternatives.**
- *Own managed rasterizer and PDF writer* (the previous proposal). Byte-identical output everywhere,
  but it would have been the biggest and riskiest part of the project. Rejected by the owner.
- *Skia for everything, no gap work.* Leaves out TIFF, 1-bit printing, mm-sized SVG and PDF/A-3.

**Consequences.**
- Determinism is per platform rather than global: golden hashes per OS and architecture. Barcodes
  are drawn aliased on dot-snapped rectangles, so printer output is expected to match everywhere,
  and a CI test proves it. MDR customers validate on the platform they run. `Druckwerk.Verification`
  (decoding the rendered barcodes) protects the part that matters most.
- Skia's PDF/A output embeds a random document UUID, so PDF files aren't byte-reproducible. The audit
  trail hashes the rendered page content, not the file.
- Linux hosts need fontconfig and fonts (`SkiaSharp.NativeAssets.Linux`). Containers install them
  explicitly.
- The Studio downloads Skia's WASM build (several MB). In return, its print preview is the real
  renderer.
- Core, Barcodes and Templates stay free of Skia, so code that only needs barcodes or the template
  model doesn't pull in native assets.

Related: [[druckwerk]], [[mdr-labelling]], [[fiscal-receipts]]
