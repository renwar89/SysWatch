[CmdletBinding()]
param(
  [string]$ProjectPath,
  [string]$Configuration,
  [string]$Platform,
  [string]$Runtime,
  [Nullable[bool]]$SelfContained,
  [string]$ExePath
)

& "$PSScriptRoot\build.ps1" -ProjectPath $ProjectPath -Configuration $Configuration -Platform $Platform -Runtime $Runtime -SelfContained $SelfContained
if ($LASTEXITCODE -eq 0) {
  & "$PSScriptRoot\run.ps1" -ProjectPath $ProjectPath -Configuration $Configuration -Platform $Platform -Runtime $Runtime -ExePath $ExePath
}
