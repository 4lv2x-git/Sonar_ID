using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonarCrew.Environment;
using SonarCrew.Submarine;

namespace SonarCrew.EditorTools
{
    /// <summary>
    /// Montaje del mapa SubmarineTest (persona A). Genera materiales, configs,
    /// prefabs y la escena completa con el diseño aprobado (2026-11-26): cinco
    /// zonas de sur a norte (bahía, cañón de columnas, terraza de corrientes,
    /// campo de minas y fosa del tesoro), un único tesoro y sin fauna.
    ///
    /// Determinista: las posiciones de juego están escritas a mano; el dressing
    /// de la pasada de pulido usa anclas manuales y jitter con semilla fija.
    /// Idempotente: reemplaza la escena y reutiliza los assets que ya existen.
    ///
    /// Coordenadas de diseño = coordenadas de mapa (MapPosition, AGENTS.md §3.1).
    /// MapRoot se coloca en la pose de aparejo del proxy de Bootstrap para que,
    /// al parentarlo bajo el WorldRoot (pose inversa del proxy, la misma
    /// arrancada de MapPosition), cada hijo quede exactamente en su coordenada
    /// de diseño. La ruta avanza hacia +Z.
    ///
    /// Menús: SonarCrew > Mapa SubmarineTest (persona A) > …
    /// </summary>
    public static class SubmarineTestSetup
    {
        // ------------------------------------------------------------------ rutas
        private const string ScenePath = "Assets/Scenes/SubmarineTest.unity";
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string DataFolder = "Assets/Data";
        private const string MaterialFolder = "Assets/Materials";

        private const string RockPrefabPath = PrefabFolder + "/Rock.prefab";
        private const string ColumnPrefabPath = PrefabFolder + "/Column.prefab";
        private const string ArchPrefabPath = PrefabFolder + "/TerrainArch.prefab";
        private const string CurrentPrefabPath = PrefabFolder + "/Current.prefab";
        private const string MinePrefabPath = PrefabFolder + "/Mine.prefab";

        private const string CurrentLateralSuavePath = DataFolder + "/CurrentConfig_LateralSuave.asset";
        private const string CurrentLateralFuertePath = DataFolder + "/CurrentConfig_LateralFuerte.asset";
        private const string CurrentVerticalSubidaPath = DataFolder + "/CurrentConfig_VerticalSubida.asset";
        private const string CurrentMinasLateralPath = DataFolder + "/CurrentConfig_MinasLateral.asset";
        private const string MineConfigAssetPath = DataFolder + "/MineConfig.asset";

        // ------------------------------------------------------ diseño aprobado
        // Pose de aparejo del proxy en Bootstrap: SubmarineFrame.Awake arranca
        // MapPosition aquí; MapRoot local debe coincidir (ver cabecera de clase).
        private static readonly Vector3 SpawnPose = new Vector3(-0.95f, 2.8529f, 3.0333f);

        // Confines jugables en coordenadas de mapa que el motor debe aplicar
        // (x ±350, y −200…+10, z −100…+1250). Menú 3.
        private static readonly Bounds PlayBounds =
            new Bounds(new Vector3(0f, -95f, 575f), new Vector3(700f, 210f, 1350f));

        // Tesoro único (colocación del hito 4 de la persona B).
        private static readonly Vector3 TreasureSpot = new Vector3(0f, -174.5f, 1150f);

        // Conteos esperados por la validación.
        private const int RockCount = 23;
        private const int ColumnCount = 11;
        private const int ArchCount = 4;
        private const int CurrentCount = 4;
        private const int MineCount = 16;
        private const float MinMineSeparation = 17f;

        // Dressing de la pasada de pulido (hito 6a): piedras sueltas, lomos de
        // terreno, algas, placas de lecho, anillos de columna, escalones,
        // dovelas de arco, balizas y plaza del tesoro; todo SIN collider para
        // no ensuciar sónar ni sonda, con anclas manuales y semilla fija.
        private const int PolishSeed = 61126;
        private static readonly string[] DressingPrefixes =
        {
            "Piedra_", "Lomo_", "Alga_", "Placa_", "Anillo_", "Escalon_",
            "Dovela_", "Baliza_", "PilarTesoro_", "Terraza_"
        };
        private static readonly int[] DressingCounts = { 40, 12, 36, 12, 55, 6, 20, 4, 4, 2 };

        // Posiciones de columnas y arcos: única fuente de verdad para el montaje
        // y para el dressing que las acompaña (anillos, escalones, dovelas).
        private static readonly Vector2[] ColumnXZ =
        {
            new Vector2(-165f, 35f), new Vector2(172f, 88f),      // Z0
            new Vector2(205f, -52f), new Vector2(232f, 48f),      // Z1 eslalon
            new Vector2(262f, -38f), new Vector2(290f, 58f),
            new Vector2(318f, -55f), new Vector2(348f, 42f),
            new Vector2(372f, -30f), new Vector2(398f, 55f),
            new Vector2(415f, 20f)
        };
        private static readonly Vector3[] ArchPlacements =
        {
            new Vector3(10f, -40f, 250f),      // Z1
            new Vector3(-15f, -40f, 395f),
            new Vector3(0f, -90f, 640f),      // Z2
            new Vector3(0f, -110f, 962f)      // Z4
        };
        private static readonly float[] ArchYaw = { 0f, 8f, 0f, 0f };
        private static readonly string[] ArchNames = { "Arco_Z1_1", "Arco_Z1_2", "Arco_Z2_1", "Arco_Z4_1" };

        // --------------------------------------------------------------- menús

