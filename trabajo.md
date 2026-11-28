# trabajo.md — Reparto de trabajo por hitos

Documento operativo para dos personas (o dos agentes) trabajando en paralelo sobre Sonar Submarine Crew.
Última actualización: 2026-11-28.

- `README.md` es la especificación de producto.
- `AGENTS.md` contiene las decisiones técnicas, la arquitectura y los hitos oficiales (0–7).
- Este archivo **reparte** esos hitos entre dos frentes y fija los contratos, la propiedad de archivos y el estado.

## 1. Cómo lo usa un agente

1. Lee `README.md`, `AGENTS.md` (§3.1 incluida) y este archivo, en ese orden.
2. Comprueba `git status` y `git log --oneline -5`.
3. Elige **una persona** (A o B). No mezcles ambas en la misma rama ni en el mismo commit.
4. Toma el hito pendiente más bajo de tu persona cuyas dependencias estén cumplidas. No adelantes hitos.
5. Al cerrar un hito: cumple su criterio de "hecho" (§7), actualiza la tabla de estado (§1) y haz un commit pequeño en español.
6. No hagas push salvo que se pida explícitamente.

Reglas de convivencia:

- Un hito no está "hecho" por compilar: exige la prueba o checklist que dice `AGENTS.md` §6.
- Los archivos compartidos (`GameEvents`, `ServiceLocator`) solo admiten cambios **aditivos** y se anuncian en el mensaje del commit.
- Las escenas y el prefab del proxy los toca una sola persona a la vez. Se trabaja con escenas aditivas y prefabs para no bloquearse (§6).
- Nada de `FindObjectOfType`/`GameObject.Find` en `Update`; comunicación por eventos o referencias inyectadas.
- Si una decisión del `AGENTS.md` estorba, no se cambia en silencio: se anota en §9 de `AGENTS.md` y se pregunta.

### Estado actual

| Hito | Persona | Estado | Nota |
|---|---|---|---|
| 0 — Base | A + B | Hecho (`260f1aa`) | `Bootstrap` con 4 slots, cámaras divididas, tripulante y casco estático |
| 1 — Submarino | A | Hecho | `SubmarineProxy`, `SubmarineFrame`, `ISubmarinePose`, rebase, confines y sonda en coordenadas de mapa; herramienta de Editor de montaje |
| 2 — Estaciones y salas | B | Hecho (`8e311fd`, en `main` por `4ada732`) | `StationBase`, `PowerSystem`, las cinco salas, las nueve estaciones y HUD por jugador; herramienta de Editor `SubmarineInteriorBuilder` |
| 3 — Navegación | A | Pendiente | |
| 4 — Objetivo | B | Hecho en rama `Hito-4` (sin fusionar) | Gancho con cabestrante y cámara de baja calidad (`RenderTexture`), pantalla en la estación, tesoro único, victoria y reinicio; herramienta de Editor `Hito4Setup` |
| 5 — Daños | B | Pendiente | |
| 6a — Entorno | A | Pendiente | |
| 6b — Pulido, HUD y audio | B | Pendiente | Bloqueado por el hito 3 de A para los *blips* |
| 7a — PlayMode y playtest | A | Pendiente | |
| 7b — EditMode de reglas | B | Pendiente | |

## 2. Contratos compartidos

El hito 0 ya dejó parte del contrato. Lo que falta se enumera aquí; es lo primero que se cierra si alguien se incorpora tarde.

| Contrato | Estado | Dueño | Consumidor | Nota |
|---|---|---|---|---|
| `IStation` | Existe | B | `CrewController` | Solo `TryOccupy`/`Release`/estado de ocupación |
| `StationBase` + ocupación única | Pendiente (hito 2) | B | A | Bloqueo por sala dañada y por falta de energía |
| `IPowerState` | Existe | A | `SubmarineMotor` | `HasPower` + `PowerChanged` |
| `PowerStateStub` | Existe | A | — | Siempre con energía; B lo sustituye en el hito 2 por `PowerSystem` |
| `PowerSystem` | Pendiente (hito 2) | B | A, estaciones | Implementa `IPowerState` |
| `GameEvents` | Existe | Compartido | Ambos | Solo cambios aditivos; hoy tiene jugador, estación, `HullImpacted`, fugas, alarma y `TreasureRecovered` (hito 4) |
| `TreasureCollectible` + `GameEvents.TreasureRecovered` | Existe (hito 4) | B | `GrappleHook`, `GameFlow` | Tesoro único bajo `WorldRoot`; se registra en el `ServiceLocator` para que el gancho lo resuelva |
| `HullZone` / `DamageLevel` | Existen (`Core/GameTypes.cs`) | B | `DamageSystem`, A | No mover de sitio; A los lee |
| `SonarContact` + evento de contactos | Pendiente (hito 3) | A | B (HUD) | Struct + lista inmutable por emisión |
| `ISubmarinePose` | Existe | A | B (2, 4, 6b) | `MapPosition`, `Yaw`, `Velocity`, `WorldToSubmarine`, `SubmarineToWorld` |
| `SubmarineFrame` + `WorldRoot` | Existe | A | B | Pose inversa y rebase; única autoridad del `WorldRoot` |
| `SubmarineProxy` con collider por sala | Existe | A | B (hito 5) | Cada collider hijo con su `HullZone` |
| Capas `Interior`, `HullProxy`, `World`, `Hazard` | Existe | A | Ambos | En `ProjectSettings/TagManager.asset` |

