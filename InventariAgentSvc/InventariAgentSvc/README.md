# InventariAgentSvc - Política de incidencias

Este servicio monitoriza métricas del equipo (CPU, GPU, RAM, disco) y crea incidencias en Firestore cuando se superan ciertos umbrales.

## Cooldown de incidencias (anti-spam)

Se añadió una política configurable para evitar incidencias duplicadas:

- NewIncidentCooldownMinutes (por defecto 120): tiempo mínimo (minutos) entre la creación de incidencias NUEVAS del mismo tipo.
- RepeatUpdateCooldownMinutes (por defecto 60): tiempo mínimo (minutos) para volver a registrar (append) una actualización en una incidencia YA ABIERTA del mismo tipo.

Implementación:
- Si ya existe una incidencia abierta con el tag de métrica (p.ej. `cpu_usage_crit`), NO se crea otra. En su lugar, si pasó al menos `RepeatUpdateCooldownMinutes` desde la última actualización, se añade una entrada en `changes` y se actualiza `updatedAt`.
- Si no existe una incidencia abierta para esa métrica, se crea una nueva con el tag específico (p.ej. `ram_usage_warn`).

## Configuración

El archivo de configuración se guarda en:
```
C:\\ProgramData\\InventariAgent\\config.json
```

Ejemplo de sección relevante añadida por defecto:
```json
{
  "DeviceId": "",
  "DeviceName": "",
  "Thresholds": {
    "CpuTempWarn": 85,
    "CpuTempCrit": 95,
    "GpuTempWarn": 85,
    "GpuTempCrit": 95,
    "CpuUsageWarn": 85,
    "CpuUsageCrit": 95
  },
  "IncidentPolicy": {
    "NewIncidentCooldownMinutes": 120,
    "RepeatUpdateCooldownMinutes": 60
  }
}
```

Puedes editar estos valores y reiniciar el servicio para aplicarlos.

## Tags de métrica usados

Se añadió un tag específico a cada incidencia para poder agrupar/deduplicar:
- Temperatura CPU: `cpu_temp_warn`, `cpu_temp_crit`
- Temperatura GPU: `gpu_temp_warn`, `gpu_temp_crit`
- Uso CPU: `cpu_usage_warn`, `cpu_usage_crit`
- Uso RAM: `ram_usage_warn`, `ram_usage_crit`
- Espacio disco: `disk_space_warn`, `disk_space_crit`

Estos tags se utilizan para localizar incidencias abiertas y aplicar el cooldown.
