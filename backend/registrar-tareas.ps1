<#
.SYNOPSIS
  Registra en el Programador de tareas de Windows:
   1) WOW! Time - Tareas: llama cada hora a POST /api/v1/tareas/ejecutar con una llave de API (no depende de que IIS esté despierto).
   2) WOW! Time - Respaldo: respaldo diario de la base con pg_dump (respaldar-bd.ps1).
   3) Con -InovaOS: WOW! Time - Checadas InovaOS, cada 15 minutos importa las checadas del lector (POST /api/v1/tareas/ejecutar?tarea=checadas_inovaos).
.PARAMETER UrlApi   URL base de la API, p. ej. https://api.wowtime.mx
.PARAMETER Llave    Llave de API con el permiso sistema.configurar (créala en Configuración > Llaves de API)
.PARAMETER HoraRespaldo Hora del respaldo diario (HH:mm). Por omisión 02:30
#>
param([Parameter(Mandatory)][string]$UrlApi, [Parameter(Mandatory)][string]$Llave, [string]$HoraRespaldo = '02:30', [switch]$InovaOS)
$ErrorActionPreference = 'Stop'
$carpeta = 'C:\ProgramData\WowTime'
New-Item -ItemType Directory -Force $carpeta | Out-Null

$script = @"
`$ErrorActionPreference = 'Stop'
try {
  `$r = Invoke-RestMethod -Method Post -Uri '$($UrlApi.TrimEnd('/'))/api/v1/tareas/ejecutar' -Headers @{ 'X-Api-Key' = '$Llave' } -TimeoutSec 600
  Add-Content '$carpeta\tareas.log' ("{0:u} OK {1}" -f (Get-Date), (`$r | ConvertTo-Json -Compress))
} catch { Add-Content '$carpeta\tareas.log' ("{0:u} ERROR {1}" -f (Get-Date), `$_.Exception.Message) }
"@
Set-Content (Join-Path $carpeta 'ejecutar-tareas.ps1') $script -Encoding utf8
icacls (Join-Path $carpeta 'ejecutar-tareas.ps1') /inheritance:r /grant:r "SYSTEM:F" "Administradores:F" | Out-Null

$accion = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$carpeta\ejecutar-tareas.ps1`""
$disparador = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName 'WOW! Time - Tareas' -Action $accion -Trigger $disparador -User 'SYSTEM' -RunLevel Highest -Force | Out-Null

$respaldo = Join-Path $PSScriptRoot 'respaldar-bd.ps1'
$accionR = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$respaldo`""
$dispR = New-ScheduledTaskTrigger -Daily -At $HoraRespaldo
Register-ScheduledTask -TaskName 'WOW! Time - Respaldo' -Action $accionR -Trigger $dispR -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
if ($InovaOS) {
  $scriptC = $script.Replace('/api/v1/tareas/ejecutar', '/api/v1/tareas/ejecutar?tarea=checadas_inovaos').Replace('tareas.log', 'checadas.log')
  Set-Content (Join-Path $carpeta 'importar-checadas.ps1') $scriptC -Encoding utf8
  icacls (Join-Path $carpeta 'importar-checadas.ps1') /inheritance:r /grant:r "SYSTEM:F" "Administradores:F" | Out-Null
  $accionC = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$carpeta\importar-checadas.ps1`""
  $dispC = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes 15)
  Register-ScheduledTask -TaskName 'WOW! Time - Checadas InovaOS' -Action $accionC -Trigger $dispC -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
  Write-Host 'Tarea registrada: "WOW! Time - Checadas InovaOS" (cada 15 minutos).' -ForegroundColor Green
}
Write-Host 'Tareas registradas: "WOW! Time - Tareas" (cada hora) y "WOW! Time - Respaldo" (diario).' -ForegroundColor Green
