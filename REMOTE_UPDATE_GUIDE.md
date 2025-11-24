# Guía de Actualización Remota del Servicio InventariAgent

## 📋 Descripción

El sistema de actualización remota permite actualizar el servicio en todos los dispositivos sin necesidad de acceso físico. El servicio verifica automáticamente cada 30 segundos si hay comandos de actualización en Firestore.

## 🏗️ Arquitectura

```
Firestore (pcs/[deviceId])
    ↓
    updateCommand: {
        version: "1.0.1",
        downloadUrl: "https://..."
    }
    ↓
Servicio detecta comando
    ↓
Descarga ZIP desde URL pública
    ↓
Extrae archivos
    ↓
Ejecuta script PowerShell
    ↓
Detiene servicio → Copia archivos → Reinicia servicio
```

## 🚀 Proceso de Actualización (Paso a Paso)

### 1️⃣ Preparar el Paquete de Actualización

```powershell
# Ejecutar desde el directorio del proyecto
.\prepare-update-package.ps1 -Version "1.0.1"
```

Esto generará:
- `updates\InventariAgent_v1.0.1.zip` - Paquete de actualización
- Hash SHA256 para verificación
- Instrucciones para distribución

### 2️⃣ Subir el Paquete a un Servidor Público

**Opción A: GitHub Releases (Recomendado)**

```bash
# 1. Crear un release en GitHub
gh release create v1.0.1 ./updates/InventariAgent_v1.0.1.zip --title "Version 1.0.1" --notes "Descripción de cambios"

# 2. Obtener la URL de descarga directa
# https://github.com/tu-usuario/tu-repo/releases/download/v1.0.1/InventariAgent_v1.0.1.zip
```

**Opción B: Servidor Web Propio**

```bash
# Subir vía FTP, SCP, o copiar al servidor web
scp ./updates/InventariAgent_v1.0.1.zip usuario@servidor:/var/www/html/updates/

# URL resultante: https://tu-servidor.com/updates/InventariAgent_v1.0.1.zip
```

**Opción C: Cloudflare R2 (10GB gratis)**

1. Ir a https://dash.cloudflare.com/
2. R2 → Crear bucket → Subir archivo
3. Habilitar acceso público y copiar URL

### 3️⃣ Enviar Comando de Actualización via Firestore

**Método 1: Firebase Console (Manual)**

1. Ir a Firebase Console → Firestore Database
2. Navegar a: `pcs` → `[ID del dispositivo]`
3. Agregar/actualizar campo:

```json
{
  "updateCommand": {
    "version": "1.0.1",
    "downloadUrl": "https://github.com/tu-repo/releases/download/v1.0.1/InventariAgent_v1.0.1.zip"
  }
}
```

**Método 2: Script PowerShell (Automatizado - Próximamente)**

```powershell
# Para actualizar un solo dispositivo
.\send-update-command.ps1 -DeviceId "PC-LAB-01" -Version "1.0.1" -DownloadUrl "https://..."

# Para actualizar todos los dispositivos
.\send-update-command.ps1 -All -Version "1.0.1" -DownloadUrl "https://..."
```

### 4️⃣ Monitorear el Proceso

El servicio actualizará automáticamente el campo `updateStatus`:

```json
{
  "updateStatus": {
    "status": "downloading",  // downloading → installing → completed → failed
    "message": "Descargando versión 1.0.1",
    "timestamp": "2025-11-24T10:30:00Z"
  }
}
```

**Estados posibles:**
- `downloading` - Descargando el paquete
- `installing` - Instalando archivos
- `completed` - Actualización exitosa
- `failed` - Error durante la actualización

## 🔍 Verificación de Actualización

### Ver versión actual en Firestore

Cada heartbeat del servicio reporta su versión actual. Verifica:

```json
{
  "lastHeartbeat": "2025-11-24T10:35:00Z",
  "serviceVersion": "1.0.1",  // Versión actualizada
  "metrics": { ... }
}
```