        [MenuItem("SonarCrew/Mapa SubmarineTest (persona A)/1. Generar mapa completo")]
        public static void Generate()
        {
            int worldLayer = LayerMask.NameToLayer("World");
            int hazardLayer = LayerMask.NameToLayer("Hazard");
            int hullLayer = LayerMask.NameToLayer("HullProxy");
            if (worldLayer < 0 || hazardLayer < 0 || hullLayer < 0)
            {
                Debug.LogError("SubmarineTestSetup: faltan las capas World/Hazard/HullProxy " +
                               "en TagManager. Abortado.");
                return;
            }

            if (!RequireCleanScenes("SubmarineTestSetup/Generar")) return;

            EnsureFolders();

            Material matSuelo = EnsureMaterial("Mat_Suelo", new Color(0.41f, 0.42f, 0.40f), 0.10f);
            Material matSueloMedio = EnsureMaterial("Mat_Suelo_Medio", new Color(0.30f, 0.28f, 0.25f), 0.06f);
            Material matSueloProfundo = EnsureMaterial("Mat_Suelo_Profundo", new Color(0.15f, 0.17f, 0.21f), 0.08f);
            Material matRoca = EnsureMaterial("Mat_Roca", new Color(0.45f, 0.43f, 0.42f), 0.15f);
            Material matRocaOscura = EnsureMaterial("Mat_Roca_Oscura", new Color(0.26f, 0.28f, 0.32f), 0.12f);
            Material matColumna = EnsureMaterial("Mat_Columna", new Color(0.30f, 0.32f, 0.35f), 0.20f);
            Material matPerimetro = EnsureMaterial("Mat_Perimetro", new Color(0.10f, 0.11f, 0.14f), 0.10f);
            Material matPiedraClara = EnsureMaterial("Mat_PiedraClara", new Color(0.59f, 0.60f, 0.57f), 0.10f);
            Material matPiedraMedia = EnsureMaterial("Mat_PiedraMedia", new Color(0.59f, 0.56f, 0.49f), 0.20f);
            Material matAcero = EnsureMaterial("Mat_Acero", new Color(0.47f, 0.39f, 0.38f), 0.35f);
            Material matMinaCuerpo = EnsureMaterial("Mat_Mina_Cuerpo", new Color(0.20f, 0.16f, 0.16f), 0.25f,
                new Color(0.28f, 0.20f, 0.16f));
            Material matMina = EnsureMaterial("Mat_Mina", new Color(0.63f, 0.27f, 0.19f), 0.20f,
                new Color(2.20f, 0.70f, 0.30f));
            Material matBaliza = EnsureMaterial("Mat_Baliza", new Color(0.51f, 0.53f, 0.57f), 0.15f);
            Material matBalizaBanda = EnsureMaterial("Mat_Baliza_Banda", new Color(0.35f, 0.38f, 0.41f), 0.25f);
            Material matTesoro = EnsureMaterial("Mat_Tesoro", new Color(0.85f, 0.62f, 0.22f), 0.35f,
                new Color(1.40f, 0.90f, 0.35f));
            Material matAlga = EnsureMaterial("Mat_Alga", new Color(0.12f, 0.29f, 0.18f), 0.30f);

            EnsureCurrentConfig(CurrentLateralSuavePath, new Vector3(1.2f, 0f, 0f));
            EnsureCurrentConfig(CurrentLateralFuertePath, new Vector3(2.5f, 0f, 0f));
            EnsureCurrentConfig(CurrentVerticalSubidaPath, new Vector3(0f, 1.8f, 0f));
            EnsureCurrentConfig(CurrentMinasLateralPath, new Vector3(0.8f, 0f, 0f));
            EnsureMineConfig(8f);
            // Estabiliza los assets recién creados antes de cablear referencias:
            // la importación de un prefab en el mismo montaje puede invalidar
            // referencias en memoria y grabar overrides a NULL.
            AssetDatabase.SaveAssets();

            GameObject rockPrefab = EnsureRockPrefab(worldLayer, matRoca);
            GameObject columnPrefab = EnsureColumnPrefab(worldLayer, matColumna);
            GameObject archPrefab = EnsureArchPrefab(worldLayer, matRoca);
            GameObject currentPrefab = EnsureCurrentPrefab(worldLayer, hullLayer);
            GameObject minePrefab = EnsureMinePrefab(hazardLayer, hullLayer, matMinaCuerpo, matMina, matAcero);

            Scene scene = BuildScene(rockPrefab, columnPrefab, archPrefab, currentPrefab, minePrefab,
                matSuelo, matSueloMedio, matSueloProfundo, matRoca, matRocaOscura, matPerimetro,
                matTesoro, matAlga, matPiedraClara, matPiedraMedia, matBaliza, matBalizaBanda);

            List<string> errores = ValidateScene();
            if (errores.Count > 0)
            {
                foreach (string error in errores) Debug.LogError("SubmarineTestSetup: " + error);
                Debug.LogError("SubmarineTestSetup: validación con " + errores.Count +
                                " errores. La escena queda ABIERTA pero NO se ha guardado.");
                return;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("SubmarineTestSetup: SubmarineTest generada y validada en " + ScenePath +
                      ". Recuerda aplicar los confines al motor (menú 3) cuando Bootstrap esté libre.");
        }

        [MenuItem("SonarCrew/Mapa SubmarineTest (persona A)/2. Validar mapa")]
        public static void Validate()
        {
            if (!RequireCleanScenes("SubmarineTestSetup/Validar")) return;

            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                Debug.LogError("SubmarineTestSetup: no existe " + ScenePath +
                               ". Ejecuta primero el menú 1. Abortado.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            List<string> errores = ValidateScene();
            if (errores.Count > 0)
            {
                foreach (string error in errores) Debug.LogError("SubmarineTestSetup: " + error);
                Debug.LogError("SubmarineTestSetup: validación FALLIDA con " + errores.Count + " errores.");
                return;
            }

            Debug.Log("SubmarineTestSetup: validación OK (" + RockCount + " rocas, " + ColumnCount +
                      " columnas, " + ArchCount + " arcos, " + CurrentCount + " corrientes, " +
                      MineCount + " minas, tesoro en " + TreasureSpot + ").");
        }

        /// <summary>
        /// Paso de integración que toca Bootstrap (propiedad de la persona A):
        /// actualiza los confines del motor a los del mapa. Coordinar antes de
        /// ejecutar si la otra persona tiene la escena abierta o en curso.
        /// </summary>
        [MenuItem("SonarCrew/Mapa SubmarineTest (persona A)/3. Aplicar confines al motor (Bootstrap)")]
        public static void ApplyMotorBounds()
        {
            if (!RequireCleanScenes("SubmarineTestSetup/Confines")) return;

            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);

            GameObject submarine = GameObject.Find("Submarine");
            if (submarine == null)
            {
                Debug.LogError("SubmarineTestSetup: no hay objeto 'Submarine' en Bootstrap. Abortado.");
                return;
            }

            SubmarineMotor motor = submarine.GetComponent<SubmarineMotor>();
            if (motor == null)
            {
                Debug.LogError("SubmarineTestSetup: el proxy no tiene SubmarineMotor. Abortado.");
                return;
            }

            SerializedObject serialized = new SerializedObject(motor);
            SerializedProperty bounds = serialized.FindProperty("worldBounds");
            if (bounds == null)
            {
                Debug.LogError("SubmarineTestSetup: SubmarineMotor no expone 'worldBounds'. Abortado.");
                return;
            }

            bounds.boundsValue = PlayBounds;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("SubmarineTestSetup: confines del motor = centro " + PlayBounds.center +
                      ", tamaño " + PlayBounds.size + " (coordenadas de mapa).");
        }

        // -------------------------------------------------------------- escena

        /// <summary>Construye la escena completa en memoria y la devuelve abierta.</summary>
        private static Scene BuildScene(GameObject rockPrefab, GameObject columnPrefab,
            GameObject archPrefab, GameObject currentPrefab, GameObject minePrefab,
            Material matSuelo, Material matSueloMedio, Material matSueloProfundo,
            Material matRoca, Material matRocaOscura, Material matPerimetro,
            Material matTesoro, Material matAlga, Material matPiedraClara,
            Material matPiedraMedia, Material matBaliza, Material matBalizaBanda)
        {
            int worldLayer = LayerMask.NameToLayer("World");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject mapRootGo = new GameObject("MapRoot");
            mapRootGo.transform.localPosition = SpawnPose;
            mapRootGo.transform.localRotation = Quaternion.identity;
            mapRootGo.AddComponent<MapRootAttachment>();
            Transform mapRoot = mapRootGo.transform;

            BuildStructure(mapRoot, worldLayer, matSuelo, matSueloMedio, matSueloProfundo,
                matRoca, matRocaOscura, matPerimetro);
            BuildRocks(mapRoot, rockPrefab);
            BuildColumns(mapRoot, columnPrefab);
            BuildArches(mapRoot, archPrefab);
            BuildCurrents(mapRoot, currentPrefab);
            BuildMines(mapRoot, minePrefab);
            BuildDressing(mapRoot, worldLayer, matRoca, matAlga, matPiedraClara,
                matPiedraMedia, matBaliza, matBalizaBanda);
            BuildTreasure(mapRoot, worldLayer, matRoca, matRocaOscura, matTesoro,
                matPiedraClara, matPiedraMedia);
            BuildAtmosphere();
            BuildPreviewUtilities();

            return scene;
        }

        /// <summary>
        /// Atmósfera de la escena EN SOLITARIO: niebla lineal y ambiente frío.
        /// En el juego integrado los RenderSettings que mandan son los de la
        /// escena ACTIVA (Bootstrap), así que esto solo mejora la previsualización
        /// del mapa; la niebla/luz del juego la configuran A/B en Bootstrap.
        /// </summary>
        private static void BuildAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 180f;
            RenderSettings.fogColor = new Color(0.063f, 0.125f, 0.204f);     // 16,32,52
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.235f, 0.337f);   // 38,60,86
            RenderSettings.ambientIntensity = 0.40f;
        }

