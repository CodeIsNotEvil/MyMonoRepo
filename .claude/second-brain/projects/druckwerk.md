---
tags: [project, dotnet, printing, barcodes, nuget, plan]
created: 2026-10-16
updated: 2026-10-16
status: idea
---
# Druckwerk (working name): plan

An MIT-licensed set of C# libraries on NuGet for barcodes, QR codes, labels, receipts and vector
graphics in general. Every result exports to PNG, JPEG, TIFF, SVG and PDF, and prints on wired printers
on Windows and Linux with adjustable size and orientation. Libraries come first. A server-side
connector and a Blazor UI that talks to many connectors come later, and they only call the libraries.

"Druckwerk" (German: printing unit) is a placeholder. It's free on nuget.org as of 2026-10-16, and so is
`CINE.*`. The name is open question 1.

Nothing here is built yet. Once the owner settles the open questions, the choices marked
**Proposed decision** become records in `decisions/`.

## Goals and non-goals

**Goals**
- Barcodes and 2D codes (1D, QR, DataMatrix, PDF417, Aztec, ...) that scan reliably on paper,
  including on 203 dpi thermal printers.
- Labels: single labels, rolls, and A4 or Letter sheets with label grids (Avery/Zweckform style), filled
  in batches from CSV or JSON data.
- Receipts: continuous 58/80 mm paper, item lines, totals, logo, QR, cut.
- General vector drawing: shapes, paths, text, images, groups and transforms, plus SVG import.
- Templates in a versioned JSON format, bound to data. Editors and UIs only produce and consume
  this format.
- Output to SVG, PDF, PNG, JPEG, WebP and TIFF, each print-ready (exact physical size, DPI metadata,
  1-bit options).
- Printing on wired printers (USB, serial, parallel through the OS) on Windows and Linux. Media size,
  orientation, scaling, margins and offset are adjustable per job.
- Later: `Druckwerk.Connector` for servers and a Blazor "Studio" that connects to several of them.

**Non-goals** (keep the scope honest)
- Fiscal receipt compliance (German KassenSichV/TSE and similar). We print what we're given.
- Prepress: CMYK, spot colours, bleed and ICC-managed output.
- PDF import or editing, and raster photo editing.
- Installing printer drivers. We use the drivers the OS has, or talk to the printer raw.
- macOS as a target. It's not promised, though the CUPS path will probably just work.

## Principles

1. **Everything we ship is MIT, and so is everything it depends on, near enough.** See "License
   policy".
2. **A pure managed core.** Units, scene graph, barcodes, fonts, templates and layout have no
   native dependencies, so they run unchanged on the server, in the CLI and in Blazor WebAssembly. A
   preview in the browser lays out exactly like the server's print output, the same idea as
   [[0003-shared-domain-analytics]]. Native code (Skia) appears only in the raster and PDF export
   package, which the browser never loads.
3. **One pipeline: template → scene → output.** Exporters and printers consume the same scene
   (physical units, mm). No output format has its own layout logic.
4. **Physical units first, pixels last.** Geometry is in millimetres until the last step, which
   rasterizes at the target DPI and snaps barcode modules to whole dots there.
5. **Raster first, native later.** Printing renders a 1-bit or grey bitmap at the printer's exact DPI
   and sends it in the printer's language. That works for every printer and looks like the preview.
   Native printer text and barcodes (ZPL `^BC`, ESC/POS `GS k`) come later, as an optimization.
6. **Trimming and AOT safe.** `IsAotCompatible`, System.Text.Json source generation, no reflection,
   so the CLI ships as a Native AOT binary and the WASM UI stays small.
7. **Comment density and conventions as in the rest of the repo** (CLAUDE.md): `CINE.` root namespace,
   2-space indentation, file-scoped namespaces, primary constructors, and `Path.Combine` everywhere,
   because Windows is a first-class target.

## License policy

**Proposed decision:** every package is MIT, `Copyright (c) 2026 CodeIsNotEvil`, with
`<PackageLicenseExpression>MIT</PackageLicenseExpression>`.

Runtime dependencies (anything a consumer receives) must use a permissive licence: MIT, BSD-2/3,
zlib, or Apache-2.0 only when no MIT alternative exists. Fonts we bundle may be under the SIL OFL 1.1
and ship in a separate, optional package. Test-only dependencies may use any OSI licence.