Regla de oro: B nunca lee el `transform` del proxy ni del motor; usa `ISubmarinePose`. A nunca escribe en `Stations/` ni en `UI/`.

## 3. Persona A — vehículo, percepción y entorno

Hitos oficiales: **1, 3, 6a, 7a**. Orden interno obligatorio: **1 → 3 → 6a → 7a**.

| Hito | Contenido | Depende de | Entrega a B |
|---|---|---|---|
| 1 | `SubmarineProxy` (Rigidbody sin gravedad, collider hijo por sala con `HullZone`), `SubmarineFrame`, `WorldRoot`, `ISubmarinePose`, rebase, confines y sonda de terreno en coordenadas de mapa, motor y timón/palancas sobre el proxy | 0 | Pose de mapa y proxy con colliders por sala |
| 3 | `SonarSystem` (`SphereCast`/`OverlapSphere` con máscara `World`/`Hazard`), `SonarContact`, altitud y coordenadas de `MapPosition` | 1 | Evento de contactos para HUD y mapa |
| 6a | Rocas, columnas, arcos estrechos, corrientes laterales/verticales y minas, todo bajo `WorldRoot` y en capas `World`/`Hazard` | 1 y 3 | Hazards reales que el sónar ya detecta |
| 7a | PlayMode: arranque, 4 slots con gamepad, rebase, corrientes y minas, cadena colisión→fuga con los sistemas de B; checklist manual con 1 y 4 gamepads | 0–6a y los hitos 2, 4 y 5 de B | — |

Notas del hito 1 (cerrado el 2026-11-26):

- El objeto `Submarine` de `Bootstrap` pasa a ser el `SubmarineProxy`; conserva `Rigidbody`, `Capsule` y `SubmarineMotor`.
- Añadir un collider hijo por sala (cinco) con su `HullZone`; son el punto de contacto que B mapea a daño.
- `SubmarineFrame` copia la pose inversa completa al `WorldRoot` en `FixedUpdate`; el interior no se toca.
- `ISubmarinePose.MapPosition` acumula recorrido y **no** se rebasa: confines, sonda, sónar y mapa lo usan. `SubmarineMotor` ya limita con `MapPosition`, no con la posición del `Rigidbody`, que se rebasa.
- Rebase cada `rebaseDistance` (2000 m por defecto) con interpolación apagada durante el salto.

Notas del hito 3: el sónar no pinta HUD; entrega contactos y posición. El mapa de papel y los *blips* son de B (6b).

Notas del hito 6a: las corrientes aplican fuerza al proxy; las minas aplican impulso al proxy. Ninguna malla del mapa puede quedar fuera del `WorldRoot`.

## 4. Persona B — estaciones, estado y UI

Hitos oficiales: **2, 4, 5, 6b, 7b**. Orden interno obligatorio: **2 → 4 → 5 → 6b → 7b**.

| Hito | Contenido | Depende de | Entrega a A |
|---|---|---|---|
| 2 | `StationBase` + ocupación única por proximidad y liberación, `PowerSystem` real, las cinco salas con sus estaciones | 0 | `IPowerState` real (sustituye al stub) |
| 4 | Un único tesoro, `GrappleStation` + gancho estático + cámara de baja calidad (`RenderTexture`), victoria y reinicio en `GameFlow` | 2 · `ISubmarinePose` (1 de A) | Estado objetivo→victoria |
| 5 | `DamageSystem` (impulso y punto de contacto del proxy → `HullZone`/severidad), `IntegrityState`, los cinco efectos por sala, fallo 50/50 de máquinas, inundación en ambos modos con bloqueo del ajuste, chapas y derrota | 2 · proxy con colliders (1 de A) | `IntegrityState` y eventos para el HUD |
| 6b | HUD de integridad, `MapHUD` con marcador y *blips*, `AlarmHUD`, alarmas y audio, feedback visual | 5 · hito 3 de A | — |
| 7b | EditMode: daño punto→sala, los cinco efectos, 50/50, inundación en ambos modos y bloqueo, arbitraje de ocupación, transición a victoria | 2, 4 y 5 | — |

