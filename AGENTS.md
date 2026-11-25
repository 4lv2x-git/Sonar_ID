# AGENTS.md — Sonar Submarine Crew

## 1. Fuente de verdad y alcance

- `README.md` es la especificación de producto. Léelo completo antes de escribir código; `AGENTS.md` contiene las decisiones técnicas y el plan.
- **Decisiones confirmadas para la versión inicial**: cooperativo local con gamepads, roles libres y tripulación que se mueve por el submarino, cinco salas concretas, mapa estático sin fauna y regla del agua configurable.
- **Submarino estático (decidido)**: el interior del casco no se mueve. Un `SubmarineProxy` con `Rigidbody` recorre el mapa y `SubmarineFrame` desplaza el mundo con su pose inversa (§3.1). No se reabre sin petición explícita.
- `trabajo.md` recoge el reparto de trabajo entre las dos personas y el estado de cada hito; es el punto de partida para cualquier agente que se incorpore.
- **Versión inicial (todo lo que hay que construir ahora)**: 1-4 jugadores locales, un submarino, un mapa hecho a mano con hazards estáticos, sónar, mapa de papel y sensores, periscopio, timón/palancas, gancho recolector, pantalla de integridad, colisiones con daños leve/grave/extremo, inundación configurable, reparación con chapas metálicas y victoria al recuperar el tesoro.
- **Fuera de alcance, no implementar**: roguelike, modificadores permanentes, ciclo de misiones, torpedos, sala de carga y lanzador, multijugador online, fauna/enemigos/peces y cualquier red casera. Son visión de versiones futuras.

## 2. Estado real del repositorio

- Unity `6000.6.7f1`, URP `17.6.0`, Input System `1.20.1`, uGUI `2.6.0`. No actualizar paquetes ni el editor sin permiso explícito.
- La plantilla URP conserva `Assets/Scenes/SampleScene.unity`, pero ya hay base jugable: `Bootstrap` con 4 `PlayerSlot`, cámaras divididas y casco estático; `Scripts/` con `Core`, `Multiplayer`, `Stations` (solo `IStation`) y `Submarine` (`SubmarineMotor`, `SubmarineMotorConfig`, `IPowerState` + `PowerStateStub`). El hito 1 está en curso: el motor mueve el proxy `Submarine` y faltan `SubmarineFrame`, `ISubmarinePose` y `WorldRoot`.
- `activeInputHandler: 1` → Input System activo. No hay paquetes de red: el online está **diferido**, así que no añadir Netcode/Relay/Lobby en esta versión.
- Cambios ya presentes en el working tree que **no se deben revertir**: `com.unity.ai.assistant` y `com.unity.ai.inference` en `Packages/manifest.json`, la eliminación de `Assets/Editor/HubForceResolve.cs(.meta)` y el trabajo del hito 1 sin commitear (`Assets/Scripts/Submarine/`, `Assets/SubmarineMotorConfig.asset`, `Assets/Scripts/Core/GameTypes.cs` y los cambios en `Bootstrap.unity`, `GameEvents.cs` e `InputRouter.cs`).
- No editar `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vs/` ni los `.csproj`/`.slnx` generados. Son artefactos.

## 3. Arquitectura de multijugador (decidida, no reabrir)

- **Local** de 1 a 4 jugadores con **un gamepad por jugador** y pantalla dividida (`Camera.rect`). Sin soporte de teclado como control de juego en la versión inicial.
- Cada `PlayerSlot` posee un gamepad y una cámara en primera persona. `InputRouter` reparte dispositivos por índice; `CameraRig` aplica el rect correspondiente.
- **Roles libres**: no hay puestos fijos. El tripulante se desplaza por el interior del submarino y usa cualquier mando cercano pulsando interactuar. Una estación solo puede ser ocupada por un jugador a la vez; al alejarse o soltarla se libera.
- El interior del submarino es **estático**: ningún jugador, cámara ni estación cuelga de un transform móvil, así que el movimiento interno es trivialmente relativo al casco. El mundo (terreno y hazards) cuelga de un único `WorldRoot` que `SubmarineFrame` desplaza con la pose inversa del `SubmarineProxy` (§3.1).
- Aislar entrada, ocupación de estaciones y cámaras detrás de interfaces. El online posterior se añadirá sobre esas interfaces, nunca con un parche de red improvisado.