**Excluded**, even though they're popular:

| Library | Why |
|---|---|
| ImageSharp, ImageSharp.Drawing, SixLabors.Fonts | Six Labors Split License: not free for every commercial user |
| QuestPDF | Community licence with a revenue limit |
| iText | AGPL |
| Aspose, Spire, IronBarcode, ... | Commercial |
| LibUsbDotNet | LGPL. Not needed anyway, see Printing |
| System.Drawing.Common | MIT, but Windows-only since .NET 6 |

**Chosen dependencies** (versions current on 2026-10-16):

| Package | Licence | Used for | Where |
|---|---|---|---|
| SkiaSharp 4.153.x (+ `NativeAssets.Linux.NoDependencies`) | MIT (Skia: BSD-3) | rasterizing, PNG/JPEG/WebP encoding, PDF | `Druckwerk.Rendering.Skia` only |
| Microsoft.Windows.CsWin32 | MIT | source-generated P/Invoke for winspool and GDI | build time only |
| System.IO.Ports | MIT | serial printers | `Druckwerk.Printing` |
| ZXing.Net 0.16.x | Apache-2.0 | **tests only**: decodes every barcode we generate | tests |

We write our own code for: barcode encoders, the SVG writer, the TIFF/PBM/BMP encoders, dithering,
an OpenType font reader, the IPP client and the printer languages. Each is small, well specified and
testable, and owning them keeps the core dependency-free and WASM-friendly.

