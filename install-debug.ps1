# install-debug.ps1
# Versión de debug del instalador que no se cierra automáticamente

# Requerir ejecución como Administrador
if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script debe ejecutarse como Administrador."
    Write-Host "Haz clic derecho en PowerShell y selecciona 'Ejecutar como administrador'"
    Read-Host "Presiona Enter para salir"
    Exit
}

$ErrorActionPreference = "Continue"  # Continuar aunque haya errores

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  INSTALADOR DEBUG - INVENTARI AGENT" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

try {
    Write-Host "[1/5] Descargando instalador principal..." -ForegroundColor Yellow
    $installerUrl = "https://raw.githubusercontent.com/inventari-la-ferreria/Servei-inventari-agent/main/install-from-github.ps1"
    $installerPath = "$env:TEMP\install-from-github.ps1"
    
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    Write-Host "✓ Instalador descargado" -ForegroundColor Green
    
    Write-Host "`n[2/5] Ejecutando instalador..." -ForegroundColor Yellow
    Write-Host "----------------------------------------`n" -ForegroundColor Gray
    
    # Ejecutar el instalador
    & $installerPath
    
    Write-Host "`n----------------------------------------" -ForegroundColor Gray
    Write-Host "✓ Instalación completada" -ForegroundColor Green
}
catch {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "  ERROR DURANTE LA INSTALACIÓN" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Detalles técnicos:" -ForegroundColor Yellow
    Write-Host $_.Exception.ToString() -ForegroundColor Gray
    Write-Host ""
    Write-Host "Stack Trace:" -ForegroundColor Yellow
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Diagnóstico del sistema:" -ForegroundColor Cyan
Write-Host ""

# Verificar servicio
$service = Get-Service -Name "InventariAgent" -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Servicio encontrado: $($service.Status)" -ForegroundColor Green
} else {
    Write-Host "Servicio NO encontrado" -ForegroundColor Yellow
}

# Verificar archivos
if (Test-Path "C:\Program Files\InventariAgent\InventariAgentSvc.exe") {
    Write-Host "Ejecutable encontrado: C:\Program Files\InventariAgent\InventariAgentSvc.exe" -ForegroundColor Green
} else {
    Write-Host "Ejecutable NO encontrado" -ForegroundColor Yellow
}

# Verificar credenciales
if (Test-Path "C:\ProgramData\InventariAgent\firebase-credentials.json") {
    Write-Host "Credenciales encontradas: C:\ProgramData\InventariAgent\firebase-credentials.json" -ForegroundColor Green
} else {
    Write-Host "Credenciales NO encontradas" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Read-Host "Presiona Enter para cerrar"
