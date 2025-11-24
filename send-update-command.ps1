# send-update-command.ps1
# Script para enviar comandos de actualización a dispositivos via Firestore

param(
    [Parameter(Mandatory=$false)]
    [string]$DeviceId,
    
    [Parameter(Mandatory=$false)]
    [switch]$All,
    
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [string]$DownloadUrl = "",
    
    [string]$CredentialsPath = "C:\ProgramData\InventariAgent\firebase-credentials.json"
)

$ErrorActionPreference = "Stop"

# Verificar que existe el archivo de credenciales
if (-not (Test-Path $CredentialsPath)) {
    Write-Error "No se encontró el archivo de credenciales en: $CredentialsPath"
    Exit 1
}

# Si no se proporciona URL, construir la URL de GitHub
if ([string]::IsNullOrEmpty($DownloadUrl)) {
    $DownloadUrl = "https://github.com/inventari-la-ferreria/Servei-inventari-agent/releases/download/v$Version/InventariAgent_v$Version.zip"
}

Write-Host "=== ENVIAR COMANDO DE ACTUALIZACIÓN ===" -ForegroundColor Cyan
Write-Host "Versión objetivo: $Version" -ForegroundColor Green
Write-Host "URL de descarga: $DownloadUrl" -ForegroundColor Green

# Verificar que la URL es accesible
Write-Host "`nVerificando URL de descarga..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri $DownloadUrl -Method Head -UseBasicParsing -ErrorAction Stop
    $sizeInMB = [math]::Round($response.Headers.'Content-Length' / 1MB, 2)
    Write-Host "✓ URL accesible - Tamaño: $sizeInMB MB" -ForegroundColor Green
} catch {
    Write-Warning "No se pudo verificar la URL. Continuando de todos modos..."
}

# Instalar módulo de Firebase si no está instalado
if (-not (Get-Module -ListAvailable -Name "Firestore")) {
    Write-Host "`nInstalando dependencias..." -ForegroundColor Yellow
    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force | Out-Null
}

# Configurar variable de entorno para credenciales
$env:GOOGLE_APPLICATION_CREDENTIALS = $CredentialsPath

# Script de Python para actualizar Firestore
$pythonScript = @"
import os
import sys
from google.cloud import firestore

def send_update_command(device_id, version, download_url):
    try:
        db = firestore.Client(project='laferreria-inventari')
        
        if device_id == '__ALL__':
            # Actualizar todos los dispositivos
            devices = db.collection('pcs').stream()
            count = 0
            for device in devices:
                db.collection('pcs').document(device.id).update({
                    'updateCommand': {
                        'version': version,
                        'downloadUrl': download_url
                    }
                })
                count += 1
                print(f'✓ Comando enviado a: {device.id}')
            print(f'\nTotal: {count} dispositivos actualizados')
        else:
            # Actualizar un solo dispositivo
            db.collection('pcs').document(device_id).update({
                'updateCommand': {
                    'version': version,
                    'downloadUrl': download_url
                }
            })
            print(f'✓ Comando de actualización enviado a: {device_id}')
        
        return True
    except Exception as e:
        print(f'✗ Error: {str(e)}', file=sys.stderr)
        return False

if __name__ == '__main__':
    device_id = sys.argv[1]
    version = sys.argv[2]
    download_url = sys.argv[3]
    
    success = send_update_command(device_id, version, download_url)
    sys.exit(0 if success else 1)
"@

# Guardar script temporal
$tempScript = Join-Path $env:TEMP "firestore_update.py"
$pythonScript | Out-File -FilePath $tempScript -Encoding UTF8

Write-Host "`nEnviando comando de actualización..." -ForegroundColor Yellow

# Determinar DeviceId
$targetDevice = if ($All) { "__ALL__" } else { $DeviceId }

if ([string]::IsNullOrEmpty($targetDevice) -and -not $All) {
    Write-Error "Debes especificar -DeviceId o usar -All"
    Exit 1
}

# Ejecutar script de Python
try {
    $pythonCmd = Get-Command python -ErrorAction SilentlyContinue
    if (-not $pythonCmd) {
        Write-Error "Python no está instalado. Instala Python 3.8+ y el paquete google-cloud-firestore"
        Exit 1
    }
    
    # Verificar/instalar paquete
    Write-Host "Verificando dependencias de Python..." -ForegroundColor Yellow
    python -m pip install --quiet google-cloud-firestore
    
    # Ejecutar
    $result = python $tempScript $targetDevice $Version $DownloadUrl
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n$result" -ForegroundColor Green
        Write-Host "`n=== SIGUIENTE PASO ===" -ForegroundColor Cyan
        Write-Host "Los dispositivos detectarán la actualización en máximo 30 segundos." -ForegroundColor White
        Write-Host "Puedes monitorear el progreso en Firebase Console:" -ForegroundColor White
        Write-Host "  Firestore → pcs → [dispositivo] → updateStatus" -ForegroundColor Gray
    } else {
        Write-Error "Error ejecutando el script: $result"
        Exit 1
    }
} catch {
    Write-Error "Error: $_"
    Exit 1
} finally {
    # Limpiar
    if (Test-Path $tempScript) {
        Remove-Item $tempScript -Force
    }
}

Write-Host "`n¡Comando enviado exitosamente!" -ForegroundColor Green
Read-Host "`nPresiona Enter para salir"