Reference implementations to read (MIT, so studying them and porting ideas is fine): QRCoder 1.8.x,
Net.Codecrete.QrCodeGenerator 3.2.x (a port of Nayuki's QR generator), SharpIppNext 5.x for IPP,
Svg.Skia 5.x for the SVG subset worth supporting. If writing our own costs too much, depending on one
of them directly stays within policy.

Trademarks: the README says "QR Code is a registered trademark of DENSO WAVE INCORPORATED" and names
ESC/POS (Epson) and ZPL (Zebra) as their owners' trademarks. The symbologies themselves are free to
implement (Denso waives the QR patent, Aztec is public domain, the GS1 specs are free to read).

`THIRD-PARTY-NOTICES.txt` and `licenses/` follow the LaunchHeim pattern ([[launchheim]], "Key ideas").

## Package map

**Proposed decision:** few packages, split along dependency lines rather than features. Splitting
later is easier than merging.

| Package | Contains | Depends on |
|---|---|---|
| `Druckwerk.Core` | `Length`/units (mm, in, pt, dots), `Size`, `Thickness`, colours, media model, scene graph (shapes, paths, text runs, images, groups, transforms), SVG path-data parser, SVG subset import | nothing |
| `Druckwerk.Barcodes` | Symbology encoders → geometry (bars or module matrix), check digits, GS1 helpers, payload builders (Wi-Fi, vCard, MeCard, URL, e-mail, SMS, geo, calendar, EPC/GiroCode SEPA, Swiss QR bill, GS1 Digital Link) | Core |
| `Druckwerk.Text` | Managed OpenType reader (cmap, hmtx, kern, GPOS pair kerning, glyf, CFF outlines), font collection and fallback, line breaking, alignment, auto-fit | Core |
| `Druckwerk.Templates` | JSON template model and schema, migrations, data binding, layout to a scene, label sheet imposition, receipt builder, media/stock catalog | Core, Barcodes, Text |
| `Druckwerk.Imaging` | Bitmap buffers (1-bit, 8-bit grey, RGBA), dithering (threshold, Floyd–Steinberg, Atkinson, Bayer), encoders: TIFF (none/PackBits/LZW/Deflate, CCITT G4 for 1-bit), PBM, BMP, with DPI metadata | Core |
| `Druckwerk.Rendering.Svg` | Scene → SVG (text as paths by default, or as `<text>` with an embedded font) | Core, Text |
| `Druckwerk.Rendering.Skia` | Scene → raster at a DPI (with barcode snapping) → PNG/JPEG/WebP, or into `Imaging` for TIFF and printing. Scene → PDF (vector, selectable text through positioned glyphs) | Core, Text, Imaging, SkiaSharp |
| `Druckwerk.Printing` | Printer abstraction, discovery, capabilities, `PrintSettings`, job pipeline, printer profiles, languages (ESC/POS, ZPL, TSPL, EPL2, Brother QL raster), transports (Windows spooler, CUPS over IPP, device file, serial, file), Windows GDI driver printing | Core, Imaging, System.IO.Ports |
| `Druckwerk.Fonts` | Optional: a few OFL fonts (a sans, a mono, a condensed one for labels, an OCR-B for barcode digits) | none |
| `Druckwerk.Cli` | `dotnet tool` and Native AOT binary `druckwerk` | all of the above |
| `Druckwerk.Connector` (phase 5) | ASP.NET Core endpoints and a hosted service | Templates, Rendering.*, Printing |
| `Druckwerk.Connector.Client` (phase 5) | Typed HTTP client and DTOs, WASM-safe | nothing heavy |
| `Druckwerk.Studio` (phase 6) | Blazor WASM UI, not a NuGet package | Core, Barcodes, Text, Templates, Rendering.Svg, Connector.Client |

The Studio deliberately gets no Skia: the browser previews through SVG, and the connector renders
anything raster.

**Package IDs** (open question 1). Either `Druckwerk.*` with the namespace `CINE.Druckwerk.*` (the
[[0004-cine-root-namespace]] rule as it stands: unprefixed assembly), or `CINE.Druckwerk.*` for the
package, assembly and namespace alike. The second option needs an exception to 0004, but it gives
consumers one name everywhere, and nuget.org lets us reserve the `CINE.` ID prefix (a verified
badge, nobody else can publish under it). Recommendation: `CINE.Druckwerk.*`. 0004 kept assemblies
unprefixed to avoid churn in apps that already existed, and this project has none.

## Core model

- **Units.** `Length` is a struct stored as double millimetres, built with `Length.Mm(50)`,
  `Length.In(2)`, `Length.Pt(10)` or `Length.Dots(406, dpi: 203)`. JSON accepts plain numbers (mm) or
  strings with a unit (`"2in"`, `"10pt"`).
- **Media.** `Media` is one of:
  - `Sheet` (A4, Letter, custom), optionally with a `LabelGrid` (rows, columns, pitch, margins);
  - `DieCutLabel` (width, height, gap, roll);
  - `Continuous` (width; the length grows with the content or is fixed);
  - `BlackMark` (width, mark pitch).

  Each has a printable area. `Druckwerk.Templates` ships a stock catalog (Avery/Zweckform sheets,
  Brother DK rolls, DYMO sizes, common Zebra sizes, 58/80 mm receipt rolls).
- **Scene.** An immutable tree: `Group(transform, clip, children)`, `Rect`, `Ellipse`, `Line`,
  `Path` (move/line/cubic/quad/arc/close, fill rule), `GlyphRun` (font, glyph ids and positions, already
  laid out), `Image` (encoded bytes plus intrinsic size; decoding is the renderer's job), `BarcodeNode`
  (the symbology's geometry plus quiet zones). Paints: solid colour and stroke (width, cap, join, dash).
  Gradients only if someone asks.
- **Why `BarcodeNode` stays a node** instead of becoming rectangles straight away: the rasterizer must
  snap module widths to whole dots at the final DPI, or a 203 dpi printer prints uneven bars that don't
  scan. SVG and PDF export can still turn it into rectangles.

## Barcodes

Encoders return geometry, never images: 1D codes return run lengths (bars and spaces in modules) plus
the human-readable text, 2D codes a `BitMatrix` plus the module shape. Validation errors are
specific ("EAN-13 needs 12 or 13 digits; check digit should be 7").

| Phase | Symbologies |
|---|---|
| 1 | Code 128 (auto A/B/C switching, GS1-128 with FNC1 and AI parsing), EAN-13, EAN-8, UPC-A, UPC-E, EAN-2/5 add-ons, Code 39 (+ full ASCII), ITF/ITF-14 (bearer bars), QR Code (numeric/alphanumeric/byte/Kanji, ECI, ECC L–H, mask choice, version auto/fixed), Data Matrix ECC 200 (incl. GS1 DataMatrix, rectangular sizes) |
| 4 | PDF417 (+ compact), Aztec (+ runes), Codabar, Code 93, MSI, GS1 DataBar (omni, limited, expanded), Micro QR, rMQR |
| later | MaxiCode, DotCode, Han Xin, postal codes, only on request |

Options: module size (mm or dots), quiet zone, bar width reduction (thermal and inkjet ink spread),
human-readable text (font, position, the EAN/UPC guard bar layout), QR/DataMatrix module shape
(square, dot, rounded) and an optional logo in the centre that respects the error-correction budget.

The `Druckwerk.Cli` command `druckwerk barcode qr "https://…" -o code.svg` is the first useful thing
we ship.

## Text and fonts

- Our own OpenType reader, because SixLabors.Fonts is excluded and HarfBuzz is native code, which the
  browser shouldn't need. Phase 2 covers Latin, Greek and Cyrillic with kerning, plus the `liga` and
  `kern` features through GPOS/GSUB lookup types 1, 2 and 4. Complex scripts (Arabic, Hebrew, Indic)
  are open question 5. If they're needed, HarfBuzzSharp (MIT) can become an optional shaper on the
  server side only.
- Fonts come from: files and streams the caller registers, `Druckwerk.Fonts`, and system fonts
  (Windows `%WINDIR%\Fonts`, Linux through fontconfig's directories), resolved with `Path.Combine`.
  The browser receives fonts from the connector or from the Studio's own bundle.
- Layout: wrapping by Unicode line-break classes (UAX #14, simplified), alignment, line height,
  max lines with an ellipsis, and auto-shrink to fit a box. Shrinking to fit matters a lot for labels.
- Output: SVG writes glyphs as paths by default (identical everywhere, no font licence questions in
  the file). PDF draws positioned glyphs through Skia, so text stays selectable and fonts are
  subsetted.

## Templates

A versioned JSON document. The format is the contract between the libraries, the CLI, the connector and
the Studio.

```json
{
  "$schema": "https://codeisnotevil.github.io/MyMonoRepo/druckwerk/template-1.schema.json",
  "formatVersion": 1,
  "name": "Shelf label",
  "media": { "kind": "dieCut", "width": "62mm", "height": "29mm", "gap": "3mm" },
  "elements": [
    { "type": "text", "x": 3, "y": 3, "width": 56, "maxLines": 2, "fit": "shrink",
      "text": "{{product.name}}", "font": { "family": "Inter", "size": "11pt", "weight": 700 } },
    { "type": "barcode", "symbology": "ean13", "x": 3, "y": 12, "height": 12,
      "data": "{{product.gtin}}" },
    { "type": "text", "x": 40, "y": 18, "align": "right", "width": 19,
      "text": "{{product.price | currency: 'EUR'}}", "visibleIf": "product.price > 0" }
  ]
}
```

- **Binding.** `{{path | filter: arg}}` over JSON data, with a fixed set of filters (number, currency,
  date, upper/lower, pad, substring, default) and culture-aware formatting. `visibleIf` takes
  comparisons, `and`/`or`/`not` and nothing else. **No general-purpose scripting** (no Roslyn, no
  Liquid engine): connectors accept templates from the network, so the language must not be able to
  run code.
- **Repeats.** A `repeat` element lays out a list (receipt items, table rows). On continuous media the
  page grows; on sheets it breaks to the next page.
- **Batch.** One template with N data records gives N labels: on a roll, one label each; on a sheet,
  imposed into the grid with an optional start position (so a half-used Avery sheet can be reused).
  `{{$index}}` and `{{$serial | pad: 6}}` (start and step set per batch) cover numbering.
- **Receipts** are templates too, plus a fluent builder for code:
  `Receipt.Create(Media.Roll80mm).Logo(png).Center().Bold().Text("Café").Items(lines).Total(...).Qr(url).Cut()`.
  It produces a scene (and later, optionally, native ESC/POS text).
- **Versioning.** `formatVersion` plus migrations in code. Old templates always load, and saving writes
  the newest version. A JSON schema for editors is generated from the model and published on the
  site.

## Output formats

| Format | Backend | Vector | Print notes |
|---|---|---|---|
| SVG | `Rendering.Svg` (own) | yes | `width`/`height` in mm, so it prints at true size; text as paths |
| PDF | `Rendering.Skia` (`SKDocument`) | yes | page size = media, fonts subsetted, metadata; PDF/A only if Skia's support proves enough (open) |
| PNG | Skia | no | pHYs chunk with the DPI; 1-bit or grey option for thermal |
| JPEG | Skia | no | JFIF density set; not recommended for barcodes (artifacts), and the docs say so |
| WebP | Skia | no | for the web |
| TIFF | `Imaging` (own) | no | X/YResolution tags; 1-bit CCITT G4 (what label and fax workflows expect), PackBits, LZW, Deflate; multi-page for batches |
| PBM/BMP | `Imaging` (own) | no | debugging and old printers |

Every raster export takes a DPI (default 300, or 203/300/600 to match a printer) and uses the same
snapping as printing. Batches export as multi-page PDF or TIFF, or as numbered files.

## Printing

### Model

```
IPrinterDiscovery → PrinterInfo (id, name, transport, language, driver/raw)
IPrinter.GetCapabilities() → media sizes, DPI(s), printable area, colour, cutter, max width in dots
IPrinter.PrintAsync(Scene[] pages | PrintPayload, PrintSettings) → PrintJob (status, progress)
```

**`PrintSettings`** is the "size and orientation" requirement:
- `Media`: from capabilities, the stock catalog, or custom width × height; continuous with auto length.
- `Orientation`: Portrait, Landscape, ReversePortrait, ReverseLandscape, Auto (follow the content's
  aspect ratio). Rotation happens in the scene before rasterizing, so it's identical on every printer
  and language.
- `Scaling`: ActualSize (default, so barcodes keep their module size), FitToPrintableArea,
  FillAndCrop, ShrinkOnly. The UI warns when scaling would make a barcode fall below its minimum
  module size.
- `Alignment` within the printable area, plus `Margins`.
- `Offset` (x/y in mm) for per-printer calibration, stored in the printer profile.
- `Copies`, `Collate`, `Dpi` (from capabilities), `ColorMode` (Mono, Grey, Colour), `Dithering`.
- Thermal only: `Darkness`, `Speed`, `Cut` (None, Partial, Full; per label or at the end), `Mirror`,
  `Feed`.

**Printer profiles:** JSON files (shipped ones plus user ones) that describe a model: language, DPI,
head width in dots, cutter, the commands it supports and quirks. For example "Epson TM-T20III: ESC/POS,
203 dpi, 576 dots, partial cut, GS v 0 raster".

### Pipeline

```
Template + data → bind → layout (managed fonts) → Scene (mm)
  → apply PrintSettings (rotate, scale, align, offset)
  → rasterize at the printer's DPI (Skia, barcode modules snapped to dots)
  → monochrome / dither (Imaging)
  → encode in the printer's language: ESC/POS │ ZPL │ TSPL │ EPL2 │ Brother QL │ GDI bitmap │ PDF
  → transport: Windows spooler │ CUPS (IPP) │ /dev/usb/lp* │ serial │ file
```

### Languages (raster first)

| Language | Printers | Image command | Phase |
|---|---|---|---|
| ESC/POS | Epson TM and most receipt printers | `GS v 0` (and `GS ( L` where supported), `GS V` cut, `ESC p` drawer kick | 3 |
| ZPL II | Zebra and many clones | `^GFA` with Z64 (zlib + base64, through `System.IO.Compression`), `^PW`, `^LL`, `^PO`, `~SD` darkness | 3 |
| TSPL/TSPL2 | TSC, many cheap label printers | `BITMAP`, `SIZE`, `GAP`, `DIRECTION` | 4 |
| EPL2 | older Zebra/Eltron | `GW` | 4 |
| Brother QL raster | QL-5xx/7xx/8xx | `ESC i a` raster mode, media info | 4 |
| Driver (GDI / PDF) | everything with an OS driver: office printers, DYMO, Brother P-touch | Windows: GDI bitmap at device DPI; Linux: PDF or PWG raster to CUPS | 3 |

Native text, barcodes and QR in ZPL and ESC/POS come in phase 4 as `PrintMode.Native`, with
automatic fallback to raster for anything the printer can't do.

### Transports, per OS (wired first)

**Windows** (`[SupportedOSPlatform("windows")]`, CsWin32):
- Discovery through `EnumPrinters`, capabilities through `DeviceCapabilities` (papers, bins,
  resolutions) and `GetDeviceCaps` (printable area, physical offset).
- **Raw** (ESC/POS, ZPL, ...): `OpenPrinter` → `StartDocPrinter` with datatype `RAW` →
  `WritePrinter`. Works for USB, LPT and COM ports behind a spooler queue, including queues on the
  "Generic / Text Only" driver.
- **Driver** (office printers, label printers with Windows drivers): `CreateDC` with a `DEVMODE`
  (paper size or custom length/width, orientation, copies, resolution) → `StartDoc`/`StartPage` →
  `StretchDIBits` of our bitmap rendered at the device's DPI, compensating for `PHYSICALOFFSETX/Y`.
  Bands for 600 dpi A4 pages, so memory stays bounded.
- Serial without a spooler: `System.IO.Ports` (COMx).

**Linux:**
- **CUPS through IPP**: our IPP/1.1 client against `http://localhost:631` (CUPS speaks IPP locally).
  `CUPS-Get-Printers` for discovery, `Get-Printer-Attributes` for capabilities (`media-supported`,
  `printer-resolution-supported`, ...), and `Print-Job` with `application/pdf` (vector path, CUPS
  filters rasterize) or raw `application/vnd.cups-raw` for ESC/POS and ZPL. Job attributes:
  `media`/`media-col`, `orientation-requested`, `print-scaling=none`, `copies`. No libcups P/Invoke
  needed. The same client reaches IPP Everywhere network printers later for free.
- **Device files**: `/dev/usb/lp*` (the usblp kernel module) and `/dev/lp*` for raw printers without
  a CUPS queue. The IEEE 1284 device ID (`LPIOC_GET_DEVICE_ID` ioctl, or `/sys/class/usbmisc/lp*/device/ieee1284_id`)
  identifies the model and picks a profile.
- **Serial**: `/dev/ttyUSB*`, `/dev/ttyACM*`, `/dev/ttyS*` through `System.IO.Ports`.
- Permissions: the user needs the `lp` group for printers and `uucp` for serial ports on Arch/CachyOS
  (`dialout` on Debian/Fedora). `druckwerk doctor` checks this and prints the exact command.

**Everywhere:** a `FileTransport` that writes the printer bytes to a file (tests, and for debugging
with emulators), and later `TcpTransport` (port 9100) for network printers. It's trivial once wired
printing works (open question 6).

**Status** (phase 4): ESC/POS `DLE EOT` (paper out, cover open) and ZPL `~HS` where the transport is
bidirectional (serial, device file). The spooler and CUPS report their own job state.

## CLI

`druckwerk` as `dotnet tool install -g` and as Native AOT binaries for linux-x64, linux-arm64 and
win-x64. It's thin: every command is a few library calls, which keeps the API honest.

```
druckwerk barcode <symbology> <data> [-o out.svg|png|pdf|tiff] [--dpi 300] [--module 0.33mm]
druckwerk render <template.json> [--data data.json|csv] -o out.pdf [--format tiff --dpi 203]
druckwerk printers [--json]            # discovery + capabilities
druckwerk print <template.json> --printer <id> [--data ...] [--media 62x29mm] [--orientation landscape] [--copies 2]
druckwerk doctor                       # permissions, CUPS reachable, fonts found
```

## Connector and Studio (phases 5–6)

**Connector** (`Druckwerk.Connector`, a library you add to any ASP.NET Core host, plus a ready host
`Druckwerk.Connector.Host` as a Windows service, systemd unit and container image):
- REST under `/api/v1`, described with OpenAPI: `GET /info` (version, format versions, features),
  `GET /printers`, `GET /printers/{id}/capabilities`, `GET /stock`, CRUD `/templates` (stored as JSON
  files by default, so they're git-friendly; storage is pluggable), `POST /render` (scene or template
  + data → any output format), `POST /jobs`, `GET /jobs/{id}`, and Server-Sent Events for job status.
- Each server exposes *its own* printers, label stock, templates and fonts. That's the
  "different printers, labels, QR codes per server" requirement.
- Auth: API keys (stored hashed) by default, or JWT bearer through OIDC (the owner already has a
  Keycloak compose in `Configurations/`). A CORS allowlist of Studio origins. Raw passthrough of
  printer bytes is off by default.
- Containers: printer access from a container needs the device or the CUPS socket passed through.
  The compose file follows the Podman rules in CLAUDE.md (fully qualified images, high ports), but
  the main deployment is a native service.

**Studio** (Blazor WebAssembly standalone, like GroceryTracker):
- A list of connectors (URL, key, name) stored in the browser, with a status for each (reachable,
  version compatible). Pick a server, then its printers, stock and templates. Templates can be copied
  between servers by export and import. They're the same format, and `/info` reports which format
  versions a server understands.
- The editor works on the template model, and the canvas is the SVG renderer's output with element
  ids for selection, handles, snapping and guides. Layout runs in the browser through the same
  libraries, so typing updates the preview immediately. "Print preview" asks the connector for a PNG
  at the printer's DPI, dithered, to show exactly what the thermal printer will produce.
- Data: sample JSON or CSV per template, live barcode validation, batch print with a record range.
- Each connector can also serve the Studio itself (same origin, no CORS), the way GroceryTracker's
  nginx does. A central copy works too.

## Repository, build and release

**Proposed layout** (open question 3: here or in its own repo):

```
Applications/Druckwerk/
  Directory.Build.props        # RootNamespace CINE.$(MSBuildProjectName), Version, package metadata, IsAotCompatible
  Directory.Packages.props     # central package management
  Druckwerk.slnx  LICENSE  README.md  CHANGELOG.md  THIRD-PARTY-NOTICES.txt  licenses/
  src/Druckwerk.Core  .Barcodes  .Text  .Templates  .Imaging  .Rendering.Svg  .Rendering.Skia
      .Printing  .Fonts  .Cli  (.Connector  .Connector.Client  .Connector.Host  .Studio later)
  tests/<one per package>  tests/Druckwerk.Golden  (golden files)
  samples/                     # console samples used in the README
```

- Target `net10.0` only (LTS until November 2028; .NET 8 leaves support in November 2026). Add
  `net12.0` when it arrives, the next LTS.
- One version for all packages in `Directory.Build.props`, the way LaunchHeim does it. SemVer, `0.x`
  until the API review in phase 4, then 1.0 with package validation against the previous version as a
  baseline.
- Package metadata: README per package, icon (SVG source, rendered PNG, as with the repo's other
  logos), SourceLink, deterministic build, `.snupkg` symbols, `PackageProjectUrl` pointing at the
  site.
- CI: GitHub Actions `druckwerk.yml` on `ubuntu-latest` and `windows-latest` (build, test, pack,
  golden diff upload on failure). The Windows job prints to "Microsoft Print to PDF" and the Linux job
  to a CUPS queue that writes files (cups-pdf, plus a raw file-backend queue), so the spooler and IPP
  paths run end to end without hardware.
- Release: tag `druckwerk-vX.Y.Z` per [[0007-per-app-releases-and-prebuilt-images]] and
  [[0008-changelog-per-app]]. The workflow packs, pushes to nuget.org (Trusted Publishing through
  GitHub OIDC; an API key secret if that isn't set up), attaches the CLI binaries to the GitHub
  release and re-runs the site.
- The site gets a Druckwerk page (packages, CLI downloads, docs) once 0.1 is out ([[app-site]]).

## Testing

- **Barcodes:** spec test vectors (known inputs → known modules), plus a round trip: render to PNG at
  several DPIs and module sizes, decode with ZXing.Net, compare. Property tests over random inputs
  per symbology.
- **Golden files:** SVG compared as text, PNG compared pixel by pixel with a tolerance, printer bytes
  (ESC/POS, ZPL, TSPL) compared byte for byte. Each in `tests/Druckwerk.Golden`, regenerated by a
  documented switch, never by hand.
- **Layout:** the same template must give the same scene on Linux, Windows and in WASM (a scene hash
  test runs in all three, the WASM one through a headless browser with the preinstalled Chromium).
- **Printing:** a fake transport records bytes, and the spooler/CUPS end-to-end runs in CI as above.
  Real hardware is checked by hand against the owner's printers before each release (a checklist in
  the README).
- **Fuzzing** the template parser and the SVG and font readers (SharpFuzz, MIT), since the connector
  takes these from the network.

## Phases

Each phase ends with something usable and a NuGet preview.

| # | Scope | Exit criteria |
|---|---|---|
| 0 | Skeleton: folder, props, licence, CI on Linux and Windows, `Core` (units, media, scene), `Rendering.Svg` for shapes and paths | CI green on both OSes; a scene with shapes exports to an SVG that prints at true size |
| 1 | `Barcodes` phase-1 set + payload builders; SVG, plus PNG and PDF through `Rendering.Skia`; `druckwerk barcode` | Every symbology round-trips through ZXing at 203/300/600 dpi; **0.1.0-preview on NuGet** |
| 2 | `Text` (font reader, layout), `Templates` (JSON, binding, repeats, sheets, batches), `Imaging` (dither, TIFF, PBM, BMP), JPEG/WebP; `druckwerk render` | Shelf label, A4 Avery sheet and receipt samples render identically on both OSes; 0.2.0-preview |
| 3 | `Printing`: model, settings (size, orientation, scaling, offset), Windows raw + GDI, CUPS IPP (PDF + raw), device files, serial, ESC/POS + ZPL raster, profiles; receipt builder; `druckwerk printers/print/doctor` | Prints correctly sized and oriented on the owner's printers on Windows and CachyOS; 0.3.0-preview |
| 4 | Phase-4 symbologies, TSPL, EPL2, Brother QL, native text and barcodes, status, more stock, docs, API review | **1.0.0** |
| 5 | `Connector` + `Connector.Client` + host (service, systemd, image), auth, OpenAPI | Two connectors on two machines, each with its own printers, driven from a test client |
| 6 | `Studio`: server list, template editor, preview, batch print | Design a label in the browser and print it on either server |

Phases 5 and 6 can start once phase 3's printing API has settled. They don't have to wait for 1.0.

## Risks and gotchas to plan for

- **Thermal barcodes and DPI.** At 203 dpi one dot is 0.125 mm. A 0.33 mm EAN module isn't a whole
  number of dots, and rounding per bar instead of per module skews the bar widths. That's why snapping
  is a renderer feature with dedicated tests, and why `ActualSize` is the default scaling.
- **Windows thermal printer drivers** often scale or add margins on their own. Raw is the default for
  printers that have a profile; driver mode is for everything else.
- **CUPS raw queues are deprecated**, and CUPS 3 moves to IPP Everywhere and "printer applications"
  (PAPPL) instead of classic queues. Device-file transport, and later an optional printer
  application, keep thermal printers working on Linux in the long run.
- **Browser to connector:** an HTTPS Studio can't call an `http://` connector (mixed content), and
  Chromium now asks for permission before pages reach local-network addresses (Local Network Access).
  The connector needs TLS (a self-signed certificate plus a trust step, or a real certificate through
  a reverse proxy), or it serves the Studio itself. Phase 5 has to document this.
- **Skia on Linux:** `SkiaSharp.NativeAssets.Linux.NoDependencies` avoids depending on fontconfig.
  We do font discovery ourselves anyway. Check musl (Alpine) and arm64 (the Pi) in CI.
- **Font consistency** between browser and server only holds if both use the same font *files*: the
  connector serves its fonts to the Studio, and templates name fonts the connector has.
- **A large surface for one maintainer.** The phases are cut so that each one is worth releasing even
  if the next never happens.

## Open questions for the owner

1. **Name and package IDs.** Keep "Druckwerk"? Publish as `CINE.Druckwerk.*` (recommended, reserve
   the prefix) or `Druckwerk.*`?
2. **Which printers do you have?** Model and connection (USB, serial, parallel) for each. Phase 3
   targets these first and adds their profiles.
3. **Where does it live?** `Applications/Druckwerk/` in this monorepo (consistent with LaunchHeim, shared
   CI, the site) or a dedicated public repo (cleaner for outside contributors and issues).
   Recommendation: start here and split it out with `git subtree split` if it grows a community.
4. **Fiscal receipts:** confirm they're out of scope.
5. **Scripts:** Latin/Greek/Cyrillic enough for now, or do you need Arabic, Hebrew, Indic or CJK?
6. **Network printers** (TCP 9100, IPP): after wired, or never?
7. **PDF/A** for archived receipts: needed?

Related: [[launchheim]], [[grocerytracker]], [[app-site]], [[0003-shared-domain-analytics]],
[[0004-cine-root-namespace]], [[0007-per-app-releases-and-prebuilt-images]]