### Ver logs del servicio

```powershell
# Ver eventos del servicio
Get-EventLog -LogName Application -Source InventariAgent -Newest 50

# Ver logs en tiempo real (si está configurado)
Get-Content "C:\ProgramData\InventariAgent\logs\service.log" -Wait -Tail 20
```

## 🛠️ Solución de Problemas

### El servicio no detecta la actualización

**Verificar:**
1. El campo `updateCommand` existe en Firestore
2. La URL de descarga es accesible públicamente (prueba abrirla en el navegador)
3. El servicio está en ejecución: `Get-Service InventariAgent`

### La descarga falla

**Posibles causas:**
- URL inaccesible desde el dispositivo
- Firewall bloqueando la descarga
- Servidor temporal fuera de línea

**Solución:**
```powershell
# Verificar conectividad desde el dispositivo
Test-NetConnection -ComputerName tu-servidor.com -Port 443
```

### La actualización falla y el servicio no inicia

**Recuperación manual:**
1. El script automático crea un backup: `InventariAgentSvc.exe.backup`
2. Si falla, restaura manualmente:

```powershell
cd "C:\Program Files\InventariAgent"
Copy-Item .\InventariAgentSvc.exe.backup .\InventariAgentSvc.exe -Force
Start-Service InventariAgent
```

### Actualización en loop (se descarga repetidamente)

**Causa:** El campo `updateCommand` no se limpia después de actualizar.

**Solución:**
- Verifica que la versión en `Worker.cs` (`SERVICE_VERSION`) coincida con la versión del paquete
- Elimina manualmente el campo `updateCommand` en Firestore

## 📦 Actualizaciones por Lotes

Para actualizar múltiples dispositivos a la vez:

```javascript
// Desde Firebase Console → Firestore → Ejecutar consulta
const batch = db.batch();

const devices = await db.collection('pcs').where('status', '==', 'active').get();

devices.forEach(doc => {
  batch.update(doc.ref, {
    updateCommand: {
      version: '1.0.1',
      downloadUrl: 'https://...'
    }
  });
});

await batch.commit();
```

## 🔐 Seguridad

### Verificación de Integridad (Futuro)

Puedes agregar verificación SHA256 del paquete descargado:

1. Genera el hash al crear el paquete
2. Inclúyelo en `updateCommand`:

```json
{
  "updateCommand": {
    "version": "1.0.1",
    "downloadUrl": "https://...",
    "sha256": "abc123def456..."
  }
}
```

3. Modifica `RemoteUpdateService.cs` para verificar el hash antes de extraer

### Rollback Automático

Si la actualización falla, el script automático restaura la versión anterior desde el backup.

## ⚠️ Mejores Prácticas

1. **Prueba primero en un dispositivo de desarrollo**
2. **Actualiza gradualmente (canary deployment):**
   - Actualiza 1-2 dispositivos primero
   - Espera 24 horas
   - Si todo va bien, actualiza el resto
3. **Notifica a los usuarios antes de actualizaciones grandes**
4. **Mantén backups de versiones anteriores**
5. **Documenta cambios en cada versión**

## 📝 Changelog

Mantén un registro de cambios en cada versión:

```markdown
### v1.0.1 (2025-11-24)
- ✨ Agregado sistema de actualización remota
- 🐛 Corregido bug en detección de temperatura GPU
- 🔧 Mejorado rendimiento de métricas

### v1.0.0 (2025-11-20)
- 🎉 Release inicial
```

## 🎯 Roadmap

Funcionalidades futuras:
- [ ] Script automatizado para enviar comandos de actualización
- [ ] Verificación de integridad SHA256
- [ ] Actualización programada (por fecha/hora)
- [ ] Interfaz web para gestión de actualizaciones
- [ ] Rollback automático si el servicio no inicia
- [ ] Notificaciones push cuando la actualización se complete
