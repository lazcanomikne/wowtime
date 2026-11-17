<#
.SYNOPSIS
  Corre todas las pruebas: reglas de negocio y API (xUnit) y recorridos de punta a punta en navegador (Playwright).
  Usa la base de pruebas (ConnectionStrings__Pruebas del .env), que se borra y recarga; no toca los datos de desarrollo.
.PARAMETER SoloBack
  Corre solo las pruebas del back.
#>
param([switch]$SoloBack)
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot

Write-Host "== Pruebas del back (reglas de negocio y API) ==" -ForegroundColor Cyan
Push-Location (Join-Path $raiz 'backend')
dotnet test --nologo
$back = $LASTEXITCODE
Pop-Location
if ($back -ne 0) { Write-Host "Fallaron pruebas del back" -ForegroundColor Red; exit $back }
if ($SoloBack) { exit 0 }

Write-Host "== Pruebas de punta a punta (Playwright) ==" -ForegroundColor Cyan
Push-Location (Join-Path $raiz 'frontend')
if (-not (Test-Path 'node_modules')) { npm ci }
npx playwright install chromium | Out-Null
npx playwright test
$e2e = $LASTEXITCODE
Pop-Location
if ($e2e -ne 0) { Write-Host "Fallaron pruebas de punta a punta" -ForegroundColor Red; exit $e2e }
Write-Host "Todas las pruebas pasaron." -ForegroundColor Green
