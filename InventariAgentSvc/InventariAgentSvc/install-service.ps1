$serviceName = "InventariAgent"
$displayName = "Servicio de Inventario y Monitoreo"
$description = "Monitorea el uso y estado del PC, reportando métricas a Firebase"
$exePath = Join-Path $PSScriptRoot "bin\Release\net8.0\publish\InventariAgentSvc.exe"

# Detener y eliminar el servicio si ya existe
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Deteniendo servicio existente..."
    Stop-Service -Name $serviceName -Force
    Write-Host "Eliminando servicio existente..."
    sc.exe delete $serviceName
}

# Crear directorios necesarios y establecer permisos
$programDataPath = Join-Path $env:ProgramData "InventariAgent"
if (-not (Test-Path $programDataPath)) {
    New-Item -ItemType Directory -Path $programDataPath | Out-Null
}

# Asegurar que el servicio tenga acceso total al directorio
$acl = Get-Acl $programDataPath
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "NT AUTHORITY\SYSTEM",
    "FullControl",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($rule)

# Asegurar que los usuarios autenticados puedan leer/escribir
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "BUILTIN\Users",
    "Modify",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl $programDataPath $acl

# Copiar archivo de credenciales si existe en el directorio actual
$credentialsSource = Join-Path $PSScriptRoot "firebase-credentials.json"
$credentialsTarget = Join-Path $programDataPath "firebase-credentials.json"
if (Test-Path $credentialsSource) {
    Write-Host "Copiando credenciales de Firebase..."
    Copy-Item -Path $credentialsSource -Destination $credentialsTarget -Force
}

# Instalar el servicio
Write-Host "Instalando servicio..."
New-Service -Name $serviceName `
            -DisplayName $displayName `
            -Description $description `
            -BinaryPathName $exePath `
            -StartupType Automatic

# Iniciar el servicio
Write-Host "Iniciando servicio..."
Start-Service -Name $serviceName

Write-Host "Servicio instalado y configurado correctamente."