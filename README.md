# Sonar Submarine Crew

## Visión del juego

Un juego hecho con Unity en el que intentas manejar un submarino con hasta 3 amigos para intentar alcanzar un tesoro hundido. Navega por los mares, esquiva artefactos y lidia con la fauna local mientras intentas hacerte de oro.

El juego es un estilo *friendslop* en primera persona inspirado en juegos como **Lethal Company** y **PlateUp!**. Como si fuera un *roguelike*, la idea es plantear una misión/objetivo, y al ser cumplido presentar opciones de modificadores permanentes a elegir entre los jugadores e incrementar la dificultad.

> Esta es la **visión completa** del producto. Lo que se construye ahora es solo la [versión inicial](#alcance-por-versión); los elementos de futuras versiones están marcados como tal más abajo.

## Submarino

### Elementos de control

#### Sónar
Un sónar que avise de elementos cercanos (terreno, rocas, hazards; en futuras versiones también minas y fauna).

#### Mapa y sensores
Mapa estático de papel y sensores de altitud y coordenadas, para localizarse en el mapa. Se consulta en la sala de información/documentación.

#### Periscopio
La única capacidad que tiene la tripulación de ver directamente lo que tienen en frente. Es capaz de rotar la cámara en cualquier dirección.

#### Timón y mandos de control
Timón para girar el submarino a la izquierda y derecha, palancas para ascender/descender y palanca para avanzar/retroceder.

#### Sala de máquinas
Contiene el motor del submarino y el suministro eléctrico. Si se daña, puede afectar a la propulsión y a las estaciones que dependen de la energía.

#### Carga de torpedos
*Visión futura, fuera de la versión inicial.* Sala aparte de la sala de mandos. Los torpedos se encuentran en cajas y deben ser cargados en los huecos de torpedos manualmente.

#### Lanzador de torpedos
*Visión futura, fuera de la versión inicial.* Junto al sónar, capacidad para fijarse en elementos del sónar y lanzar un torpedo teledirigido.

#### Gancho recolector
Activa un gancho en la parte inferior del submarino junto con una cámara de baja calidad para recolectar el tesoro u objetivo.

#### Pantalla de integridad del submarino
Muestra la salud actual del submarino con una vista de águila del mismo. Avisa de los daños producidos marcando en la pantalla la sala donde ha ocurrido.

### Salas y reparto de la tripulación

La tripulación no tiene roles fijos: cada jugador puede moverse libremente por el interior del submarino y usar cualquier mando siempre que no esté ya ocupado por otro jugador.

| Sala | Elementos |
|---|---|
| Sala de mandos | Timón y periscopio |
| Sala de sensores | Sónar y sensores |
| Sala de máquinas | Motor y suministro eléctrico |
| Sala de información/documentación | Mapa en papel e información del submarino |
| Sala del gancho recolector | Gancho y cámara de baja calidad |

### Interacciones con el entorno

Si el submarino se choca contra el suelo oceánico, una roca u otro obstáculo, sufrirá daños en el casco. Dependiendo de la fuerza y orientación con la que se produzca la colisión, es posible que la trayectoria y rotación del submarino se vea afectada. En futuras versiones también existirán colisiones con ballenas y fauna.

#### Daños al submarino

Si el submarino sufre daños por cualquier cosa, se provocará el evento correspondiente:

- **Daños leves**: suenan las alarmas por daños, pero no afecta a la integridad del submarino.
- **Daños graves**: suenan las alarmas y se empieza a inundar el submarino. Debe ser arreglado con chapas de metal o será Game Over. Dependiendo de la sala dañada, se puede perder cierta funcionalidad hasta reparar los daños.
- **Daños extremos**: el submarino se hunde completamente; Game Over completo. Reservado para las interacciones más locas.

#### Efectos de los daños por sala

Un daño grave sin reparar produce estos efectos:

| Sala dañada | Efecto |
|---|---|
| Sala de mandos | Se pierde el control del timón: es imposible girarlo y el submarino queda quieto. El periscopio se puede usar, pero no se puede rotar la cámara. |
| Sala de sensores | El sónar deja de funcionar y los sensores dan datos erróneos y aleatorios. |
| Sala de máquinas | Con un 50% de probabilidades, cada una: paro completo del motor (deja de acelerar) o fallo eléctrico. |
| Sala de información/documentación | El mapa muestra daños falsos, suenan alarmas ocasionalmente y la información mostrada no es precisa. |
| Sala del gancho recolector | El gancho se retrae automáticamente y deja de funcionar. |

Reparar con una chapa cierra la fuga y restaura la funcionalidad de la sala.

#### Regla del agua (opcional)

El comportamiento del nivel de agua es configurable, con **valor por defecto desactivado**:

- **Activada**: el agua nunca baja, aunque el submarino no tenga daños.
- **Desactivada** (por defecto): cuando el submarino no tiene daños, el nivel baja lentamente.

El ajuste se puede cambiar **antes de empezar la partida, pero no durante**. Reparar con chapas cierra la fuga y restaura la funcionalidad, pero no reduce por sí mismo el agua ya acumulada.

## Alcance por versión

### Versión inicial (v0.1) — entregable actual

- **Multijugador local** de 1 a 4 jugadores, **un gamepad por jugador** y pantalla dividida. El multijugador online se reserva para versiones posteriores.
- **Roles libres**: los tripulantes se desplazan por el interior del submarino y operan los mandos que no estén ocupados.
- **Un mapa creado a mano**, estático, con **un único tesoro como objetivo**. Incluye rocas, columnas, arcos estrechos de terreno, corrientes laterales/verticales que empujan al submarino y minas. **Sin fauna, enemigos, peces ni ballenas.**
- Cinco salas: mandos, sensores, máquinas, información/documentación y gancho recolector.
- Incluye sónar, mapa de papel y sensores, periscopio, timón y palancas, gancho con cámara de baja calidad, pantalla de integridad, colisiones, daños leve/grave/extremo, inundación configurable, reparación con chapas y victoria al recuperar el tesoro.
- **No incluye**: roguelike, modificadores, ciclo de misiones, torpedos, carga ni lanzador de torpedos, online, fauna.

### Versiones posteriores (visión, no implementar ahora)

- Modo roguelike: misiones, modificadores permanentes elegidos por los jugadores y dificultad creciente.
- Misiones con varios objetivos (la versión inicial tiene uno solo).
- Multijugador online.
- Torpedos, sala de carga y lanzador teledirigido.
- Fauna local: peces, ballenas y enemigos.

## Control de versiones y convenciones

- **Motor y paquetes**: Unity `6000.6.7f1`, URP `17.6.0`, Input System `1.20.1`, uGUI `2.6.0`. La configuración exacta está en `ProjectSettings/ProjectVersion.txt` y `Packages/manifest.json` / `packages-lock.json`. No actualizar versiones ni añadir paquetes sin una decisión explícita.
- **Git**: commits pequeños y descriptivos en español, incluyendo siempre los archivos `.meta` de Unity. No versionar `Library/`, `Temp/`, `Logs/`, `UserSettings/` ni artefactos generados; `.gitignore` y `.gitattributes` definen las exclusiones.
- **Archivos `.meta`**: los genera Unity. No editar GUIDs a mano; mover o renombrar assets desde el Editor o mediante `AssetDatabase`.
- **Documentación**: este `README.md` es la especificación de producto; `AGENTS.md` recoge las decisiones técnicas, la arquitectura, el plan por hitos y las directivas para agentes.
- **Automatización**: no hacer commit ni push salvo que se solicite explícitamente.
