---
tags: [project, dotnet, printing, barcodes, nuget, plan]
created: 2026-10-16
updated: 2026-10-16
status: idea
---
# Druckwerk: plan

An MIT-licensed set of C# libraries on NuGet for barcodes, QR codes, labels, receipts (fiscal ones
included) and vector graphics in general. Every result exports to PNG, JPEG, TIFF, SVG and PDF, and
prints on any printer a customer may have, however it's connected, on Windows and Linux, with
adjustable size and orientation. Labels can be made MDR-conform. The libraries come first. A
server-side connector and a Blazor UI that talks to many connectors come later, and they only call
the libraries. **Germany is the first market**: language, defaults, fiscal law and medical labelling
are built for Germany before anywhere else.

Nothing here is built yet. Choices marked **Proposed decision** become records in `decisions/` once
the owner confirms them.

**Settled by the owner (2026-10-16):**
- The name stays **Druckwerk** (German: printing unit). It was free on nuget.org on 2026-10-16, and so
  was `CINE.*`.
- It lives in `Applications/Druckwerk/` while it's `0.x`, and gets its own repository at 1.0
  ([[0018-apps-get-their-own-repo-at-1-0]]).
- **MDR-conform labelling** is required ([[mdr-labelling]]).
- **Every kind of printer connection**, because we don't know what customers use.
- **Fiscal receipts are in scope** ([[fiscal-receipts]]).
- **Print jobs can come from anywhere**: other machines, networks and systems, not only a Studio on
  the same LAN.
- **SkiaSharp wherever it can do the job**, and our own code only for its gaps
  ([[0019-druckwerk-renders-with-skiasharp]]).
- **Germany first** (see "Germany first").

## Goals and non-goals

**Goals**
- Barcodes and 2D codes (1D, QR, DataMatrix, PDF417, Aztec, ...) that scan reliably on paper,
  including on 203 dpi thermal printers.
- **Medical device labels per MDR/IVDR and the German MPDG**: UDI from all four issuing entities (GS1,
  HIBCC, ICCBBA, IFA), AIDC plus HRI, ISO 15223-1 symbol placement, German plus other languages, and
  the controls a validated process needs (approved templates, audit trail, print verification).
- Labels in general: single labels, rolls, and A4 sheets with label grids (Avery Zweckform and Herma
  first), filled in batches from CSV or JSON data.
- Receipts: continuous 58/80 mm paper, item lines, totals, logo, QR, cut. **German fiscal receipts**
  (KassenSichV, §33 UStDV) first, other countries later. Electronic receipts.
- General vector drawing: shapes, paths, text, images, groups and transforms, plus SVG import.
- Templates in a versioned JSON format, bound to data. Editors and UIs only produce and consume
  this format.
- Output to SVG, PDF (incl. PDF/A), PNG, JPEG, WebP and TIFF, each print-ready (exact physical size,
  DPI metadata, 1-bit options).
- Printing over **every kind of connection**: OS queues, USB, serial, parallel, Ethernet/Wi-Fi (raw,
  LPD, IPP), Bluetooth Classic and BLE, plus forwarding to another connector. Media size, orientation,
  scaling, margins and offset are adjustable per job.
- Later: `Druckwerk.Connector` for servers, taking jobs from many sources, and a Blazor "Studio" (German
  first) that connects to several connectors.

**Non-goals** (keep the scope honest)
- Being the fiscal recording system: cash register logic, DSFinV-K export and the Kassenmeldepflicht
  belong to the POS. Whether we talk to TSEs is open question 3.
- Certifying anyone's MDR compliance. We supply features and validation evidence; the manufacturer
  validates (see "Compliance").
- Prepress: CMYK, spot colours, bleed and ICC-managed output.
- PDF import or editing, and raster photo editing.
- Installing printer drivers. We use the drivers the OS has, or talk to the printer raw.
- macOS as a target. It's not promised, though the CUPS/IPP path will probably just work.

## Principles

1. **Everything we ship is MIT, and so is everything it depends on, near enough.** See "License
   policy".
2. **SkiaSharp first, our code for the gaps** ([[0019-druckwerk-renders-with-skiasharp]]). Skia
   shapes text, rasterizes and writes PNG, JPEG, WebP, PDF and SVG. We write barcodes, TIFF, 1-bit
   output and dithering, mm-sized SVG, PDF/A-3 attachments, and everything about printing.
3. **The same code on the server, in the CLI and in the browser.** Core, Barcodes and Templates are
   pure managed code. Rendering runs everywhere through SkiaSharp's native builds, WebAssembly
   included, so the Studio's preview uses the real renderer, the same idea as
   [[0003-shared-domain-analytics]].
4. **Reproducible output per platform.** The same template, data and version give the same result on
   a given OS and architecture, enforced by golden hashes in CI. Barcodes are drawn aliased on
   dot-snapped rectangles, so printer output is expected to match across platforms, and a test
   proves it.
5. **One pipeline: template → scene → output.** Exporters and printers consume the same scene
   (physical units, mm). No output format has its own layout logic.
6. **Physical units first, pixels last.** Geometry is in millimetres until the last step, which
   rasterizes at the target DPI and snaps barcode modules to whole dots there.
7. **Raster first, native later.** Printing renders a 1-bit or grey bitmap at the printer's exact DPI
   and sends it in the printer's language. That works for every printer, needs no code pages for
   umlauts, and looks like the preview. Native printer text and barcodes come later, as an
   optimization.
8. **Connections are plug-ins.** The library never assumes where a printer is or where a job comes
   from. Every transport implements the same interface and is addressed by a URI.
9. **Trimming and AOT safe.** `IsAotCompatible`, System.Text.Json source generation, no reflection,
   so the CLI ships as a Native AOT binary and the WASM UI stays as small as Skia allows.
10. **Comment density and conventions as in the rest of the repo** (CLAUDE.md): `CINE.` root
    namespace, 2-space indentation, file-scoped namespaces, primary constructors, and `Path.Combine`
    everywhere, because Windows is a first-class target.

## Germany first

What "fit for Germany" means in each part. Other countries come after 0.4, as profiles and
translations, without changes to the architecture.

