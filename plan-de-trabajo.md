# Plan de Trabajo — Proyecto Final

**Nombre del proyecto:** Sin definir (se dejará para el final del desarrollo)
**Fecha de creación del plan:** 13 de agosto de 2026

---

## 1. Descripción general

Videojuego estilo *Galaga* en el que el jugador controla una nave que debe destruir enemigos que aparecen desde arriba y desde los costados de la pantalla, acumulando puntaje. El proyecto está pensado para presentarse en una feria de ciencias.

- **Modo de juego:** un jugador (sin multiplayer)
- **Competitivo:** sí, mediante un marcador que registra el tiempo (minuto:segundo) alcanzado
- **Vínculo con electrónica:** no

---

## 2. Condiciones de victoria y derrota

| Condición | Descripción |
|---|---|
| **Victoria** | Completar la quinta oleada |
| **Derrota** | Quedarse sin salud |

---

## 3. Loop general del juego

1. El juego inicia al presionar el botón **Play** desde el menú principal.
2. Cada oleada tiene un tiempo límite para derrotar a todos los enemigos que la componen.
3. Al superar una oleada (o al agotarse el tiempo), el jugador puede invertir sus puntos en mejoras antes de continuar.
4. El juego finaliza de forma natural al completar la quinta oleada (victoria) o al quedarse sin salud (derrota), mostrando una pantalla de fin de partida con opciones para reiniciar o volver al menú principal.

**Comportamiento al agotarse el tiempo de una oleada:**
Si el tiempo se acaba sin haber derrotado a todos los enemigos, el menú de mejoras aparece igualmente (no hay penalización ni derrota inmediata). Los enemigos que quedaron vivos **no desaparecen**: pasan a la siguiente oleada junto con los nuevos enemigos de esa oleada. Al ser enemigos "de arrastre", ya no otorgan puntos al ser derrotados en la oleada siguiente (solo los enemigos nuevos de esa oleada dan puntos).

**Nota de diseño:** se descartó el sistema de pausa (Quit/Restart/Main Menu a mitad de partida). Dado que una run dura como máximo ~5 minutos, no se justifica pausar el juego; los botones de "Restart" y "Main Menu" van a vivir en la pantalla de fin de partida (victoria/derrota) en su lugar.

---

## 4. Enemigos

| Tier | Puntos | Ataque | Cadencia | Movimiento / aparición |
|---|---|---|---|---|
| **Tier 1** | 1 | 1 bala hacia el jugador, velocidad media | 1 seg | Se mantiene arriba de la pantalla, se desplaza lentamente de izquierda a derecha (o viceversa). Aparece desde el costado superior (derecho o izquierdo) |
| **Tier 2** | 2 | 2 balas rápidas hacia el jugador, con 0.5 seg de retraso entre cada una | 1 seg | Aparece solo (no en grupo), desde casi cualquier punto de la pantalla (costados o arriba) |
| **Tier 3** | 3 | 3 balas rápidas (0.5 seg de retraso entre cada una) + 1 bala grande lenta al final (2 seg de retraso respecto a la 3ra bala) | — | Aparece desde arriba o desde los costados; una vez que entra en pantalla, se mueve de izquierda a derecha mientras dispara |
| **Tier 4** | 4 | Rayo láser que atraviesa toda la pantalla, apunta hacia el jugador con leve retraso | 1 seg | No se mueve. Aparece desde arriba, de a pocos y solo |
| **Tier 5** | 5 | No dispara | — | Mucha vida. Se mueve lentamente hacia el jugador con leve retraso. Aparece desde arriba o los costados |

*Pendiente:* Diseño de la progresión de qué tiers aparecen en cada una de las 5 oleadas.

---

## 5. Sistema de mejoras (Upgrade) y economía

- **Moneda del juego:** puntos.
- **Obtención:** cada enemigo destruido otorga puntos; los enemigos más fuertes (versiones superiores) valen más que los débiles.
- **Visualización:** el puntaje se muestra en la esquina superior derecha de la pantalla.
- **Uso:** entre oleadas, el jugador gasta los puntos acumulados para mejorar la nave, eligiendo entre:
  - Daño
  - Velocidad
  - Salud
- **Sistema de compra:** cada mejora tiene niveles con **costo creciente**; el jugador puede comprar cuantos niveles pueda pagar con los puntos que tenga en ese momento (no es una elección excluyente tipo "elegís una sola").

*Pendiente:* valores concretos de costo base y crecimiento por nivel de cada mejora (a balancear jugando), y el efecto numérico real de cada nivel (cuánto suma de daño/velocidad/salud).

Este sistema de progresión es también la **mecánica principal** del juego: la sensación de ir haciéndose más fuerte oleada tras oleada es lo que sostiene el interés y la diversión.

---

## 6. Requisitos físicos para la feria

- Proyector
- Mesa
- Cable HDMI

---

## 7. Cronograma

> Hoy: **13 de agosto de 2026**

| Etapa | Período | Objetivo |
|---|---|---|
| **1. Preproducción y diseño** | 13 ago – 24 ago | GDD básico, arte de referencia, diseño de la nave y enemigos, diseño de las 5 oleadas (qué tiers aparecen en cada una) |
| **2. Desarrollo del core** | 25 ago – 20 sep | Movimiento de la nave, disparo, spawn de enemigos por tier, sistema de salud, sistema de puntos, oleadas con temporizador y arrastre de enemigos no derrotados |
| **3. Sistema de mejoras (Upgrade)** | 8 sep – 20 sep | Tienda/panel de mejoras entre oleadas (Daño, Velocidad, Salud), balance de costos y efectos |
| **4. UI completa** | 15 sep – 24 sep | Menú principal, HUD (salud, puntos, oleada, tiempo), pausa, pantallas de victoria/derrota, marcador de tiempo |
| **5. Integración y pulido pre-feria** | 25 sep – 27 sep | Testeo general, corrección de bugs críticos, ensayo de la presentación con proyector |
| **✅ Proyecto funcionando** | **28 de septiembre de 2026** | Entrega/checkpoint: el juego debe estar jugable de punta a punta |
| **6. Feria de ciencias** | posterior al 28 sep | Presentación del proyecto |
| **7. Retoques post-feria** | **noviembre 2026** | Ajustes de balance, feedback recogido en la feria, arte final, sonido/música, corrección de bugs restantes |
| **✅ Publicación en Itch.io** | **13 de noviembre de 2026** | Build final publicada |

---

## 8. Puntos pendientes de definir

- [ ] Patrón de movimiento/aparición del enemigo Tier 3
- [ ] Progresión de oleadas: qué tiers de enemigos y en qué cantidad aparecen en cada una de las 5 oleadas
- [ ] Valores concretos de las mejoras (Daño, Velocidad, Salud) y su costo en puntos
- [ ] Arte final (sprites de nave, enemigos, fondos, UI) — temática espacial, estilo visualmente único
- [ ] Sonido y música
