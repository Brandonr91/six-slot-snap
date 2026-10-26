$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'bin\SixSlotSnap.exe'
if (-not (Test-Path -LiteralPath $executable)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
Start-Process -FilePath $executable -WindowStyle Hidden

