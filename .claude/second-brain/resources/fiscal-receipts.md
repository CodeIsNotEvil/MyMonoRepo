---
tags: [resource, fiscal, receipts, kassensichv, compliance]
created: 2026-10-16
updated: 2026-10-16
status: active
---
# Fiscal receipts: what the law asks of a receipt printer library

Fiscal receipts are in scope for [[druckwerk]] (owner, 2026-10-16). This note maps what that means.
Check the current law before building against it. It's a map, not tax advice.

## Where the line runs
Fiscal laws put their duties on the **recording system** (the POS): recording every transaction,
signing it with a certified security device and exporting the data to the tax office. A print library
isn't that system. Its job is the **receipt**: it must carry every mandatory field, show the signature
data in the mandated form, mark copies, and be archivable. Whether Druckwerk also talks to signing
devices is an open question in the plan. The default is no: the POS hands us the signed data.

## Germany (first target)
- **§146a AO + KassenSichV**: electronic POS systems need a certified **TSE** (technische
  Sicherheitseinrichtung, BSI TR-03153; hardware such as Swissbit or Epson, or a cloud TSE).
  **Belegausgabepflicht**: every transaction gets a receipt, on paper or electronically.
- **§6 KassenSichV**, mandatory receipt content: name and address of the business; date of issue plus
  the transaction's start and end time; quantity and kind of goods or services; transaction number;
  amount and tax per VAT rate (or the exemption); the serial number of the POS or of the TSE. The
  AEAO to §146a adds the TSE signature counter and signature value.
- The TSE data can be printed as text or as a **QR code** in the format the DFKA defined (it starts
  with `V0;` and holds the POS serial, process type and data, transaction number, signature counter,
  start and end time, algorithm, time format, signature and public key). Most POS systems use the QR.
- **TSE failure**: the receipt must show that the TSE was unavailable.
- **GoBD**: retention is about the POS's data, but an archived copy of the receipt (PDF/A) is a common
  requirement in customer projects.

## Austria (second)
- **RKSV**: each receipt carries a machine-readable code (QR, or a link) with a signed data string
  starting `_R1-AT…_`, plus required text fields. Failure of the signature device must be printed too.

## Others, on demand
Italy (RT, Registratore Telematico), France (NF 525), Poland, Hungary, Croatia and others have their
own rules, many tied to certified fiscal printers or online reporting. Add a country profile when a
customer needs it.

## Consequences for Druckwerk
- **Country profiles** in `Druckwerk.Receipts.Fiscal`: the mandatory fields (validated *before*
  printing; a missing field is an error, not a blank line), the QR or code payload builder, the
  wording and markers (copy or duplicate, training receipt, TSE failure).
- **Reprints** are always marked as copies, and the original rendering is reproducible from the job
  record (same deterministic renderer as [[mdr-labelling]] needs).
- **Electronic receipts** (PDF/A-3 with the receipt data embedded as JSON, or a link and QR to a
  stored copy) satisfy the Belegausgabepflicht without paper.
- The plan's audit trail and template lifecycle (from the MDR work) serve fiscal customers too.

Related: [[druckwerk]], [[mdr-labelling]]
