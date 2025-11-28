# Prompt para Implementar Modo Examen en la Web

**Copia y pega este prompt en tu chat con la IA que gestiona tu proyecto Web (React):**

---

Actúa como un Desarrollador Senior de React y Firebase. Necesito implementar la funcionalidad **"Modo Examen"** en mi aplicación de gestión de inventario escolar.

### Contexto
Tenemos un servicio de Windows (Agente) instalado en los ordenadores del centro. Este agente sincroniza su estado con **Firebase Firestore** en la colección `pcs`.
Recientemente, se ha actualizado el agente (v1.0.45) para escuchar un nuevo campo llamado `examMode` (booleano) en el documento de cada PC.
- Si `examMode` es `true`: El PC bloquea el acceso a sitios de IA (ChatGPT, Gemini, etc.).
- Si `examMode` es `false`: El acceso es libre.

### Objetivo
Necesito actualizar el Dashboard Web para permitir a los profesores activar o desactivar este modo.

### Tareas a realizar

1.  **Actualizar Modelo de Datos**:
    - Modificar la interfaz TypeScript (probablemente `PC` o `Asset`) para incluir el campo opcional:
      ```typescript
      examMode?: boolean;
      ```

2.  **Interfaz de Usuario (Detalle de PC)**:
    - En la vista de detalle de un PC (`PcDetailPage` o similar), añadir un **Toggle/Switch** visual con la etiqueta "Modo Examen".
    - Este switch debe reflejar el estado actual del documento en Firestore.

3.  **Lógica de Actualización**:
    - Implementar la función que, al cambiar el switch, haga un `updateDoc` al documento del PC en Firestore cambiando el valor de `examMode`.

4.  **Funcionalidad Masiva (Opcional pero recomendada)**:
    - En la vista de lista de PCs o vista de Aula, añadir un botón "Activar Modo Examen en Aula".
    - Implementar una escritura por lotes (`writeBatch`) para poner `examMode: true` a todos los PCs de esa ubicación/aula a la vez.

### Stack Tecnológico
- **Frontend**: React, TypeScript.
- **Backend/DB**: Firebase Firestore (SDK v9 modular).
- **UI**: (Menciona aquí tu librería, ej: Material UI, Tailwind, etc.)

Por favor, genera el código necesario para estas tareas.

---
