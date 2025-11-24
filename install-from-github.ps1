# install-from-github.ps1
# Script de instalación automática del servicio InventariAgent desde GitHub

# Requerir ejecución como Administrador
if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script debe ejecutarse como Administrador."
    Start-Process powershell -Verb RunAs -ArgumentList "-File `"$PSCommandPath`""
    Exit
}

$ErrorActionPreference = "Stop"

# Configuración
$REPO_OWNER = "inventari-la-ferreria"
$REPO_NAME = "Servei-inventari-agent"
$SERVICE_NAME = "InventariAgent"
$INSTALL_PATH = "C:\Program Files\InventariAgent"
$DATA_PATH = "C:\ProgramData\InventariAgent"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  INSTALADOR INVENTARI AGENT SERVICE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Función para verificar e instalar .NET Runtime
function Install-DotNetRuntime {
    try {
        Write-Host "Verificando .NET 8.0 Runtime..." -ForegroundColor Yellow
        
        # Verificar si .NET 8.0 está instalado
        $dotnetVersion = & dotnet --list-runtimes 2>$null | Select-String "Microsoft.NETCore.App 8.0"
        
        if ($dotnetVersion) {
            Write-Host "✓ .NET 8.0 Runtime ya está instalado" -ForegroundColor Green
            return $true
        }
        
        Write-Host ".NET 8.0 Runtime no encontrado. Instalando..." -ForegroundColor Yellow
        
        # Descargar el instalador de .NET 8.0 Runtime
        $dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/6224f00f-08da-4e7f-85b1-00d42c2bb3d3/b775de636b91e023574a0bbc291f705a/dotnet-runtime-8.0.11-win-x64.exe"
        $installerPath = Join-Path $env:TEMP "dotnet-runtime-8.0-installer.exe"
        
        Write-Host "Descargando .NET 8.0 Runtime..." -ForegroundColor Yellow
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $dotnetUrl -OutFile $installerPath -UseBasicParsing
        $ProgressPreference = 'Continue'
        
        Write-Host "Instalando .NET 8.0 Runtime (esto puede tardar un minuto)..." -ForegroundColor Yellow
        $process = Start-Process -FilePath $installerPath -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru
        
        if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
            Write-Host "✓ .NET 8.0 Runtime instalado correctamente" -ForegroundColor Green
            
            # Limpiar instalador
            Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
            return $true
        }
        else {
            Write-Warning "La instalación de .NET Runtime terminó con código: $($process.ExitCode)"
            Write-Host "Intentando continuar de todos modos..." -ForegroundColor Yellow
            return $false
        }
    }
    catch {
        Write-Warning "Error instalando .NET Runtime: $_"
        Write-Host "El servicio puede no funcionar sin .NET 8.0 Runtime" -ForegroundColor Yellow
        return $false
    }
}

# Función para obtener la última versión desde GitHub
function Get-LatestRelease {
    try {
        Write-Host "Obteniendo información de la última versión..." -ForegroundColor Yellow
        $apiUrl = "https://api.github.com/repos/$REPO_OWNER/$REPO_NAME/releases/latest"
        $headers = @{
            "User-Agent" = "InventariAgentInstaller"
        }
        
        $response = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method Get
        
        $version = $response.tag_name.TrimStart('v')
        $downloadUrl = $null
        
        foreach ($asset in $response.assets) {
            if ($asset.name -like "*.zip" -and $asset.name -notlike "*.sha256") {
                $downloadUrl = $asset.browser_download_url
                break
            }
        }
        
        if (-not $downloadUrl) {
            throw "No se encontró el archivo ZIP en el release"
        }
        
        Write-Host "✓ Última versión disponible: $version" -ForegroundColor Green
        
        return @{
            Version = $version
            DownloadUrl = $downloadUrl
            ReleaseName = $response.name
        }
    }
    catch {
        Write-Error "Error obteniendo información del release: $_"
        Exit 1
    }
}

# Función para descargar el paquete
function Download-Package {
    param(
        [string]$Url,
        [string]$Version
    )
    
    try {
        $tempPath = Join-Path $env:TEMP "InventariAgent_v$Version.zip"
        
        Write-Host "`nDescargando InventariAgent v$Version..." -ForegroundColor Yellow
        Write-Host "URL: $Url" -ForegroundColor Gray
        
        # Descargar con barra de progreso
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $Url -OutFile $tempPath -UseBasicParsing
        $ProgressPreference = 'Continue'
        
        $sizeInMB = [math]::Round((Get-Item $tempPath).Length / 1MB, 2)
        Write-Host "✓ Descarga completada ($sizeInMB MB)" -ForegroundColor Green
        
        return $tempPath
    }
    catch {
        Write-Error "Error descargando el paquete: $_"
        Exit 1
    }
}

