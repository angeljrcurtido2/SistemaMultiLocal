# ─────────────────────────────────────────────────────────────────
#  publicar_actualizacion.ps1
#  Uso: .\publicar_actualizacion.ps1 -Version "2.1.0" -Notas "..." -Destino "\\SERVIDOR\ElectroMarUpdates"
# ─────────────────────────────────────────────────────────────────
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Notas,
    [string] $Destino = "\\SERVIDOR-PC\ElectroMarUpdates"   # <-- cambiar por tu ruta de red real
)

$ErrorActionPreference = "Stop"
$raiz    = $PSScriptRoot
$publish = Join-Path $raiz "publish_update"

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  ElectroMar — Publicar actualización v$Version" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Actualizar la versión en appsettings.json del proyecto
$settingsPath = Join-Path $raiz "CrediSoft.UI\appsettings.json"
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$settings.App.Version = $Version
$settings | ConvertTo-Json -Depth 5 | Set-Content $settingsPath -Encoding utf8
Write-Host "[1/4] Version actualizada en appsettings.json: $Version" -ForegroundColor Green

# 2. Compilar y publicar
Write-Host "[2/4] Compilando..." -ForegroundColor Yellow
dotnet publish CrediSoft.UI -c Release -r win-x64 --self-contained false -o $publish
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR en compilacion" -ForegroundColor Red; exit 1 }
Write-Host "      Compilacion exitosa -> $publish" -ForegroundColor Green

# 3. Crear version.json en la carpeta de publish
$fecha      = Get-Date -Format "dd/MM/yyyy"
$exePath    = Join-Path $Destino "ElectroMar.exe"
$versionObj = [ordered]@{
    version  = $Version
    notas    = $Notas
    exe_path = $exePath
    fecha    = $fecha
}
$versionJson = $versionObj | ConvertTo-Json -Depth 2
$versionJson | Set-Content (Join-Path $publish "version.json") -Encoding utf8
Write-Host "[3/4] version.json generado" -ForegroundColor Green

# 4. Copiar al servidor
if (Test-Path $Destino) {
    Write-Host "[4/4] Copiando archivos al servidor: $Destino" -ForegroundColor Yellow

    $archivos = @("ElectroMar.exe", "version.json")
    # DLLs opcionales (solo si existen en publish)
    foreach ($dll in @("CrediSoft.Core.dll","CrediSoft.Data.dll","CrediSoft.Core.pdb","CrediSoft.Data.pdb")) {
        $p = Join-Path $publish $dll
        if (Test-Path $p) { $archivos += $dll }
    }

    foreach ($arch in $archivos) {
        $origen  = Join-Path $publish $arch
        $destino = Join-Path $Destino $arch
        if (Test-Path $origen) {
            Copy-Item $origen $destino -Force
            Write-Host "      Copiado: $arch" -ForegroundColor Gray
        }
    }
    Write-Host ""
    Write-Host "ACTUALIZACIÓN PUBLICADA EXITOSAMENTE" -ForegroundColor Green
    Write-Host "  Version : $Version"
    Write-Host "  Servidor: $Destino"
    Write-Host "  Fecha   : $fecha"
    Write-Host ""
    Write-Host "Los usuarios verán el aviso al abrir ElectroMar." -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "[4/4] ADVERTENCIA: La carpeta del servidor no existe: $Destino" -ForegroundColor Yellow
    Write-Host "      Los archivos compilados quedaron en: $publish"
    Write-Host "      Copialos manualmente cuando el servidor esté disponible."
}