| Area | Germany first |
|---|---|
| Language | Studio, CLI output, docs and every message an end user sees (validation errors, check results) in **German first**, English second (resx, `de` as the neutral fallback for the Studio). Exception messages for developers stay English, as usual for a NuGet library. |
| Defaults | `de-DE` formatting (`1.234,56 €`, `16.10.2026`), A4, millimetres, Euro, Europe/Berlin for receipt timestamps. VAT rates are *data*, never constants (19 % and 7 % today; they were 16 % and 5 % in the second half of 2020). |
| Fiscal | KassenSichV / §146a AO receipt content, TSE data as text or the DFKA `V0;` QR code, TSE-failure note, §33 UStDV *Kleinbetragsrechnung* content up to 250 €, §14 UStG invoice content above that, electronic receipts as PDF/A for the *Belegausgabepflicht*, GoBD-compliant archive copies. Details: [[fiscal-receipts]]. |
| Medical | MDR/IVDR plus the **MPDG** (German-language information for users in Germany), the IFA PPN in DataMatrix with the ISO/IEC 15434 envelope, BfArM as the competent authority in the docs. EU rules first; US FDA Part 11 later. Details: [[mdr-labelling]]. |
| Payments | GiroCode (EPC QR) for receipts and invoices. |
| Stock catalog | Avery Zweckform and Herma A4 sheets first, DIN A4/A5/A6, the DIN 5008 address field for window envelopes, then Brother, DYMO and Zebra rolls and 58/80 mm receipt rolls. |
| Printers | Profiles first for printers common in German retail, pharmacy and healthcare: Epson TM (incl. models with a built-in TSE), Star Micronics, Bixolon for receipts; Zebra, Brother, DYMO, TSC for labels. |
| Data protection | DSGVO by design: self-hosted by default, no telemetry, configurable retention and deletion for jobs and audit data (the tax retention periods for receipts versus erasure requests are documented), and a template for the processing record (*Verzeichnis von Verarbeitungstätigkeiten*) in the docs. |
| Character sets | Raster printing has no umlaut problem. Native mode later needs code pages: ESC/POS CP858 or WPC1252 for `äöüß` and `€`, ZPL `^CI28` (UTF-8). |
| Accessibility | The Studio aims at WCAG 2.1 AA. E-receipts sent to consumers can fall under the BFSG (since 2025-06-28), so they get a tagged, readable form. |

## License policy

**Proposed decision:** every package is MIT, `Copyright (c) 2026 CodeIsNotEvil`, with
`<PackageLicenseExpression>MIT</PackageLicenseExpression>`.

Runtime dependencies (anything a consumer receives) must use a permissive licence: MIT, BSD-2/3,
zlib, or Apache-2.0 only when no MIT alternative exists. Fonts we bundle may be under the SIL OFL 1.1
and ship in a separate, optional package. Test-only and build-only dependencies may use any OSI
licence.

**Excluded**, even though they're popular:

| Library | Why |
|---|---|
| ImageSharp, ImageSharp.Drawing, SixLabors.Fonts | Six Labors Split License: not free for every commercial user |
| QuestPDF | Community licence with a revenue limit |
| iText | AGPL |
| Aspose, Spire, IronBarcode, ... | Commercial |
| LibUsbDotNet | LGPL. We talk to usbfs and WinUSB directly instead |
| System.Drawing.Common | MIT, but Windows-only since .NET 6 |

**Chosen dependencies** (stable versions current on 2026-10-16):

| Package | Licence | Used for | Where |
|---|---|---|---|
| SkiaSharp 4.153.1 | MIT (Skia: BSD-3) | rasterizing, PNG/JPEG/WebP, PDF, SVG, image decoding | `Druckwerk.Rendering` |
| SkiaSharp.HarfBuzz 4.153.1 | MIT (HarfBuzz: Old MIT) | text shaping, complex scripts | `Druckwerk.Rendering` |
| SkiaSharp.NativeAssets.Linux / .WebAssembly 4.153.1 | MIT | native Skia for Linux (needs fontconfig) and for Blazor WASM | `Druckwerk.Rendering`, Studio |
| PDFsharp 6.5.0 | MIT | PDF/A-3 attachments and metadata after Skia, merging batches | `Druckwerk.Pdf` |
| Microsoft.Windows.CsWin32 | MIT | source-generated P/Invoke (winspool, GDI, SetupAPI, WinUSB, Winsock Bluetooth) | build time only |
| System.IO.Ports | MIT | serial printers, Bluetooth SPP as a COM port | `Druckwerk.Printing` |
| Tmds.DBus.Protocol 0.95.x | MIT | BlueZ (Bluetooth) and Avahi over D-Bus on Linux | `Druckwerk.Printing.Linux` |
| ZXing.Net 0.16.x + Bindings.SkiaSharp | Apache-2.0 | decodes rendered barcodes | `Druckwerk.Verification`, tests |
| veraPDF | GPL/MPL (Java) | **CI only**: checks our PDF/A output | CI |

ZXing.Net is the one Apache-2.0 runtime dependency, kept in its own package so nobody gets it without
asking.

**Our own code** covers the gaps: barcode encoders and dot snapping, the TIFF encoder (BitMiracle.
LibTiff.NET 2.4.x, BSD-3, as a fallback), 1-bit conversion and dithering, PBM/BMP and 1-bit PNG
writers, mm sizing for Skia's SVG, the IPP and LPD clients, mDNS discovery, printer languages and
transports.