# Función para detener el servicio si existe
function Stop-ExistingService {
    $service = Get-Service -Name $SERVICE_NAME -ErrorAction SilentlyContinue
    
    if ($service) {
        Write-Host "`nServicio existente detectado" -ForegroundColor Yellow
        
        if ($service.Status -eq 'Running') {
            Write-Host "Deteniendo servicio..." -ForegroundColor Yellow
            Stop-Service -Name $SERVICE_NAME -Force
            Start-Sleep -Seconds 2
            Write-Host "✓ Servicio detenido" -ForegroundColor Green
        }
        
        return $true
    }
    
    return $false
}

# Función para extraer e instalar
function Install-Package {
    param(
        [string]$ZipPath,
        [string]$Version
    )
    
    try {
        # Crear directorios
        Write-Host "`nPreparando directorios de instalación..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $INSTALL_PATH -Force | Out-Null
        New-Item -ItemType Directory -Path $DATA_PATH -Force | Out-Null
        
        # Extraer archivos
        Write-Host "Extrayendo archivos..." -ForegroundColor Yellow
        Expand-Archive -Path $ZipPath -DestinationPath $INSTALL_PATH -Force
        Write-Host "✓ Archivos extraídos en: $INSTALL_PATH" -ForegroundColor Green
        
        # Descargar credenciales de Firebase desde el repositorio
        $credPath = Join-Path $DATA_PATH "firebase-credentials.json"
        if (-not (Test-Path $credPath)) {
            Write-Host "`nDescargando credenciales de Firebase..." -ForegroundColor Yellow
            
            $credUrl = "https://raw.githubusercontent.com/$REPO_OWNER/$REPO_NAME/main/firebase-credentials.json"
            
            try {
                Invoke-WebRequest -Uri $credUrl -OutFile $credPath -UseBasicParsing
                Write-Host "✓ Credenciales de Firebase descargadas" -ForegroundColor Green
            }
            catch {
                Write-Warning "No se pudieron descargar las credenciales automáticamente"
                Write-Host "Error: $_" -ForegroundColor Red
                Write-Host "El servicio se instalará pero no funcionará sin las credenciales." -ForegroundColor Yellow
                Write-Host "Copia manualmente el archivo a: $DATA_PATH" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "✓ Credenciales de Firebase ya existen" -ForegroundColor Green
        }
        
        return $true
    }
    catch {
        Write-Error "Error durante la instalación: $_"
        return $false
    }
}

# Función para instalar/actualizar el servicio de Windows
function Install-WindowsService {
    param(
        [bool]$IsUpdate
    )
    
    try {
        $exePath = Join-Path $INSTALL_PATH "InventariAgentSvc.exe"
        
        if (-not (Test-Path $exePath)) {
            Write-Error "No se encontró el ejecutable del servicio: $exePath"
            Exit 1
        }
        
        $service = Get-Service -Name $SERVICE_NAME -ErrorAction SilentlyContinue
        
        if ($service) {
            Write-Host "`nActualizando servicio existente..." -ForegroundColor Yellow
            # El servicio ya existe, solo necesitamos iniciarlo
        }
        else {
            Write-Host "`nInstalando servicio de Windows..." -ForegroundColor Yellow
            
            # Crear el servicio
            New-Service -Name $SERVICE_NAME `
                        -BinaryPathName $exePath `
                        -DisplayName "Inventari Agent Service" `
                        -Description "Servicio de monitoreo y gestión remota de dispositivos para Inventari La Ferreria" `
                        -StartupType Automatic
            
            Write-Host "✓ Servicio instalado" -ForegroundColor Green
        }
        
        # Configurar el servicio
        Write-Host "Configurando el servicio..." -ForegroundColor Yellow
        
        # El servicio ejecutará el menú de configuración en el primer inicio
        Write-Host "✓ Servicio configurado" -ForegroundColor Green
        
        return $true
    }
    catch {
        Write-Error "Error instalando el servicio de Windows: $_"
        return $false
    }
}

# Función para configurar el dispositivo
function Configure-Device {
    try {
        Write-Host "`n========================================" -ForegroundColor Cyan
        Write-Host "  CONFIGURACIÓN DEL DISPOSITIVO" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        
        $exePath = Join-Path $INSTALL_PATH "InventariAgentSvc.exe"
        
        if (-not (Test-Path $exePath)) {
            Write-Error "No se encontró el ejecutable: $exePath"
            return $false
        }
        
        Write-Host "Ejecutando configuración inicial..." -ForegroundColor Yellow
        Write-Host "Se abrirá el menú de selección de dispositivo." -ForegroundColor White
        Write-Host ""
        Write-Host "IMPORTANTE: Selecciona el ID del PC desde el menú que aparecerá." -ForegroundColor Yellow
        Write-Host ""
        Start-Sleep -Seconds 2
        
        # Ejecutar el servicio en modo consola para configuración
        # Usar Start-Process -Wait para esperar a que termine
        $processInfo = Start-Process -FilePath $exePath -Wait -PassThru -NoNewWindow
        
        if ($processInfo.ExitCode -eq 0) {
            Write-Host ""
            Write-Host "✓ Configuración completada exitosamente" -ForegroundColor Green
            return $true
        }
        else {
            Write-Warning "El configurador terminó con código: $($processInfo.ExitCode)"
            return $false
        }
    }
    catch {
        Write-Warning "Error durante la configuración: $_"
        Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Gray
        return $false
    }
}

# Función para iniciar el servicio
function Start-AgentService {
    try {
        Write-Host "`nIniciando servicio..." -ForegroundColor Yellow
        Start-Service -Name $SERVICE_NAME
        Start-Sleep -Seconds 2
        
        $service = Get-Service -Name $SERVICE_NAME
        if ($service.Status -eq 'Running') {
            Write-Host "✓ Servicio iniciado correctamente" -ForegroundColor Green
            return $true
        }
        else {
            Write-Warning "El servicio no está en ejecución"
            return $false
        }
    }
    catch {
        Write-Error "Error iniciando el servicio: $_"
        return $false
    }
}

# ============================================
# PROCESO PRINCIPAL
# ============================================

try {
    # 1. Instalar .NET Runtime si no está presente
    Install-DotNetRuntime
    
    # 2. Obtener última versión
    $release = Get-LatestRelease
    
    # 3. Verificar si hay servicio existente
    $isUpdate = Stop-ExistingService
    
    if ($isUpdate) {
        Write-Host "`nEsto es una ACTUALIZACIÓN del servicio existente" -ForegroundColor Cyan
    }
    else {
        Write-Host "`nEsto es una INSTALACIÓN NUEVA del servicio" -ForegroundColor Cyan
    }
    
    # 4. Descargar paquete
    $zipPath = Download-Package -Url $release.DownloadUrl -Version $release.Version
    
    # 5. Instalar archivos
    $installed = Install-Package -ZipPath $zipPath -Version $release.Version
    
    if (-not $installed) {
        Write-Error "La instalación falló"
        Exit 1
    }
    
    # 6. Instalar/actualizar servicio de Windows
    $serviceInstalled = Install-WindowsService -IsUpdate $isUpdate
    
    if (-not $serviceInstalled) {
        Write-Error "Error configurando el servicio de Windows"
        Exit 1
    }
    
    # 7. Configurar dispositivo (solo en instalación nueva)
    if (-not $isUpdate) {
        $configured = Configure-Device
        
        if (-not $configured) {
            Write-Warning "La configuración no se completó correctamente"
            Write-Host "Puedes configurar el dispositivo más tarde ejecutando:" -ForegroundColor Yellow
            Write-Host "  cd '$INSTALL_PATH'" -ForegroundColor Cyan
            Write-Host "  .\InventariAgentSvc.exe" -ForegroundColor Cyan
        }
    }
    
    # 8. Iniciar servicio
    $started = Start-AgentService
    
    # 9. Limpiar archivos temporales
    Write-Host "`nLimpiando archivos temporales..." -ForegroundColor Yellow
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    
    # Resumen final
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "  INSTALACIÓN COMPLETADA" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Versión instalada: $($release.Version)" -ForegroundColor White
    Write-Host "Ubicación: $INSTALL_PATH" -ForegroundColor White
    Write-Host "Estado del servicio: " -NoNewline -ForegroundColor White
    
    $service = Get-Service -Name $SERVICE_NAME
    if ($service.Status -eq 'Running') {
        Write-Host "En ejecución ✓" -ForegroundColor Green
    }
    else {
        Write-Host "Detenido" -ForegroundColor Yellow
    }
    
    Write-Host "`nComandos útiles:" -ForegroundColor Cyan
    Write-Host "  Ver estado:    Get-Service $SERVICE_NAME" -ForegroundColor Gray
    Write-Host "  Detener:       Stop-Service $SERVICE_NAME" -ForegroundColor Gray
    Write-Host "  Iniciar:       Start-Service $SERVICE_NAME" -ForegroundColor Gray
    Write-Host "  Reiniciar:     Restart-Service $SERVICE_NAME" -ForegroundColor Gray
    Write-Host "  Ver logs:      Get-EventLog -LogName Application -Source $SERVICE_NAME -Newest 20" -ForegroundColor Gray
    
    Write-Host "`n¡Instalación exitosa!" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "  ERROR EN LA INSTALACIÓN" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Por favor, reporta este error con los detalles mostrados arriba." -ForegroundColor Yellow
    Exit 1
}

# Pausar para ver el resultado
Write-Host ""
Read-Host "Presiona Enter para salir"
