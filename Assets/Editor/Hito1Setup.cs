using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonarCrew.Core;
using SonarCrew.Submarine;

namespace SonarCrew.EditorTools
{
    /// <summary>
    /// Montaje del hito 1 (persona A): capas del proyecto, matriz de colisión,
    /// config de rebase, WorldRoot con mundo placeholder y volúmenes de sala del
    /// proxy con su wiring a SubmarineFrame. Idempotente: se puede repetir.
    /// Menú: SonarCrew > Hito 1 (persona A) > Montar escena Bootstrap.
    /// </summary>
    public static class Hito1Setup
    {
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        private const string DataFolder = "Assets/Data";
        private const string FrameConfigPath = "Assets/Data/SubmarineFrameConfig.asset";

        private const string InteriorLayer = "Interior";
        private const string HullProxyLayer = "HullProxy";
        private const string WorldLayer = "World";
        private const string HazardLayer = "Hazard";

        private static readonly string[] LayerNames = { InteriorLayer, HullProxyLayer, WorldLayer, HazardLayer };

        private static readonly string[] InteriorObjects =
        {
            "HullFloor", "HullWall_E", "HullWall_W", "HullWall_N", "HullWall_S"
        };

        [MenuItem("SonarCrew/Hito 1 (persona A)/Montar escena Bootstrap")]
        public static void Setup()
        {
            EnsureLayers();
            EnsureInteriorVsHullDisabled();
            SubmarineFrameConfig frameConfig = EnsureFrameConfig();

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != BootstrapPath)
                scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);

            GameObject submarine = GameObject.Find("Submarine");
            if (submarine == null)
            {
                Debug.LogError("Hito1Setup: no hay un objeto 'Submarine' en la escena. Abortado.");
                return;
            }

            int hullLayer = LayerMask.NameToLayer(HullProxyLayer);
            int worldLayer = LayerMask.NameToLayer(WorldLayer);

            // Capa del casco y de su collider principal.
            submarine.layer = hullLayer;
            Collider hull = submarine.GetComponent<Collider>();
            if (hull != null) hull.gameObject.layer = hullLayer;

            // Componentes del proxy y del marco.
            SubmarineProxy proxy = submarine.GetComponent<SubmarineProxy>();
            if (proxy == null) proxy = Undo.AddComponent<SubmarineProxy>(submarine);

            SubmarineFrame frame = submarine.GetComponent<SubmarineFrame>();
            if (frame == null) frame = Undo.AddComponent<SubmarineFrame>(submarine);

            GameObject worldRoot = GameObject.Find("WorldRoot");
            if (worldRoot == null)
            {
                worldRoot = new GameObject("WorldRoot");
                Undo.RegisterCreatedObjectUndo(worldRoot, "Crear WorldRoot");
            }
            worldRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            SerializedObject frameSerialized = new SerializedObject(frame);
            frameSerialized.FindProperty("proxy").objectReferenceValue = proxy;
            frameSerialized.FindProperty("worldRoot").objectReferenceValue = worldRoot.transform;
            frameSerialized.FindProperty("config").objectReferenceValue = frameConfig;
            frameSerialized.ApplyModifiedPropertiesWithoutUndo();

