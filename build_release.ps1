# Script para construir y empaquetar la Release

$projectPath = ".\InventariAgentSvc\InventariAgentSvc\InventariAgentSvc.csproj"
$publishDir = ".\publish"
$zipName = "InventariAgent.zip"
$credsFile = ".\firebase-credentials.json"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   CONSTRUCTOR DE RELEASE INVENTARI" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Limpiar
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $zipName) { Remove-Item $zipName -Force }

# 2. Publicar (Compilar)
Write-Host "`n[1/4] Compilando proyecto..."
dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $publishDir

if (Test-Path ".\install-from-github.ps1") {
    Copy-Item ".\install-from-github.ps1" -Destination "$publishDir\install.ps1"
}

# NOTA: Ya no incluimos credenciales en el ZIP para evitar baneos.
# El script de instalación las descarga de Google Drive.

# 5. Comprimir
Write-Host "`n[4/4] Creando ZIP..."
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipName -Force

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "   RELEASE CREADA: $zipName" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Sube este archivo a GitHub Releases."