Notas del hito 2: `CrewController` ya detecta estación cercana y pide ocupación; `StationBase` debe ser quien arbitre el dueño único y libere al alejarse. `PowerSystem` implementa `IPowerState` y decide qué estaciones quedan sin energía.

Notas del hito 4 (cerrado el 2026-11-28, rama `Hito-4`): el gancho (`GrappleHook`) es equipo del casco y cuelga por debajo del suelo en el marco estático del interior, como el periscopio: nada se parenta a un transform móvil (§3.1). Por eso la captura se mide en el mundo dibujado entre la punta del gancho y el cofre, sin depender de `ISubmarinePose`. La estación manda en remoto (stick derecho ajusta el cable, `[X]` suelta/iza del todo) y **cede la vista al ocupante como el periscopio**: cámara fija bajo el casco, sin rotación, con máscara `World`/`Hazard`/`HullProxy` para verse el propio gancho; no hay pantalla en el puesto. El tesoro cuelga de `WorldRoot` y `Hito4Setup` lo coloca 6 m por debajo del casco (a esa cota el cabestrante siempre llega, aunque el fondo del mapa quede más abajo). El reinicio recarga la escena de arranque, así que `Bootstrap` debe estar en Build Settings (lo añade la herramienta).

Notas del hito 5: `DamageSystem` se añade al prefab `SubmarineProxy` (propiedad de A; se coordina la edición del prefab). Los cinco efectos se gatean desde `DamageSystem`/`IntegrityState`, no desde cada estación.

Notas del hito 6b: los *blips* del sónar consumen el evento de A; el marcador del mapa usa `MapPosition` y el tamaño de mapa acordado (A). El audio y las alarmas son de B.

## 5. Integración y bloqueos

| Punto | Cuándo | Qué necesita |
|---|---|---|
| `ISubmarinePose` disponible | Al cerrar el hito 1 de A | B puede empezar el 4 y preparar el 5 |
| Colliders por sala en el proxy | Al cerrar el hito 1 de A | B puede cerrar el 5; sin ellos, usa el prefab proxy con colliders de prueba |
| `SonarContact` | Al cerrar el hito 3 de A | B cierra los *blips* y el mapa del 6b |
| Hazards reales | Al cerrar el 6a de A | Sónar útil y minas que dañan |
| `PowerSystem` real | Al cerrar el hito 2 de B | A quita el stub y el motor respeta el fallo eléctrico |

Un hito de B no se cierra si depende de un contrato de A que aún no existe; se deja el consumo detrás de la interfaz y se prueba con un stub propio.

## 6. Propiedad de archivos y escenas