        /// <summary>Suelos por zona, terrazas, perímetro cerrado y muros interiores.</summary>
        private static void BuildStructure(Transform mapRoot, int worldLayer,
            Material matSuelo, Material matSueloMedio, Material matSueloProfundo,
            Material matRoca, Material matRocaOscura, Material matPerimetro)
        {
            // name, centro, tamaño, material. La paleta oscurece con la profundidad
            // para que el descenso se lea sin necesitan de niebla.
            AddSlab(mapRoot, worldLayer, "Suelo_Z0Z1", new Vector3(0f, -45f, 160f), new Vector3(710f, 10f, 520f), matSuelo);          // bahía + cañón
            AddSlab(mapRoot, worldLayer, "Suelo_Z2a", new Vector3(0f, -55f, 470f), new Vector3(710f, 10f, 100f), matSueloMedio);    // terraza −50
            AddSlab(mapRoot, worldLayer, "Suelo_Z2b", new Vector3(0f, -75f, 570f), new Vector3(710f, 10f, 100f), matSueloMedio);    // terraza −70
            AddSlab(mapRoot, worldLayer, "Suelo_Z2c", new Vector3(0f, -95f, 660f), new Vector3(710f, 10f, 80f), matSueloMedio);     // terraza −90
            AddSlab(mapRoot, worldLayer, "Suelo_Z3", new Vector3(0f, -105f, 825f), new Vector3(710f, 10f, 250f), matSueloProfundo); // campo de minas
            AddSlab(mapRoot, worldLayer, "Suelo_Z4a", new Vector3(0f, -115f, 990f), new Vector3(710f, 10f, 80f), matSueloProfundo); // fosa −110
            AddSlab(mapRoot, worldLayer, "Suelo_Z4b", new Vector3(0f, -145f, 1070f), new Vector3(710f, 10f, 80f), matSueloProfundo); // fosa −140
            AddSlab(mapRoot, worldLayer, "Suelo_Z4c", new Vector3(0f, -195f, 1180f), new Vector3(710f, 10f, 140f), matSueloProfundo); // fosa −190
            AddSlab(mapRoot, worldLayer, "Suelo_Base", new Vector3(0f, -210f, 575f), new Vector3(710f, 10f, 1350f), matPerimetro);  // red de seguridad
            AddSlab(mapRoot, worldLayer, "Techo_Limite", new Vector3(0f, 20f, 575f), new Vector3(710f, 10f, 1350f), matPerimetro);

            // Perímetro cerrado: nunca hay vacío ni salida del mapa.
            AddSlab(mapRoot, worldLayer, "Pared_Oeste", new Vector3(-355f, -92.5f, 575f), new Vector3(10f, 230f, 1360f), matPerimetro);
            AddSlab(mapRoot, worldLayer, "Pared_Este", new Vector3(355f, -92.5f, 575f), new Vector3(10f, 230f, 1360f), matPerimetro);
            AddSlab(mapRoot, worldLayer, "Pared_Sur", new Vector3(0f, -92.5f, -105f), new Vector3(710f, 230f, 10f), matPerimetro);
            AddSlab(mapRoot, worldLayer, "Pared_Norte", new Vector3(0f, -92.5f, 1255f), new Vector3(710f, 230f, 10f), matPerimetro);

            // Cañón de columnas (Z1): paredes que estrechan el paso.
            AddSlab(mapRoot, worldLayer, "Canon_Pared_Oeste", new Vector3(-114f, -15f, 285f), new Vector3(8f, 50f, 270f), matRocaOscura);
            AddSlab(mapRoot, worldLayer, "Canon_Pared_Este", new Vector3(114f, -15f, 285f), new Vector3(8f, 50f, 270f), matRocaOscura);

            // Fosa del tesoro (Z4): muros de la fosa con un hueco de entrada al sur
            // (x −13…13), donde se coloca el arco de entrada.
            AddSlab(mapRoot, worldLayer, "Fosa_Muro_OesteA", new Vector3(-39f, -102.5f, 967.5f), new Vector3(52f, 185f, 35f), matRocaOscura);
            AddSlab(mapRoot, worldLayer, "Fosa_Muro_EsteA", new Vector3(39f, -102.5f, 967.5f), new Vector3(52f, 185f, 35f), matRocaOscura);
            AddSlab(mapRoot, worldLayer, "Fosa_Muro_OesteB", new Vector3(-69f, -102.5f, 1117.5f), new Vector3(8f, 185f, 265f), matRocaOscura);
            AddSlab(mapRoot, worldLayer, "Fosa_Muro_EsteB", new Vector3(69f, -102.5f, 1117.5f), new Vector3(8f, 185f, 265f), matRocaOscura);
        }

        private static void BuildRocks(Transform mapRoot, GameObject rockPrefab)
        {
            // posiciones de diseño (x, y, z) y escala relativa; hundidas ~30 % en el suelo.
            Vector3[] positions =
            {
                new Vector3(-218f, -41.65f, -68f),   // Z0
                new Vector3(152f, -41.35f, 12f),
                new Vector3(66f, -42.10f, 96f),
                new Vector3(-96f, -41.50f, -18f),
                new Vector3(247f, -41.80f, 62f),
                new Vector3(-43f, -41.95f, 118f),
                new Vector3(52f, -51.50f, 448f),     // Z2, terraza −50
                new Vector3(-80f, -51.65f, 470f),
                new Vector3(75f, -51.95f, 500f),
                new Vector3(110f, -51.50f, 490f),
                new Vector3(-58f, -71.35f, 545f),     // Z2, terraza −70
                new Vector3(62f, -71.80f, 575f),
                new Vector3(-72f, -72.10f, 600f),
                new Vector3(48f, -91.50f, 685f),      // Z2, terraza −90
                new Vector3(-45f, -91.65f, 655f),
                new Vector3(95f, -91.50f, 670f),
                new Vector3(-135f, -101.50f, 760f),   // Z3, bordes del campo
                new Vector3(140f, -101.80f, 800f),
                new Vector3(-150f, -101.95f, 870f),
                new Vector3(-38f, -111.50f, 1005f),  // Z4, fosa
                new Vector3(40f, -141.65f, 1045f),
                new Vector3(-35f, -141.80f, 1100f),
                new Vector3(42f, -191.50f, 1180f)
            };
            Vector3[] scales =
            {
                new Vector3(1.3f, 1.1f, 1.2f),
                new Vector3(1.5f, 0.9f, 1.3f),
                new Vector3(1.0f, 1.4f, 1.1f),
                new Vector3(1.2f, 1.0f, 0.8f),
                new Vector3(1.6f, 1.2f, 1.4f),
                new Vector3(0.9f, 1.3f, 1.0f),
                new Vector3(1.2f, 1.0f, 0.9f),
                new Vector3(1.4f, 1.1f, 1.2f),
                new Vector3(1.1f, 1.3f, 1.0f),
                new Vector3(1.0f, 1.0f, 0.9f),
                new Vector3(1.5f, 0.9f, 1.3f),
                new Vector3(1.0f, 1.2f, 0.9f),
                new Vector3(1.3f, 1.4f, 1.1f),
                new Vector3(1.2f, 1.0f, 1.3f),
                new Vector3(1.4f, 1.1f, 0.9f),
                new Vector3(1.1f, 1.0f, 0.9f),
                new Vector3(1.3f, 1.0f, 1.1f),
                new Vector3(1.5f, 1.2f, 1.0f),
                new Vector3(1.1f, 1.3f, 1.2f),
                new Vector3(1.2f, 1.0f, 1.1f),
                new Vector3(1.4f, 1.1f, 1.0f),
                new Vector3(1.1f, 1.2f, 1.3f),
                new Vector3(1.3f, 1.0f, 1.2f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject rock = Place(rockPrefab, mapRoot, "Roca_" + (i + 1).ToString("00"),
                    positions[i], Quaternion.identity, scales[i]);
                rock.transform.localRotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);
            }
        }

        private static void BuildColumns(Transform mapRoot, GameObject columnPrefab)
        {
            // Dos decorativas en la bahía y nueve en eslalon dentro del cañón.
            float floorY = -40f;

            for (int i = 0; i < ColumnXZ.Length; i++)
            {
                Place(columnPrefab, mapRoot, "Columna_" + (i + 1).ToString("00"),
                    new Vector3(ColumnXZ[i].x, floorY + 27.5f, ColumnXZ[i].y), Quaternion.identity, Vector3.one);
            }
        }

        private static void BuildArches(Transform mapRoot, GameObject archPrefab)
        {
            // Hueco de 18 m de ancho × 14 m de alto; la raíz se apoya en el suelo.
            for (int i = 0; i < ArchPlacements.Length; i++)
            {
                Place(archPrefab, mapRoot, ArchNames[i], ArchPlacements[i],
                    Quaternion.Euler(0f, ArchYaw[i], 0f), Vector3.one);
            }
        }

