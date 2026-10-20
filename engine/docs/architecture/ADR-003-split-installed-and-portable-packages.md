# ADR-003: Split installed and portable distribution packages

**Status:** Accepted
**Date:** 2026-08-25
**Decider:** Steve the Killer

## Context

KillerPDF 1.8 moves the Windows application to .NET 10. The current public executable contains the complete self-contained Windows Desktop runtime so it can install or run portably on a clean Windows computer without prerequisites.

The self-contained payload is approximately 161 MB before compression and produces an approximately 69 MB executable. A framework-dependent build of the same application contains 21 files, occupies approximately 23 MB before compression, and compresses to approximately 10 MB. Most of the difference is the private .NET and WPF runtime rather than KillerPDF application code.

One package cannot simultaneously provide the smallest routine download and complete offline portability. Unsupported WPF trimming reduced the compressed payload, but it introduces reflection and XAML compatibility risks that are not acceptable for the primary release package.

## Decision

Publish two official Windows packages:

1. **KillerPDF Installer** is the default download. It contains the framework-dependent application, detects the required .NET 10 Windows Desktop Runtime, and guides the user through installing that prerequisite when it is absent.
2. **KillerPDF Portable** is the offline, self-contained package. It includes the required runtime and continues to work without installation, administrator rights, network access, or a separately installed .NET runtime.

Both packages are built from the same source revision, carry the same KillerPDF version, install or launch the same application features, and pass the same application and engine test gates. The package type changes deployment only, not the document engine or user feature set.

The website and package managers should present the installer as the normal choice. Release pages should retain the portable package and label it clearly as the larger offline option.

## Options considered

### Continue shipping one self-contained executable

This preserves the simplest release matrix and guarantees zero prerequisites, but every user downloads the private runtime even when the machine already has it. The approximately 69 MB download is disproportionate to the application payload.

### Replace the current package with a framework-dependent build

This produces the smallest download, but removes the no-install and offline guarantees that portable users, technicians, and restricted environments depend on.

### Trim the self-contained WPF application

Experimental partial trimming produced a compressed payload of approximately 48 MB and passed a basic launch check. The .NET SDK does not support trimming WPF applications because XAML, reflection, resources, and dynamically discovered types can be removed incorrectly. The reduction does not justify an unbounded release risk.

### Publish installed and portable packages

This gives most users an approximately 10 MB application download while retaining a complete offline artifact for users who need it. It adds packaging and release-matrix work but does not require unsupported runtime behavior. This is the selected option.

## Consequences

- Routine downloads become substantially smaller when the required runtime is already present.
- A clean computer may need to download the Microsoft .NET 10 Windows Desktop Runtime before the installed build can launch.
- Offline and restricted environments retain a self-contained portable choice.
- Release automation, signing, checksums, documentation, and validation must cover two artifacts.
- Bug reports must identify the package type when the failure concerns installation, startup, or runtime discovery.
- Winget and Chocolatey should install or declare the desktop runtime for the normal installed package.
- Future compression improvements may reduce the portable package without changing this decision.
- Unsupported WPF trimming remains excluded from release builds unless Microsoft supports it or exhaustive evidence justifies a new decision.

## Implementation requirements

1. Keep the existing self-contained portable build and its verified payload manifest.
2. Add a framework-dependent installed build with deterministic file and integrity validation.
3. Detect the .NET 10 Windows Desktop Runtime before launching the installed application.
4. Provide a clear prerequisite path when the runtime is missing.
5. Give the two artifacts unambiguous filenames and release descriptions.
6. Run build, unit, launch, installation, upgrade, uninstall, and clean-machine tests for both packages.
7. Publish both artifacts from the same commit and version.
