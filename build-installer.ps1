param(
  [string]$Runtime = "win-x64",
  [string]$OutDir = "d:\Insti\Proyecto\Servicio pcs\dist",
  [string]$CredsPath = "" # opcional: ruta al firebase-credentials.json para hornear dentro del EXE
)

$ErrorActionPreference = 'Stop'

$svcProj = "d:\Insti\Proyecto\Servicio pcs\InventariAgentSvc\InventariAgentSvc\InventariAgentSvc.csproj"
$svcOut = Join-Path $OutDir "InventariAgentSvc"
$payloadZip = Join-Path $OutDir "payload.zip"
$installerProj = "d:\Insti\Proyecto\Servicio pcs\Installer\InventariInstaller.csproj"
$installerOut = Join-Path $OutDir "Installer"

Write-Host "1) Publicando servicio..."
dotnet publish $svcProj -c Release -r $Runtime --self-contained true -o $svcOut | Out-Null

# Asegurar incluir Config/appblock.json
$srcAppBlock = "d:\Insti\Proyecto\Servicio pcs\InventariAgentSvc\InventariAgentSvc\Config\appblock.json"
$dstConfigDir = Join-Path $svcOut "Config"
if (-not (Test-Path $dstConfigDir)) { New-Item -ItemType Directory -Path $dstConfigDir | Out-Null }
Copy-Item $srcAppBlock -Destination (Join-Path $dstConfigDir "appblock.json") -Force

# Copiar scripts de instalación y configuración
$srcInstall = "d:\Insti\Proyecto\Servicio pcs\InventariAgentSvc\InventariAgentSvc\install.ps1"
$srcConfigure = "d:\Insti\Proyecto\Servicio pcs\InventariAgentSvc\InventariAgentSvc\configure-device.ps1"
if (Test-Path $srcInstall) { Copy-Item $srcInstall -Destination (Join-Path $svcOut "install.ps1") -Force }
if (Test-Path $srcConfigure) { Copy-Item $srcConfigure -Destination (Join-Path $svcOut "configure-device.ps1") -Force }

# Si pasaron credenciales, copiarlas al payload para hornearlas dentro del EXE
if ($CredsPath -and (Test-Path $CredsPath)) {
  Write-Host "Incluyendo credenciales en el instalador: $CredsPath"
  Copy-Item $CredsPath -Destination (Join-Path $svcOut "firebase-credentials.json") -Force
}

Write-Host "2) Empaquetando payload.zip..."
Remove-Item $payloadZip -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $svcOut '*') -DestinationPath $payloadZip -Force

Write-Host "3) Publicando instalador EXE..."
dotnet publish $installerProj -c Release -o $installerOut | Out-Null

Write-Host "Hecho. Instalador en: $installerOut\InventariInstaller.exe"