        private static void BuildCurrents(Transform mapRoot, GameObject currentPrefab)
        {
            PlaceCurrent(mapRoot, currentPrefab, "Corriente_Z1_LateralSuave",
                new Vector3(0f, -25f, 360f), new Vector3(140f, 40f, 120f), CurrentLateralSuavePath);
            PlaceCurrent(mapRoot, currentPrefab, "Corriente_Z2_LateralFuerte",
                new Vector3(0f, -30f, 500f), new Vector3(160f, 40f, 60f), CurrentLateralFuertePath);
            PlaceCurrent(mapRoot, currentPrefab, "Corriente_Z2_Vertical",
                new Vector3(0f, -83f, 640f), new Vector3(34f, 28f, 34f), CurrentVerticalSubidaPath);
            PlaceCurrent(mapRoot, currentPrefab, "Corriente_Z3_MinasLateral",
                new Vector3(0f, -80f, 815f), new Vector3(170f, 45f, 180f), CurrentMinasLateralPath);
        }

        private static void BuildMines(Transform mapRoot, GameObject minePrefab)
        {
            // Campo en zigzag (Z3): pasillos diagonales de ~15 m libres.
            float y = -75f;
            Vector2[] xz =
            {
                new Vector2(-45f, 740f), new Vector2(-9f, 740f), new Vector2(27f, 740f),
                new Vector2(-63f, 780f), new Vector2(-27f, 780f), new Vector2(9f, 780f), new Vector2(45f, 780f),
                new Vector2(-45f, 820f), new Vector2(-9f, 820f), new Vector2(27f, 820f),
                new Vector2(-63f, 860f), new Vector2(-27f, 860f), new Vector2(9f, 860f), new Vector2(45f, 860f)
            };
            for (int i = 0; i < xz.Length; i++)
            {
                Place(minePrefab, mapRoot, "Mina_Z3_" + (i + 1).ToString("00"),
                    new Vector3(xz[i].x, y, xz[i].y), Quaternion.identity, Vector3.one);
            }

            // Guardando la retirada de la fosa (Z4), a ambos lados del paso del arco.
            Place(minePrefab, mapRoot, "Mina_Z4_15", new Vector3(-10f, -101f, 983f), Quaternion.identity, Vector3.one);
            Place(minePrefab, mapRoot, "Mina_Z4_16", new Vector3(10f, -101f, 983f), Quaternion.identity, Vector3.one);
        }