Reference implementations to read (MIT, so studying them and porting ideas is fine): QRCoder 1.8.x,
Net.Codecrete.QrCodeGenerator 3.2.x (a port of Nayuki's QR generator), SharpIppNext 5.x for IPP,
Svg.Skia 5.x (MIT, Skia-based) for SVG import into a scene. If writing our own costs too much,
depending on one of them directly stays within policy.

Trademarks: the README says "QR Code is a registered trademark of DENSO WAVE INCORPORATED" and names
ESC/POS (Epson) and ZPL (Zebra) as their owners' trademarks. The symbologies themselves are free to
implement (Denso waives the QR patent, Aztec is public domain, the GS1 specs are free to read).
**ISO 7000 / 15223-1 symbol artwork is copyrighted by ISO** and doesn't ship until that's cleared (see
[[mdr-labelling]]).

`THIRD-PARTY-NOTICES.txt` and `licenses/` follow the LaunchHeim pattern ([[launchheim]], "Key ideas").
An SBOM (CycloneDX) is generated for every release.

## Package map

**Proposed decision:** split along dependency and platform lines rather than features. Splitting
later is easier than merging. Package IDs are open question 1.

| Package | Contains | Depends on |
|---|---|---|
| `Druckwerk.Core` | `Length`/units (mm, in, pt, dots), `Size`, `Thickness`, colours, media model, scene graph (shapes, paths, text blocks, images, groups, transforms), SVG path-data parser | nothing |
| `Druckwerk.Barcodes` | Symbology encoders → geometry, check digits, data structures: GS1 AIs, HIBC LIC/PAS, ISBT 128, ISO/IEC 15434 envelope (IFA PPN), payload builders (GiroCode/EPC, Wi-Fi, vCard, MeCard, URL, e-mail, SMS, geo, calendar, Swiss QR bill, GS1 Digital Link) | Core |
| `Druckwerk.Templates` | JSON template model and schema, identity/version/approval fields, migrations, data binding, data schemas, label sheet imposition, media/stock catalog | Core, Barcodes |
| `Druckwerk.Rendering` | Layout (template + data → scene, text through Skia + HarfBuzz), fonts, rasterizing at a DPI with barcode snapping, PNG/JPEG/WebP, PDF/PDF-A-2b, SVG in mm. **Gaps:** TIFF, 1-bit/dithering, PBM/BMP | Core, Barcodes, Templates, SkiaSharp, SkiaSharp.HarfBuzz |
| `Druckwerk.Pdf` | PDF/A-3 with embedded files (e-receipts, later ZUGFeRD), metadata fixes found by veraPDF, merging batches | Rendering, PDFsharp |
| `Druckwerk.Receipts` | Receipt builder, fiscal profiles (Germany first), mandatory-field validation, fiscal QR payloads, copy/training/TSE-failure markers, electronic receipts | Templates, Rendering, Pdf |
| `Druckwerk.Medical` | MDR/IVDR/MPDG label kit: UDI composition per issuing entity, HRI formatting, symbol slots per ISO 15223-1, mandatory-content checks, German-first sample templates | Templates, Barcodes |
| `Druckwerk.Verification` | Decodes rendered barcodes and compares them with the intended data; checks minimum X-dimension, quiet zones and contrast at the target DPI | Rendering, ZXing.Net |
| `Druckwerk.Printing` | Printer abstraction, transport and discovery interfaces, URI scheme, `PrintSettings`, job pipeline, printer profiles, languages (ESC/POS, ZPL, TSPL, EPL2, DPL, Brother QL raster, PCL/PWG raster), cross-platform transports (TCP raw, LPD, IPP/IPPS, serial, file, connector forward), mDNS/DNS-SD and SNMP discovery | Core, Rendering, System.IO.Ports |
| `Druckwerk.Printing.Windows` | Spooler (raw + GDI driver printing), USB without a queue (USBPRINT interface, WinUSB), parallel ports, Bluetooth Classic (Winsock `AF_BTH`) and BLE (WinRT), Windows discovery | Printing; TFM `net10.0-windows10.0.19041.0` |
| `Druckwerk.Printing.Linux` | CUPS specifics, usblp device files, raw USB through usbfs, parallel `/dev/lp*`, Bluetooth Classic (RFCOMM sockets) and BLE (BlueZ over D-Bus), IEEE 1284 IDs from sysfs | Printing, Tmds.DBus.Protocol |
| `Druckwerk.Fonts` | Optional: a few OFL fonts (a sans, a mono, a condensed one for labels, an OCR-B for barcode digits), all with full German coverage | none |
| `Druckwerk.Cli` | `dotnet tool` and Native AOT binary `druckwerk`, German and English output | all of the above |
| `Druckwerk.Connector` (phase 6) | ASP.NET Core endpoints, job queue, job sources, audit trail, template lifecycle | Templates, Rendering, Receipts, Medical, Printing.* |
| `Druckwerk.Connector.Client` (phase 6) | Typed HTTP client and DTOs, WASM-safe | nothing heavy |
| `Druckwerk.Studio` (phase 7) | Blazor WASM UI, not a NuGet package | Core, Barcodes, Templates, Rendering (Skia WASM), Connector.Client |

`Druckwerk.Printing` picks up the platform package that matches the OS at runtime, if it's
referenced. A meta-package `Druckwerk` (all common packages for both OSes) keeps the first install a
one-liner. Code that only needs barcodes or the template model takes `Barcodes` or `Templates` and
gets no native assets.

## Core model

- **Units.** `Length` is a struct stored as double millimetres, built with `Length.Mm(50)`,
  `Length.In(2)`, `Length.Pt(10)` or `Length.Dots(406, dpi: 203)`. JSON accepts plain numbers (mm) or
  strings with a unit (`"2in"`, `"10pt"`). Conversions to dots round in one place with one rule.
- **Media.** `Media` is one of:
  - `Sheet` (A4 by default, others and custom), optionally with a `LabelGrid` (rows, columns, pitch,
    margins);
  - `DieCutLabel` (width, height, gap, roll);
  - `Continuous` (width; the length grows with the content or is fixed);
  - `BlackMark` (width, mark pitch).

  Each has a printable area. `Druckwerk.Templates` ships the stock catalog (see "Germany first").
- **Scene.** An immutable tree: `Group(transform, clip, children)`, `Rect`, `Ellipse`, `Line`,
  `Path` (move/line/cubic/quad/arc/close, fill rule), `TextBlock` (laid-out text: font, glyphs,
  positions), `Image` (encoded bytes plus intrinsic size), `BarcodeNode` (the symbology's geometry,
  quiet zones and the encoded data, which the verifier compares against). Paints: solid colour and
  stroke (width, cap, join, dash). Gradients only if someone asks.
- **Why `BarcodeNode` stays a node** instead of becoming rectangles straight away: the renderer must
  snap module widths to whole dots at the final DPI, or a 203 dpi printer prints uneven bars that don't
  scan. SVG and PDF export can still turn it into rectangles.

## Barcodes

Encoders return geometry, never images: 1D codes return run lengths (bars and spaces in modules) plus
the human-readable text, 2D codes a `BitMatrix` plus the module shape. Validation errors are
specific and translated ("EAN-13 braucht 12 oder 13 Ziffern; die Prüfziffer müsste 7 sein").

| Phase | Symbologies |
|---|---|
| 1 | Code 128 (auto A/B/C switching, GS1-128 with FNC1 and AI parsing, HIBC, ISBT 128), EAN-13, EAN-8, UPC-A, UPC-E, EAN-2/5 add-ons, Code 39 (+ full ASCII, HIBC), ITF/ITF-14 (bearer bars), QR Code (numeric/alphanumeric/byte/Kanji, ECI, ECC L–H, mask choice, version auto/fixed, FNC1 modes), Data Matrix ECC 200 (GS1 DataMatrix, ISO/IEC 15434 macros 05/06 for IFA, rectangular sizes) |
| 5 | PDF417 (+ compact), Aztec (+ runes), Codabar, Code 93, MSI, GS1 DataBar (omni, limited, expanded, stacked), Micro QR, rMQR |
| later | MaxiCode, DotCode, Han Xin, postal codes, only on request |

The phase-1 set already covers every UDI carrier the four issuing entities allow, the PZN (Code 39,
the German pharmacy number), GiroCode and the fiscal QR code.

Options: module size (mm or dots), quiet zone, bar width reduction (thermal and inkjet ink spread),
human-readable text (font, position, the EAN/UPC guard bar layout, GS1 HRI with AIs in brackets),
QR/DataMatrix module shape (square, dot, rounded) and an optional logo in the centre that respects the
error-correction budget.

The `Druckwerk.Cli` command `druckwerk barcode qr "https://…" -o code.svg` is the first useful thing
we ship.

## Text and fonts

- Skia + HarfBuzz shape the text (`SkiaSharp.HarfBuzz`), so kerning, ligatures and complex scripts
  (Arabic, Hebrew, Indic, CJK) work from the start. Bidi and line breaking (UAX #9, UAX #14) are ours,
  on top of the shaper.
- Fonts come from: files and streams the caller registers, `Druckwerk.Fonts`, and system fonts
  through Skia's font manager (DirectWrite on Windows, fontconfig on Linux, nothing in the browser,
  where the connector or the Studio supplies the files). For validated setups, templates pin fonts by
  file hash, so a changed system font can't silently change a released label.
- Layout: wrapping, alignment, line height, max lines with an ellipsis, and auto-shrink to fit a box.
  Text that doesn't fit is an error in strict mode, never silently cut, because a cut-off lot number
  or warning on a medical label is a defect.
- Output: Skia's PDF embeds subsetted fonts with a ToUnicode map (selectable, searchable, PDF/A
  needs both). SVG gets text as paths by default.

