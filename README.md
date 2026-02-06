# SysWatch

SysWatch is a WinUI 3 desktop dashboard for real‑time process and network visibility on Windows. It hosts a Blazor server UI inside a WebView2 control and surfaces process trees, per‑process details, and live connection telemetry.

## Features
- Process overview with parent/child tree layout
- Expandable per‑process detail blocks (witr‑style output)
- Real‑time network connections page with live charts
- Filters, search, and row‑count controls
- High‑contrast dashboard theme optimized for readability

## Build Requirements (Windows)
These are required to compile the project:

- **Windows 10/11** (target framework: `net8.0-windows10.0.19041.0`)
- **.NET 8 SDK**
- **Visual Studio 2022** or **Build Tools for Visual Studio 2022**
  - Windows 10/11 SDK (AppX / PRI tooling)
  - MSBuild
- **WebView2 Runtime** (for the embedded web UI)
- **PowerShell** (for the build/run scripts)

All NuGet dependencies are restored automatically from the project file:
- Microsoft Windows App SDK
- Microsoft Windows SDK Build Tools
- MudBlazor
- CommunityToolkit.Mvvm
- System.Management
- AngleSharp, LiteDB, Newtonsoft.Json, UTF.Unknown

## Build & Run
From the repository root:

```powershell
powershell -NoProfile -NonInteractive -File scripts\clean.ps1
powershell -NoProfile -NonInteractive -File scripts\build.ps1
powershell -NoProfile -NonInteractive -File scripts\run.ps1
```

If MSBuild is installed, the scripts will use it automatically. WinUI 3 builds require the Windows SDK PRI tooling (included with Visual Studio or Build Tools).

## Notes
- The Network page uses `netstat -ano` to enumerate connections. Some processes may require elevated privileges to resolve fully.
- If the UI fails to render styles after a rebuild, fully close/reopen the app to refresh the WebView cache.

## License
MIT License (see `LICENSE`).
