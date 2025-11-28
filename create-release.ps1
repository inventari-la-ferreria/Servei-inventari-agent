# Script de Despliegue Automatico "One-Click"
# Uso: .\create-release.ps1 1.0.26 "Notas de la version"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$Notes = "Release automatica"
)

$workerFile = ".\InventariAgentSvc\InventariAgentSvc\Worker.cs"
$csprojFile = ".\InventariAgentSvc\InventariAgentSvc\InventariAgentSvc.csproj"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   CREADOR DE RELEASE AUTOMATICO" -ForegroundColor Cyan
Write-Host "   Version: $Version" -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Actualizar Version en Archivos
Write-Host "`n[1/5] Actualizando version en el codigo..."

# Update Worker.cs
$workerContent = Get-Content $workerFile
$workerContent = $workerContent -replace 'private const string SERVICE_VERSION = ".*?";', "private const string SERVICE_VERSION = ""$Version"";"
$workerContent | Set-Content $workerFile

# Update .csproj
$csprojContent = Get-Content $csprojFile
$csprojContent = $csprojContent -replace "<Version>.*?</Version>", "<Version>$Version</Version>"
$csprojContent = $csprojContent -replace "<AssemblyVersion>.*?</AssemblyVersion>", "<AssemblyVersion>$Version.0</AssemblyVersion>"
$csprojContent = $csprojContent -replace "<FileVersion>.*?</FileVersion>", "<FileVersion>$Version.0</FileVersion>"
$csprojContent | Set-Content $csprojFile

Write-Host "OK - Codigo actualizado a v$Version" -ForegroundColor Green

# 2. Git Commit & Push
Write-Host "`n[2/5] Subiendo cambios a GitHub..."
git add .
git commit -m "bump: v$Version"
git push origin main

if ($LASTEXITCODE -ne 0) {
    Write-Error "Error en git push. Verifica tu conexion."
    exit
}

# 3. Construir ZIP (Llamamos al otro script)
Write-Host "`n[3/5] Construyendo paquete..."
.\build_release.ps1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Error en el build."
    exit
}

# 4. Crear Release en GitHub
Write-Host "`n[4/5] Publicando Release en GitHub..."
# Borrar release si existe (para re-intentos)
gh release delete "v$Version" --cleanup-tag -y 2>$null

# Crear nueva
gh release create "v$Version" .\InventariAgent.zip --title "v$Version" --notes "$Notes"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n==========================================" -ForegroundColor Green
    Write-Host "   TODO LISTO! v$Version PUBLICADA" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host "URL: https://github.com/inventari-la-ferreria/Servei-inventari-agent/releases/tag/v$Version"
} else {
    Write-Error "Fallo la creacion de la release en GitHub."
}