## Templates

A versioned JSON document. The format is the contract between the libraries, the CLI, the connector and
the Studio.

```json
{
  "$schema": "https://codeisnotevil.github.io/MyMonoRepo/druckwerk/template-1.schema.json",
  "formatVersion": 1,
  "id": "4f0c…", "revision": 7, "state": "released",
  "name": "Regaletikett",
  "culture": "de-DE",
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
  date, upper/lower, pad, substring, default) formatted in the template's culture (`de-DE` by
  default). `visibleIf` takes comparisons, `and`/`or`/`not` and nothing else. **No general-purpose
  scripting** (no Roslyn, no Liquid engine): connectors accept templates from the network, so the
  language must not be able to run code. A data schema per template declares required fields and
  types; missing data fails the job instead of printing a blank.
- **Languages.** Text elements can hold per-language strings (`{"de": "…", "en": "…"}`), printed as a
  list or as one language per label variant. MDR labels need this.
- **Repeats.** A `repeat` element lays out a list (receipt items, table rows). On continuous media the
  page grows; on sheets it breaks to the next page.
- **Batch.** One template with N data records gives N labels: on a roll, one label each; on a sheet,
  imposed into the grid with an optional start position (so a half-used Zweckform sheet can be
  reused). `{{$index}}` and `{{$serial | pad: 6}}` (start and step set per batch) cover numbering.
- **Identity and lifecycle.** `id`, `revision` and `state` (draft, in review, released, retired) are
  part of the format from the first version, and a content hash identifies exactly what was approved.
  The libraries only carry and check these fields. Approval workflows and signatures live in the
  connector (phase 6). A `strict` print policy refuses anything but released templates.
- **Versioning.** `formatVersion` plus migrations in code. Old templates always load, and saving writes
  the newest version. Migrating a released template creates a new draft revision, so approval is
  never inherited silently. A JSON schema for editors is generated from the model and published on
  the site.

## Receipts and German fiscal receipts

Details and sources: [[fiscal-receipts]].

- `Receipt.Create(Media.Roll80mm).Logo(png).Center().Bold().Text("Café Beispiel").Items(lines).Taxes(...).Total(...).Fiscal(tse).Cut()`
  builds a template-backed scene. Receipt templates work like every other template.
- **`FiscalProfile.Germany`** is the first profile:
  - It validates the KassenSichV and AEAO content before anything prints: business name and address,
    date, start and end time, items, transaction number, amounts and tax per rate, the POS and TSE
    serial numbers, signature counter and signature.
  - It adds the §33 UStDV *Kleinbetragsrechnung* content and switches to §14 UStG invoice content
    above 250 €.
  - It builds the DFKA `V0;…` QR code or prints the TSE data as text.
  - It marks copies (*Kopie*), training receipts (*Trainingsbuchung*) and TSE failure
    (*TSE ausgefallen*).
- **Electronic receipts**: the same receipt as PDF/A-3 with the receipt data embedded as JSON
  (`Druckwerk.Pdf`), for e-mail, download or a QR link. That fulfils the *Belegausgabepflicht* without
  paper.
- Archive copies go out as PDF/A, with retention configurable to match the tax periods.
- More countries (Austria RKSV next) become profiles after 0.4.
- Native ESC/POS text (printer fonts, code page CP858 or WPC1252 for umlauts and `€`) is an optional
  fast path in phase 5.

## Medical device labels (MDR/IVDR/MPDG)

Details and sources: [[mdr-labelling]].

- `Druckwerk.Medical` composes the **UDI** from structured input for the chosen issuing entity (GS1,
  HIBCC, ICCBBA, IFA), picks a valid carrier (GS1 DataMatrix or GS1-128, HIBC in DataMatrix or Code
  128, ISBT 128, IFA DataMatrix with the ISO 15434 envelope) and formats the HRI to match, so the
  barcode and the text can't disagree.
- **Content checks** per MDR Annex I §23, ISO 15223-1 and the MPDG's language rule: a declarative
  checklist per label type (device, unit of use, higher packaging level, IVD). It flags missing items
  such as LOT or expiry, missing "MD"/"IVD" or UDI symbols, a carrier below its minimum size, or no
  German text on a label for the German market. Check results are in German first.
- **Symbols** go in named slots (`symbol: "iso15223/manufacturer"`) with sizes and positions in the
  template, and their artwork comes from a symbol set the customer loads (ISO copyright, see
  [[mdr-labelling]]).
- **Verification** (`Druckwerk.Verification`): every barcode in the rendered raster, at the printer's
  DPI, is decoded and compared with the data the template meant to encode before the job reaches the
  printer. Optionally, a scanner after the printer (HID or serial) checks the printed label (phase
  6). This is a software check, not ISO/IEC 15415/15416 grading, which needs a calibrated verifier.

## Rendering and output formats

[[0019-druckwerk-renders-with-skiasharp]]: Skia does the work, and we fill its gaps.

| Format | Done by | Vector | Print notes |
|---|---|---|---|
| PDF | Skia `SKDocument` | yes | page size = media, subsetted fonts, metadata |
| PDF/A-2b | Skia (`PdfA` metadata), checked by veraPDF, fixed up with PDFsharp if needed | yes | archive copies, audit exports |
| PDF/A-3b | Skia + **our step** with PDFsharp (embedded files) | yes | e-receipts with JSON; ZUGFeRD later |
| SVG | Skia `SKSvgCanvas` + **our mm sizing** | yes | `width`/`height` in mm and a `viewBox`, so it prints at true size; text as paths |
| PNG | Skia; **ours** for 1-bit | no | pHYs chunk with the DPI; 1-bit, grey or RGBA |
| JPEG | Skia | no | JFIF density set; not recommended for barcodes (artifacts), and the docs say so |
| WebP | Skia | no | for the web |
| TIFF | **ours** (LibTiff.NET as fallback) | no | X/YResolution tags; 1-bit CCITT G4, PackBits, Deflate; multi-page for batches |
| PBM/BMP | **ours** | no | debugging and old printers |

- Raster output for printers is drawn **aliased** at the device DPI, with barcode modules snapped to
  whole dots, then converted to 1-bit by **our** thresholding or dithering (Floyd–Steinberg, Atkinson,
  ordered). Photos and logos get dithered, text and barcodes get thresholded, so barcode edges stay
  sharp.
- Every raster export takes a DPI (default 300, or 203/300/600 to match a printer) and uses the same
  snapping as printing. Batches export as multi-page PDF or TIFF, or as numbered files.
- SVG import (logos, symbol artwork) goes through Svg.Skia into the scene.

## Printing

### Model

```
ITransport          open/write/read/close a byte stream; says whether it's bidirectional
ITransportProvider  discovers endpoints of its kind and opens transports from URIs
IPrinter            transport + language + profile; GetCapabilities(), PrintAsync(pages, PrintSettings) → PrintJob
```

Every printer is addressed by a **connection URI**, so configurations can be stored, shared and
typed in by hand:

```
spooler://Zebra ZD421                     Windows queue (local, shared, WSD, Universal Print)
cups://Label_Printer                      CUPS queue (local or remote CUPS)
ipp://host/ipp/print   ipps://…           IPP Everywhere / AirPrint printers, direct
socket://host:9100                        raw TCP (AppSocket/JetDirect)
lpd://host/queue                          LPD/LPR (RFC 1179)
usb://04b8:0e28?serial=X123               USB without a queue (usbfs / USBPRINT / WinUSB)
device:///dev/usb/lp0                     Linux usblp device file
serial:///dev/ttyUSB0?baud=115200&flow=rtscts   serial / USB-CDC / COM3
parallel:///dev/lp0   parallel://LPT1     parallel port
bt://00:11:22:33:44:55?channel=1          Bluetooth Classic SPP (RFCOMM)
ble://00:11:22:33:44:55?service=…&char=…  Bluetooth Low Energy GATT (many mobile label printers)
connector://lager.example.de/printers/zebra-1   forward to a printer on another connector
file:///tmp/out.prn                        write the bytes to a file (tests, emulators)
```

A third-party package can register more schemes (for example a vendor SDK or a cloud print service)
without changes to the core.

**`PrintSettings`** is the "size and orientation" requirement:
- `Media`: from capabilities, the stock catalog, or custom width × height; continuous with auto length.
- `Orientation`: Portrait, Landscape, ReversePortrait, ReverseLandscape, Auto (follow the content's
  aspect ratio). Rotation happens in the scene before rasterizing, so it's identical on every printer,
  language and transport.
- `Scaling`: ActualSize (default, so barcodes keep their module size), FitToPrintableArea,
  FillAndCrop, ShrinkOnly. Strict mode refuses scaling that would put a barcode below its minimum
  size; otherwise it warns.
- `Alignment` within the printable area, plus `Margins`.
- `Offset` (x/y in mm) for per-printer calibration, stored in the printer profile.
- `Copies`, `Collate`, `Dpi` (from capabilities), `ColorMode` (Mono, Grey, Colour), `Dithering`.
- Thermal only: `Darkness`, `Speed`, `Cut` (None, Partial, Full; per label or at the end), `Mirror`,
  `Feed`.

**Printer profiles:** JSON files (shipped ones plus user ones) that describe a model: language, DPI,
head width in dots, cutter, the commands it supports, quirks, and its USB IDs and IEEE 1284 ID for
automatic matching. For example "Epson TM-T20III: ESC/POS, 203 dpi, 576 dots, partial cut, GS v 0
raster". Unknown printers get a guided calibration: print a test pattern, measure, save the profile.

### Pipeline

```
Template + data → bind → validate (data schema, fiscal/MDR checks) → layout (Skia + HarfBuzz) → Scene (mm)
  → apply PrintSettings (rotate, scale, align, offset)
  → rasterize at the printer's DPI (Skia, aliased, barcode modules snapped to dots)
  → 1-bit / dither (ours)
  → verify (decode every barcode, compare with the intended data)          [optional; required in strict mode]
  → encode in the printer's language: ESC/POS │ ZPL │ TSPL │ EPL2 │ DPL │ Brother QL │ PCL/PWG raster │ GDI │ PDF
  → transport (any URI above) → job status, and a job record with hashes of the template, data and output