### 3.1 Marco de referencia: proxy físico y mundo móvil

- `SubmarineProxy`: `Rigidbody` **sin gravedad**, con un collider hijo por sala etiquetado con su `HullZone`. Es lo único que se mueve y lo que recibe `SubmarineMotor`, corrientes y colisiones. En la escena actual el proxy es el objeto `Submarine` con su `Capsule`.
- El **interior es estático**: suelo, paredes, estaciones, jugadores y cámaras no cuelgan de ningún transform móvil. El `CharacterController` de la tripulación no hereda velocidad ni rotación del casco.
- `SubmarineFrame` copia en `FixedUpdate` la **pose inversa completa** (posición y rotación) del proxy al `WorldRoot`, y es la única autoridad sobre ese transform. Todo el mapa —terreno, rocas, columnas, corrientes, minas y tesoro— cuelga de `WorldRoot`; el proxy queda fuera.
- Consecuencia visual asumida: la cabina no se inclina aunque el casco reciba un impacto; lo que se mueve es el horizonte. Si el hito 6 quiere balanceo, se inclina solo la raíz visual del interior, nunca los colliders ni el `CharacterController`.
- **Rebase**: cada `rebaseDistance` (ScriptableObject, 2000 m por defecto) `SubmarineFrame` traslada `WorldRoot` y `SubmarineProxy` juntos hacia el origen, en `FixedUpdate` y con la interpolación apagada durante el salto. El rebase no altera `MapPosition`.
- API única para el resto de sistemas: `ISubmarinePose { MapPosition, Yaw, Velocity, WorldToSubmarine, SubmarineToWorld }`. `MapPosition` acumula el recorrido y **no se rebasa**: es la coordenada del mapa, del sónar y del gancho. Leer el `transform` del proxy fuera de `SubmarineFrame` está prohibido.
- Los confines del mapa y la sonda de terreno se evalúan en `MapPosition`, no en la posición del transform del proxy.
- Capas en `ProjectSettings/TagManager.asset`: `Interior`, `HullProxy`, `World`, `Hazard`. Las máscaras de sónar, gancho y sonda de terreno excluyen `Interior`.

## 4. Estructura y salas del submarino

```text
Assets/
  Scenes/            # Bootstrap.unity (arranque y sistemas), SubmarineInterior.unity (casco y salas, aditiva), SubmarineTest.unity (mapa a mano)
  Prefabs/           # SubmarineInterior, SubmarineProxy, CrewPlayer, Station_*, Rock, Column, TerrainArch, Current, Mine, Treasure
  Data/              # ScriptableObjects de balance (daño, inundación, sónar, motor, agua, rebase)
  Scripts/
    Core/            # GameFlow, GameEvents, GameTypes (HullZone, DamageLevel), ServiceLocator mínimo
    Submarine/       # SubmarineMotor, SubmarineProxy, SubmarineFrame, ISubmarinePose, PowerSystem, DamageSystem, FloodingSystem, IntegrityState
    Stations/        # IStation, StationBase, Wheel, Levers, Sonar, Periscope, Grapple
    Environment/     # Rock, Column, TerrainArch, Current, Mine, TreasureCollectible (sin fauna ni IA)
    UI/              # HUD por jugador, MapHUD, IntegrityHUD, AlarmHUD
    Multiplayer/     # PlayerSlot, InputRouter, CameraRig, CrewController
  Tests/             # EditMode y PlayMode
```

Jerarquía en runtime: `Bootstrap` carga `SubmarineInterior` de forma aditiva; todo el mapa cuelga de `WorldRoot`; el `SubmarineProxy` queda fuera del `WorldRoot` y el interior nunca se parenta a él.

Salas y contenido obligatorio:

| Sala | Estaciones / elementos |
|---|---|
| Sala de mandos | Timón (yaw) y periscopio |
| Sala de sensores | Sónar y sensores |
| Sala de máquinas | Motor del submarino y suministro eléctrico |
| Sala de información/documentación | Mapa en papel e información del submarino |
| Sala del gancho recolector | Gancho y cámara de baja calidad |

