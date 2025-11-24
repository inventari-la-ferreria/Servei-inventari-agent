# Script de configuración de dispositivo para InventariAgent
# Este script se ejecuta durante la instalación para seleccionar el PC correcto

param(
    [string]$InstallPath = "C:\Program Files\InventariAgent"
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Configuración de InventariAgent ===" -ForegroundColor Cyan
Write-Host ""

# Verificar que existen las credenciales de Firebase
$credPath = "C:\ProgramData\InventariAgent\firebase-credentials.json"
if (-not (Test-Path $credPath)) {
    Write-Host "ERROR: No se encontró el archivo de credenciales de Firebase." -ForegroundColor Red
    Write-Host "Ruta esperada: $credPath" -ForegroundColor Yellow
    Read-Host "Presiona Enter para salir"
    exit 1
}

# Ejecutar el configurador usando el ejecutable del servicio
$exePath = Join-Path $InstallPath "InventariAgentSvc.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: No se encontró el ejecutable del servicio." -ForegroundColor Red
    Write-Host "Ruta esperada: $exePath" -ForegroundColor Yellow
    Read-Host "Presiona Enter para salir"
    exit 1
}

Write-Host "Conectando a Firebase para obtener la lista de dispositivos..." -ForegroundColor Yellow
Write-Host ""

# El servicio tiene un modo de configuración interactivo cuando no hay DeviceId
# Lo ejecutamos directamente en la consola actual
try {
    # Llamar directamente sin capturar salida para mantener interactividad
    . $exePath
    
    Write-Host ""
    
    # Verificar si se configuró correctamente
    $configPath = "C:\ProgramData\InventariAgent\config.json"
    if (Test-Path $configPath) {
        $config = Get-Content $configPath | ConvertFrom-Json
        if ($config.DeviceId) {
            Write-Host ""
            Write-Host "Configuración completada exitosamente." -ForegroundColor Green
            Write-Host "Dispositivo: $($config.DeviceId)" -ForegroundColor Green
            Write-Host ""
            Read-Host "Presiona Enter para continuar"
            exit 0
        }
    }
    
    Write-Host ""
    Write-Host "No se seleccionó ningún dispositivo." -ForegroundColor Yellow
    Read-Host "Presiona Enter para salir"
    exit 1
}
catch {
    Write-Host ""
    Write-Host "Error durante la configuración: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Detalles del error:" -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Presiona Enter para salir"
    exit 1
}
