# KillerPDF 1.8.90 corpus benchmark

Test date: 2026-10-13

KillerPDF 1.8.90 reproduced every 1.8.80 file outcome and diagnostic detail across all three normal collections. The build received one warmup and five measured passes per collection, alternating with a second build in the same session. Every pass agreed on every file's path, status, diagnostic detail, and exit code. The damaged-file gate had zero crashes and zero timeouts.

## Results

| Collection | Inputs | Saved | Skipped | Save failures | 1.8.90 median seconds |
| --- | ---: | ---: | ---: | ---: | ---: |
| Public regression | 16,696 | 15,171 | 1,233 | 292 | 283.235 |
| Standards and color | 649 | 377 | 235 | 37 | 6.314 |
| Private stress | 29,599 | 25,015 | 2,957 | 1,627 | 649.38 |
| Damaged-file safety | 80 | 0 | 78 | 2 | Not timed as a batch |

The three normal collections contain 46,944 inputs, with 40,563 saved, 4,425 skipped, and 1,956 save failures per pass. Including the separate safety collection gives 47,024 inputs.

These are hostile collections, not a zero-failure workload. All normal batch processes returned exit code 1 because their logs contain save failures. The runner completed normally; this result is based on the recorded per-file results, not on treating the batch exit code as success.

## Comparison with 1.8.80

Outcomes match exactly. The 1.8.90 logs agree with the retained 1.8.80 logs on status and diagnostic detail for all 16,696 regression, 649 standards, and 29,599 stress inputs, and the 292, 37, and 1,627 existing save failures carry the same diagnostics. No previously saved file became a skip or a failure. Damaged-file results also match: 78 skips and two reported failures, with no missing logs, crashes, or timeouts.

## Same-session timing check against 1.8.80

The corpus series ran on a busy host: a stuck system process held a full CPU core and other development work was running. Its 1.8.90 medians were 19.8% (regression), 9.1% (standards), and 15.2% (stress) above the recorded 2026-10-04 1.8.80 medians. To separate the code from the host, 1.8.80 (`a3f18871`) and 1.8.90 (`860646ee`) were built from source with the same publish command and run in alternating order on a quiet host, together with the frozen 1.8.90 build used for the series. Each build received one warmup and four measured passes per collection.

| Collection | 1.8.80 median seconds | 1.8.90 median seconds | Frozen 1.8.90 median seconds | 1.8.90 change |
| --- | ---: | ---: | ---: | ---: |
| Public regression | 228.228 | 222.307 | 223.461 | 2.59% shorter |
| Standards and color | 5.999 | 5.876 | 6.176 | 2.05% shorter |

Process CPU time per regression pass was 119.4 seconds for 1.8.80 and 119.7 seconds for 1.8.90, and every pass of every build produced the same per-file log. 1.8.90 has no timing regression against 1.8.80; the higher series medians came from host load. The batch resave path (the batch runner, the engine sources, and the resave call) is unchanged between the two versions. The private stress set was not part of this check. The check's logs are at `C:\Users\steve\kp-bench-render\perf-1880-vs-1890-20261013-quiet`.

## Builds and method

- Source commit: `860646ee6336e29274a18cfb89740992a2cdec09`.
- Build: KillerPDF 1.8.90 Release, win-x64.
- Executable SHA-256: `427BBC7DB4ADC9019A694EFC13831BBDDFA0BE6A84206D042347C4072320B927`.
- Mode: batch resave of each collection with a per-file CSV log and quiet output.
- One warmup and five measured passes per collection. Odd pairs ran 1.8.90 first and even pairs ran it second. Each pass used a fresh process, and the frozen build files were hashed before each pass.
- No cache flushing. Memory was not measured. Host load was not certified idle.
- All 80 damaged inputs were tested individually with a 30-second timeout.
- Input identity: file counts and byte totals match the recorded corpus identity for all four collections. After the series every input was rehashed against the published manifests: 16,696 of 16,696 regression entries, 552 of 552 standards entries (97 local standards files are not in the manifest), and 80 of 80 damaged-file entries matched. The private stress set has no published manifest and was verified by file count and byte total.
- Full per-file logs remain outside the application repository at `C:\Users\steve\kp-bench-render\release-1.8.90-vs-1.9-alternating-20261013`.

The committed [run data](benchmark-runs.csv) and [summary](benchmark-summary.csv) preserve the release-level measurements. The previous published corpus record is [KillerPDF 1.8.80](../1.8.80/CORPUS.md).

This run tests batch opening and saving plus malformed-input process safety. It does not replace interactive rendering checks or a separate qpdf/veraPDF before-and-after conformance sweep.