- Namespace `SonarCrew.<Modulo>`. Un `.asmdef` por módulo es opcional; si se añade, actualizar referencias y no romper los Tests.
- Configuración numérica (umbrales de daño, velocidades, radio de sónar, capacidad de chapas, modo de agua) en `ScriptableObject`, nunca literal repetido.

## 5. Sistemas y responsabilidades

| Sistema | Responsabilidad | Dependencias |
|---|---|---|
| `CrewController` | Movimiento en primera persona dentro del submarino, detección de estación cercana, liberación al alejarse. | `PlayerSlot`, `InputRouter` |
| `StationBase` / `IStation` | Dueño único, *prompt*, `TryOccupy`/`Release`, bloqueo si la sala está dañada o sin energía. | `CrewController`, `HullZone` |
| `PlayerSlot` / `InputRouter` / `CameraRig` | 4 *slots*, un gamepad por slot, rects de cámara, arbitraje de ocupación. | Input System |
| `SubmarineMotor` | Empuje avance/retroceso, ascenso/descenso, timón (yaw) y arrastre sobre el `Rigidbody` del **proxy**; confines y sonda de terreno en coordenadas de mapa. Solo física en `FixedUpdate`. | `SubmarineProxy`, mandos de la sala de mandos, `PowerSystem` |
| `SubmarineProxy` / `SubmarineFrame` / `ISubmarinePose` | Collider hijo por sala, pose inversa del `WorldRoot`, rebase y pose de mapa; el resto de sistemas leen solo la interfaz. | `SubmarineMotor`, `TagManager` |
| `PowerSystem` | Suministro eléctrico de la sala de máquinas; decide qué estaciones quedan sin energía. | `HullZone` |
| `SonarSystem` | `SphereCast`/`OverlapSphere` desde la pose del proxy, filtrado por capas (`World`/`Hazard`, nunca `Interior`); lista de contactos y *blips* en HUD. Detecta terreno y hazards estáticos; sin fauna. | `GameEvents`, `PowerSystem`, `ISubmarinePose` |
| `NavigationSystem` | Coordenadas y altitud desde `ISubmarinePose.MapPosition`; marcador sobre el mapa de papel de la sala de información. | `ISubmarinePose` |
| `PeriscopeStation` | Cámara fija en el interior estático; solo rota su mira 360º hacia el `WorldRoot`; cesión temporal de la vista al ocupante. | `CameraRig`, `PowerSystem`, `ISubmarinePose` |
| `GrappleStation` | Gancho estático en el interior + cámara de baja calidad (`RenderTexture` pequeña); alcance y distancia en marco submarino vía `ISubmarinePose.WorldToSubmarine`; recoger tesoro. | `TreasureCollectible`, `ISubmarinePose` |
| `DamageSystem` | Mapear `OnCollisionEnter` de los colliders hijo del proxy (impulso y punto de contacto) a `HullZone`/sala y a nivel leve/grave/extremo; emitir eventos. | `IntegrityState`, `SubmarineProxy` |
| `FloodingSystem` | Apertura de fugas por sala dañada, reparación con chapas y derrota. Respeta el modo de agua configurado. | `DamageSystem` |
| `IntegrityHUD` | Vista de águila esquemática, salas marcadas, alarmas y funcionalidades perdidas. | `IntegrityState`, `PowerSystem` |
| `GameFlow` | Objetivo → victoria; daño extremo o inundación → derrota; reinicio de partida. | Todos |

### Reglas de estado

- Daño **leve**: solo alarma, sin pérdida de integridad. Daño **grave**: alarma + fuga + pérdida de función según la sala. Daño **extremo**: derrota inmediata.
- Una sala con daño grave sin reparar aplica **exactamente** este efecto:

| Sala dañada | Efecto funcional |
|---|---|
| Sala de mandos | Se pierde el control del timón: imposible girarlo y el submarino queda quieto. El periscopio se puede usar, pero **no se puede rotar la cámara**. |
| Sala de sensores | El sónar deja de funcionar; los sensores dan datos **erróneos y aleatorios**. |
| Sala de máquinas | Al producirse el daño se elige al azar (50%/50%): **paro completo del motor** (el submarino deja de acelerar) o **fallo eléctrico** (pierden energía las estaciones dependientes). |
| Sala de información/documentación | El mapa muestra daños **falsos**, suenan alarmas ocasionalmente y la información mostrada no es precisa. |
| Sala del gancho recolector | El gancho se retrae automáticamente y deja de funcionar. |

