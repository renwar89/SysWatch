[CmdletBinding()]
param(
  [string]$ProjectPath,
  [string]$Configuration,
  [string]$Platform,
  [string]$Runtime,
  [string]$ExePath
)

. "$PSScriptRoot\common.ps1"
$config = Get-PidConfig

$project = Resolve-ProjectPath -ProjectPath $ProjectPath -Config $config
if (-not $Configuration) { $Configuration = $config.GuiConfiguration }
if (-not $Platform) { $Platform = $config.GuiPlatform }
if (-not $Runtime) { $Runtime = $config.GuiRuntime }

$exe = Resolve-ExePath -ProjectDir (Split-Path -Parent $project) -Config $config -Runtime $Runtime -Configuration $Configuration -Platform $Platform -ExePath $ExePath

& $exe
