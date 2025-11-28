# Guía de Instalación Automática - InventariAgent

Esta guía explica cómo instalar el agente usando el script automático de PowerShell.

## 🚀 Instalación Rápida (Recomendada)

Hemos creado un script que hace todo el trabajo: descarga la última versión, la instala, te pide las credenciales y configura el servicio.

### Instalación Automática (Recomendado)

Ejecuta este comando en **PowerShell como Administrador** para instalar, configurar y arrancar el servicio automáticamente:

```powershell
powershell -ExecutionPolicy Bypass -NoProfile -Command "iwr -useb https://raw.githubusercontent.com/inventari-la-ferreria/Servei-inventari-agent/main/install-from-github.ps1 | iex"
```

---

## 🔧 Configuración Manual (o Re-configuración)

El script anterior abrirá automáticamente el menú de configuración. Si necesitas cambiar el PC vinculado más adelante o configurarlo manualmente, ejecuta:

1.  Abre una terminal (CMD o PowerShell) como Administrador.
2.  Ejecuta el siguiente comando:

```powershell
C:\InventariAgent\InventariAgentSvc.exe --configure
```

Sigue las instrucciones en pantalla para seleccionar el dispositivo.
