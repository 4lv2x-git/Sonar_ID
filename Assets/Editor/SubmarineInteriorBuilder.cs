using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonarCrew.Core;
using SonarCrew.Stations;
using SonarCrew.Submarine;
using SonarCrew.UI;

namespace SonarCrew.EditorTools
{
    /// <summary>
    /// Genera la maqueta del interior del submarino del hito 2 (persona B):
    /// pasillo central, las cinco salas del AGENTS §4 y las nueve estaciones con
    /// geometría de pruebas, en su propia escena aditiva `SubmarineInterior` que
    /// `Bootstrap` (escena de A) carga en runtime.
    ///
    /// No toca `Bootstrap.unity` ni `TagManager.asset`: la escena del interior se
    /// construye aparte, se guarda y se cierra, dejando abierta la que hubiera.
    /// Es idempotente: cada ejecución rehace la escena y los prefabs `Station_*`.
    /// Se puede lanzar en lote con
    /// -executeMethod SonarCrew.EditorTools.SubmarineInteriorBuilder.BuildInterior
    /// </summary>
    public static class SubmarineInteriorBuilder
    {
        private const string InteriorScenePath = "Assets/Scenes/SubmarineInterior.unity";
        private const string RootName = "SubmarineInterior";
        private const string MaterialFolder = "Assets/Materials/Interior";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SceneFolder = "Assets/Scenes";

        private const float WallThickness = 0.35f;
        private const float WallHeight = 3.2f;
        private const float DoorWidth = 2.6f;
        private const float DoorHeight = 2.6f;
        private const float CorridorHalfWidth = 3.5f;
        private const float HullInner = 19.5f;

        // Hito 4: el gancho arranca justo debajo del suelo del casco (el forjado
        // ocupa de y = -1 a y = 0) y llega al fondo placeholder del mapa.
        private const float HookDropBelowFloor = -1.05f;
        private const float HookMaxDepth = 7.5f;
        private const float HookWinchSpeed = 2.5f;
        private const float HookCaptureRadius = 1.2f;

        private enum StationKind
        {
            Wheel,
            Levers,
            Periscope,
            Sonar,
            Sensors,
            Engine,
            Power,
            Map,
            Grapple
        }

        private sealed class Palette
        {
            public Material Wall;
            public Material Body;
            public Material Accent;
            public Material Screen;
        }

        private sealed class RoomSpec
        {
            public HullZone Id;
            public string NodeName;
            public string DisplayName;
            public Vector2 Center;
            public Vector2 Size;
            public Color LightColor;
            public bool DependsOnPower = true;
        }

        private sealed class StationSpec
        {
            public StationKind Kind;
            public HullZone Room;
            public Vector3 Position;
            public float Yaw;
        }

