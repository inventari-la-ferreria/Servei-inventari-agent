# Instalador portable para InventariAgentSvc

# Requerir ejecución como Administrador
if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script debe ejecutarse como Administrador."
    Start-Process powershell -Verb RunAs -ArgumentList "-File `"$PSCommandPath`""
    Exit
}

$serviceName = "InventariAgent"
$serviceDisplayName = "Inventari Agent"
$serviceDescription = "Servicio de monitoreo y gestión de PCs para Inventari."

$sourcePath = Split-Path -Parent $PSCommandPath
$installPath = "C:\\Program Files\\InventariAgent"
$exePath = Join-Path $installPath "InventariAgentSvc.exe"

Write-Host "Instalando servicio '$serviceName' desde $sourcePath ..."

# Detener y eliminar servicio previo si existe
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "El servicio ya existe. Deteniendo y eliminando..."
    try { Stop-Service -Name $serviceName -Force } catch {}
    try { sc.exe delete $serviceName | Out-Null } catch {}
    Start-Sleep -Seconds 3
}

# Crear carpeta destino y copiar
if (-Not (Test-Path $installPath)) { New-Item -Path $installPath -ItemType Directory | Out-Null }
Write-Host "Copiando archivos a '$installPath'..."
Copy-Item -Path (Join-Path $sourcePath '*') -Destination $installPath -Recurse -Force

# Asegurar carpeta de datos en ProgramData
$programDataPath = Join-Path $env:ProgramData "InventariAgent"
if (-not (Test-Path $programDataPath)) { New-Item -ItemType Directory -Path $programDataPath | Out-Null }

# Copiar appblock.json si viene en el paquete
$pkgAppBlock = Join-Path $sourcePath "Config\appblock.json"
$dstAppBlockDir = Join-Path $installPath "Config"
if (Test-Path $pkgAppBlock) {
    if (-Not (Test-Path $dstAppBlockDir)) { New-Item -ItemType Directory -Path $dstAppBlockDir | Out-Null }
    Copy-Item $pkgAppBlock -Destination (Join-Path $dstAppBlockDir "appblock.json") -Force
}

# Si hay firebase-credentials.json junto al instalador, copiar a ProgramData
$credSrc = Join-Path $sourcePath "firebase-credentials.json"
$credDst = Join-Path $programDataPath "firebase-credentials.json"
if (Test-Path $credSrc) {
    Write-Host "Copiando credenciales de Firebase a $credDst ..."
    Copy-Item $credSrc -Destination $credDst -Force
}

# Copiar configure-device.ps1 a la carpeta de instalación
$configureSrc = Join-Path $sourcePath "configure-device.ps1"
$configureDst = Join-Path $installPath "configure-device.ps1"
if (Test-Path $configureSrc) {
    Copy-Item $configureSrc -Destination $configureDst -Force
}

# Crear servicio
Write-Host "Creando servicio de Windows..."
New-Service -Name $serviceName `
            -BinaryPathName $exePath `
            -DisplayName $serviceDisplayName `
            -Description $serviceDescription `
            -StartupType Automatic

# NO iniciar el servicio automáticamente - se iniciará después de la configuración
Write-Host "Instalación de archivos completada. Continuando con la configuración del dispositivo..."
