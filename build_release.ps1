# Script para construir y empaquetar la Release
# Este script compila el proyecto y crea el ZIP listo para subir a GitHub

$projectPath = ".\InventariAgentSvc\InventariAgentSvc.csproj"
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

if ($LASTEXITCODE -ne 0) {
    Write-Error "Error en la compilación."
    exit
}

# 3. Copiar Credenciales
Write-Host "`n[2/4] Buscando credenciales..."
if (Test-Path $credsFile) {
    Copy-Item $credsFile -Destination "$publishDir\firebase-credentials.json"
    Write-Host "✓ Credenciales incluidas en el paquete (Instalación automática activada)" -ForegroundColor Green
} else {
    Write-Warning "⚠️  No se encontró '$credsFile'. El ZIP no tendrá credenciales automáticas."
}

# 4. Copiar Script de Instalación (One-Liner support)
Write-Host "`n[3/4] Copiando script de instalación..."
if (Test-Path ".\install-from-github.ps1") {
    Copy-Item ".\install-from-github.ps1" -Destination "$publishDir\install.ps1"
}

# 5. Comprimir
Write-Host "`n[4/4] Creando ZIP..."
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipName -Force

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "   ✅ RELEASE CREADA: $zipName" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Sube este archivo a GitHub Releases."
Write-Host ""
