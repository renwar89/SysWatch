Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
  $root = Resolve-Path (Join-Path $PSScriptRoot '..')
  return $root.Path
}

function Get-PidConfig {
  $root = Get-RepoRoot
  $path = Join-Path $root 'pid.settings.json'
  if (-not (Test-Path $path)) {
    throw "Missing pid.settings.json at $path"
  }
  try {
    return (Get-Content -Path $path -Raw | ConvertFrom-Json)
  } catch {
    throw "Failed to parse pid.settings.json: $($_.Exception.Message)"
  }
}

function Resolve-ProjectPath {
  param(
    [string]$ProjectPath,
    $Config
  )
  if (-not $ProjectPath) {
    $ProjectPath = $Config.GuiProjectPath
  }
  if (-not $ProjectPath) {
    throw 'GuiProjectPath is not set. Update pid.settings.json.'
  }
  $root = Get-RepoRoot
  $full = Resolve-Path -Path (Join-Path $root $ProjectPath) -ErrorAction SilentlyContinue
  if (-not $full) {
    throw "Project path not found: $ProjectPath (expected under $root)"
  }
  return $full.Path
}

function Resolve-ExePath {
  param(
    [string]$ProjectDir,
    $Config,
    [string]$Runtime,
    [string]$Configuration,
    [string]$Platform,
    [string]$ExePath
  )

  if ($ExePath) {
    $full = Resolve-Path -Path $ExePath -ErrorAction SilentlyContinue
    if (-not $full) {
      throw "ExePath not found: $ExePath"
    }
    return $full.Path
  }

  if ($Config.GuiExePath) {
    $full = Resolve-Path -Path (Join-Path (Get-RepoRoot) $Config.GuiExePath) -ErrorAction SilentlyContinue
    if (-not $full) {
      throw "GuiExePath not found: $($Config.GuiExePath)"
    }
    return $full.Path
  }

  $binRoot = Join-Path $ProjectDir (Join-Path 'bin' (Join-Path $Platform $Configuration))
  if (-not (Test-Path $binRoot)) {
    throw "Build output not found at $binRoot. Run scripts/build.ps1 first."
  }

  $candidates = Get-ChildItem -Path $binRoot -Recurse -Filter *.exe -ErrorAction SilentlyContinue
  if ($Runtime) {
    $runtimeSegment = [IO.Path]::DirectorySeparatorChar + $Runtime + [IO.Path]::DirectorySeparatorChar
    $runtimePattern = [regex]::Escape($runtimeSegment)
    $candidates = $candidates | Where-Object { $_.FullName -match $runtimePattern }
  }

  $exe = $candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if (-not $exe) {
    throw "No exe found under $binRoot. Update GuiExePath or build the project."
  }

  return $exe.FullName
}

function Resolve-MSBuildPath {
  $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
  if (-not (Test-Path $vswhere)) {
    return $null
  }

  $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1

  if (-not $msbuild) {
    return $null
  }

  return $msbuild
}
