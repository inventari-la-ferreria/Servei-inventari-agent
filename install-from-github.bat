@echo off
REM install-from-github.bat
REM Instalador simplificado - Llama al script PowerShell

echo ========================================
echo   INSTALADOR INVENTARI AGENT SERVICE
echo ========================================
echo.
echo Este instalador descargara e instalara automaticamente
echo la ultima version del servicio desde GitHub.
echo.
echo Requisitos:
echo   - Ejecutar como Administrador
echo   - Conexion a Internet
echo   - Archivo firebase-credentials.json
echo.
pause

REM Verificar privilegios de administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo ERROR: Este script debe ejecutarse como Administrador
    echo Por favor, haz clic derecho y selecciona "Ejecutar como administrador"
    echo.
    pause
    exit /b 1
)

REM Ejecutar el script PowerShell
echo.
echo Iniciando instalacion...
echo.

powershell.exe -ExecutionPolicy Bypass -File "%~dp0install-from-github.ps1"

if %errorLevel% neq 0 (
    echo.
    echo ERROR: La instalacion fallo
    pause
    exit /b 1
)

echo.
echo Instalacion completada
pause
