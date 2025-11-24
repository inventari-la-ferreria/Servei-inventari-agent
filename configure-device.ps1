# configure-device.ps1
# Script para configurar manualmente el dispositivo

# Requerir ejecución como Administrador
if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script debe ejecutarse como Administrador."
    Read-Host "Presiona Enter para salir"
    Exit
}

$INSTALL_PATH = "C:\Program Files\InventariAgent"
$exePath = Join-Path $INSTALL_PATH "InventariAgentSvc.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CONFIGURADOR DE DISPOSITIVO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que el ejecutable existe
if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: No se encontró el ejecutable del servicio" -ForegroundColor Red
    Write-Host "Ruta esperada: $exePath" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "¿El servicio está instalado?" -ForegroundColor Yellow
    Read-Host "Presiona Enter para salir"
    Exit 1
}

Write-Host "Ejecutable encontrado: $exePath" -ForegroundColor Green
Write-Host ""
Write-Host "Iniciando configuración del dispositivo..." -ForegroundColor Yellow
Write-Host "Se mostrará un menú para seleccionar el PC." -ForegroundColor White
Write-Host ""
Start-Sleep -Seconds 2

try {
    # Ejecutar en la misma ventana para ver la salida
    Set-Location $INSTALL_PATH
    & $exePath
    
    Write-Host ""
    Write-Host "Configuración finalizada." -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "ERROR durante la configuración:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
}

Write-Host ""
Read-Host "Presiona Enter para salir"
