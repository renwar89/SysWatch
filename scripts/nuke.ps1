[CmdletBinding()]
param(
  [string]$ProjectPath,
  [string]$Runtime
)

. "$PSScriptRoot\common.ps1"
$config = Get-PidConfig

$root = Get-RepoRoot
$guiRoot = Join-Path $root 'src\gui'
if (Test-Path $guiRoot) {
  Get-ChildItem -Path $guiRoot -Recurse -Directory -Force |
    Where-Object { $_.Name -in @('bin','obj') } |
    ForEach-Object { Remove-Item -Recurse -Force -Path $_.FullName -ErrorAction SilentlyContinue }
}

Set-Location $root

dotnet nuget locals all --clear

try {
  $project = Resolve-ProjectPath -ProjectPath $ProjectPath -Config $config
  if (-not $Runtime) { $Runtime = $config.GuiRuntime }
  dotnet restore $project -r $Runtime
} catch {
  Write-Warning $_.Exception.Message
  Write-Warning 'Skipping restore; update pid.settings.json when the GUI project exists.'
}