- Reparar con una chapa cierra la fuga y restaura la función de esa sala. Si se dañan varias salas a la vez se aplican todos sus efectos; no hay precedencia definida entre ellos.
- **Agua** (`WaterConfig.aguaNuncaBaja`):
  - **Valor por defecto: desactivado** → el nivel baja lentamente mientras el submarino no tenga daños activos.
  - **Activado**: el nivel nunca baja, incluso cuando no hay daños ni fugas.
  - El modo se configura **antes de empezar la partida y queda bloqueado durante la misma**; no se puede alternar en caliente.
  - Reparar una chapa cierra la fuga y restaura la función; **no reduce el agua por sí mismo**. La reducción, si la hay, la decide únicamente el modo configurado.
- La colisión debe influir en trayectoria y rotación mediante el impulse físico, no con animaciones falsas: el impulso se aplica al `Rigidbody` del proxy y la rotación visible es la de la pose en `SubmarineFrame`.
- El entorno de la versión inicial es estático: **rocas, columnas, arcos estrechos de terreno, corrientes laterales/verticales que empujan el submarino y minas**. **No crear peces, ballenas ni IA de fauna.**

## 6. Orden de implementación y definición de "hecho"

1. **Hito 0 — Base**: escena `Bootstrap`, iluminación/agua/niebla URP, interior estático placeholder con `SubmarineProxy` de prueba, 4 `PlayerSlot` con gamepad, 4 `CameraRig` con `Camera.rect` y `CrewController` caminando dentro del casco. *Hecho*: arranca con 1 y 4 gamepads; cada jugador se mueve de forma independiente y ninguna cámara cuelga de un transform móvil.
2. **Hito 1 — Submarino**: `SubmarineMotor` + `SubmarineProxy` + `SubmarineFrame` + `ISubmarinePose` + timón/palancas + confines del mapa. *Hecho*: manejo estable en `FixedUpdate` sin atravesar el suelo; el interior no se mueve; el mundo se desplaza sin deriva ni jitter y cruza el umbral de rebase conservando `MapPosition`.
3. **Hito 2 — Estaciones y salas**: framework `IStation`, ocupación por proximidad y liberación, cinco salas del §4 y `PowerSystem`. *Hecho*: cualquier jugador llega a cualquier estación libre; una estación ocupada no puede ser arrebatada; la sala dañada aplica el efecto del §5 y la falta de energía desactiva las estaciones dependientes.
4. **Hito 3 — Navegación**: sónar de terreno/hazards desde la pose del proxy, mapa de papel, altitud y coordenadas de `ISubmarinePose.MapPosition`. *Hecho*: contactos visibles en HUD y marcador coherente con la posición de mapa, también después de un rebase.
5. **Hito 4 — Objetivo**: **un único tesoro** + gancho + cámara de baja calidad + victoria. *Hecho*: partida completable con el mapa a mano (varios objetivos quedan para versiones futuras).
6. **Hito 5 — Daños**: zonas/salas, umbrales, alarmas, los cinco efectos por sala, fallo 50/50 de máquinas, inundación en **ambos modos** con bloqueo tras el inicio, chapas y derrota. *Hecho*: leve/grave/extremo cumplen la spec; en modo "nunca baja" el agua se mantiene; con el modo por defecto baja sin daños.
7. **Hito 6 — Entorno y pulido**: rocas, columnas, arcos estrechos, corrientes laterales/verticales y minas, todos bajo `WorldRoot`; HUD de integridad por salas, alarmas y audio, feedback visual. *Hecho*: partida legible, sin fauna y sin placeholders de texto; ninguna pieza del mapa fuera del `WorldRoot`.
8. **Hito 7 — Verificación**: tests y playtest con 1 y 4 gamepads. *Hecho*: tests en verde y checklist manual completado.

