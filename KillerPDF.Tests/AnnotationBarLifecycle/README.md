# Annotation bar lifecycle regression

Run on Windows with the .NET 10 SDK and WindowsDesktop runtime:

```powershell
& .\KillerPDF.Tests\AnnotationBarLifecycle\Run.ps1
```

This standalone test compiles the live annotation positioning methods from Shell/WindowChrome.cs verbatim into a small fixture. It avoids the application constructor and startup, PDF engine, user documents and preferences. The fixture owns one hidden popup for real WPF load and visibility events; it never shows a window. A dispatcher callback bound, Send-priority timeout, and ten-second process watchdog ensure even the old infinite retry is cleaned up using only the returned helper process handle. Evidence is retained in a unique temporary folder.

The cases assert Input and Background responsiveness with a zero-height 98SE band, coalescing, inset updates after measurement, pending-work cancellation and recovery through minimize/restore, hidden parents, detachment/reattachment and initial load, plus floating-theme placement and inset clearing. Minimize is simulated as the fixture's state while the actual production repositioning method runs; this does not claim a physical minimize or black-viewport reproduction.

The pre-fix methods fail the first case with 32 Loaded callbacks and neither lower-priority callback executed. To repeat that comparison, save the previous WindowChrome.cs to a temporary file and pass it as -SourcePath. The test fails closed if the expected source region cannot be found. -OutputDirectory selects the evidence folder.
