<#
.SYNOPSIS
  Levanta WOW! Time en local: API (http://localhost:5080) y front (http://localhost:5173).
.PARAMETER Semilla
  Borra la base y carga los datos de demostración antes de iniciar.
.EXAMPLE
  .\iniciar.ps1
  .\iniciar.ps1 -Semilla
#>
param([switch]$Semilla)
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$api = Join-Path $raiz 'backend\WowTime.Api'
$front = Join-Path $raiz 'frontend'

if (-not (Test-Path (Join-Path $api '.env'))) {
  Write-Host "Falta backend\WowTime.Api\.env. Copia .env.example como .env y llena la cadena de conexión y Jwt__Clave." -ForegroundColor Red
  exit 1
}
if (-not (Test-Path (Join-Path $front '.env.local'))) { Copy-Item (Join-Path $front '.env.example') (Join-Path $front '.env.local') }
if (-not (Test-Path (Join-Path $front 'node_modules'))) { Push-Location $front; npm ci; Pop-Location }

Push-Location $api
dotnet build -nologo -v q
if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
if ($Semilla) { dotnet run --no-build -- semilla --reiniciar } else { dotnet run --no-build -- migrar }
Pop-Location

Write-Host "Iniciando API en http://localhost:5080 (documentación: http://localhost:5080/api/docs)" -ForegroundColor Green
$procApi = Start-Process dotnet -ArgumentList 'run','--no-build','--launch-profile','http' -WorkingDirectory $api -PassThru -NoNewWindow
Write-Host "Iniciando front en http://localhost:5173" -ForegroundColor Green
try {
  Push-Location $front
  npm run dev
} finally {
  Pop-Location
  if ($procApi -and -not $procApi.HasExited) { Stop-Process -Id $procApi.Id -Force }
}
