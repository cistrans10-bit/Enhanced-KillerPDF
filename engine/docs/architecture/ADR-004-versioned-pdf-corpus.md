# ADR-004: Maintain a versioned, provenance-checked PDF corpus

**Status:** Accepted
**Date:** 2026-08-31
**Decider:** Steve the Killer

## Context

PDF software encounters far more variation than a focused unit-test suite can represent. Real files combine damaged structures, unusual fonts, forms, signatures, annotations, layers, color profiles, large pages, old features, and valid but uncommon feature combinations. A change can pass its direct test while breaking a document family that was never represented in that test.

KillerPDF needs evidence that a release can open and save a broad set of PDFs safely, as well as a way to tell whether a result changed because of the application, the input set, or the test environment. A file that is private, unlicensed for redistribution, or deliberately malformed still has value for local testing, but cannot be treated as a public release asset.

The KillerPDF Corpus exists to turn that work into a repeatable release gate. It provides common inputs for KillerPDF and other PDF tools, keeps the provenance of each file visible, and records baselines that later versions can compare without relying on memory or ad hoc spot checks.

## Decision

Maintain The KillerPDF Corpus as a separately versioned, provenance-checked test collection and use it as a release gate for KillerPDF and KillerPdf.Engine changes that affect opening, saving, editing, importing, or document preservation.

The corpus has three deliberate lanes:

1. Public regression and standards collections are redistributed as versioned release assets so other people can reproduce the public portion of a result.
2. Restricted stress and color overlays are tested locally from their official sources when their terms do not allow republication. Their source records, hashes, and results remain public even though the PDFs do not ship in corpus archives.
3. Deliberately damaged fuzz inputs remain separate from ordinary files and are evaluated primarily for safe rejection, application crashes, and timeouts.

Every corpus version records each input's relative path, byte count, source, and SHA-256 digest. Sources are pinned to an upstream revision or dated release, files are deduplicated by digest, and exclusions are recorded rather than silently discarded. The corpus accepts only files with useful coverage and clear provenance and redistribution terms.

A benchmark warms each collection, runs the selected collection five measured times, keeps output outside the input tree, and records detailed and summary CSV results. Published baselines identify the exact executable hash, corpus version, collection selection, outcome counts, elapsed times, and test environment. Outcome categories distinguish a successful save, an intentional skip or refusal, and an operation that entered the save path but failed. The damaged-file gate also records crashes and timeouts separately.

## Options considered

### Rely on unit and feature tests only

Unit tests are fast and precise, but their fixtures necessarily model known cases. They cannot demonstrate that a change preserves behavior across the wide range of real, historical, malformed, and standards-oriented PDFs that users supply.

### Keep an unversioned maintainer folder for manual spot checks

This can find problems during development, but it does not provide stable inputs, provenance, public reproduction, comparable totals, or a dependable release record. Its results cannot distinguish a code change from a changed set of files.

### Publish every useful test file

This would make the largest visible collection, but it would violate the redistribution terms of some valuable official test suites and issue-attachment archives. It also mixes ordinary compatibility work with intentionally hostile inputs.

### Maintain a versioned public corpus with documented local-only overlays

This gives users and contributors a reproducible public baseline while retaining valuable restricted coverage on the maintainer workstation. It makes licensing, provenance, and expected failure boundaries explicit. This is the selected option.

## Consequences

- A preservation, editing, or import change is not release-ready until its focused tests and the relevant corpus gates have been run.
- A corpus baseline measures safe batch open-and-save behavior. It does not prove rendering fidelity, text extraction quality, layout reconstruction, semantic recovery, or full standards conformance.
- Results must compare the same corpus version, collection selection, executable, and outcome definitions. A changed corpus is a new baseline, not evidence by itself of an application regression.
- Public release assets remain reproducible, while restricted sources remain external downloads with manifest and baseline coverage rather than copied into the release.
- Maintaining source records, manifests, hashes, runners, baselines, and storage requires ongoing work, but it prevents regressions from being hidden by incomplete fixtures or changing input sets.
- Failure totals require investigation. An intentional skip is not an unexpected save failure, and a deterministic save failure is not an application crash.

## Implementation requirements

1. Keep the corpus repository's public files, manifests, source policy, license records, packager, and benchmark runner under version control.
2. Require a pinned source, clear licensing or redistribution status, file hashes, deduplication, and documented coverage before accepting a new collection.
3. Keep restricted overlays and malformed fuzz inputs out of public release archives when their terms or purpose require it.
4. Run a warmup and five measured passes for published comparison baselines, then verify that each measured pass has identical outcome totals.
5. Record the tool version and executable SHA-256, corpus version, selected collections, outcome totals, elapsed times, and test environment with every published baseline.
6. Preserve previous baselines. Correct a bad public corpus release by publishing a new immutable version instead of replacing its assets.
7. Add a focused regression test when a corpus result exposes a reproducible bug, so the smallest useful reproduction remains fast to run during everyday development.