```

### Languages (raster first)

| Language | Printers | Image command | Phase |
|---|---|---|---|
| ESC/POS | Epson TM, Star (ESC/POS mode), Bixolon and most receipt printers | `GS v 0` (and `GS ( L` where supported), `GS V` cut, `ESC p` drawer kick | 3 |
| ZPL II | Zebra and many clones | `^GFA` with Z64 (zlib + base64, through `System.IO.Compression`), `^PW`, `^LL`, `^PO`, `~SD` darkness | 3 |
| Driver (GDI / PDF) | everything with an OS driver: office printers, DYMO, Brother P-touch | Windows: GDI bitmap at device DPI; Linux: PDF to CUPS | 3 |
| IPP Everywhere / PWG raster, PCL 3/5 raster | driverless office printers | PDF or PWG raster over IPP; PCL raster over raw TCP | 3 (IPP), 5 (PCL) |
| Star Line Mode / StarPRNT | Star Micronics receipt printers in native mode | raster commands | 5 |
| TSPL/TSPL2 | TSC, many cheap label printers | `BITMAP`, `SIZE`, `GAP`, `DIRECTION` | 5 |
| EPL2 | older Zebra/Eltron | `GW` | 5 |
| DPL | Honeywell/Datamax | graphics `D` records | 5 |
| Brother QL raster | QL-5xx/7xx/8xx | `ESC i a` raster mode, media info | 5 |
| Vendor BLE protocols | mobile label printers (Niimbot, Phomemo, ...) | per profile, reverse-engineered protocols | on request |

Native text, barcodes and QR in ZPL and ESC/POS come in phase 5 as `PrintMode.Native`, with
automatic fallback to raster for anything the printer can't do. Native mode is never allowed in strict
mode, because the verifier can't see what the printer renders itself.

### Transports and discovery

| Connection | Windows | Linux | Phase |
|---|---|---|---|
| OS queue | spooler: `EnumPrinters`, raw `WritePrinter`, GDI driver printing with `DEVMODE` (paper, orientation, copies, resolution), `DeviceCapabilities` | CUPS through our IPP client at `localhost:631` (discovery, `media-supported`, `Print-Job` with PDF or `application/vnd.cups-raw`) | 3 |
| Raw TCP 9100 | sockets | sockets | 3 |
| IPP / IPPS | our IPP client | our IPP client | 3 |
| LPD | our client | our client | 3 |
| Serial, USB-CDC | `System.IO.Ports` (COMn) | `System.IO.Ports` (`/dev/ttyUSB*`, `/dev/ttyACM*`, `/dev/ttyS*`) | 3 |
| USB printer class | queue, or USBPRINT device interface (SetupAPI + `CreateFile`, bidirectional) without a queue | `/dev/usb/lp*` (usblp) | 3 |
| USB vendor-specific | WinUSB (needs the WinUSB driver bound, e.g. through a shipped INF) | usbfs (`/dev/bus/usb`, ioctls), udev rule shipped | 5 |
| Parallel | queue, or `\\.\LPTn` | `/dev/lp*` | 3 |
| Bluetooth Classic (SPP) | `AF_BTH` sockets, or the paired virtual COM port | RFCOMM sockets (`AF_BLUETOOTH`), BlueZ pairing over D-Bus | 5 |
| Bluetooth LE | WinRT `Windows.Devices.Bluetooth` | BlueZ GATT over D-Bus | 5 |
| Another connector | HTTPS to its API | HTTPS to its API | 6 |
| File / null | yes | yes | 0 |

- **Discovery**: OS queues, USB enumeration with IEEE 1284 IDs (sysfs on Linux, SetupAPI on Windows),
  serial port enumeration, mDNS/DNS-SD (`_ipp._tcp`, `_ipps._tcp`, `_pdl-datastream._tcp`,
  `_printer._tcp`) with our own minimal mDNS client, Bluetooth scans, and SNMP (Printer MIB) for
  status and models on networks without mDNS.
- **Status**: the spooler and CUPS report job state. Bidirectional transports query the printer
  itself: ESC/POS `DLE EOT` (paper out, cover open), ZPL `~HS`, IPP printer state, SNMP.
- **Permissions on Linux**: the `lp` group for printers, `uucp` for serial on Arch/CachyOS (`dialout`
  on Debian/Fedora), udev rules for usbfs, the `bluetooth` group for some BlueZ setups. `druckwerk
  doctor` checks all of them and prints the exact fix, in German or English.

## Where jobs come from

We don't know where print jobs originate, so the connector accepts them from many sources and treats
every source as untrusted until it authenticates.

| Source | How | Phase |
|---|---|---|
| Studio and apps | REST `/api/v1/jobs` (template id + data, or a rendered document) | 6 |
| Legacy software that "prints to a printer" | a raw TCP 9100 listener and an IPP printer endpoint per virtual printer, so an OS, POS or ERP adds the connector as an ordinary network printer; incoming ZPL/ESC/POS/PDF is forwarded, or mapped to a template | 6 / 8 |
| Batch and ERP integrations | hot folders (template + JSON/CSV dropped into a watched directory), webhooks | 6 |
| Message brokers | MQTT and AMQP adapters as separate packages | on request |
| Other connectors | `connector://` forwarding, so one site prints on another's printer | 6 |
| Behind NAT or a firewall | **relay mode**: the connector dials *out* to a self-hosted relay over WebSocket, and clients reach it through the relay, with no inbound port to open | 8 |

## Connector and Studio (phases 6–8)

**Connector** (`Druckwerk.Connector`, a library you add to any ASP.NET Core host, plus a ready host
`Druckwerk.Connector.Host` as a Windows service, systemd unit and container image):
- REST under `/api/v1`, described with OpenAPI: `GET /info` (version, format versions, features,
  languages), `GET /printers`, `GET /printers/{id}/capabilities`, `GET /stock`, CRUD `/templates`,
  `POST /render` (scene or template + data → any output format), `POST /jobs`, `GET /jobs/{id}`, and
  Server-Sent Events for job status. Problem details (`application/problem+json`) are in the language
  the client asks for (`Accept-Language`, German by default).
- Each server exposes *its own* printers, label stock, templates and fonts. That's the
  "different printers, labels, QR codes per server" requirement.
- **Security, because clients can be anywhere**: TLS required except on loopback; authentication
  required (API keys stored hashed, mTLS for machine clients, OIDC/JWT for people; the owner's
  Keycloak compose in `Configurations/` works for testing); per-client permissions per printer and
  template; rate limits; size limits on templates, data and images (decompression bombs); templates
  can't fetch URLs (no SSRF); raw printer passthrough off by default; a CORS allowlist of Studio
  origins.
- **Compliance features** (MDR, fiscal):
  - A template lifecycle with review and release steps and electronic signatures (user, meaning,
    timestamp, re-authentication). Released revisions are immutable.
  - An append-only, hash-chained **audit trail** of template changes and print jobs: who, when,
    template revision and hash, data, a hash of the rendered page content, printer, copies, result,
    and the verification outcome.
  - Reprints are marked and linked to the original.
  - Audit export as PDF/A and JSON.
  - DSGVO: retention and deletion policies per data type, and data minimization (store hashes instead
    of payloads where that's allowed).
  - Storage: files by default (templates as JSON, git-friendly), with PostgreSQL optional for audit
    and jobs at scale.
- Containers: printer access from a container needs the device, the CUPS socket or host networking
  passed through, and the image needs fontconfig and fonts for Skia. The compose file follows the
  Podman rules in CLAUDE.md (fully qualified images, high ports), but the main deployment is a native
  service.

**Studio** (Blazor WebAssembly standalone, like GroceryTracker; German first, English second):
- A list of connectors (URL, credentials, name; direct or through a relay) stored in the browser, with
  a status for each (reachable, version compatible). Pick a server, then its printers, stock and
  templates. Templates can be copied between servers by export and import (same format; `/info`
  reports which format versions a server understands). An import arrives as a draft, so it goes
  through that server's approval.
- The editor works on the template model. The canvas uses SVG DOM elements for selection, handles,
  snapping and guides, and the print preview comes from Skia in WebAssembly at the printer's DPI,
  dithered, so it shows exactly what the thermal printer will produce, without a round trip.
- Data: sample JSON or CSV per template, live barcode and MDR/fiscal checks, batch print with a record
  range, review and approval screens, audit trail viewer.
- Each connector can also serve the Studio itself (same origin, no CORS), the way GroceryTracker's
  nginx does. A central copy works too.

## Compliance and validation support

The libraries are MIT and "AS IS". Conformity with the MDR, IVDR, MPDG or tax law stays the duty of
the manufacturer or business using them. What we provide makes that duty cheap:

- **Reproducible builds and output**: deterministic packages (SourceLink, deterministic build, signed
  tags), golden output hashes per platform in CI, and a published list of the platforms each release
  was validated on.
- **Validation kit per release**: a requirements list with IDs, a traceability matrix from
  requirements to tests, the test results from CI, known anomalies, the changelog and a CycloneDX
  SBOM, in German and English. It's a release asset, so a customer's IQ/OQ can cite it.
- **Customer-side OQ**: `druckwerk validate` runs a fixed test set on the customer's machine against
  their printers and stock (render, verify, print a test sheet, compare hashes) and writes a signed
  PDF/A report.
- **SOUP information** (IEC 62304) for software manufacturers embedding the libraries: the
  validation kit plus a stable support and deprecation policy after 1.0.
- `SECURITY.md` with a private reporting channel and coordinated disclosure, which customers and the
  EU Cyber Resilience Act expect.

## Repository, build and release

```
Applications/Druckwerk/
  Directory.Build.props        # RootNamespace CINE.$(MSBuildProjectName), Version, package metadata, IsAotCompatible
  Directory.Packages.props     # central package management
  Druckwerk.slnx  LICENSE  README.md (German + English)  CHANGELOG.md  SECURITY.md  THIRD-PARTY-NOTICES.txt  licenses/
  src/Druckwerk.Core  .Barcodes  .Templates  .Rendering  .Pdf  .Receipts  .Medical  .Verification
      .Printing  .Printing.Windows  .Printing.Linux  .Fonts  .Cli
      (.Connector  .Connector.Client  .Connector.Host  .Relay  .Studio later)
  tests/<one per package>  tests/Druckwerk.Golden  (golden files and hashes per platform)
  validation/                  # requirements, traceability, OQ test set
  samples/                     # console samples used in the README
```

- It moves to its own repository with the 1.0 release ([[0018-apps-get-their-own-repo-at-1-0]]), so
  everything stays self-contained under `Applications/Druckwerk/` from day one: own workflow, own tags,
  nothing shared with other apps except the site.
- Target `net10.0` (LTS until November 2028; .NET 8 leaves support in November 2026), plus
  `net10.0-windows10.0.19041.0` for `Printing.Windows`. Add `net12.0` when it arrives, the next LTS.
- One version for all packages in `Directory.Build.props`, the way LaunchHeim does it. SemVer, `0.x`
  until the API review, then 1.0 with package validation against the previous version as a baseline.
  SkiaSharp upgrades are deliberate: they can change output hashes, so each one gets a changelog line.
- Package metadata: README per package, icon (SVG source, rendered PNG, as with the repo's other
  logos), SourceLink, deterministic build, `.snupkg` symbols, `PackageProjectUrl` pointing at the
  site.
- CI: GitHub Actions `druckwerk.yml` on `ubuntu-latest`, `windows-latest` and linux-arm64 (build,
  test, pack, golden hash check, veraPDF, golden diff upload on failure). The Windows job prints to
  "Microsoft Print to PDF" and the Linux job to CUPS queues that write files, plus loopback servers
  for raw TCP, LPD and IPP and a virtual serial pair (`socat`), so transports run end to end without
  hardware.
- Release: tag `druckwerk-vX.Y.Z` per [[0007-per-app-releases-and-prebuilt-images]] and
  [[0008-changelog-per-app]]. The workflow packs, pushes to nuget.org (Trusted Publishing through
  GitHub OIDC; an API key secret if that isn't set up), attaches the CLI binaries, the SBOM and the
  validation kit to the GitHub release and re-runs the site.
- The site gets a Druckwerk page (packages, CLI downloads, docs, German first) once 0.1 is out
  ([[app-site]]).

## Testing

- **Barcodes:** spec test vectors (known inputs → known modules), plus a round trip: render at
  several DPIs and module sizes, decode with ZXing.Net, compare. Property tests over random inputs
  per symbology. UDI, PZN, GiroCode and fiscal payloads against the published examples of GS1, HIBCC,
  IFA, the EPC and the DFKA.
- **Golden files and hashes:** SVG compared as text, PNG/TIFF and printer bytes compared by hash per
  platform, PDF compared by page content. A cross-platform test proves that aliased barcode output is
  identical everywhere. Regenerated by a documented switch, never by hand, and a changed hash needs a
  changelog line (it matters to validated customers).
- **Rendering in WASM:** the same tests run in a headless browser with the preinstalled Chromium.
- **German content:** fiscal receipts and MDR labels checked against the legal field lists, umlauts
  and `ß` in every font and output format, `de-DE` formatting.
- **Transports:** a fake transport records bytes; loopback servers and the CI print queues as above;
  Bluetooth and USB through hardware-in-the-loop checks before each release (a checklist in the
  README).
- **Fuzzing** the template parser, the SVG import and the IPP/LPD/mDNS parsers (SharpFuzz, MIT), since
  the connector takes these from the network.

## Phases

Each phase ends with something usable and a NuGet preview.

| # | Scope | Exit criteria |
|---|---|---|
| 0 | Skeleton: folder, props, licence, CI on Linux and Windows, `Core` (units, media, scene), `Rendering` basics (shapes to PNG, PDF and SVG in mm through Skia), file transport | CI green on both OSes; a scene exports to PDF and SVG that print at true size |
| 1 | `Barcodes` phase-1 set incl. GS1/HIBC/ISBT/IFA structures, PZN, GiroCode and other payload builders; dot snapping; JPEG/WebP; `druckwerk barcode` | Every symbology round-trips through ZXing at 203/300/600 dpi, aliased output identical on every platform; **0.1.0-preview** |
| 2 | `Templates` (JSON, binding, data schema, lifecycle fields, languages, repeats, sheets, batches), layout with Skia + HarfBuzz, the gaps (TIFF, 1-bit, dithering, PBM/BMP), PDF/A-2b, `Druckwerk.Pdf` (PDF/A-3); `druckwerk render` | Shelf label, Zweckform A4 sheet and receipt samples render with stable hashes; PDF/A passes veraPDF; 0.2.0-preview |
| 3 | `Printing`: model, URIs, settings, profiles, calibration; spooler (raw + GDI), CUPS/IPP, raw TCP, LPD, serial, USB printer class, parallel; ESC/POS + ZPL raster; discovery; `druckwerk printers/print/doctor` | Prints correctly sized and oriented over every phase-3 connection on Windows and CachyOS; 0.3.0-preview |
| 4 | **Germany**: `Receipts` with `FiscalProfile.Germany` and e-receipts; `Medical` (UDI, MDR/MPDG checks, symbol slots, languages); `Verification`; strict mode; German translations of all user-facing text | A KassenSichV receipt and an MDR device label for the German market pass their checks and verification end to end; 0.4.0-preview |
| 5 | Remaining connections (USB vendor-specific, Bluetooth Classic and BLE, mDNS, SNMP), TSPL, EPL2, DPL, Star, Brother QL, PCL, native mode with German code pages, status queries, phase-5 symbologies | Every row of the transport table works on both OSes; 0.5.0-preview |
| 6 | `Connector` + `Connector.Client` + host: API, security, job sources (REST, hot folder, webhooks, 9100 listener, forwarding), lifecycle, signatures, audit trail, DSGVO retention | Two connectors on two machines, each with its own printers, take jobs from three sources and keep an audit trail; 0.6.0-preview |
| 7 | `Studio` (German first): server list, template editor, Skia WASM print preview, approval screens, batch print, audit viewer | Design, approve and print a label in the browser on either server; 0.7.0-preview |
| 8 | Relay mode, IPP printer endpoint, `druckwerk validate`, validation kit, API review, docs in German and English, then the second fiscal country (Austria) | **1.0.0**, and the move to its own repository |

Phases 6 and 7 can start once phase 3's printing API has settled.

## Risks and gotchas to plan for

- **Skia output can differ by platform and version** (anti-aliasing, SIMD paths, PDF internals).
  That's why there are per-platform golden hashes, aliased rendering for printers, page-content
  hashes for PDF, and deliberate SkiaSharp upgrades.
- **Skia's SVG and PDF/A support has limits.** `SKSvgCanvas` writes pixel units, and its PDF/A flag
  only adds metadata. Our mm rewrite, veraPDF in CI and the PDFsharp step cover that. If Skia's SVG
  proves unusable, a minimal own SVG writer is the fallback.
- **Skia in WebAssembly** adds several MB to the Studio's download, and it needs the `wasm-tools`
  workload to build.
- **Linux hosts and containers need fontconfig and fonts** for Skia's default font manager. `druckwerk
  doctor` checks for them.
- **Thermal barcodes and DPI.** At 203 dpi one dot is 0.125 mm. A 0.33 mm EAN module isn't a whole
  number of dots, and rounding per bar instead of per module skews the bar widths. That's why
  snapping is ours, with dedicated tests, and why `ActualSize` is the default scaling.
- **Windows thermal printer drivers** often scale or add margins on their own. Raw is the default for
  printers that have a profile; driver mode is for everything else.
- **CUPS raw queues are deprecated**, and CUPS 3 moves to IPP Everywhere and "printer applications"
  (PAPPL) instead of classic queues. Device files, usbfs and direct network transports keep thermal
  printers working on Linux in the long run, and the connector's IPP endpoint can act as a printer
  application.
- **Bluetooth** differs most between Windows and Linux, and many cheap BLE label printers use
  undocumented protocols. Support them per profile, only with a device to test on.
- **USB without a queue on Windows** needs the WinUSB driver bound to the device, which replaces the
  vendor's driver. Document it, ship an INF, and prefer USBPRINT or the spooler where they work.
- **Browser to connector:** an HTTPS Studio can't call an `http://` connector (mixed content), and
  Chromium asks for permission before pages reach local-network addresses (Local Network Access). TLS
  is required anyway, and relay mode and connector-served Studio copies cover the rest.
- **ISO symbol artwork** can't ship until ISO's terms are clear ([[mdr-labelling]]).
- **German fiscal law moves**: VAT rates, AEAO updates, DFKA format versions, retention periods. The
  German profile names the legal version it implements, and rates and periods are configuration.
- **Tax retention versus DSGVO erasure**: receipts must be kept, while people can ask for deletion.
  The connector documents which rule wins for which data and keeps personal data out of fiscal records
  where it can.
- **A large surface for one maintainer.** The phases are cut so that each one is worth releasing even
  if the next never happens.

## Open questions for the owner

1. **Package IDs.** Publish as `CINE.Druckwerk.*` (recommended: one name for package, assembly and
   namespace, and nuget.org can reserve the `CINE.` prefix; needs an exception to
   [[0004-cine-root-namespace]], whose unprefixed rule was about churn in existing apps) or as
   `Druckwerk.*` with `CINE.Druckwerk.*` namespaces?
2. **Test hardware.** Which printers do you have, or can borrow, to test with? Every transport and
   language needs at least one real device before it counts as supported.
3. **TSE.** Only print what the POS hands us (recommended), or also talk to TSEs? Candidates are cloud
   TSEs such as fiskaly, hardware TSEs from Swissbit, and Epson printers with a built-in TSE. That
   last kind is common in Germany, and there the printer *is* the TSE.
4. **E-invoicing.** German B2B e-invoicing is being phased in (receiving since 2025, issuing from
   2027/2028). Should ZUGFeRD/XRechnung (PDF/A-3 plus XML) be on the roadmap? The PDF/A-3 work makes
   ZUGFeRD cheap.

Related: [[launchheim]], [[grocerytracker]], [[app-site]], [[mdr-labelling]], [[fiscal-receipts]],
[[0003-shared-domain-analytics]], [[0004-cine-root-namespace]],
[[0007-per-app-releases-and-prebuilt-images]], [[0018-apps-get-their-own-repo-at-1-0]],
[[0019-druckwerk-renders-with-skiasharp]]
