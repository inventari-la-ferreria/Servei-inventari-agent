# Guía de Contribución

¡Gracias por tu interés en contribuir al Inventari Agent Service!

## 🚀 Proceso de Desarrollo

### 1. Configurar el Entorno

```powershell
# Clonar el repositorio
git clone https://github.com/inventari-la-ferreria/Servei-inventari-agent.git
cd Servei-inventari-agent

# Restaurar dependencias
dotnet restore

# Compilar
dotnet build
```

### 2. Crear una Rama

```bash
# Actualizar main
git checkout main
git pull origin main

# Crear rama para tu feature
git checkout -b feature/nombre-descriptivo
# o para un bugfix
git checkout -b fix/descripcion-del-bug
```

### 3. Hacer Cambios

- Escribe código limpio y documentado
- Sigue las convenciones de C# y .NET
- Agrega comentarios donde sea necesario
- Actualiza la documentación si es relevante

### 4. Probar Localmente

```powershell
# Compilar
dotnet build -c Release

# Ejecutar en modo desarrollo
cd InventariAgentSvc/InventariAgentSvc
dotnet run

# Crear paquete de prueba
.\prepare-update-package.ps1 -Version "1.0.0-test"
```

### 5. Commit y Push

```bash
# Agregar archivos
git add .

# Commit con mensaje descriptivo
git commit -m "feat: descripción del cambio"
# o
git commit -m "fix: descripción del bug corregido"

# Push a tu rama
git push origin feature/nombre-descriptivo
```

### 6. Crear Pull Request

1. Ve a GitHub
2. Crea un Pull Request desde tu rama a `main`
3. Describe los cambios realizados
4. Espera la revisión del código

## 📝 Convenciones de Commits

Usa [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` Nueva funcionalidad
- `fix:` Corrección de bug
- `docs:` Cambios en documentación
- `style:` Formato, punto y coma faltante, etc.
- `refactor:` Refactorización de código
- `test:` Agregar tests
- `chore:` Tareas de mantenimiento

Ejemplos:
```
feat: add SHA256 verification for updates
fix: resolve CPU temperature reading issue
docs: update installation guide
refactor: improve metrics collection performance
```

## 🏗️ Estructura del Código

```
InventariAgentSvc/
├── Config/              # Configuración
│   ├── AgentConfig.cs
│   └── ConfigStore.cs
├── Models/              # Modelos de datos
│   ├── MetricsSnapshot.cs
│   └── Incident.cs
├── Services/            # Lógica de negocio
│   ├── MetricsCollector.cs
│   ├── FirebaseClient.cs
│   ├── AppBlocker.cs
│   └── RemoteUpdateService.cs
├── Program.cs           # Configuración DI
└── Worker.cs            # Loop principal
```

## 🧪 Testing

```powershell
# Ejecutar tests (cuando estén disponibles)
dotnet test

# Test de integración manual
# 1. Instalar servicio localmente
# 2. Verificar métricas en Firestore
# 3. Probar actualización remota
```

## 📋 Checklist para Pull Requests

- [ ] El código compila sin errores ni warnings
- [ ] Los cambios han sido probados localmente
- [ ] La documentación está actualizada (si aplica)
- [ ] El commit sigue las convenciones
- [ ] No se incluyen credenciales o datos sensibles
- [ ] Se actualizó la versión en `.csproj` (si es un release)
- [ ] Se actualizó `SERVICE_VERSION` en `Worker.cs` (si es un release)

## 🔒 Seguridad

### NUNCA subir al repositorio:

- `firebase-credentials.json`
- Archivos `config.json` con datos reales
- API keys o tokens
- Información de dispositivos reales

### Si accidentalmente subes información sensible:

1. Elimina el archivo del historial: `git filter-branch --force --index-filter "git rm --cached --ignore-unmatch ruta/archivo" --prune-empty --tag-name-filter cat -- --all`
2. Revoca las credenciales comprometidas
3. Notifica al equipo inmediatamente

## 🎯 Áreas de Mejora Prioritarias

### High Priority
- [ ] Implementar verificación SHA256 de actualizaciones
- [ ] Agregar tests unitarios
- [ ] Mejorar manejo de errores en `RemoteUpdateService`
- [ ] Implementar rollback automático

### Medium Priority
- [ ] Panel web para gestión de actualizaciones
- [ ] Actualizaciones programadas
- [ ] Notificaciones push cuando se complete actualización
- [ ] Logs estructurados (JSON)

### Low Priority
- [ ] Soporte para múltiples configuraciones por grupo
- [ ] Dashboard de métricas en tiempo real
- [ ] Exportación de reportes

## 💡 Ideas y Sugerencias

Si tienes ideas para nuevas funcionalidades:

1. Abre un [Issue](https://github.com/inventari-la-ferreria/Servei-inventari-agent/issues) con el tag `enhancement`
2. Describe el problema que resuelve
3. Propón una solución
4. Espera feedback antes de implementar

## 🐛 Reportar Bugs

Al reportar un bug, incluye:

1. **Descripción**: Qué esperabas vs qué ocurrió
2. **Pasos para reproducir**: Cómo provocar el error
3. **Logs**: Eventos relevantes de Windows Event Viewer
4. **Entorno**: Versión de Windows, .NET, versión del servicio
5. **Configuración**: Configuración relevante (sin datos sensibles)

## 📞 Contacto

Para preguntas o dudas:
- Abre un [Issue](https://github.com/inventari-la-ferreria/Servei-inventari-agent/issues)
- Contacta al equipo de desarrollo

¡Gracias por contribuir! 🎉
