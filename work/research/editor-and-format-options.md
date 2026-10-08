# Editor and format technology decision

Research date: 2026-10-04.

## Decision

- **Desktop UI:** Avalonia 12.1.3 with .NET 10. Avalonia's current platform matrix lists Windows 10 22H2 x64 as Tier 2 (best effort), Windows 11 22H2 as Tier 2, and Windows 11 24H2 as Tier 1. .NET 10 is the current LTS SDK line. We target Windows 10 22H2 and Windows 11 as approved, while retaining explicit Windows-device verification as a release requirement.
- **Text editing foundation:** AvaloniaEdit 12.0.0, MIT, hosted by the AvaloniaUI organization. It provides an editable and extensible Avalonia text surface with word wrap, undo/editing primitives, and line virtualization. It is used for the initial working screenplay surface. It does not itself provide screenplay element behavior or production pagination; those remain app-owned editor/model work and need a focused pagination fidelity spike before the editor phase is considered complete.
- **PDF:** PDFsharp 6.2.4 is the initial candidate. Its maintainers publish it under MIT and say it can be used in commercial products without royalties. Before release, verify redistribution notices and compare the PDF layout capabilities with our title-page, pagination, and five-format requirements. Do not add it until the PDF prototype confirms fit.
- **Fountain import:** implement a small app-owned parser against the public Fountain syntax reference. Fountain is an open, plain-text format. Its syntax has several explicit/forced forms and screenplay-specific whitespace behavior, so preserve source text and report constructs that the current importer cannot map. Do not claim complete import coverage until representative fixtures have been manually reviewed.
- **View model support:** CommunityToolkit.Mvvm 8.4.2, MIT; used by the Avalonia MVVM template.

## Evidence and limitations

- The generated Avalonia MVVM app targets `net10.0` and uses Avalonia 12.1.3. The initial solution, including AvaloniaEdit 12.0.0, builds on Linux with the .NET 10.0.401 SDK.
- Avalonia's documented Windows support tiers vary by OS release. Windows 10 22H2 is supported on a best-effort basis, not the fully tested Tier 1 tier. Keep compatibility checks in the release matrix and test the published build on real Windows 10 22H2 and Windows 11 systems.
- The current screenplay page proves the packages can coexist and provides an editable prototype. It is not yet a proof of stable pagination, editor-to-PDF fidelity, page locks, or screenplay keyboard behavior.
- Library licenses are a dependency-screening result, not legal advice. Include license/notice review in release preparation.

## Sources

- [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)
- [.NET downloads and LTS recommendation](https://dotnet.microsoft.com/en-us/download)
- [AvaloniaEdit source, features, and MIT license](https://github.com/AvaloniaUI/AvaloniaEdit)
- [PDFsharp license FAQ](https://docs.pdfsharp.net/General/License/License-FAQ.html)
- [Fountain syntax reference](https://fountain.io/syntax/)
