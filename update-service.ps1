# update-service.ps1

# Requerir ejecución como Administrador
if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script debe ejecutarse como Administrador."
    Start-Process powershell -Verb RunAs -ArgumentList "-File `"$PSCommandPath`""
    Exit
}

# --- Configuración ---
$serviceName = "InventariAgent"
$sourcePath = "D:\Insti\Proyecto\Servicio pcs\publish" # Directorio donde publicaste el servicio
$installPath = "C:\Program Files\InventariAgent"
$exePath = Join-Path $installPath "InventariAgentSvc.exe"
$oldExePath = Join-Path $installPath "InventariAgentSvc.exe.old"

# --- Lógica de Actualización ---

Write-Host "Iniciando la actualización del servicio '$serviceName'..."

# 1. Detener el servicio
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq 'Running') {
    Write-Host "Deteniendo el servicio '$serviceName'..."
    try {
        Stop-Service -Name $serviceName -Force
        Write-Host "Servicio detenido."
    } catch {
        Write-Error "No se pudo detener el servicio. Error: $_"
        Exit
    }
    Start-Sleep -Seconds 3
} else {
    Write-Host "El servicio no estaba en ejecución. Se procederá a copiar los archivos."
}

# 2. Renombrar el ejecutable antiguo
if (Test-Path $exePath) {
    Write-Host "Renombrando ejecutable antiguo..."
    try {
        Rename-Item -Path $exePath -NewName "InventariAgentSvc.exe.old" -Force
    } catch {
        Write-Error "No se pudo renombrar el ejecutable antiguo. Error: $_"
        Exit
    }
}

# 3. Copiar los archivos actualizados
Write-Host "Copiando archivos nuevos a '$installPath'..."
try {
    Copy-Item -Path "$sourcePath\*" -Destination $installPath -Recurse -Force
    Write-Host "Archivos actualizados correctamente."
} catch {
    Write-Error "No se pudieron copiar los archivos. Error: $_"
    # Intentar restaurar el ejecutable antiguo
    if (Test-Path $oldExePath) {
        Rename-Item -Path $oldExePath -NewName "InventariAgentSvc.exe" -Force
    }
    if ($service) { Start-Service -Name $serviceName }
    Exit
}

# 4. Eliminar el ejecutable antiguo
if (Test-Path $oldExePath) {
    Write-Host "Eliminando ejecutable antiguo..."
    Remove-Item -Path $oldExePath -Force
}

# 5. Iniciar el servicio
Write-Host "Iniciando el servicio '$serviceName'..."
try {
    Start-Service -Name $serviceName
    Write-Host "¡Actualización completada! El servicio '$serviceName' se ha reiniciado con la nueva versión."
} catch {
    Write-Error "El servicio se actualizó pero no pudo iniciarse. Error: $_"
    Write-Host "Puedes intentar iniciarlo manualmente desde la consola de Servicios (services.msc)."
}

# (Opcional) Pausar para ver el resultado
Read-Host "Presiona Enter para salir."