# Instalación de InventariAgentSvc (Windows)

Este instalador EXE único configura automáticamente el servicio InventariAgent en Windows.

## Requisitos
- Windows 10/11 o Windows Server
- Credenciales de Firebase incluidas en el instalador

## Pasos de instalación
1. Copia `InventariInstaller.exe` al PC destino
2. Doble clic en `InventariInstaller.exe` y acepta el UAC
3. El instalador:
   - Copia los archivos a `C:\Program Files\InventariAgent`
   - Crea el servicio Windows `InventariAgent`
   - **Muestra un menú para seleccionar el PC** de la lista de dispositivos en Firestore
   - Guarda la configuración automáticamente
   - Inicia el servicio

## Durante la instalación
- Te pedirá seleccionar el dispositivo escribiendo su ID (ejemplo: PC-AULA-001)
- Se mostrarán todos los PCs disponibles en Firestore
- Una vez seleccionado, guardará automáticamente la configuración y arrancará el servicio

## Configuración manual (opcional)
Si necesitas cambiar el dispositivo después:
1. Detén el servicio:
   ```powershell
   Stop-Service InventariAgent
   ```
2. Ejecuta el configurador:
   ```powershell
   cd "C:\Program Files\InventariAgent"
   .\configure-device.ps1
   ```
3. Inicia el servicio:
   ```powershell
   Start-Service InventariAgent
   ```

## Desinstalación
```powershell
Stop-Service InventariAgent
sc.exe delete InventariAgent
Remove-Item 'C:\Program Files\InventariAgent' -Recurse -Force
# (Opcional) Remove-Item 'C:\ProgramData\InventariAgent' -Recurse -Force
```

## Notas
- El instalador incluye las credenciales de Firebase embebidas
- Los campos `location`, `tag` y `brand_model` configurados en Firestore se preservan
- Solo se actualizan las especificaciones técnicas (CPU, RAM, GPU, almacenamiento, OS)
