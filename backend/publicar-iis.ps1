<#
.SYNOPSIS
  Publica la API de WOW! Time en IIS (hosting in-process con ASP.NET Core Module V2).
  Solo crea o toca el grupo de aplicaciones y el sitio de WOW! Time; no modifica otros sitios.
.PARAMETER Destino     Carpeta física del sitio. Por omisión C:\inetpub\wowtime-api
.PARAMETER Sitio       Nombre del sitio y del grupo de aplicaciones. Por omisión WowTimeApi
.PARAMETER Puerto      Puerto HTTP del sitio (si se crea). Por omisión 7030
.PARAMETER HostName    Encabezado de host opcional para el enlace (p. ej. api.wowtime.mx)
.PARAMETER CrearSitio  Crea el grupo de aplicaciones y el sitio si no existen
.PARAMETER CopiarEnv   Copia backend\WowTime.Api\.env junto a la aplicación publicada
.EXAMPLE
  .\publicar-iis.ps1 -CrearSitio -CopiarEnv
#>
param(
  [string]$Destino = 'C:\inetpub\wowtime-api',
  [string]$Sitio = 'WowTimeApi',
  [int]$Puerto = 7030,
  [string]$HostName = '',
  [switch]$CrearSitio,
  [switch]$CopiarEnv
)
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
$proyecto = Join-Path $PSScriptRoot 'WowTime.Api'

if (-not (Test-Path 'C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll')) {
  throw 'Falta el ASP.NET Core Hosting Bundle (.NET 10). Instálalo antes de publicar.'
}

# Detener el sitio para liberar archivos
if (Test-Path "IIS:\AppPools\$Sitio") { if ((Get-WebAppPoolState $Sitio).Value -ne 'Stopped') { Stop-WebAppPool $Sitio; Start-Sleep 3 } }

dotnet publish $proyecto -c Release -o $Destino --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish' }

if ($CopiarEnv) {
  $env = Join-Path $proyecto '.env'
  if (-not (Test-Path $env)) { throw "No existe $env" }
  Copy-Item $env (Join-Path $Destino '.env') -Force
}
if (-not (Test-Path (Join-Path $Destino '.env'))) { Write-Warning "No hay .env en $Destino. La API no arrancará sin ConnectionStrings__Default y Jwt__Clave (o configúralos como variables de entorno del sitio)." }

if ($CrearSitio) {
  if (-not (Test-Path "IIS:\AppPools\$Sitio")) {
    New-WebAppPool $Sitio | Out-Null
    Set-ItemProperty "IIS:\AppPools\$Sitio" managedRuntimeVersion ''
    Set-ItemProperty "IIS:\AppPools\$Sitio" startMode 'AlwaysRunning'
    Set-ItemProperty "IIS:\AppPools\$Sitio" processModel.idleTimeout ([TimeSpan]::Zero)
    Set-ItemProperty "IIS:\AppPools\$Sitio" recycling.periodicRestart.time ([TimeSpan]::Zero)
  }
  if (-not (Test-Path "IIS:\Sites\$Sitio")) {
    New-Website -Name $Sitio -PhysicalPath $Destino -ApplicationPool $Sitio -Port $Puerto -HostHeader $HostName | Out-Null
    Set-ItemProperty "IIS:\Sites\$Sitio" applicationDefaults.preloadEnabled $true
  }
}

# Permisos del grupo de aplicaciones sobre la carpeta de archivos (fuera de la carpeta pública)
$ruta = (Get-Content (Join-Path $Destino '.env') -ErrorAction SilentlyContinue | Where-Object { $_ -like 'Archivos__Ruta=*' }) -replace '^Archivos__Ruta=', ''
if ($ruta) {
  New-Item -ItemType Directory -Force $ruta | Out-Null
  icacls $ruta /grant "IIS AppPool\${Sitio}:(OI)(CI)M" /T /Q | Out-Null
}
icacls $Destino /grant "IIS AppPool\${Sitio}:(OI)(CI)RX" /T /Q | Out-Null

Start-WebAppPool $Sitio -ErrorAction SilentlyContinue
Start-Website $Sitio -ErrorAction SilentlyContinue
Write-Host "Publicado en $Destino. Prueba: http://localhost:$Puerto/api/v1/salud" -ForegroundColor Green
