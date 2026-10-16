---
tags: [resource, mdr, medical, labelling, udi, compliance]
created: 2026-10-16
updated: 2026-10-16
status: active
---
# MDR labelling: what it asks of label software

What "MDR conform" means for [[druckwerk]]. Read the regulation and standards themselves before
building against this. It's a map, not legal advice.

## The rules that matter
- **Regulation (EU) 2017/745 (MDR)** and its twin **2017/746 (IVDR)** for in-vitro diagnostics.
  Annex I Chapter III §23 lists what a label must carry. Article 27 and Annex VI Part C define
  **UDI** (Unique Device Identification).
- **UDI** = UDI-DI (the device model) + UDI-PI (production data: lot, serial, expiry, manufacturing
  date, software version). On the label it must appear twice: as a machine-readable carrier (**AIDC**,
  a barcode) and as human-readable text (**HRI**). It goes on the device or its packaging and on every
  higher packaging level.
- **Issuing entities** (Commission Implementing Decision (EU) 2019/939): **GS1** (GS1 DataMatrix,
  GS1-128 with Application Identifiers `(01)` GTIN, `(17)` expiry, `(10)` lot, `(21)` serial, `(11)`
  manufacturing date), **HIBCC** (HIBC LIC/PAS in Code 128, Code 39, DataMatrix or QR, mod-43 check
  character), **ICCBBA** (ISBT 128, for blood, tissue and cells) and **IFA** (the German PPN, carried in
  DataMatrix with the ISO/IEC 15434 envelope and ASC MH10.8.2 data identifiers).
- **EN ISO 15223-1:2021**: the label symbols (manufacturer, date of manufacture, use-by date, LOT,
  REF, SN, "MD", "UDI", sterile variants, "do not reuse", "consult instructions", ...).
  **EN ISO 20417:2021**: the information the manufacturer supplies in general.
- Labels are often multilingual (each member state decides which languages).

## What it asks of the *software*
The label software isn't a medical device itself. Manufacturers use it inside their quality
management system, so:
- **ISO 13485:2016 §4.1.6 and §7.5.6** require the manufacturer to *validate* software used in the
  QMS and in production, proportionate to risk, before first use and after every change. ISO/TR
  80002-2 is the guidance. In the US, FDA's QMSR (aligned with ISO 13485 since 2026-02-02) and 21 CFR
  Part 11 (electronic records and signatures) apply. GAMP 5 would put configured label templates in
  category 4.
- In practice, auditors ask for: controlled, versioned and approved label templates; proof that what
  gets printed matches what was approved; a record of every print (who, when, what, which lot/serial,
  which printer); access control; and reproducible behaviour per software version.
- A medical-device *software* manufacturer embedding the libraries treats them as **SOUP** (IEC 62304)
  and needs their requirements, versions, known anomalies and an SBOM.

## Consequences for Druckwerk
- The licence says "AS IS". MDR conformity is the manufacturer's job. We make it *possible and cheap*:
  features plus a validation kit, never a promise of certification.
- Deterministic output (same input + same version = byte-identical result on every OS) means a
  customer validates once instead of once per platform.
- Template lifecycle (draft → review → released → retired) with electronic signatures, immutable
  released versions, an append-only audit trail, and decoding every barcode back after rendering to
  compare it with the intended data.
- **Gotcha: the ISO 7000 / ISO 15223-1 symbol artwork is copyrighted by ISO.** Don't ship the symbol
  graphics in an MIT package until it's clear what ISO allows. Ship placeholders and slots, and let
  customers load the artwork they're licensed for.

Related: [[druckwerk]], [[fiscal-receipts]]
