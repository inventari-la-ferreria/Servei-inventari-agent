# install-service.ps1

# Requerir ejecución como Administrador
if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script debe ejecutarse como Administrador."
    Start-Process powershell -Verb RunAs -ArgumentList "-File `"$PSCommandPath`""
    Exit
}

# --- Configuración ---
$serviceName = "InventariAgent"
$serviceDisplayName = "Inventari Agent"
$serviceDescription = "Servicio de monitoreo y gestión de PCs para Inventari."

# Rutas (ajusta la ruta de origen si es necesario)
$sourcePath = "D:\Insti\Proyecto\Servicio pcs\publish" # Directorio donde publicaste el servicio
$installPath = "C:\Program Files\InventariAgent"
$exePath = Join-Path $installPath "InventariAgentSvc.exe"

# --- Lógica de Instalación ---

Write-Host "Iniciando la instalación del servicio '$serviceName'..."

# 1. Detener y eliminar el servicio si ya existe (para reinstalaciones)
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "El servicio ya existe. Deteniendo y eliminándolo para reinstalar..."
    try {
        Stop-Service -Name $serviceName -Force
        Remove-Service -Name $serviceName
        Write-Host "Servicio anterior eliminado."
    } catch {
        Write-Error "No se pudo eliminar el servicio anterior. Error: $_"
        Exit
    }
    # Esperar un poco para que el sistema libere los archivos
    Start-Sleep -Seconds 5
}

# 2. Crear el directorio de instalación y copiar los archivos
Write-Host "Copiando archivos de la aplicación a '$installPath'..."
if (-Not (Test-Path $installPath)) {
    New-Item -Path $installPath -ItemType Directory | Out-Null
}
Copy-Item -Path "$sourcePath\*" -Destination $installPath -Recurse -Force

# 3. Crear el servicio de Windows
Write-Host "Creando el nuevo servicio de Windows..."
try {
    New-Service -Name $serviceName `
                -BinaryPathName $exePath `
                -DisplayName $serviceDisplayName `
                -Description $serviceDescription `
                -StartupType Automatic
    
    Write-Host "Servicio '$serviceName' creado con éxito."
} catch {
    Write-Error "Error al crear el servicio. Error: $_"
    # Limpiar archivos si falla la creación del servicio
    Remove-Item -Path $installPath -Recurse -Force
    Exit
}

# 4. Iniciar el servicio
Write-Host "Iniciando el servicio..."
try {
    Start-Service -Name $serviceName
    Write-Host "¡Instalación completada! El servicio '$serviceName' se está ejecutando."
} catch {
    Write-Error "El servicio se instaló pero no pudo iniciarse. Error: $_"
    Write-Host "Puedes intentar iniciarlo manualmente desde la consola de Servicios (services.msc)."
}

# (Opcional) Pausar para ver el resultado
Read-Host "Presiona Enter para salir."
