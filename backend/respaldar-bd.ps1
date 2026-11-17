<#
.SYNOPSIS
  Respaldo de la base de WOW! Time con pg_dump (formato personalizado, comprimido). Conserva los últimos N días.
  Lee la cadena de conexión de backend\WowTime.Api\.env (o de -Env).
.PARAMETER Destino  Carpeta de respaldos. Por omisión C:\Desarrollo\wowtime-data\respaldos
.PARAMETER Dias     Días a conservar. Por omisión 14
.EXAMPLE
  .\respaldar-bd.ps1
  Restaurar: pg_restore -h localhost -p 5433 -U wowtime -d wowtime --clean --if-exists <archivo.dump>
#>
param([string]$Destino = 'C:\Desarrollo\wowtime-data\respaldos', [int]$Dias = 14, [string]$Env = (Join-Path $PSScriptRoot 'WowTime.Api\.env'))
$ErrorActionPreference = 'Stop'
$linea = Get-Content $Env | Where-Object { $_ -like 'ConnectionStrings__Default=*' } | Select-Object -First 1
if (-not $linea) { throw "No se encontró ConnectionStrings__Default en $Env" }
$cs = @{}
foreach ($p in ($linea -replace '^ConnectionStrings__Default=', '').Split(';')) { $kv = $p.Split('=', 2); if ($kv.Count -eq 2) { $cs[$kv[0].Trim().ToLower()] = $kv[1].Trim() } }
$pgdump = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\pg_dump.exe' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $pgdump) { throw 'No se encontró pg_dump.exe' }
New-Item -ItemType Directory -Force $Destino | Out-Null
$archivo = Join-Path $Destino ("wowtime-{0:yyyyMMdd-HHmm}.dump" -f (Get-Date))
$env:PGPASSWORD = $cs['password']
& $pgdump.FullName -h $cs['host'] -p $cs['port'] -U $cs['username'] -d $cs['database'] -F c -Z 6 -f $archivo
if ($LASTEXITCODE -ne 0) { throw 'Falló pg_dump' }
Remove-Item Env:PGPASSWORD
Get-ChildItem $Destino -Filter 'wowtime-*.dump' | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$Dias) } | Remove-Item
Write-Host "Respaldo creado: $archivo" -ForegroundColor Green
