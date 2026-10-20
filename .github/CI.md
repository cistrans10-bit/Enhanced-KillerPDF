# Pull request CI

The local workflow `workflows/pr-build-test.yml` restores and builds the application and runs the
existing regression suites on GitHub-hosted Windows 2025. Publication is separately authorized;
these local definitions have not run on GitHub. Logs and test results are retained for seven days.
Official checkout, setup-dotnet, and upload-artifact actions are pinned to immutable commit hashes.

It uses only `pull_request`, `contents: read`, and checkout with persisted credentials disabled.
There is no publishing, deployment, signing, secret input, or privileged self-hosted runner.
Interactive desktop testing is separate; passing these jobs does not certify a user's full window,
keyboard, screen reader, or hardware.

The application and engine xUnit projects each have a separate job. The isolated FlaUI project
is compiled and runs its windowless synthetic PDF parse/render/save/reopen assertion.
Its FlaUI desktop tests are not run. The SDK follows global.json.
See ../tests/KillerPDF.UiSmoke/README.md.
