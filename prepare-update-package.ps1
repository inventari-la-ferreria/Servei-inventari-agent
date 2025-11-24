# prepare-update-package.ps1
# Script para preparar un paquete de actualización remota

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\updates"
)

# Requerir ejecución como Administrador
if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script debe ejecutarse como Administrador."
    Start-Process powershell -Verb RunAs -ArgumentList "-File `"$PSCommandPath`" -Version $Version -Configuration $Configuration -OutputDir $OutputDir"
    Exit
}

$ErrorActionPreference = "Stop"

Write-Host "=== PREPARANDO PAQUETE DE ACTUALIZACIÓN ===" -ForegroundColor Cyan
Write-Host "Versión: $Version" -ForegroundColor Green
Write-Host "Configuración: $Configuration" -ForegroundColor Green

# Rutas
$projectPath = ".\InventariAgentSvc\InventariAgentSvc\InventariAgentSvc.csproj"
$publishPath = ".\InventariAgentSvc\InventariAgentSvc\bin\$Configuration\net8.0\publish"
$updatePackagePath = Join-Path $OutputDir "InventariAgent_v$Version.zip"

# Crear directorio de salida
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 1. Compilar el proyecto
Write-Host "`nCompilando proyecto..." -ForegroundColor Yellow
dotnet publish $projectPath -c $Configuration -o $publishPath --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Error "Error durante la compilación"
    Exit 1
}

Write-Host "Compilación exitosa" -ForegroundColor Green

# 2. Crear archivo ZIP
Write-Host "`nCreando paquete ZIP..." -ForegroundColor Yellow

if (Test-Path $updatePackagePath) {
    Remove-Item $updatePackagePath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $updatePackagePath -CompressionLevel Optimal

Write-Host "Paquete creado: $updatePackagePath" -ForegroundColor Green

# 3. Mostrar información del paquete
$zipInfo = Get-Item $updatePackagePath
Write-Host "`n=== INFORMACIÓN DEL PAQUETE ===" -ForegroundColor Cyan
Write-Host "Archivo: $($zipInfo.Name)" -ForegroundColor White
Write-Host "Tamaño: $([math]::Round($zipInfo.Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host "Ruta completa: $($zipInfo.FullName)" -ForegroundColor White

# 4. Generar hash SHA256 para verificación
Write-Host "`nGenerando hash SHA256..." -ForegroundColor Yellow
$hash = Get-FileHash -Path $updatePackagePath -Algorithm SHA256
Write-Host "SHA256: $($hash.Hash)" -ForegroundColor Green

# 5. Instrucciones
Write-Host "`n=== SIGUIENTES PASOS ===" -ForegroundColor Cyan
Write-Host "1. Sube el archivo ZIP a un servidor web público (GitHub Releases, tu servidor, etc.)" -ForegroundColor White
Write-Host "2. Obtén la URL de descarga directa del archivo ZIP" -ForegroundColor White
Write-Host "3. En Firebase Console, actualiza el documento del dispositivo:" -ForegroundColor White
Write-Host "`n   Firestore -> pcs -> [ID_DISPOSITIVO]" -ForegroundColor Gray
Write-Host "`n   Agrega/actualiza el campo:" -ForegroundColor Gray
Write-Host "`n   updateCommand: {" -ForegroundColor Yellow
Write-Host "     version: `"$Version`"," -ForegroundColor Yellow
Write-Host "     downloadUrl: `"https://tu-servidor.com/path/InventariAgent_v$Version.zip`"" -ForegroundColor Yellow
Write-Host "   }" -ForegroundColor Yellow
Write-Host "`n4. El servicio detectará la actualización en el siguiente ciclo (max 30 segundos)" -ForegroundColor White
Write-Host "5. La actualización se descargará, instalará y el servicio se reiniciará automáticamente" -ForegroundColor White

Write-Host "`n=== OPCIONES DE HOSTING GRATUITAS ===" -ForegroundColor Cyan
Write-Host "• GitHub Releases: https://github.com/tu-repo/releases" -ForegroundColor White
Write-Host "• Cloudflare R2 (10GB gratis): https://www.cloudflare.com/products/r2/" -ForegroundColor White
Write-Host "• Tu propio servidor HTTP/HTTPS" -ForegroundColor White

Write-Host "`nPaquete listo para distribución!" -ForegroundColor Green

# Pausar para ver el resultado
Read-Host "`nPresiona Enter para salir"