No avanzar al hito siguiente si el anterior no cumple su "hecho".

## 7. Convenciones de código

- C# con `namespace SonarCrew.*`, PascalCase para tipos/métodos, `_camelCase` para campos privados, `[SerializeField]` para referencias de escena.
- Comunicación entre sistemas con eventos C# (`GameEvents`) o referencias inyectadas; **nada de** `FindObjectOfType`/`GameObject.Find` dentro de `Update`.
- Física del submarino en `FixedUpdate`; movimiento/interacción del tripulante y UI en `Update` o callbacks del Input System.
- El `WorldRoot` solo lo mueve `SubmarineFrame` en `FixedUpdate`; prohibido mover el interior o parentar tripulación al proxy. Las coordenadas de diseño del mapa son las de `ISubmarinePose.MapPosition`, independientes del rebase.
- Las capas `Interior`, `HullProxy`, `World` y `Hazard` se documentan en `ProjectSettings/TagManager.asset`; el interior nunca entra en las máscaras de sónar, gancho ni sonda de terreno.
- Un gamepad por `PlayerSlot`; no asumir teclado. Usar `InputAction` por jugador con `PlayerInput` o un `InputRouter` explícito.
- Usar `LayerMask` para sónar/colisiones; documentar etiquetas y capas en `ProjectSettings/TagManager.asset` si se crean nuevas.
- UI con uGUI. No asumir TextMeshPro disponible sin verificarlo en el manifest.
- Los `.meta` los genera Unity: no editar GUIDs a mano; mover/renombrar archivos desde el Editor o con `AssetDatabase`.
- No editar escenas/`.asset` a mano salvo que sea imprescindible; si se hace, conservar YAML y referencias intactas y revisar el diff.
- Commits pequeños y en español, incluyendo siempre los `.meta`. **No hacer commit ni push salvo que se pida explícitamente.**

## 8. Pruebas

- **EditMode**: pose inversa de `SubmarineFrame` y conversión mundo↔submarino, rebase que conserva `MapPosition`, daño por impulse y mapeo punto→sala, los cinco efectos por sala (incluido periscopio sin rotación, datos aleatorios de sensores, mapa con daños falsos y gancho retraído), elección aleatoria 50/50 del fallo de máquinas, ambos modos de agua y bloqueo del ajuste tras iniciar partida, filtro de sónar, arbitraje de ocupación, transición objetivo→victoria.
- **PlayMode**: arranque de escena, 4 slots con gamepad, colisión→alarma→fuga→reparación→restauración de función→estado final, cambio de jugador de estación, corrientes que empujan, minas que dañan y navegación que atraviesa el umbral de rebase sin deriva ni jitter.
- **Manual**: partida con 1 y con 4 gamepads; recorrer las cinco salas y provocar un daño en cada una; periscopio, gancho, sónar y mapa; colisión leve, grave y extrema; comprobar el único tesoro, los dos modos de agua y que el ajuste no cambia en partida; cruzar el umbral de rebase y verificar que mapa y sónar siguen cuadrando con la posición real.

## 9. Decisiones pendientes (no bloquean empezar)

1. ¿Hay límite de tiempo en la partida? (El número de objetivos ya está cerrado: uno en la versión inicial.)
2. Arte y audio: usar primitivas/placeholders o importar assets propios.
3. Tamaño definitivo del mapa y `rebaseDistance`: provisional 2000 m hasta cerrar el mapa a mano.
4. Balanceo de cabina ante impactos: ¿solo el horizonte (por defecto) o además una inclinación cosmética de la raíz visual del interior en el hito 6?

El resto de decisiones (multijugador, roles, salas, efectos de daño, agua, tesoro y hazards) están cerradas; no reabrirlas sin petición explícita.

## 10. Primer paso del agente

1. Leer `README.md` y este `AGENTS.md`; respetar las decisiones confirmadas en §3.
2. Comprobar `git status` y no tocar los cambios del usuario reseñados en §2.
3. Comprobar en `trabajo.md` el estado real: si el hito 0 ya está cerrado (lo está), continuar por el hito pendiente más bajo de la persona elegida con sus dependencias cumplidas; verificar compilación y tests antes de seguir.
