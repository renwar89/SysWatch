[CmdletBinding()]
param(
  [string]$ProjectPath,
  [string]$Configuration,
  [string]$Platform
)

. "$PSScriptRoot\common.ps1"
$config = Get-PidConfig

$project = Resolve-ProjectPath -ProjectPath $ProjectPath -Config $config
if (-not $Configuration) { $Configuration = $config.GuiConfiguration }
if (-not $Platform) { $Platform = $config.GuiPlatform }

$root = Get-RepoRoot
Set-Location $root

dotnet clean $project -c $Configuration -p:Platform=$Platform