        [MenuItem("SonarCrew/Hito 2 (persona B)/Reconstruir SubmarineInterior", false, 20)]
        public static void BuildInterior()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Hito 2] Sal del modo Play antes de reconstruir el interior.");
                return;
            }

            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            Palette palette = BuildPalette();
            Dictionary<StationKind, GameObject> prefabs = BuildStationPrefabs(palette);

            Scene previousScene = SceneManager.GetActiveScene();
            bool additively = !string.IsNullOrEmpty(previousScene.path);

            if (!additively && !Application.isBatchMode)
            {
                Debug.LogWarning("[Hito 2] La escena abierta no está guardada. Abre una escena guardada " +
                                 "(por ejemplo Bootstrap) y vuelve a lanzar el comando.");
                return;
            }

            // Con una escena guardada abierta el interior se construye aparte y no
            // se toca lo que hubiera; en lote no hay escena que preservar.
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                additively ? NewSceneMode.Additive : NewSceneMode.Single);

            SceneManager.SetActiveScene(scene);

            try
            {
                GameObject root = new GameObject(RootName);
                Transform structure = CreateChild(root.transform, "Estructura");
                Transform roomsRoot = CreateChild(root.transform, "Salas");
                Transform systems = CreateChild(root.transform, "Sistemas");

                BuildWalls(structure, palette.Wall);
                BuildRooms(roomsRoot, root.transform, palette, prefabs);
                PowerSystem powerSystem = BuildSystems(systems, palette);
                WireEngineRoom(roomsRoot, systems, powerSystem);

                if (!EditorSceneManager.SaveScene(scene, InteriorScenePath))
                    Debug.LogError("[Hito 2] No se pudo guardar " + InteriorScenePath);
            }
            finally
            {
                if (additively)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previousScene.IsValid())
                        SceneManager.SetActiveScene(previousScene);
                }
            }

            AssetDatabase.SaveAssets();
            EnsureSceneInBuildSettings(InteriorScenePath);

            Debug.Log("[Hito 2] SubmarineInterior reconstruida: 5 salas, 9 estaciones, red eléctrica " +
                      "y HUD instalado en runtime. GameFlow la carga de forma aditiva. " +
                      "Prueba con F1-F5 (daño grave por sala), F6 (reparar), F7 (red) y F8 (avería eléctrica).");
        }

        /// <summary>
        /// Deja la escena del interior en Build Settings (sin tocar el resto de
        /// entradas) para que GameFlow pueda cargarla de forma aditiva.
        /// </summary>
        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            AssetDatabase.ImportAsset(scenePath);
            GUID guid = AssetDatabase.GUIDFromAssetPath(scenePath);
            if (guid.Empty())
            {
                Debug.LogWarning("[Hito 2] No se pudo obtener el GUID de " + scenePath);
                return;
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == scenePath) return;
            }

            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[current.Length + 1];
            current.CopyTo(updated, 0);
            updated[current.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;
            Debug.Log("[Hito 2] " + scenePath + " añadida a Build Settings.");
        }

        /// <summary>Abre la escena del interior para inspeccionarla.</summary>
        [MenuItem("SonarCrew/Hito 2 (persona B)/Abrir SubmarineInterior", false, 21)]
        public static void OpenInterior()
        {
            if (!System.IO.File.Exists(InteriorScenePath))
            {
                Debug.LogWarning("[Hito 2] Todavía no existe " + InteriorScenePath + ". Reconstrúyela primero.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(InteriorScenePath, OpenSceneMode.Additive);
        }

        // ---------------------------------------------------------------- casco

        private static void BuildWalls(Transform parent, Material wallMaterial)
        {
            BuildSegmentedWall(parent, "MuroPasilloOeste", true, -CorridorHalfWidth, -6f, HullInner,
                new[] { 0f, 12f }, wallMaterial);
            BuildSegmentedWall(parent, "MuroPasilloEste", true, CorridorHalfWidth, -6f, HullInner,
                new[] { 0f, 12f }, wallMaterial);

            BuildWallBlock(parent, "DivisoriaNorteOeste", false, 6f, -HullInner, -CorridorHalfWidth - WallThickness * 0.5f, wallMaterial);
            BuildWallBlock(parent, "DivisoriaNorteEste", false, 6f, CorridorHalfWidth + WallThickness * 0.5f, HullInner, wallMaterial);

            BuildSegmentedWall(parent, "MuroPopa", false, -6f, -HullInner, HullInner, new[] { 0f }, wallMaterial);

            BuildHullShell(parent, wallMaterial);
            BuildCorridorLights(parent);
        }

        /// <summary>
        /// Cierra el casco: el interior es estático y opaco, así que el mundo solo
        /// se ve por las ventanas y visores (periscopio). Sin estas paredes y el
        /// techo las cámaras de la tripulación mirarían directamente al mapa.
        /// </summary>
        private static void BuildHullShell(Transform parent, Material wallMaterial)
        {
            BuildWallBlock(parent, "CascoProa", false, HullInner, -HullInner, HullInner, wallMaterial);
            BuildWallBlock(parent, "CascoPopa", false, -HullInner, -HullInner, HullInner, wallMaterial);
            BuildWallBlock(parent, "CascoBabor", true, -HullInner, -HullInner, HullInner, wallMaterial);
            BuildWallBlock(parent, "CascoEstribor", true, HullInner, -HullInner, HullInner, wallMaterial);

            CreateBox(parent, "Techo", new Vector3(0f, WallHeight + 0.1f, 0f),
                new Vector3(HullInner * 2f, 0.2f, HullInner * 2f), Quaternion.identity, wallMaterial);
        }

        private static void BuildCorridorLights(Transform parent)
        {
            Transform lights = CreateChild(parent, "LucesPasillo");
            for (int i = 0; i < 4; i++)
            {
                GameObject lightObject = new GameObject("LuzPasillo" + (i + 1));
                lightObject.transform.SetParent(lights, false);
                lightObject.transform.localPosition = new Vector3(0f, WallHeight - 0.35f, -3f + i * 6f);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 12f;
                light.intensity = 1.6f;
                light.color = new Color(0.85f, 0.92f, 1f);
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Muro recto con huecos de puerta: tramos de pared y dinteles.</summary>
        private static void BuildSegmentedWall(Transform parent, string name, bool alongZ, float fixedCoord,
            float from, float to, float[] doorCenters, Material material)
        {
            List<Vector2> gaps = new List<Vector2>();
            if (doorCenters != null)
            {
                foreach (float center in doorCenters)
                    gaps.Add(new Vector2(center - DoorWidth * 0.5f, center + DoorWidth * 0.5f));
            }
            gaps.Sort((a, b) => a.x.CompareTo(b.x));

            int index = 0;
            float cursor = from;
            foreach (Vector2 gap in gaps)
            {
                if (gap.x > cursor + 0.01f)
                    BuildWallBlock(parent, name + "_" + index++, alongZ, fixedCoord, cursor, gap.x, material);

                BuildLintel(parent, name + "_Dintel" + index, alongZ, fixedCoord, gap.x, gap.y, material);
                cursor = Mathf.Max(cursor, gap.y);
            }

            if (to > cursor + 0.01f)
                BuildWallBlock(parent, name + "_" + index, alongZ, fixedCoord, cursor, to, material);
        }

        private static void BuildWallBlock(Transform parent, string name, bool alongZ, float fixedCoord,
            float from, float to, Material material)
        {
            float length = to - from;
            if (length <= 0.01f) return;

            Vector3 size = alongZ
                ? new Vector3(WallThickness, WallHeight, length)
                : new Vector3(length, WallHeight, WallThickness);
            Vector3 center = alongZ
                ? new Vector3(fixedCoord, WallHeight * 0.5f, (from + to) * 0.5f)
                : new Vector3((from + to) * 0.5f, WallHeight * 0.5f, fixedCoord);

            CreateBox(parent, name, center, size, Quaternion.identity, material);
        }

        private static void BuildLintel(Transform parent, string name, bool alongZ, float fixedCoord,
            float from, float to, Material material)
        {
            float length = to - from;
            float height = WallHeight - DoorHeight;
            if (length <= 0.01f || height <= 0.01f) return;

            Vector3 size = alongZ
                ? new Vector3(WallThickness, height, length)
                : new Vector3(length, height, WallThickness);
            Vector3 center = alongZ
                ? new Vector3(fixedCoord, DoorHeight + height * 0.5f, (from + to) * 0.5f)
                : new Vector3((from + to) * 0.5f, DoorHeight + height * 0.5f, fixedCoord);

            CreateBox(parent, name, center, size, Quaternion.identity, material);
        }

        // --------------------------------------------------------------- salas

        private static void BuildRooms(Transform roomsRoot, Transform hullRoot, Palette palette,
            Dictionary<StationKind, GameObject> prefabs)
        {
            foreach (RoomSpec room in BuildRoomSpecs())
            {
                GameObject roomNode = new GameObject(room.NodeName);
                roomNode.transform.SetParent(roomsRoot, false);
                roomNode.transform.localPosition = new Vector3(room.Center.x, 0f, room.Center.y);

                HullRoom zone = roomNode.AddComponent<HullRoom>();
                SetEnumField(zone, "zone", (int)room.Id);
                SetStringField(zone, "displayName", room.DisplayName);
                SetBoolField(zone, "dependsOnPower", room.DependsOnPower);

                BoxCollider bounds = roomNode.AddComponent<BoxCollider>();
                bounds.isTrigger = true;
                bounds.center = new Vector3(0f, WallHeight * 0.5f, 0f);
                bounds.size = new Vector3(room.Size.x, WallHeight, room.Size.y);

                GameObject lightObject = new GameObject("Luz");
                lightObject.transform.SetParent(roomNode.transform, false);
                lightObject.transform.localPosition = new Vector3(0f, WallHeight - 0.3f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 13f;
                light.intensity = 2.2f;
                light.color = room.LightColor;
                light.shadows = LightShadows.None;

                foreach (StationSpec spec in BuildStationSpecs())
                {
                    if (spec.Room != room.Id) continue;
                    if (!prefabs.TryGetValue(spec.Kind, out GameObject prefab)) continue;
                    InstantiateStation(spec, prefab, roomNode.transform, hullRoot);
                }
            }
        }

        private static List<RoomSpec> BuildRoomSpecs()
        {
            return new List<RoomSpec>
            {
                new RoomSpec
                {
                    Id = HullZone.Mandos, NodeName = "Sala_Mandos", DisplayName = "Sala de mandos",
                    Center = new Vector2(-11.5f, 12.75f), Size = new Vector2(16f, 13.5f),
                    LightColor = new Color(1f, 0.92f, 0.78f)
                },
                new RoomSpec
                {
                    Id = HullZone.Sensores, NodeName = "Sala_Sensores", DisplayName = "Sala de sensores",
                    Center = new Vector2(11.5f, 12.75f), Size = new Vector2(16f, 13.5f),
                    LightColor = new Color(0.78f, 0.9f, 1f)
                },
                new RoomSpec
                {
                    Id = HullZone.Maquinas, NodeName = "Sala_Maquinas", DisplayName = "Sala de máquinas",
                    Center = new Vector2(-11.5f, 0f), Size = new Vector2(16f, 12f),
                    LightColor = new Color(1f, 0.82f, 0.65f)
                },
                new RoomSpec
                {
                    Id = HullZone.Informacion, NodeName = "Sala_Informacion", DisplayName = "Sala de información y documentación",
                    Center = new Vector2(11.5f, 0f), Size = new Vector2(16f, 12f),
                    LightColor = new Color(0.85f, 1f, 0.88f)
                },
                new RoomSpec
                {
                    Id = HullZone.Gancho, NodeName = "Sala_Gancho", DisplayName = "Sala del gancho recolector",
                    Center = new Vector2(0f, -12.75f), Size = new Vector2(39f, 13.5f),
                    LightColor = new Color(1f, 0.8f, 0.86f)
                }
            };
        }

        private static List<StationSpec> BuildStationSpecs()
        {
            return new List<StationSpec>
            {
                new StationSpec { Kind = StationKind.Wheel, Room = HullZone.Mandos, Position = new Vector3(-11.5f, 0f, 17.4f), Yaw = 180f },
                new StationSpec { Kind = StationKind.Levers, Room = HullZone.Mandos, Position = new Vector3(-16.5f, 0f, 17.4f), Yaw = 180f },
                new StationSpec { Kind = StationKind.Periscope, Room = HullZone.Mandos, Position = new Vector3(-6.5f, 0f, 16.5f), Yaw = 180f },
                // El sónar se queda en la sala de sensores; los sensores de datos se
                // consultan junto al mapa en la sala de información (README).
                new StationSpec { Kind = StationKind.Sonar, Room = HullZone.Sensores, Position = new Vector3(11.5f, 0f, 17.4f), Yaw = 180f },
                new StationSpec { Kind = StationKind.Sensors, Room = HullZone.Informacion, Position = new Vector3(7.5f, 0f, 4.6f), Yaw = 180f },
                new StationSpec { Kind = StationKind.Engine, Room = HullZone.Maquinas, Position = new Vector3(-11.5f, 0f, 4.4f), Yaw = 180f },
                new StationSpec { Kind = StationKind.Power, Room = HullZone.Maquinas, Position = new Vector3(-17.5f, 0f, 4.4f), Yaw = 180f },
                new StationSpec { Kind = StationKind.Map, Room = HullZone.Informacion, Position = new Vector3(14.5f, 0f, 2.5f), Yaw = 180f },
                new StationSpec { Kind = StationKind.Grapple, Room = HullZone.Gancho, Position = new Vector3(0f, 0f, -15f), Yaw = 0f }
            };
        }

        private static void InstantiateStation(StationSpec spec, GameObject prefab, Transform roomNode,
            Transform hullRoot)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, roomNode);
            instance.transform.position = spec.Position;
            instance.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);
            instance.name = "Estacion_" + spec.Kind;

            SensorStation sensor = instance.GetComponent<SensorStation>();
            if (sensor != null)
                SetObjectField(sensor, "referencePoint", hullRoot);
        }

        // ------------------------------------------------------------- sistemas

        private static PowerSystem BuildSystems(Transform systems, Palette palette)
        {
            GameObject powerObject = new GameObject("RedElectrica");
            powerObject.transform.SetParent(systems, false);
            PowerSystem powerSystem = powerObject.AddComponent<PowerSystem>();

            GameObject controlsObject = new GameObject("MandosSubmarino");
            controlsObject.transform.SetParent(systems, false);
            SubmarineControlState controls = controlsObject.AddComponent<SubmarineControlState>();
            // El motor arranca en marcha para no bloquear el pilotaje del hito 1;
            // la estación de motor de la sala de máquinas lo para y lo vuelve a arrancar.
            SetBoolField(controls, "engineRunningAtStart", true);

            GameObject hudObject = new GameObject("InstaladorHUD");
            hudObject.transform.SetParent(systems, false);
            hudObject.AddComponent<StationHudInstaller>();

            // Hito 4: el gancho es equipo del casco y cuelga por debajo del suelo,
            // en el marco estático del interior (nunca de un transform móvil).
            BuildGrappleRig(systems, palette);

            return powerSystem;
        }

        /// <summary>
        /// Gancho del hito 4: cable, punta y cámara de baja calidad por debajo del
        /// suelo del casco. La estación de la sala del gancho lo acciona en remoto y
        /// reproduce su cámara en la pantalla del puesto.
        /// </summary>
        private static void BuildGrappleRig(Transform systems, Palette palette)
        {
            GameObject rig = new GameObject("GanchoRecolector");
            rig.transform.SetParent(systems, false);
            rig.transform.localPosition = new Vector3(0f, HookDropBelowFloor, 0f);
            GrappleHook hook = rig.AddComponent<GrappleHook>();

            GameObject cable = CreateBox(rig.transform, "Cable", new Vector3(0f, -HookMaxDepth * 0.5f, 0f),
                new Vector3(0.06f, HookMaxDepth, 0.06f), Quaternion.identity, palette.Accent);

            GameObject head = new GameObject("Punta");
            head.transform.SetParent(rig.transform, false);
            head.transform.localPosition = new Vector3(0f, -HookMaxDepth, 0f);
            CreateBox(head.transform, "Garfio", new Vector3(0f, -0.05f, 0f), new Vector3(0.3f, 0.2f, 0.3f), Quaternion.identity, palette.Body);
            CreateBox(head.transform, "PuaA", new Vector3(-0.16f, -0.22f, 0f), new Vector3(0.06f, 0.24f, 0.06f), Quaternion.Euler(0f, 0f, -25f), palette.Accent);
            CreateBox(head.transform, "PuaB", new Vector3(0.16f, -0.22f, 0f), new Vector3(0.06f, 0.24f, 0.06f), Quaternion.Euler(0f, 0f, 25f), palette.Accent);

            GameObject cameraObject = new GameObject("CamaraGancho", typeof(Camera));
            cameraObject.transform.SetParent(head.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Camera feedCamera = cameraObject.GetComponent<Camera>();
            feedCamera.enabled = false;
            feedCamera.fieldOfView = 65f;
            feedCamera.nearClipPlane = 0.05f;
            feedCamera.farClipPlane = 200f;

            // El gancho no es física: los primitivos traen collider y no debe chocar.
            foreach (Collider collider in rig.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            SerializedObject serialized = new SerializedObject(hook);
            serialized.FindProperty("cable").objectReferenceValue = cable.transform;
            serialized.FindProperty("hookHead").objectReferenceValue = head.transform;
            serialized.FindProperty("feedCamera").objectReferenceValue = feedCamera;
            serialized.FindProperty("maxDepth").floatValue = HookMaxDepth;
            serialized.FindProperty("winchSpeed").floatValue = HookWinchSpeed;
            serialized.FindProperty("captureRadius").floatValue = HookCaptureRadius;
            SetLayerMask(serialized, "worldMask", new[] { "World", "Hazard" });
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Escribe una máscara de capas por nombre (el campo se serializa como m_Bits).</summary>
        private static void SetLayerMask(SerializedObject serialized, string fieldName, string[] layerNames)
        {
            SerializedProperty property = serialized.FindProperty(fieldName + ".m_Bits") ?? serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning("[Hito 4] Máscara no encontrada: " + fieldName);
                return;
            }

            int mask = 0;
            foreach (string layerName in layerNames)
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0) mask |= 1 << layer;
            }
            property.intValue = mask;
        }

        private static void WireEngineRoom(Transform roomsRoot, Transform systems, PowerSystem powerSystem)
        {
            Transform engineRoom = roomsRoot.Find("Sala_Maquinas");
            if (engineRoom == null)
            {
                Debug.LogWarning("[Hito 2] No se encontró la sala de máquinas para cablear el fallo 50/50.");
                return;
            }

            EngineStation engineStation = engineRoom.GetComponentInChildren<EngineStation>(true);
            if (engineStation == null)
                Debug.LogWarning("[Hito 2] La sala de máquinas no tiene estación de motor.");

            EngineRoomFailure failure = engineRoom.GetComponent<EngineRoomFailure>();
            if (failure == null)
                failure = engineRoom.gameObject.AddComponent<EngineRoomFailure>();

            SetObjectField(failure, "engineStation", engineStation);
            SetObjectField(failure, "powerSystem", powerSystem);

            GameObject debugObject = new GameObject("DepuracionHito2");
            debugObject.transform.SetParent(systems, false);
            InteriorDebugControls debug = debugObject.AddComponent<InteriorDebugControls>();

            SerializedObject serialized = new SerializedObject(debug);
            SerializedProperty zones = serialized.FindProperty("zones");
            zones.arraySize = 5;
            for (int i = 0; i < 5; i++)
                zones.GetArrayElementAtIndex(i).objectReferenceValue = FindRoom(engineRoom.parent, (HullZone)i);
            serialized.FindProperty("powerSystem").objectReferenceValue = powerSystem;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HullRoom FindRoom(Transform roomsRoot, HullZone zone)
        {
            foreach (HullRoom room in roomsRoot.GetComponentsInChildren<HullRoom>(true))
            {
                if (room.Zone == zone) return room;
            }
            return null;
        }

        // ----------------------------------------------------------- estaciones

        private static Dictionary<StationKind, GameObject> BuildStationPrefabs(Palette palette)
        {
            Dictionary<StationKind, GameObject> prefabs = new Dictionary<StationKind, GameObject>();

            foreach (StationKind kind in BuildStationKinds())
            {
                EnsureFolder(PrefabFolder);
                GameObject prototype = BuildStationPrototype(kind, palette);
                string path = PrefabFolder + "/Station_" + kind + ".prefab";

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prototype, path);
                Object.DestroyImmediate(prototype);

                if (prefab == null)
                    Debug.LogError("[Hito 2] No se pudo guardar el prefab " + path);
                else
                    prefabs[kind] = prefab;
            }

            return prefabs;
        }

        private static IEnumerable<StationKind> BuildStationKinds()
        {
            yield return StationKind.Wheel;
            yield return StationKind.Levers;
            yield return StationKind.Periscope;
            yield return StationKind.Sonar;
            yield return StationKind.Sensors;
            yield return StationKind.Engine;
            yield return StationKind.Power;
            yield return StationKind.Map;
            yield return StationKind.Grapple;
        }

        private static GameObject BuildStationPrototype(StationKind kind, Palette palette)
        {
            switch (kind)
            {
                case StationKind.Wheel: return BuildWheel(palette);
                case StationKind.Levers: return BuildLevers(palette);
                case StationKind.Periscope: return BuildPeriscope(palette);
                case StationKind.Sonar: return BuildSonar(palette);
                case StationKind.Sensors: return BuildSensors(palette);
                case StationKind.Engine: return BuildEngine(palette);
                case StationKind.Power: return BuildPower(palette);
                case StationKind.Map: return BuildMap(palette);
                default: return BuildGrapple(palette);
            }
        }

        private static GameObject BuildWheel(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Wheel");
            WheelStation station = root.AddComponent<WheelStation>();
            SetStringField(station, "stationName", "Timón");

            CreateBox(root.transform, "Pedestal", new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.5f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Consola", new Vector3(0f, 1.05f, 0.28f), new Vector3(0.8f, 0.16f, 0.24f), Quaternion.identity, palette.Accent);

            GameObject wheel = new GameObject("Volante");
            wheel.transform.SetParent(root.transform, false);
            wheel.transform.localPosition = new Vector3(0f, 1.32f, 0.3f);
            CreateCylinder(wheel.transform, "Disco", Vector3.zero, new Vector3(0.8f, 0.05f, 0.8f), new Quaternion(0.7071f, 0f, 0f, 0.7071f), palette.Accent);
            CreateBox(wheel.transform, "RadioV", Vector3.zero, new Vector3(0.06f, 0.8f, 0.06f), Quaternion.identity, palette.Body);
            CreateBox(wheel.transform, "RadioH", Vector3.zero, new Vector3(0.8f, 0.06f, 0.06f), Quaternion.identity, palette.Body);

            SetObjectField(station, "wheelVisual", wheel.transform);
            return root;
        }

        private static GameObject BuildLevers(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Levers");
            LeversStation station = root.AddComponent<LeversStation>();
            SetStringField(station, "stationName", "Palancas");

            CreateBox(root.transform, "Base", new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1f, 0.55f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Panel", new Vector3(0f, 1.02f, 0.16f), new Vector3(1.1f, 0.08f, 0.3f), Quaternion.identity, palette.Accent);

            GameObject throttle = new GameObject("PalancaAvance");
            throttle.transform.SetParent(root.transform, false);
            throttle.transform.localPosition = new Vector3(-0.28f, 1.04f, 0f);
            CreateBox(throttle.transform, "Mango", new Vector3(0f, 0.28f, 0f), new Vector3(0.09f, 0.56f, 0.09f), Quaternion.identity, palette.Accent);

            GameObject vertical = new GameObject("PalancaCota");
            vertical.transform.SetParent(root.transform, false);
            vertical.transform.localPosition = new Vector3(0.28f, 1.04f, 0f);
            CreateBox(vertical.transform, "Mango", new Vector3(0f, 0.28f, 0f), new Vector3(0.09f, 0.56f, 0.09f), Quaternion.identity, palette.Accent);

            SetObjectField(station, "throttleLever", throttle.transform);
            SetObjectField(station, "verticalLever", vertical.transform);
            return root;
        }

        private static GameObject BuildPeriscope(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Periscope");
            PeriscopeStation station = root.AddComponent<PeriscopeStation>();
            SetStringField(station, "stationName", "Periscopio");

            CreateCylinder(root.transform, "Base", new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.5f, 0.9f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Pedestal", new Vector3(0f, 0.35f, 0f), new Vector3(0.6f, 0.7f, 0.6f), Quaternion.identity, palette.Body);

            GameObject head = new GameObject("Cabeza");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            CreateCylinder(head.transform, "Tubo", new Vector3(0f, 1.6f, 0f), new Vector3(0.2f, 1.6f, 0.2f), Quaternion.identity, palette.Accent);
            CreateBox(head.transform, "Optica", new Vector3(0f, 3.1f, 0.16f), new Vector3(0.5f, 0.3f, 0.4f), Quaternion.identity, palette.Body);
            CreateBox(head.transform, "Lente", new Vector3(0f, 3.1f, 0.38f), new Vector3(0.3f, 0.18f, 0.06f), Quaternion.identity, palette.Screen);

            GameObject cameraObject = new GameObject("CamaraPeriscopio", typeof(Camera));
            cameraObject.transform.SetParent(head.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 3.1f, 0.45f);
            Camera periscopeCamera = cameraObject.GetComponent<Camera>();
            periscopeCamera.enabled = false;
            periscopeCamera.fieldOfView = 50f;
            periscopeCamera.nearClipPlane = 0.05f;
            periscopeCamera.farClipPlane = 400f;

            SetObjectField(station, "periscopeHead", head.transform);
            SetObjectField(station, "periscopeCamera", periscopeCamera);
            return root;
        }

        private static GameObject BuildSonar(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Sonar");
            SonarStation station = root.AddComponent<SonarStation>();
            SetStringField(station, "stationName", "Sónar");

            CreateBox(root.transform, "Mesa", new Vector3(0f, 0.45f, 0f), new Vector3(1.9f, 0.9f, 0.65f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Marco", new Vector3(0f, 1.25f, 0.05f), new Vector3(1.2f, 1.1f, 0.12f), Quaternion.identity, palette.Body);
            CreateCylinder(root.transform, "Pantalla", new Vector3(0f, 1.25f, 0.14f), new Vector3(1f, 0.03f, 1f), new Quaternion(0.7071f, 0f, 0f, 0.7071f), palette.Screen);
            return root;
        }

        private static GameObject BuildSensors(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Sensors");
            SensorStation station = root.AddComponent<SensorStation>();
            SetStringField(station, "stationName", "Sensores");

            CreateBox(root.transform, "Armario", new Vector3(0f, 1f, -0.1f), new Vector3(1.7f, 2f, 0.45f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Panel", new Vector3(0f, 1.3f, 0.15f), new Vector3(1.3f, 0.65f, 0.08f), Quaternion.identity, palette.Screen);
            CreateBox(root.transform, "Repisa", new Vector3(0f, 0.75f, 0.25f), new Vector3(1.4f, 0.08f, 0.3f), Quaternion.identity, palette.Accent);
            return root;
        }

        private static GameObject BuildEngine(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Engine");
            EngineStation station = root.AddComponent<EngineStation>();
            SetStringField(station, "stationName", "Motor");

            GameObject motor = CreateBox(root.transform, "Motor", new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 1.4f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Bancada", new Vector3(0f, 0.15f, 0f), new Vector3(2.4f, 0.3f, 1.7f), Quaternion.identity, palette.Accent);
            CreateCylinder(root.transform, "Turbina", new Vector3(0f, 1.7f, 0f), new Vector3(0.7f, 0.7f, 0.7f), new Quaternion(0.7071f, 0f, 0f, 0.7071f), palette.Accent);
            CreateBox(root.transform, "Lever", new Vector3(0f, 1.1f, 0.85f), new Vector3(0.12f, 0.6f, 0.12f), Quaternion.identity, palette.Accent);

            SetObjectField(station, "motorVisual", motor.transform);
            return root;
        }

        private static GameObject BuildPower(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Power");
            PowerStation station = root.AddComponent<PowerStation>();
            SetStringField(station, "stationName", "Suministro eléctrico");

            CreateBox(root.transform, "Cuadro", new Vector3(0f, 1f, -0.1f), new Vector3(1.4f, 2f, 0.45f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Fusibles", new Vector3(-0.25f, 1.5f, 0.15f), new Vector3(0.5f, 0.35f, 0.08f), Quaternion.identity, palette.Screen);
            CreateBox(root.transform, "Aviso", new Vector3(0.3f, 1.5f, 0.15f), new Vector3(0.3f, 0.3f, 0.08f), Quaternion.identity, palette.Accent);

            GameObject breaker = new GameObject("Interruptor");
            breaker.transform.SetParent(root.transform, false);
            breaker.transform.localPosition = new Vector3(0f, 1.05f, 0.2f);
            CreateBox(breaker.transform, "Palanca", new Vector3(0f, 0.12f, 0.04f), new Vector3(0.12f, 0.3f, 0.1f), Quaternion.identity, palette.Accent);

            SetObjectField(station, "breakerLever", breaker.transform);
            return root;
        }

        private static GameObject BuildMap(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Map");
            MapTableStation station = root.AddComponent<MapTableStation>();
            SetStringField(station, "stationName", "Mapa y documentación");

            CreateBox(root.transform, "Tablero", new Vector3(0f, 0.92f, 0f), new Vector3(2.3f, 0.1f, 1.4f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "Mapa", new Vector3(0f, 0.99f, 0f), new Vector3(2.1f, 0.02f, 1.2f), Quaternion.identity, palette.Accent);
            CreateBox(root.transform, "PataA", new Vector3(-1f, 0.45f, -0.55f), new Vector3(0.12f, 0.9f, 0.12f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "PataB", new Vector3(1f, 0.45f, -0.55f), new Vector3(0.12f, 0.9f, 0.12f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "PataC", new Vector3(-1f, 0.45f, 0.55f), new Vector3(0.12f, 0.9f, 0.12f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "PataD", new Vector3(1f, 0.45f, 0.55f), new Vector3(0.12f, 0.9f, 0.12f), Quaternion.identity, palette.Body);
            return root;
        }

        private static GameObject BuildGrapple(Palette palette)
        {
            GameObject root = NewStationRoot("Station_Grapple");
            GrappleStation station = root.AddComponent<GrappleStation>();
            SetStringField(station, "stationName", "Gancho recolector");

            CreateBox(root.transform, "Cabestrante", new Vector3(0f, 0.6f, 0f), new Vector3(1.5f, 1.2f, 1f), Quaternion.identity, palette.Body);
            CreateCylinder(root.transform, "Tambor", new Vector3(0f, 1f, 0f), new Vector3(0.9f, 0.8f, 0.9f), new Quaternion(0f, 0f, 0.7071f, 0.7071f), palette.Accent);
            CreateBox(root.transform, "Consola", new Vector3(0f, 1.1f, 0.7f), new Vector3(0.9f, 0.2f, 0.4f), Quaternion.identity, palette.Accent);

            // Hito 4: la pantalla reproduce la cámara de baja calidad del gancho. El
            // cable y el garfio ya no viven en este puesto: cuelgan por debajo del
            // casco (GanchoRecolector) y esta estación los manda en remoto.
            CreateBox(root.transform, "SoportePantalla", new Vector3(0f, 1.55f, 0.52f), new Vector3(0.1f, 0.9f, 0.1f), Quaternion.identity, palette.Body);
            CreateBox(root.transform, "MarcoPantalla", new Vector3(0f, 1.95f, 0.45f), new Vector3(1.6f, 0.95f, 0.08f), Quaternion.identity, palette.Body);
            GameObject screen = CreateBox(root.transform, "PantallaGancho", new Vector3(0f, 1.95f, 0.5f),
                new Vector3(1.45f, 0.82f, 0.02f), Quaternion.Euler(10f, 0f, 0f), palette.Screen);

            SetObjectField(station, "monitorRenderer", screen.GetComponent<Renderer>());
            return root;
        }

        // ----------------------------------------------------------- utilidades

        private static GameObject NewStationRoot(string name)
        {
            GameObject root = new GameObject(name);
            root.transform.position = Vector3.zero;
            return root;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size,
            Quaternion rotation, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = rotation;
            box.transform.localScale = size;
            SetMaterial(box, material);
            return box;
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, Vector3 size,
            Quaternion rotation, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localRotation = rotation;
            cylinder.transform.localScale = size;
            SetMaterial(cylinder, material);
            return cylinder;
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            if (material == null) return;

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static Palette BuildPalette()
        {
            return new Palette
            {
                Wall = EnsureMaterial("InteriorWall", new Color(0.30f, 0.33f, 0.36f), 0.55f, 0.35f),
                Body = EnsureMaterial("StationBody", new Color(0.20f, 0.23f, 0.26f), 0.7f, 0.45f),
                Accent = EnsureMaterial("StationAccent", new Color(0.62f, 0.66f, 0.7f), 0.35f, 0.6f),
                Screen = EnsureMaterial("StationScreen", new Color(0.14f, 0.62f, 0.66f), 0.1f, 0.9f)
            };
        }

        private static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("[Hito 2] No se encontró el shader URP/Lit; revisa el render pipeline.");
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetStringField(Object target, string fieldName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning("[Hito 2] Campo no encontrado: " + fieldName + " en " + target.name);
                return;
            }
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectField(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning("[Hito 2] Campo no encontrado: " + fieldName + " en " + target.name);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBoolField(Object target, string fieldName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null) return;
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnumField(Object target, string fieldName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null) return;
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