| Ruta | Dueño | Regla |
|---|---|---|
| `Assets/Scenes/Bootstrap.unity` | A | B no la edita; entrega prefabs y A los instancia |
| `Assets/Scenes/SubmarineTest.unity` | A | Mapa a mano |
| `Assets/Scenes/SubmarineInterior.unity` | B | Casco y salas; se carga aditiva desde `Bootstrap` |
| `Assets/Scripts/Core/GameEvents.cs` | Compartido | Solo aditivo; anunciar el cambio |
| `Assets/Scripts/Core/GameTypes.cs` | B | `HullZone`, `DamageLevel`; A solo lee |
| `Assets/Scripts/Core/GameFlow.cs` | B | Victoria, derrota y reinicio |
| `Assets/Scripts/Core/ServiceLocator.cs` | Compartido | Solo aditivo |
| `Assets/Scripts/Multiplayer/PlayerSlot.cs`, `InputRouter.cs`, `CameraRig.cs` | A | Entrada y cámaras |
| `Assets/Scripts/Multiplayer/CrewController.cs` | B | Desde el hito 2; A no lo toca salvo bug del hito 0 |
| `Assets/Scripts/Submarine/SubmarineMotor.cs`, `SubmarineMotorConfig.cs`, `SubmarineProxy.cs`, `SubmarineFrame.cs`, `ISubmarinePose.cs`, `PowerStateStub.cs` | A | Physics, pose y proxy |
| `Assets/Scripts/Submarine/PowerSystem.cs`, `DamageSystem.cs`, `FloodingSystem.cs`, `IntegrityState.cs` | B | Estado y reglas |
| `Assets/Scripts/Stations/*` | B | `IStation`, `StationBase`, timón, palancas, periscopio, gancho y estación de sónar |
| `Assets/Scripts/Submarine/SonarSystem.cs` | A | Detección; su HUD es de B |
| `Assets/Scripts/Environment/*` | A | Rocas, columnas, arcos, corrientes y minas |
| `Assets/Scripts/Environment/TreasureCollectible.cs` | B | Excepción: pertenece al objetivo (hito 4) |
| `Assets/Scripts/UI/*` | B | Todo el HUD |
| `Assets/Data/` | A: motor, sónar, rebase · B: daño, inundación/agua, energía, estaciones | Un ScriptableObject no se toca sin avisar |
| `Assets/Prefabs/SubmarineProxy.prefab` | A | B añade componentes en ventana acordada |
| `Assets/Prefabs/Station_*`, `Treasure` | B | |
| `Assets/Prefabs/Rock`, `Column`, `TerrainArch`, `Current`, `Mine` | A | |
| `ProjectSettings/TagManager.asset` | A | Capas y etiquetas |
| `Assets/Tests/EditMode/*` | A: pose, rebase, sónar · B: daño, inundación, energía, ocupación, victoria | |
| `Assets/Tests/PlayMode/*` | A | B añade casos de estación previa coordinación |

Escenas: como Unity no fusiona escenas, se trabaja en escenas aditivas (`SubmarineInterior` para B) y con prefabs. Si hay que tocar la escena del otro, se hace cuando esa persona no la tenga abierta y se avisa antes.

## 7. Definición de "hecho" por hito

Copiada de `AGENTS.md` §6 para tenerla a mano:

| Hito | Hecho cuando |
|---|---|
| 0 | Arranca con 1 y 4 gamepads; cada jugador se mueve de forma independiente y ninguna cámara cuelga de un transform móvil |
| 1 | Manejo estable en `FixedUpdate` sin atravesar el suelo; el interior no se mueve; el mundo cruza el umbral de rebase sin deriva ni jitter y conserva `MapPosition` |
| 2 | Cualquier jugador llega a cualquier estación libre; no se puede arrebatar una ocupada; la sala dañada aplica su efecto y la falta de energía desactiva las estaciones dependientes |
| 3 | Contactos visibles en HUD y marcador coherente con la posición de mapa, también tras un rebase |
| 4 | Partida completable en el mapa a mano con un único tesoro |
| 5 | Leve/grave/extremo cumplen la spec; en "nunca baja" el agua se mantiene; por defecto baja sin daños; bloqueo del ajuste tras el inicio |
| 6 | Partida legible, sin fauna, sin placeholders de texto y con todo el mapa bajo `WorldRoot` |
| 7 | Tests en verde y checklist manual completado con 1 y 4 gamepads |

## 8. Riesgos conocidos

- **Escenas de Unity**: no se fusionan. Si ambos editan `Bootstrap.unity`, el conflicto es manual; por eso la escena es de A y B trabaja en `SubmarineInterior` y prefabs.
- **Coordenadas después del rebase**: cualquier código que use `transform.position` del proxy se rompe en silencio al rebasar. Solo `MapPosition` es estable.
- **Contrato de daño**: si B empieza el hito 5 sin los cinco colliders por sala, el mapeo punto→sala hay que repetirlo. Es la primera entrega del hito 1.
- **Física y pose**: mover el `WorldRoot` con interpolación activa produce jitter; el rebase va en `FixedUpdate` y sin interpolación durante el salto.
- **UI antes de tiempo**: el HUD del hito 6b no debe adelantarse al evento `SonarContact` del hito 3; mientras no exista, B usa datos falsos solo en tests.
- **Mandos fantasma**: algunos drivers reconectan el mando cada pocos frames y `Gamepad.all` cambia de tamaño. `GameFlow` exige que el número de mandos se mantenga `playerSettleTime` (1,5 s) antes de partir o juntar la pantalla, e `InputRouter` agrupa los eventos de dispositivo en un solo refresco por frame. Si vuelve a aparecer un jugador de más, el log `[SonarCrew] Jugadores en partida` da nombre e id de cada mando.