        /// <summary>
        /// Dressing de la pasada de pulido: piedras sueltas, lomos de terreno y
        /// algas. Todo SIN collider para no ensuciar el sónar ni la sonda de
        /// terreno, con anclas manuales y jitter determinista (semilla fija).
        /// </summary>
        private static void BuildDressing(Transform mapRoot, int worldLayer,
            Material matRoca, Material matAlga, Material matPiedraClara,
            Material matPiedraMedia, Material matBaliza, Material matBalizaBanda)
        {
            System.Random rng = new System.Random(PolishSeed);

            // Piedras sueltas: 10 anclas × 4, hundidas ~25 %.
            Vector3[] anclasPiedras =
            {
                new Vector3(-160f, -40f, 30f), new Vector3(150f, -40f, 90f),
                new Vector3(24f, -40f, 252f), new Vector3(-4f, -40f, 248f),
                new Vector3(50f, -50f, 450f), new Vector3(-60f, -70f, 560f),
                new Vector3(45f, -90f, 660f), new Vector3(-130f, -100f, 770f),
                new Vector3(135f, -100f, 830f), new Vector3(-40f, -110f, 1005f)
            };
            int piedra = 0;
            foreach (Vector3 ancla in anclasPiedras)
            {
                for (int i = 0; i < 4; i++)
                {
                    piedra++;
                    float s = 0.6f + (float)rng.NextDouble() * 1.0f;
                    Vector3 escala = new Vector3(1.4f, 0.9f, 1.2f) * s;
                    Vector3 pos = new Vector3(
                        ancla.x + ((float)rng.NextDouble() * 2f - 1f) * 5f,
                        ancla.y - 0.25f * escala.y,
                        ancla.z + ((float)rng.NextDouble() * 2f - 1f) * 5f);
                    AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Piedra_" + piedra.ToString("00"),
                        pos, escala, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), matRoca);
                }
            }

            // Lomos de terreno: media docena+ de ondulaciones hundidas ~55 %,
            // fuera de la línea central de la ruta para no distraer del pilotaje.
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_01",
                new Vector3(-120f, -44.4f, -30f), new Vector3(26f, 8f, 18f), Quaternion.Euler(0f, 18f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_02",
                new Vector3(210f, -43.85f, 40f), new Vector3(22f, 7f, 16f), Quaternion.Euler(0f, -25f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_03",
                new Vector3(-85f, -43.3f, 300f), new Vector3(18f, 6f, 14f), Quaternion.Euler(0f, 40f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_04",
                new Vector3(85f, -43.85f, 340f), new Vector3(20f, 7f, 15f), Quaternion.Euler(0f, 10f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_05",
                new Vector3(-90f, -54.4f, 500f), new Vector3(24f, 8f, 18f), Quaternion.Euler(0f, 65f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_06",
                new Vector3(95f, -73.85f, 590f), new Vector3(20f, 7f, 16f), Quaternion.Euler(0f, -30f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_07",
                new Vector3(-75f, -93.3f, 665f), new Vector3(18f, 6f, 14f), Quaternion.Euler(0f, 20f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_08",
                new Vector3(-160f, -104.4f, 850f), new Vector3(26f, 8f, 20f), Quaternion.Euler(0f, -50f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_09",
                new Vector3(160f, -103.85f, 780f), new Vector3(22f, 7f, 18f), Quaternion.Euler(0f, 35f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_10",
                new Vector3(-55f, -113.3f, 1020f), new Vector3(16f, 6f, 12f), Quaternion.Euler(0f, 15f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_11",
                new Vector3(50f, -142.75f, 1090f), new Vector3(15f, 5f, 11f), Quaternion.Euler(0f, -40f, 0f), matRoca);
            AddVisual(PrimitiveType.Sphere, mapRoot, worldLayer, "Lomo_12",
                new Vector3(35f, -193.3f, 1190f), new Vector3(18f, 6f, 14f), Quaternion.Euler(0f, 70f, 0f), matRoca);

            // Algas: matas verticales estáticas (sin fauna, sin animación).
            Vector3[] anclasAlgas =
            {
                new Vector3(-140f, -40f, 60f), new Vector3(180f, -40f, 20f),
                new Vector3(-70f, -40f, 180f), new Vector3(65f, -40f, 210f),
                new Vector3(70f, -50f, 480f), new Vector3(-70f, -70f, 610f),
                new Vector3(-110f, -100f, 880f), new Vector3(110f, -100f, 900f)
            };
            int alga = 0;
            for (int c = 0; c < anclasAlgas.Length; c++)
            {
                int tiras = c % 2 == 0 ? 5 : 4;
                for (int i = 0; i < tiras; i++)
                {
                    alga++;
                    float altura = 9f + (float)rng.NextDouble() * 7f;
                    Vector3 pos = new Vector3(
                        anclasAlgas[c].x + ((float)rng.NextDouble() * 2f - 1f) * 4f,
                        anclasAlgas[c].y + altura * 0.5f - 0.5f,
                        anclasAlgas[c].z + ((float)rng.NextDouble() * 2f - 1f) * 4f);
                    Quaternion rot = Quaternion.Euler(
                        ((float)rng.NextDouble() * 2f - 1f) * 10f,
                        (float)rng.NextDouble() * 360f,
                        ((float)rng.NextDouble() * 2f - 1f) * 10f);
                    AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Alga_" + alga.ToString("00"),
                        pos, new Vector3(0.5f, altura, 0.5f), rot, matAlga);
                }
            }

            // Placas de lecho: losas aplanadas de piedra clara que rompen la
            // planicie y bajan el valor base del suelo (bahía y cañón).
            Vector3[] anclasPlacas =
            {
                new Vector3(-70f, -40f, 60f), new Vector3(80f, -40f, 20f),
                new Vector3(-40f, -40f, 165f), new Vector3(35f, -40f, 330f)
            };
            int placa = 0;
            foreach (Vector3 ancla in anclasPlacas)
            {
                for (int i = 0; i < 3; i++)
                {
                    placa++;
                    float lado = 3f + (float)rng.NextDouble() * 5f;
                    Vector3 pos = new Vector3(
                        ancla.x + ((float)rng.NextDouble() * 2f - 1f) * 10f,
                        ancla.y + 0.08f,
                        ancla.z + ((float)rng.NextDouble() * 2f - 1f) * 10f);
                    AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Placa_" + placa.ToString("00"),
                        pos, new Vector3(lado, 0.15f, lado),
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), matPiedraClara);
                }
            }

            // Anillos de estratificación en el fuste de cada columna: bandas que
            // rompen el "monolito" y dan silueta. Sin collider.
            int anillo = 0;
            float[] alturasAnillo = { -22f, -11f, 0f, 11f, 20f };
            for (int i = 0; i < ColumnXZ.Length; i++)
            {
                float desfase = (i % 3) * 2f - 2f;
                for (int r = 0; r < alturasAnillo.Length; r++)
                {
                    anillo++;
                    AddVisual(PrimitiveType.Cylinder, mapRoot, worldLayer, "Anillo_" + anillo.ToString("00"),
                        new Vector3(ColumnXZ[i].x, -40f + 27.5f + alturasAnillo[r] + desfase, ColumnXZ[i].y),
                        new Vector3(9.7f, 0.25f, 9.7f),
                        Quaternion.Euler(0f, (i * 17f) % 360f, 0f), matPiedraMedia);
                }
            }

            // Escalones de basa en algunas columnas (visuales, sin collider).
            int[] columnasEscalon = { 0, 1, 3, 6, 8, 10 };
            int escalon = 0;
            foreach (int i in columnasEscalon)
            {
                escalon++;
                AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Escalon_" + escalon.ToString("00"),
                    new Vector3(ColumnXZ[i].x, -39f, ColumnXZ[i].y), new Vector3(14f, 2f, 14f),
                    Quaternion.Euler(0f, (i * 23f) % 360f, 0f), matPiedraMedia);
            }

            // Dovelas de arco: claves claras sobre el dintel e impostas en las
            // jambas (a ras de la cara interna de la pata: no estrechan el vano).
            int dovela = 0;
            for (int a = 0; a < ArchPlacements.Length; a++)
            {
                Quaternion yaw = Quaternion.Euler(0f, ArchYaw[a], 0f);
                Vector3 origen = ArchPlacements[a];

                for (int k = 0; k < 3; k++)
                {
                    dovela++;
                    Vector3 local = new Vector3(-8f + 8f * k, 20.4f, 0f);
                    AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Dovela_" + dovela.ToString("00"),
                        origen + yaw * local, new Vector3(2.2f, 0.8f, 1.6f), yaw, matPiedraClara);
                }
                for (int lado = 0; lado < 2; lado++)
                {
                    dovela++;
                    float signo = lado == 0 ? 1f : -1f;
                    Vector3 local = new Vector3(9.4f * signo, 0.5f, 0f);
                    AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Dovela_" + dovela.ToString("00"),
                        origen + yaw * local, new Vector3(1.4f, 1.0f, 2.0f), yaw, matPiedraClara);
                }
            }

            // Balizas monumentales: obeliscos que anclan la navegación sur→norte
            // (entrada del cañón, salida, terrazas y entrada de la fosa).
            Vector3[] balizas =
            {
                new Vector3(-30f, -40f, 160f), new Vector3(28f, -40f, 425f),
                new Vector3(-40f, -70f, 585f), new Vector3(30f, -110f, 955f)
            };
            for (int b = 0; b < balizas.Length; b++)
            {
                GameObject balizaGo = new GameObject("Baliza_" + (b + 1).ToString("00"));
                balizaGo.layer = worldLayer;
                Transform baliza = balizaGo.transform;
                baliza.SetParent(mapRoot, false);
                baliza.localPosition = balizas[b];
                baliza.localRotation = Quaternion.Euler(0f, (b * 40f) % 360f, 0f);

                AddVisual(PrimitiveType.Cylinder, baliza, worldLayer, "Shaft",
                    new Vector3(0f, 13f, 0f), new Vector3(3f, 13f, 3f), Quaternion.identity, matBaliza);
                AddVisual(PrimitiveType.Cube, baliza, worldLayer, "Banda",
                    new Vector3(0f, 17f, 0f), new Vector3(3.3f, 1f, 3.3f), Quaternion.identity, matBalizaBanda);
                AddVisual(PrimitiveType.Cube, baliza, worldLayer, "Cap",
                    new Vector3(0f, 26.5f, 0f), new Vector3(3.6f, 1.8f, 3.6f), Quaternion.identity, matBaliza);
            }
        }

        private static void BuildTreasure(Transform mapRoot, int worldLayer,
            Material matRoca, Material matRocaOscura, Material matTesoro,
            Material matPiedraClara, Material matPiedraMedia)
        {
            // Pedestal: el hueco del tesoro queda 0,5 m sobre su cara superior.
            AddSlab(mapRoot, worldLayer, "Pedestal_Tesoro", new Vector3(0f, -182.5f, 1150f),
                new Vector3(10f, 15f, 10f), matRoca);

            // Faldón escalonado y aro dorado: solo visuales, sin collider, para
            // no tocar la mecánica del hueco del tesoro (hito 4 de la persona B).
            AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Pedestal_Faldon",
                new Vector3(0f, -184.5f, 1150f), new Vector3(16f, 11f, 16f), Quaternion.identity, matRocaOscura);
            AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Pedestal_Aro",
                new Vector3(0f, -176f, 1150f), new Vector3(12.6f, 1f, 12.6f), Quaternion.identity, matTesoro);

            // Plaza del tesoro: dos terrazas concéntricas (visuales).
            AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Terraza_Tesoro_A",
                new Vector3(0f, -190.2f, 1150f), new Vector3(24f, 0.5f, 24f), Quaternion.identity, matPiedraMedia);
            AddVisual(PrimitiveType.Cube, mapRoot, worldLayer, "Terraza_Tesoro_B",
                new Vector3(0f, -189.75f, 1150f), new Vector3(18f, 0.4f, 18f), Quaternion.identity, matPiedraClara);

            // Pilares esquineros que enmarcan el hueco del tesoro (visuales).
            for (int p = 0; p < 4; p++)
            {
                float sx = (p % 2 == 0) ? 1f : -1f;
                float sz = (p < 2) ? 1f : -1f;
                AddVisual(PrimitiveType.Cylinder, mapRoot, worldLayer, "PilarTesoro_" + (p + 1),
                    new Vector3(3.6f * sx, -173.8f, 1150f + 3.6f * sz), new Vector3(0.4f, 1.2f, 0.4f),
                    Quaternion.identity, matPiedraClara);
            }

            // Faro del tesoro: luz cálida que lo hace visible desde la fosa.
            GameObject luzGo = new GameObject("Luz_Tesoro");
            luzGo.transform.SetParent(mapRoot, false);
            luzGo.transform.localPosition = new Vector3(0f, -168f, 1150f);
            Light luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.72f, 0.32f);
            luz.intensity = 2.5f;
            luz.range = 28f;
            luz.shadows = LightShadows.None;

            // Luz cercana de gradación: una sola fuente cálida sobre el hueco
            // para que el pedestal modele volumen, no solo emisión plana.
            GameObject luzCercaGo = new GameObject("Luz_Tesoro_Cerca");
            luzCercaGo.transform.SetParent(mapRoot, false);
            luzCercaGo.transform.localPosition = new Vector3(0f, -173f, 1150f);
            Light luzCerca = luzCercaGo.AddComponent<Light>();
            luzCerca.type = LightType.Point;
            luzCerca.color = new Color(1f, 0.78f, 0.47f);
            luzCerca.intensity = 2.5f;
            luzCerca.range = 12f;
            luzCerca.shadows = LightShadows.None;

            GameObject spot = new GameObject("TreasureSpot");
            spot.transform.SetParent(mapRoot, false);
            spot.transform.localPosition = TreasureSpot;
        }

        /// <summary>Cámara y luz apagadas para previsualizar la escena en solitario.</summary>
        private static void BuildPreviewUtilities()
        {
            GameObject camGo = new GameObject("PreviewCamera_Editor");
            camGo.transform.localPosition = new Vector3(0f, 48f, 500f);
            camGo.transform.localRotation = Quaternion.Euler(52f, 0f, 0f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 3000f;
            cam.enabled = false;

            GameObject lightGo = new GameObject("PreviewLight_Editor");
            lightGo.transform.localRotation = Quaternion.Euler(25f, -30f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.90f, 0.94f, 1.00f);
            light.intensity = 2.3f;
            // Sin esto la luz nace con LightShadows.None y el contraste por
            // sombra (la base de la jerarquía de valores) no existe.
            light.shadows = LightShadows.Soft;
            light.enabled = false;
        }

        // ----------------------------------------------------------- validación

        /// <summary>
        /// Inspecciona la escena activa: capas, triggers, conteos, separación de
        /// minas, confines de corrientes, pose de MapRoot y raíces de escena.
        /// </summary>
        private static List<string> ValidateScene()
        {
            List<string> errores = new List<string>();
            int worldLayer = LayerMask.NameToLayer("World");
            int hazardLayer = LayerMask.NameToLayer("Hazard");

            GameObject mapRoot = GameObject.Find("MapRoot");
            if (mapRoot == null)
            {
                errores.Add("No existe MapRoot en la escena.");
                return errores;
            }

            if ((mapRoot.transform.localPosition - SpawnPose).sqrMagnitude > 1e-6f)
                errores.Add("MapRoot no está en la pose de aparejo " + SpawnPose + ".");
            if (mapRoot.transform.localRotation != Quaternion.identity)
                errores.Add("MapRoot no tiene rotación identidad.");
            if (mapRoot.GetComponent<MapRootAttachment>() == null)
                errores.Add("MapRoot no tiene MapRootAttachment.");

            // Solo MapRoot y las utilidades de previsualización en la raíz.
            Scene scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "MapRoot" && root.name != "PreviewCamera_Editor" &&
                    root.name != "PreviewLight_Editor")
                    errores.Add("Objeto suelto en la raíz de la escena: '" + root.name + "'.");
            }

            // Capas y triggers: los triggers solo pueden ser volúmenes de corriente.
            foreach (Collider collider in mapRoot.GetComponentsInChildren<Collider>(true))
            {
                if (collider.gameObject.layer != worldLayer && collider.gameObject.layer != hazardLayer)
                    errores.Add("Collider fuera de capa World/Hazard: '" + collider.name + "'.");

                bool esCorriente = collider.GetComponent<CurrentVolume>() != null;
                if (collider.isTrigger && !esCorriente)
                    errores.Add("Trigger sin CurrentVolume: '" + collider.name + "'.");
                if (esCorriente && !collider.isTrigger)
                    errores.Add("CurrentVolume sin collider de trigger: '" + collider.name + "'.");
            }

            // Conteos por prefijo de nombre.
            int rocas = 0, columnas = 0, arcos = 0, corrientes = 0, minas = 0;
            foreach (Transform child in mapRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == mapRoot.transform) continue;
                if (child.name.StartsWith("Roca_")) rocas++;
                else if (child.name.StartsWith("Columna_")) columnas++;
                else if (child.name.StartsWith("Arco_")) arcos++;
                else if (child.name.StartsWith("Corriente_")) corrientes++;
                else if (child.name.StartsWith("Mina_")) minas++;
            }
            if (rocas != RockCount) errores.Add("Rocas: " + rocas + " (esperadas " + RockCount + ").");
            if (columnas != ColumnCount) errores.Add("Columnas: " + columnas + " (esperadas " + ColumnCount + ").");
            if (arcos != ArchCount) errores.Add("Arcos: " + arcos + " (esperados " + ArchCount + ").");
            if (corrientes != CurrentCount) errores.Add("Corrientes: " + corrientes + " (esperadas " + CurrentCount + ").");
            if (minas != MineCount) errores.Add("Minas: " + minas + " (esperadas " + MineCount + ").");

            // Tesoro único en su sitio exacto.
            Transform spot = mapRoot.transform.Find("TreasureSpot");
            if (spot == null)
            {
                errores.Add("Falta el hueco TreasureSpot.");
            }
            else if ((spot.localPosition - TreasureSpot).sqrMagnitude > 0.0025f)
            {
                errores.Add("TreasureSpot en " + spot.localPosition + " (esperado " + TreasureSpot + ").");
            }

            // Minas: config cableada, capa Hazard y separación mínima entre ellas.
            List<Vector3> posicionesMinas = new List<Vector3>();
            foreach (Mine mina in mapRoot.GetComponentsInChildren<Mine>(true))
            {
                SerializedObject serialized = new SerializedObject(mina);
                if (serialized.FindProperty("config").objectReferenceValue == null)
                    errores.Add("Mina sin MineConfig: '" + mina.name + "'.");
                if (serialized.FindProperty("hullMask.m_Bits").intValue == 0)
                    errores.Add("Mina sin máscara de casco: '" + mina.name + "'.");
                if (mina.gameObject.layer != hazardLayer)
                    errores.Add("Mina fuera de capa Hazard: '" + mina.name + "'.");
                posicionesMinas.Add(mina.transform.localPosition);
            }
            for (int i = 0; i < posicionesMinas.Count; i++)
            {
                for (int j = i + 1; j < posicionesMinas.Count; j++)
                {
                    if (Vector3.Distance(posicionesMinas[i], posicionesMinas[j]) < MinMineSeparation)
                        errores.Add("Minas " + (i + 1) + " y " + (j + 1) + " más cerca de " +
                                    MinMineSeparation + " m.");
                }
            }

            // Corrientes: config cableada y volumen dentro de los confines jugables.
            foreach (CurrentVolume corriente in mapRoot.GetComponentsInChildren<CurrentVolume>(true))
            {
                SerializedObject serialized = new SerializedObject(corriente);
                if (serialized.FindProperty("config").objectReferenceValue == null)
                    errores.Add("Corriente sin CurrentConfig: '" + corriente.name + "'.");
                if (serialized.FindProperty("hullMask.m_Bits").intValue == 0)
                    errores.Add("Corriente sin máscara de casco: '" + corriente.name + "'.");

                BoxCollider box = corriente.GetComponent<BoxCollider>();
                Vector3 centro = corriente.transform.localPosition + box.center;
                Vector3 mitad = Vector3.Scale(box.size * 0.5f, corriente.transform.localScale);
                Bounds volumen = new Bounds(centro, mitad * 2f);
                if (!PlayBounds.Contains(volumen.min) || !PlayBounds.Contains(volumen.max))
                    errores.Add("Corriente fuera de confines: '" + corriente.name + "'.");
            }

            // Minas dentro de los confines jugables.
            foreach (Vector3 posicion in posicionesMinas)
            {
                if (!PlayBounds.Contains(posicion))
                    errores.Add("Mina fuera de confines: " + posicion + ".");
            }

            // Dressing del pulido: conteos y reglas (visual = sin collider).
            int[] dressing = new int[DressingPrefixes.Length];
            foreach (Transform child in mapRoot.GetComponentsInChildren<Transform>(true))
            {
                for (int d = 0; d < DressingPrefixes.Length; d++)
                {
                    if (child.name.StartsWith(DressingPrefixes[d])) { dressing[d]++; break; }
                }
            }
            for (int d = 0; d < DressingPrefixes.Length; d++)
            {
                if (dressing[d] != DressingCounts[d])
                    errores.Add("Dressing '" + DressingPrefixes[d] + "': " + dressing[d] +
                        " (esperados " + DressingCounts[d] + ").");
            }

            foreach (Transform child in mapRoot.GetComponentsInChildren<Transform>(true))
            {
                bool esDressing = false;
                for (int d = 0; d < DressingPrefixes.Length; d++)
                    if (child.name.StartsWith(DressingPrefixes[d])) { esDressing = true; break; }
                if (!esDressing) continue;

                if (child.GetComponent<Collider>() != null)
                    errores.Add("Dressing con collider (debe ser visual): '" + child.name + "'.");
                if (child.gameObject.layer != worldLayer)
                    errores.Add("Dressing fuera de capa World: '" + child.name + "'.");
            }

            // Estructura de los arcos tras el pulido: patas a ±12 (hueco de 18 m).
            foreach (Transform arco in mapRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!arco.name.StartsWith("Arco_")) continue;
                Transform legWest = arco.Find("LegWest");
                Transform legEast = arco.Find("LegEast");
                if (legWest == null || legEast == null || arco.Find("Beam") == null)
                {
                    errores.Add("Arco sin estructura completa: '" + arco.name + "'.");
                    continue;
                }
                if (Mathf.Abs(Mathf.Abs(legWest.localPosition.x) - 12f) > 0.01f ||
                    Mathf.Abs(Mathf.Abs(legEast.localPosition.x) - 12f) > 0.01f)
                    errores.Add("Arco con patas desplazadas (hueco ≠ 18 m): '" + arco.name + "'.");
            }

            // Pedestal y faro del tesoro.
            Transform pedestal = mapRoot.transform.Find("Pedestal_Tesoro");
            if (pedestal == null)
            {
                errores.Add("Falta el pedestal del tesoro.");
            }
            else if ((pedestal.localPosition - new Vector3(0f, -182.5f, 1150f)).sqrMagnitude > 0.01f ||
                     (pedestal.localScale - new Vector3(10f, 15f, 10f)).sqrMagnitude > 0.01f)
            {
                errores.Add("Pedestal del tesoro cambiado de sitio o tamaño.");
            }

            GameObject luzTesoro = GameObject.Find("Luz_Tesoro");
            if (luzTesoro == null)
            {
                errores.Add("Falta la luz del tesoro.");
            }
            else
            {
                Light luz = luzTesoro.GetComponent<Light>();
                if (luz == null || !luz.enabled || luz.range > 30f)
                    errores.Add("Luz del tesoro sin Light activo o de largo alcance.");
                if (luzTesoro.transform.parent != mapRoot.transform)
                    errores.Add("Luz del tesoro fuera del MapRoot.");
            }

            GameObject luzCerca = GameObject.Find("Luz_Tesoro_Cerca");
            if (luzCerca == null)
            {
                errores.Add("Falta la luz cercana del tesoro.");
            }
            else
            {
                Light luz = luzCerca.GetComponent<Light>();
                if (luz == null || !luz.enabled || luz.range > 15f)
                    errores.Add("Luz cercana del tesoro sin Light activo o de largo alcance.");
                if (luzCerca.transform.parent != mapRoot.transform)
                    errores.Add("Luz cercana del tesoro fuera del MapRoot.");
            }

            return errores;
        }

        /// <summary>Persistencia: nunca ejecutar con escenas sucias (evita perder trabajo ajeno).</summary>
        private static bool RequireCleanScenes(string tool)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.isDirty)
                {
                    Debug.LogError(tool + ": la escena '" + loaded.name + "' tiene cambios sin guardar. " +
                                   "Guárdala o descártala y repite. Abortado.");
                    return false;
                }
            }
            return true;
        }

        // --------------------------------------------------------------- assets

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");
        }

        private static Material EnsureMaterial(string name, Color baseColor, float smoothness)
        {
            return EnsureMaterial(name, baseColor, smoothness, null);
        }

        private static Material EnsureMaterial(string name, Color baseColor, float smoothness, Color? emission)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            // Reaplica siempre la paleta: ajustar el diseño y regenerar actualiza
            // los materiales existentes en vez de dejar assets desactualizados.
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = (mat.globalIlluminationFlags &
                    ~MaterialGlobalIlluminationFlags.EmissiveIsBlack) |
                    MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static CurrentConfig EnsureCurrentConfig(string path, Vector3 force)
        {
            CurrentConfig config = AssetDatabase.LoadAssetAtPath<CurrentConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CurrentConfig>();
                config.force = force;
                AssetDatabase.CreateAsset(config, path);
            }
            return config;
        }

        private static MineConfig EnsureMineConfig(float impulse)
        {
            MineConfig config = AssetDatabase.LoadAssetAtPath<MineConfig>(MineConfigAssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MineConfig>();
                config.impulse = impulse;
                AssetDatabase.CreateAsset(config, MineConfigAssetPath);
            }
            return config;
        }

        // -------------------------------------------------------------- prefabs

        private static GameObject EnsureRockPrefab(int worldLayer, Material mat)
        {
            // Se regenera SIEMPRE: la herramienta es la única fuente de verdad del
            // prefab (SaveAsPrefabAsset sobre la misma ruta conserva su GUID).
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Rock";
            root.GetComponent<MeshRenderer>().sharedMaterial = mat;
            root.transform.localScale = new Vector3(6f, 4.5f, 5f);

            // Protuberancias: roca con carácter; sin collider extra (el perfil
            // físico sigue siendo la esfera raíz que valida el layout).
            AddLump(root, new Vector3(0.45f, -0.15f, 0.3f), new Vector3(0.55f, 0.65f, 0.6f), new Vector3(0f, 25f, 10f));
            AddLump(root, new Vector3(-0.4f, 0.2f, -0.35f), new Vector3(0.45f, 0.5f, 0.55f), new Vector3(0f, 160f, -12f));
            AddLump(root, new Vector3(-0.1f, 0.35f, 0.45f), new Vector3(0.5f, 0.45f, 0.5f), new Vector3(0f, 70f, 18f));
            SetLayerRecursive(root, worldLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RockPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static GameObject EnsureColumnPrefab(int worldLayer, Material mat)
        {
            // Se regenera SIEMPRE: la herramienta es la única fuente de verdad del
            // prefab (SaveAsPrefabAsset sobre la misma ruta conserva su GUID).
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Column";
            root.GetComponent<MeshRenderer>().sharedMaterial = mat;
            root.transform.localScale = new Vector3(9f, 27.5f, 9f);   // fuste: 9 m de diámetro, 55 m de alto

            // Basa y capitel: ensanchan base y remate; con collider (son terreno),
            // el eslalom sigue dejando pasillos de sobra entre columnas.
            GameObject basa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basa.name = "Base";
            basa.GetComponent<MeshRenderer>().sharedMaterial = mat;
            basa.transform.SetParent(root.transform, false);
            basa.transform.localPosition = new Vector3(0f, -25f, 0f);
            basa.transform.localScale = new Vector3(12f, 5f, 12f);

            GameObject capitel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            capitel.name = "Capital";
            capitel.GetComponent<MeshRenderer>().sharedMaterial = mat;
            capitel.transform.SetParent(root.transform, false);
            capitel.transform.localPosition = new Vector3(0f, 25.5f, 0f);
            capitel.transform.localScale = new Vector3(12f, 4f, 12f);

            SetLayerRecursive(root, worldLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ColumnPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>Arco estrecho: hueco de 18 m de ancho × 14 m de alto; raíz en el suelo.</summary>
        private static GameObject EnsureArchPrefab(int worldLayer, Material mat)
        {
            // Se regenera SIEMPRE: la herramienta es la única fuente de verdad del
            // prefab (SaveAsPrefabAsset sobre la misma ruta conserva su GUID).
            GameObject root = new GameObject("TerrainArch");

            GameObject legWest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legWest.name = "LegWest";
            legWest.GetComponent<MeshRenderer>().sharedMaterial = mat;
            legWest.transform.SetParent(root.transform, false);
            legWest.transform.localPosition = new Vector3(-12f, 7f, 0f);
            legWest.transform.localScale = new Vector3(6f, 14f, 6f);

            GameObject legEast = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legEast.name = "LegEast";
            legEast.GetComponent<MeshRenderer>().sharedMaterial = mat;
            legEast.transform.SetParent(root.transform, false);
            legEast.transform.localPosition = new Vector3(12f, 7f, 0f);
            legEast.transform.localScale = new Vector3(6f, 14f, 6f);

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.GetComponent<MeshRenderer>().sharedMaterial = mat;
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0f, 17f, 0f);
            beam.transform.localScale = new Vector3(32f, 6f, 6f);

            // Escombros en los pies (con collider: son terreno). Nunca a |x| < 10
            // para no estrechar el hueco de 18 m del arco.
            AddArchRubble(mat, root, "RubbleE1", new Vector3(12f, 0.8f, 2.2f), new Vector3(3f, 1.6f, 2.6f), new Vector3(0f, 15f, 8f));
            AddArchRubble(mat, root, "RubbleE2", new Vector3(14.5f, 0.6f, -1.8f), new Vector3(2.4f, 1.2f, 2.2f), new Vector3(0f, -25f, -6f));
            AddArchRubble(mat, root, "RubbleE3", new Vector3(11.5f, 1.1f, 0.6f), new Vector3(2f, 2.2f, 2f), new Vector3(0f, 40f, 14f));
            AddArchRubble(mat, root, "RubbleW1", new Vector3(-12f, 0.7f, -2.4f), new Vector3(2.6f, 1.4f, 2.4f), new Vector3(0f, 20f, -7f));
            AddArchRubble(mat, root, "RubbleW2", new Vector3(-14f, 1f, 1.6f), new Vector3(2.2f, 2f, 2.2f), new Vector3(0f, -15f, 10f));
            AddArchRubble(mat, root, "RubbleW3", new Vector3(-11.2f, 0.5f, 0.2f), new Vector3(1.8f, 1f, 1.8f), new Vector3(0f, 55f, -12f));

            SetLayerRecursive(root, worldLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ArchPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static GameObject EnsureCurrentPrefab(int worldLayer, int hullLayer)
        {
            // Se regenera SIEMPRE: la herramienta es la única fuente de verdad del
            // prefab (SaveAsPrefabAsset sobre la misma ruta conserva su GUID).
            // Config por defecto del prefab, cargada por ruta (ver PlaceCurrent).
            CurrentConfig config = AssetDatabase.LoadAssetAtPath<CurrentConfig>(CurrentLateralSuavePath);

            GameObject root = new GameObject("Current");
            root.layer = worldLayer;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one;
            box.center = Vector3.zero;

            CurrentVolume volume = root.AddComponent<CurrentVolume>();
            SerializedObject serialized = new SerializedObject(volume);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("hullMask.m_Bits").intValue = 1 << hullLayer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CurrentPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static GameObject EnsureMinePrefab(int hazardLayer, int hullLayer,
            Material matCuerpo, Material matAro, Material matAcero)
        {
            // Se regenera SIEMPRE: la herramienta es la única fuente de verdad del
            // prefab (SaveAsPrefabAsset sobre la misma ruta conserva su GUID).
            // Config por defecto del prefab, cargada por ruta (ver PlaceCurrent).
            MineConfig config = AssetDatabase.LoadAssetAtPath<MineConfig>(MineConfigAssetPath);

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Mine";
            root.GetComponent<MeshRenderer>().sharedMaterial = matCuerpo;
            root.transform.localScale = Vector3.one * 3f;   // esfera de 3 m de diámetro

            // Aro ecuatorial de aviso: aquí vive el acento naranja y la emisión,
            // para que la mina lea como objeto Y como peligro (no como estrella).
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.GetComponent<MeshRenderer>().sharedMaterial = matAro;
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.identity;
            ring.transform.localScale = new Vector3(1.05f, 0.04f, 1.05f);

            // Segundo aro, en plano vertical: la mina lee como objeto desde
            // cualquier rumbo y no como un logotipo horizontal flotante.
            GameObject ring2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring2.name = "Ring2";
            UnityEngine.Object.DestroyImmediate(ring2.GetComponent<Collider>());
            ring2.GetComponent<MeshRenderer>().sharedMaterial = matAro;
            ring2.transform.SetParent(root.transform, false);
            ring2.transform.localPosition = Vector3.zero;
            ring2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring2.transform.localScale = new Vector3(1.05f, 0.04f, 1.05f);

            // Tetones de acero: masa visual corta en los seis ejes.
            AddStud(root, matAcero, new Vector3(0.45f, 0f, 0f), new Vector3(0f, 0f, -90f));
            AddStud(root, matAcero, new Vector3(-0.45f, 0f, 0f), new Vector3(0f, 0f, 90f));
            AddStud(root, matAcero, new Vector3(0f, 0f, 0.45f), new Vector3(90f, 0f, 0f));
            AddStud(root, matAcero, new Vector3(0f, 0f, -0.45f), new Vector3(-90f, 0f, 0f));
            AddStud(root, matAcero, new Vector3(0f, 0.45f, 0f), Vector3.zero);
            AddStud(root, matAcero, new Vector3(0f, -0.45f, 0f), new Vector3(180f, 0f, 0f));

            Mine mine = root.AddComponent<Mine>();
            SerializedObject serialized = new SerializedObject(mine);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("hullMask.m_Bits").intValue = 1 << hullLayer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursive(root, hazardLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MinePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>Tetón de acero de la mina: cilindro corto sin collider.</summary>
        private static void AddStud(GameObject parent, Material mat, Vector3 localPosition, Vector3 localEuler)
        {
            GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stud.name = "Stud";
            UnityEngine.Object.DestroyImmediate(stud.GetComponent<Collider>());
            stud.GetComponent<MeshRenderer>().sharedMaterial = mat;
            stud.transform.SetParent(parent.transform, false);
            stud.transform.localPosition = localPosition;
            stud.transform.localRotation = Quaternion.Euler(localEuler);
            stud.transform.localScale = new Vector3(0.06f, 0.2f, 0.06f);
        }

        // -------------------------------------------------------------- helpers

        private static void AddSlab(Transform parent, int layer, string name,
            Vector3 localPosition, Vector3 size, Material mat)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.layer = layer;
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = size;
        }

        /// <summary>
        /// Pieza visual de dressing: primitiva SIN collider para no ensuciar el
        /// sónar ni la sonda de terreno (AGENTS.md §3.1: sin collider no
        /// aparece en ninguna consulta física).
        /// </summary>
        private static GameObject AddVisual(PrimitiveType type, Transform parent, int layer, string name,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material mat)
        {
            GameObject piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.layer = layer;
            UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.GetComponent<MeshRenderer>().sharedMaterial = mat;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = localRotation;
            piece.transform.localScale = localScale;
            return piece;
        }

        /// <summary>Protuberancia visual de una roca: esfera hija sin collider.</summary>
        private static void AddLump(GameObject parent, Vector3 localPosition, Vector3 localScale, Vector3 localEuler)
        {
            GameObject lump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lump.name = "Lump";
            UnityEngine.Object.DestroyImmediate(lump.GetComponent<Collider>());
            lump.GetComponent<MeshRenderer>().sharedMaterial = parent.GetComponent<MeshRenderer>().sharedMaterial;
            lump.transform.SetParent(parent.transform, false);
            lump.transform.localPosition = localPosition;
            lump.transform.localRotation = Quaternion.Euler(localEuler);
            lump.transform.localScale = localScale;
        }

        /// <summary>Bloque de escombro en el pie de un arco (con collider: es terreno).</summary>
        private static void AddArchRubble(Material mat, GameObject parent, string name,
            Vector3 localPosition, Vector3 localScale, Vector3 localEuler)
        {
            GameObject rubble = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rubble.name = name;
            rubble.GetComponent<MeshRenderer>().sharedMaterial = mat;
            rubble.transform.SetParent(parent.transform, false);
            rubble.transform.localPosition = localPosition;
            rubble.transform.localRotation = Quaternion.Euler(localEuler);
            rubble.transform.localScale = localScale;
        }

        private static GameObject Place(GameObject prefab, Transform parent, string name,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
            return instance;
        }

        private static void PlaceCurrent(Transform mapRoot, GameObject prefab, string name,
            Vector3 localPosition, Vector3 localSize, string configPath)
        {
            GameObject instance = Place(prefab, mapRoot, name, localPosition, Quaternion.identity, localSize);

            // La config se carga por ruta JUSTO antes de cablear: una referencia
            // obtenida al crear los assets del mismo montaje puede quedar
            // invalidada por la importación del prefab y grabar un NULL.
            CurrentConfig config = AssetDatabase.LoadAssetAtPath<CurrentConfig>(configPath);
            if (config == null)
            {
                Debug.LogError("SubmarineTestSetup: no existe la config " + configPath +
                               " (instancia '" + name + "').");
                return;
            }

            SerializedObject serialized = new SerializedObject(instance.GetComponent<CurrentVolume>());
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        // --------------------------------------------------------------- gizmos

        /// <summary>Marcador del hueco del tesoro (solo editor, sin huella en runtime).</summary>
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawTreasureSpotGizmo(Transform transform, GizmoType gizmoType)
        {
            if (transform.name != "TreasureSpot") return;

            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 2.5f);
            Gizmos.DrawLine(transform.position + Vector3.down * 2.5f, transform.position + Vector3.down * 18f);
            Gizmos.DrawWireCube(transform.position + Vector3.down * 18f, new Vector3(1.2f, 0.4f, 1.2f));
        }
    }
}
