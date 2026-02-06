[CmdletBinding()]
param(
  [string]$ProjectPath,
  [string]$Configuration,
  [string]$Platform,
  [string]$Runtime,
  [Nullable[bool]]$SelfContained
)

. "$PSScriptRoot\common.ps1"
$config = Get-PidConfig

$project = Resolve-ProjectPath -ProjectPath $ProjectPath -Config $config
if (-not $Configuration) { $Configuration = $config.GuiConfiguration }
if (-not $Platform) { $Platform = $config.GuiPlatform }
if (-not $Runtime) { $Runtime = $config.GuiRuntime }
if ($null -eq $SelfContained) { $SelfContained = [bool]$config.GuiSelfContained }

$root = Get-RepoRoot
Set-Location $root

$msbuild = Resolve-MSBuildPath
if ($msbuild) {
  Write-Host "Using MSBuild: $msbuild"
  & $msbuild $project /restore /t:Build /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=$Runtime /p:WindowsAppSDKSelfContained=$SelfContained
} else {
  dotnet build $project -c $Configuration -p:Platform=$Platform -r $Runtime -p:WindowsAppSDKSelfContained=$SelfContained
}
