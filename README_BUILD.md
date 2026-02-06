# PID (WinUI 3) - build + run

## VS Code
Use the tasks in .vscode/tasks.json:
- PID: Clean GUI
- PID: Build GUI
- PID: Run GUI
- PID: Clean Build Run GUI
- PID: Nuke GUI
- PID: Build Diag

## Command line
```powershell
powershell -NoProfile -NonInteractive -File scripts\clean.ps1
powershell -NoProfile -NonInteractive -File scripts\build.ps1
powershell -NoProfile -NonInteractive -File scripts\run.ps1
powershell -NoProfile -NonInteractive -File scripts\clean-build-run.ps1
```

## MSBuild note (WinUI 3)
WinUI 3 builds require the PRI tooling that ships with Visual Studio / Build Tools.
The scripts automatically use MSBuild if it is installed (via vswhere).

Direct MSBuild example:
```powershell
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  .\src\gui\PidGui\PidGui.csproj /restore /t:Build /p:Configuration=Debug /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 /p:WindowsAppSDKSelfContained=true
```

## Configure
Edit pid.settings.json to point to your WinUI project:
- GuiProjectPath: relative path to the .csproj
- GuiPlatform / GuiRuntime: match your target (x64, win-x64, etc.)
- GuiSelfContained: true or false
- GuiExePath: optional explicit exe path override

## Output
WinUI 3 build output typically lands under:
<Project>\bin\<Platform>\<Configuration>\net8.0-windows10.0.19041.0\<Runtime>\*.exe

If the scripts cannot find the exe, update GuiExePath or build first.
