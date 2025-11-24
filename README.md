# Inventari Agent Service

Servicio de Windows para monitoreo y gestión remota de dispositivos del sistema Inventari La Ferreria.

[![Build and Release](https://github.com/inventari-la-ferreria/Servei-inventari-agent/actions/workflows/release.yml/badge.svg)](https://github.com/inventari-la-ferreria/Servei-inventari-agent/actions/workflows/release.yml)

## 🚀 Características

- **Monitoreo en tiempo real**: CPU, GPU, RAM, disco, temperaturas
- **Gestión remota**: Actualización automática sin acceso físico
- **Control de aplicaciones**: Bloqueo de apps según políticas
- **Alertas automáticas**: Creación de incidencias cuando se superan umbrales
- **Integración Firebase**: Sincronización en tiempo real con Firestore

## 📋 Requisitos

- Windows 10/11 o Windows Server 2019+
- .NET 8.0 Runtime
- Permisos de administrador
- Conectividad a Internet
- Credenciales de Firebase configuradas

## 📦 Instalación

### Instalación Nueva

1. Descarga el último release desde [Releases](https://github.com/inventari-la-ferreria/Servei-inventari-agent/releases)
2. Extrae el ZIP a `C:\Program Files\InventariAgent`
3. Coloca el archivo `firebase-credentials.json` en `C:\ProgramData\InventariAgent\`
4. Ejecuta como administrador: `.\install-service.ps1`
5. El servicio se configurará automáticamente y se iniciará

### Actualización Manual

```powershell
# Detener el servicio
Stop-Service InventariAgent

# Copiar archivos nuevos (respaldando los anteriores)
Copy-Item ".\nuevo-release\*" "C:\Program Files\InventariAgent\" -Recurse -Force

# Iniciar el servicio
Start-Service InventariAgent
```

## 🔄 Actualización Remota

El servicio soporta actualizaciones remotas sin necesidad de acceso físico al dispositivo.

### Para Administradores

**1. Crear un nuevo release:**

```powershell
# Desde el directorio del proyecto
.\prepare-update-package.ps1 -Version "1.0.1"
```

**2. Subir a GitHub:**

```bash
# Crear tag y push
git tag v1.0.1
git push origin v1.0.1

# El workflow de GitHub Actions creará automáticamente el release
```

**3. Enviar comando de actualización:**

En Firebase Console → Firestore → `pcs/[ID_DISPOSITIVO]`:

```json
{
  "updateCommand": {
    "version": "1.0.1",
    "downloadUrl": "https://github.com/inventari-la-ferreria/Servei-inventari-agent/releases/download/v1.0.1/InventariAgent_v1.0.1.zip"
  }
}
```

**4. Monitorear el proceso:**

El servicio actualizará el campo `updateStatus`:

```json
{
  "updateStatus": {
    "status": "downloading",  // downloading → installing → completed
    "message": "Descargando versión 1.0.1",
    "timestamp": "2025-11-24T10:30:00Z"
  }
}
```

Ver la [Guía Completa de Actualización Remota](REMOTE_UPDATE_GUIDE.md) para más detalles.

## 🛠️ Desarrollo

### Compilar el proyecto

```powershell
# Restaurar dependencias
dotnet restore

# Compilar
dotnet build -c Release

# Publicar
dotnet publish -c Release -o ./publish
```

### Ejecutar en modo desarrollo

```powershell
cd InventariAgentSvc\InventariAgentSvc
dotnet run
```

### Estructura del proyecto

```
InventariAgentSvc/
├── Config/           # Configuración del agente
├── Models/           # Modelos de datos
├── Services/         # Servicios principales
│   ├── MetricsCollector.cs      # Recolección de métricas
│   ├── FirebaseClient.cs        # Cliente de Firestore
│   ├── AppBlocker.cs            # Control de aplicaciones
│   └── RemoteUpdateService.cs   # Actualización remota
├── Program.cs        # Punto de entrada
└── Worker.cs         # Servicio principal
```

## 📊 Métricas Monitoreadas

- **CPU**: Uso (%), Temperatura (°C)
- **GPU**: Uso (%), Temperatura (°C)
- **RAM**: Uso (%), Disponible (GB)
- **Disco**: Espacio libre (%), Total (GB)
- **Red**: Dirección IP, MAC Address

## 🔧 Configuración

### Archivo: `C:\ProgramData\InventariAgent\config.json`

```json
{
  "DeviceId": "PC-LAB-01",
  "Thresholds": {
    "CpuTempWarn": 70,
    "CpuTempCrit": 85,
    "GpuTempWarn": 75,
    "GpuTempCrit": 90,
    "CpuUsageWarn": 80,
    "CpuUsageCrit": 95
  },
  "IncidentPolicy": {
    "RepeatUpdateCooldownMinutes": 15
  }
}
```

## 🔒 Seguridad

- Las credenciales de Firebase se almacenan en `ProgramData` con permisos restringidos
- El servicio se ejecuta con permisos de SYSTEM
- Las actualizaciones se descargan solo desde URLs HTTPS
- (Futuro) Verificación SHA256 de paquetes de actualización

## 📝 Logs

Ver logs del servicio:

```powershell
# Event Viewer
Get-EventLog -LogName Application -Source InventariAgent -Newest 50

# Archivo de log (si está configurado)
Get-Content "C:\ProgramData\InventariAgent\logs\service.log" -Wait -Tail 20
```

## 🐛 Troubleshooting

### El servicio no inicia

```powershell
# Verificar estado
Get-Service InventariAgent

# Ver último error
Get-EventLog -LogName Application -Source InventariAgent -EntryType Error -Newest 1

# Verificar credenciales de Firebase
Test-Path "C:\ProgramData\InventariAgent\firebase-credentials.json"
```

### Actualización remota falla

```powershell
# Verificar conectividad
Test-NetConnection github.com -Port 443

# Ver estado de actualización en Firestore
# Firestore → pcs → [ID] → updateStatus

# Rollback manual si es necesario
cd "C:\Program Files\InventariAgent"
Copy-Item .\InventariAgentSvc.exe.backup .\InventariAgentSvc.exe -Force
Start-Service InventariAgent
```

## 🤝 Contribuir

1. Fork el repositorio
2. Crea una rama para tu feature (`git checkout -b feature/AmazingFeature`)
3. Commit tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. Push a la rama (`git push origin feature/AmazingFeature`)
5. Abre un Pull Request

## 📄 Licencia

Este proyecto es propiedad de La Ferreria - Inventari System.

## 📞 Soporte

Para problemas o preguntas:
- Abre un [Issue](https://github.com/inventari-la-ferreria/Servei-inventari-agent/issues)
- Contacta al equipo de desarrollo

## 🗺️ Roadmap

- [x] Monitoreo básico de métricas
- [x] Integración con Firebase
- [x] Sistema de actualización remota
- [x] Control de aplicaciones (AppBlocker)
- [ ] Verificación SHA256 de actualizaciones
- [ ] Panel web de gestión de actualizaciones
- [ ] Actualizaciones programadas
- [ ] Notificaciones push
- [ ] Rollback automático
- [ ] Soporte para múltiples configuraciones por grupo
