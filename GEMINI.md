# Reglas y Directrices de Desarrollo del Proyecto (Space Wars)

Este documento contiene las reglas de arquitectura, patrones y precauciones críticas aprendidas durante el desarrollo de este juego arcade en Unity.

---

## 1. Reglas Críticas de C# en Unity (Evitar Errores Comunes)

### ⚠️ Prohibido usar el operador `??` con tipos derivados de `UnityEngine.Object`
* **Causa**: Unity sobrecarga el operador `==`, pero el operador nativo de C# `??` evalúa la referencia CLR subyacente. Los objetos destruidos o wrappers vacíos evalúan como no nulos para `??`, arrojando `MissingComponentException` o `NullReferenceException`.
* **Incorrecto**:
  ```csharp
  AudioSource src = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
  ```
* **Correcto**:
  ```csharp
  AudioSource src = GetComponent<AudioSource>();
  if (src == null) src = gameObject.AddComponent<AudioSource>();
  ```

### ⚠️ RenderTextures Temporales
* Al utilizar `RenderTexture.GetTemporary` y `RenderTexture.ReleaseTemporary`, **siempre** restablecer `RenderTexture.active` antes de liberar para evitar advertencias de *Releasing render texture that is set to be RenderTexture.active!*.
  ```csharp
  RenderTexture prev = RenderTexture.active;
  // ... operaciones ...
  RenderTexture.active = (prev != rt) ? prev : null;
  RenderTexture.ReleaseTemporary(rt);
  ```

---

## 2. Flujo de Compilación Rápida por Terminal (CLI Roslyn)

Para compilar y probar cambios en caliente sin depender del Editor de Unity ni esperar una compilación completa:

1. **Compilar con el compilador Roslyn integrado de Unity**:
   ```powershell
   & "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\NetCoreRuntime\dotnet.exe" "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\DotNetSdkRoslyn\csc.dll" "@Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.rsp"
   ```
2. **Copiar la DLL generada a AMBAS ubicaciones críticas**:
   * A la caché de scripts: `Library\ScriptAssemblies\Assembly-CSharp.dll`
   * A la carpeta del ejecutable independiente: `Build And Run\Proyecto final Ilán Fischer_Data\Managed\Assembly-CSharp.dll`
   ```powershell
   Copy-Item "Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.dll" "Library\ScriptAssemblies\Assembly-CSharp.dll" -Force
   Copy-Item "Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.dll" "Build And Run\Proyecto final Ilán Fischer_Data\Managed\Assembly-CSharp.dll" -Force
   ```
   *Nota*: El ejecutable standalone lee directamente de `Build And Run\..._Data\Managed\Assembly-CSharp.dll`. Si no se copia allí, las pruebas del ejecutable no reflejarán los cambios.

---

## 3. Arquitectura de Pantallas (1 Monitor vs 2 Monitores)

* **Modo Pantalla Única (Laptop sola)**:
  * Las cámaras del juego SIEMPRE renderizan en `targetDisplay = 0`.
  * La cámara secundaria del Leaderboard (`secondaryCam`) DEBE estar apagada (`enabled = false`, `gameObject.SetActive(false)`). Dejarla encendida con profundidad alta o máscara nula genera una pantalla negra que oculta el juego.
  * Los menús y tablas de overlay usan `RenderMode.ScreenSpaceOverlay` con `sortingOrder` alto (9999) y se activan/desactivan con `[F10]` o `[L]` (cerrándose con `[Esc]`).
* **Modo Doble Pantalla (Laptop + Monitor Grande / TV)**:
  * Activar el segundo display con `Display.displays[1].Activate()`.
  * Presionar `[F10]` intercambia `targetDisplay` entre 0 y 1 para alternar la visualización del juego y la tabla de clasificación.
  * **Consejo de Input Lag en TV**: Activar el "Modo Juego" en la TV o renombrar la entrada HDMI a "PC" para desactivar el post-procesamiento interno y reducir el retraso a menos de 5ms.

---

## 4. Síntesis Procedural de Sonidos (`AudioClip.Create`)

Para evitar depender de archivos `.wav` faltantes en el build y garantizar máxima fidelidad sonora:
* Sintetizar sonidos en código con `AudioClip.Create(nombre, totalSamples, 1, sampleRate, false)`.
* Generar las ondas matemáticamente (barridos exponenciales de frecuencia, transitorios de ataque de 3ms, sub-graves de 40Hz, armónicos cristalinos y ruido blanco filtrado determinístico).
* **Siempre normalizar**: `float norm = 0.85f / peak;` para evitar distorsión digital o clipping mientras se mantiene un volumen potente y limpio.

---

## 5. Cinemáticas y Efectos Arcade en Shmup

* **Animación de Muerte**:
  * Utilizar `Time.unscaledDeltaTime` para que las explosiones y fragmentos no se congelen si el juego pausa el tiempo (`Time.timeScale = 0`).
  * Secuencia en 3 fases: Espasmos/sacudidas $\to$ Detonación multicapa y expulsión de esquirlas $\to$ Dispersión y llamada al `GameManager`.
  * Las esquirlas físicas deben correr en componentes independientes (`PlayerDebrisPiece`) para que no se interrumpan si la nave es desactivada.
  * El temblor de cámara (`ShakeCamera`) debe proteger la posición original con bloques `try...finally` para garantizar que la cámara vuelva a su coordenada base.