            // Confines y sonda de terreno en coordenadas de mapa y solo con World/Hazard.
            SubmarineMotor motor = submarine.GetComponent<SubmarineMotor>();
            if (motor != null)
            {
                int terrainMask = (1 << worldLayer) | (1 << LayerMask.NameToLayer(HazardLayer));
                SerializedObject motorSerialized = new SerializedObject(motor);
                SerializedProperty bits = motorSerialized.FindProperty("terrainMask.m_Bits");
                if (bits != null)
                {
                    bits.intValue = terrainMask;
                    motorSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            BuildHullZones(submarine, hull as CapsuleCollider, hullLayer);
            TagInterior();
            EnsureWorldPlaceholder(worldRoot.transform, worldLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Hito1Setup: escena Bootstrap montada (capas, WorldRoot, cinco volúmenes de sala " +
                      "y wiring de SubmarineFrame). Reproduce para verificar hito 1.");
        }

        /// <summary>Crea las capas del proyecto que falten (primer hueco libre de usuario).</summary>
        private static void EnsureLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            foreach (string layerName in LayerNames)
            {
                if (LayerMask.NameToLayer(layerName) >= 0) continue;

                bool placed = false;
                for (int i = 8; i < layers.arraySize && !placed; i++)
                {
                    SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = layerName;
                        placed = true;
                    }
                }

                if (!placed)
                    Debug.LogError("Hito1Setup: no hay hueco libre para la capa '" + layerName + "'.");
            }

            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        /// <summary>El interior no colisiona con el casco: son el mismo espacio.</summary>
        private static void EnsureInteriorVsHullDisabled()
        {
            int interior = LayerMask.NameToLayer(InteriorLayer);
            int hull = LayerMask.NameToLayer(HullProxyLayer);
            if (interior < 0 || hull < 0) return;

            SerializedObject dynamics = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            SerializedProperty matrix = dynamics.FindProperty("m_LayerCollisionMatrix");
            if (matrix == null) return;

            SerializedProperty rowInterior = matrix.GetArrayElementAtIndex(interior);
            SerializedProperty rowHull = matrix.GetArrayElementAtIndex(hull);
            rowInterior.uintValue &= ~(1u << hull);
            rowHull.uintValue &= ~(1u << interior);

            dynamics.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Crea el asset de configuración del marco si no existe.</summary>
        private static SubmarineFrameConfig EnsureFrameConfig()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets", "Data");

            SubmarineFrameConfig config = AssetDatabase.LoadAssetAtPath<SubmarineFrameConfig>(FrameConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SubmarineFrameConfig>();
                AssetDatabase.CreateAsset(config, FrameConfigPath);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        /// <summary>
        /// Cinco volúmenes de sala (triggers) a lo largo del casco. No intervienen en
        /// la física: sirven para resolver el punto de contacto de una colisión a su
        /// HullZone (lo consume el DamageSystem de la persona B).
        /// </summary>
        private static void BuildHullZones(GameObject submarine, CapsuleCollider hull, int hullLayer)
        {
            HullZone[] zones =
            {
                HullZone.Gancho, HullZone.Maquinas, HullZone.Informacion, HullZone.Sensores, HullZone.Mandos
            };

            // El casco viaja a lo largo de su forward: el collider se alinea en Z.
            if (hull != null)
            {
                Undo.RecordObject(hull, "Alinear collider del casco");
                hull.direction = 2;
                EditorUtility.SetDirty(hull);
            }

            float hullLength = hull != null ? Mathf.Max(1f, hull.height) : 2f;
            float halfLength = hullLength * 0.5f;
            float segment = hullLength / zones.Length;
            float radius = hull != null ? Mathf.Max(0.2f, hull.radius) : 0.5f;

            for (int i = 0; i < zones.Length; i++)
            {
                string objectName = "HullZone_" + zones[i];
                Transform existing = submarine.transform.Find(objectName);
                GameObject room = existing != null ? existing.gameObject : new GameObject(objectName);
                if (existing == null)
                {
                    Undo.RegisterCreatedObjectUndo(room, "Crear volumen de sala");
                    room.transform.SetParent(submarine.transform, false);
                }

                room.layer = hullLayer;
                room.transform.localPosition = new Vector3(0f, 0f, -halfLength + segment * (i + 0.5f));
                room.transform.localRotation = Quaternion.identity;

                BoxCollider box = room.GetComponent<BoxCollider>();
                if (box == null) box = Undo.AddComponent<BoxCollider>(room);
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = new Vector3(radius * 2f, radius * 2f, segment);

                HullZoneMarker marker = room.GetComponent<HullZoneMarker>();
                if (marker == null) marker = Undo.AddComponent<HullZoneMarker>(room);
                marker.EditorSetZone(zones[i]);
                EditorUtility.SetDirty(marker);
            }
        }

        /// <summary>El casco interior (suelo y paredes) va a la capa Interior.</summary>
        private static void TagInterior()
        {
            int layer = LayerMask.NameToLayer(InteriorLayer);
            if (layer < 0) return;

            foreach (string objectName in InteriorObjects)
            {
                GameObject interiorObject = GameObject.Find(objectName);
                if (interiorObject != null)
                    interiorObject.layer = layer;
            }
        }

        /// <summary>
        /// Mundo placeholder bajo el WorldRoot: un plano grande y unas columnas que
        /// permiten ver el desplazamiento del mundo y el rebase. Hito 6a lo sustituye
        /// por el mapa a mano.
        /// </summary>
        private static void EnsureWorldPlaceholder(Transform worldRoot, int worldLayer)
        {
            if (worldRoot.Find("WorldPlaceholder") != null) return;

            GameObject placeholder = new GameObject("WorldPlaceholder");
            Undo.RegisterCreatedObjectUndo(placeholder, "Crear mundo placeholder");
            placeholder.transform.SetParent(worldRoot, false);
            placeholder.layer = worldLayer;

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(plane, "Crear plano placeholder");
            plane.name = "Ground";
            plane.layer = worldLayer;
            plane.transform.SetParent(placeholder.transform, false);
            plane.transform.localPosition = new Vector3(0f, -6f, 0f);
            plane.transform.localScale = new Vector3(20f, 1f, 20f);

            for (int i = 0; i < 6; i++)
            {
                GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(column, "Crear columna placeholder");
                column.name = "Column" + i;
                column.layer = worldLayer;
                column.transform.SetParent(placeholder.transform, false);
                column.transform.localPosition = new Vector3((i % 2 == 0 ? -1f : 1f) * 15f, -3f, 20f + i * 20f);
                column.transform.localScale = new Vector3(3f, 6f, 3f);
            }
        }
    }
}
