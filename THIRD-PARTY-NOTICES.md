# Third-party notices — v0.1.0 candidate

Project code is MIT (see LICENSE). Third-party components retain their own licenses.

| Component | Version | Source and evidence | Distribution |
| --- | --- | --- | --- |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.14 | Official NuGet package: MIT license expression, LICENSE.TXT, THIRD-PARTY-NOTICES.TXT | Included in self-contained EXE |
| Microsoft.WindowsDesktop.App.Runtime.win-x64 | 10.0.14 | Official NuGet package: MIT, LICENSE; WPF notices from matching repository commit | Included in self-contained EXE |
| PySNMP | 7.1.22 | PyPI wheel LICENSE.rst and BSD-2-Clause metadata | Test tool only, not included in EXE |
| pyasn1 | 0.6.5 | PyPI wheel LICENSE.rst and BSD-2-Clause metadata | Test tool only, not included in EXE |

Runtime package repository commit: `dotnet/dotnet@0427b9abbead3fa64c6655eb4290c537397131fa`.
WPF notices source: https://github.com/dotnet/dotnet/blob/0427b9abbead3fa64c6655eb4290c537397131fa/src/wpf/THIRD-PARTY-NOTICES.TXT

Full runtime/WPF notices and licenses are retained verbatim in `licenses/` and embedded in the EXE, accessible using the 授權聲明 button. The broad runtime notice list may cover components not present on Windows; its presence does not mean every listed component is separately shipped by this application.

There are no direct or transitive third-party PackageReference dependencies in the application projects. This does not mean there are no bundled runtime/native components. This inventory records package-level license evidence and retained upstream notices, not a completed per-native-component legal compliance opinion. Final binary-level attribution review is pending before formal release, especially for additional permissive terms in the runtime notice bundle.

Build tools (.NET SDK, GitHub Actions, Python) execute during development/CI and are not included in the application. No application fonts, icons or third-party copied snippets have been added.
