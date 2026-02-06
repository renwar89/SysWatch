[CmdletBinding()]
param(
  [string]$ProjectPath,
  [string]$Configuration,
  [string]$Platform,
  [string]$Runtime,
  [Nullable[bool]]$SelfContained,
  [string]$OutFile = 'build-diag.txt'
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

$fullOut = Join-Path $root $OutFile

$msbuild = Resolve-MSBuildPath
if ($msbuild) {
  $cmd = @(
    $project,
    '/restore',
    '/t:Build',
    '/p:Configuration=' + $Configuration,
    '/p:Platform=' + $Platform,
    '/p:RuntimeIdentifier=' + $Runtime,
    '/p:WindowsAppSDKSelfContained=' + $SelfContained,
    '/v:diag'
  )
  & $msbuild @cmd *>&1 | Set-Content -Path $fullOut
} else {
  $cmd = @(
    'build',
    $project,
    '-c', $Configuration,
    '-p:Platform=' + $Platform,
    '-r', $Runtime,
    '-p:WindowsAppSDKSelfContained=' + $SelfContained,
    '-v:diag'
  )
  & dotnet @cmd *>&1 | Set-Content -Path $fullOut
}

Write-Host "Build diagnostics saved to $fullOut"
