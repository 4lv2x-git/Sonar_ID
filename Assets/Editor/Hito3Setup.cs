using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonarCrew.Submarine;

namespace SonarCrew.EditorTools
{
    /// <summary>
    /// Montaje del hito 3 (persona A): config del sónar, SonarSystem y NavigationSystem
    /// en el proxy, máscaras de capas y hazards placeholder para verificar contactos.
    /// Idempotente: se puede repetir.
    /// Menú: SonarCrew > Hito 3 (persona A) > Montar sónar y navegación.
    /// </summary>
    public static class Hito3Setup
    {
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        private const string DataFolder = "Assets/Data";
        private const string SonarConfigPath = "Assets/Data/SonarConfig.asset";

        [MenuItem("SonarCrew/Hito 3 (persona A)/Montar sónar y navegación")]
        public static void Setup()
        {
            int worldLayer = LayerMask.NameToLayer("World");
            int hazardLayer = LayerMask.NameToLayer("Hazard");
            if (worldLayer < 0 || hazardLayer < 0)
            {
                Debug.LogError("Hito3Setup: faltan las capas World/Hazard. Ejecuta antes " +
                               "SonarCrew > Hito 1 (persona A) > Montar escena Bootstrap.");
                return;
            }

            SonarConfig config = EnsureSonarConfig();

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != BootstrapPath)
                scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);

            GameObject submarine = GameObject.Find("Submarine");
            if (submarine == null)
            {
                Debug.LogError("Hito3Setup: no hay un objeto 'Submarine' en la escena. Abortado.");
                return;
            }

            SonarSystem sonar = submarine.GetComponent<SonarSystem>();
            if (sonar == null) sonar = Undo.AddComponent<SonarSystem>(submarine);

            if (submarine.GetComponent<NavigationSystem>() == null)
                Undo.AddComponent<NavigationSystem>(submarine);

            SubmarineFrame frame = submarine.GetComponent<SubmarineFrame>();
            if (frame == null)
                Debug.LogError("Hito3Setup: falta SubmarineFrame; ejecuta el montaje del hito 1.");

            SerializedObject sonarSerialized = new SerializedObject(sonar);
            sonarSerialized.FindProperty("frame").objectReferenceValue = frame;
            sonarSerialized.FindProperty("config").objectReferenceValue = config;

            SerializedProperty contactMask = sonarSerialized.FindProperty("contactMask.m_Bits");
            if (contactMask != null)
                contactMask.intValue = (1 << worldLayer) | (1 << hazardLayer);

            SerializedProperty hazardMask = sonarSerialized.FindProperty("hazardMask.m_Bits");
            if (hazardMask != null)
                hazardMask.intValue = 1 << hazardLayer;

            sonarSerialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureHazards(hazardLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Hito3Setup: sónar y navegación montados (máscara World|Hazard, hazards " +
                      "placeholder en WorldRoot). Reproduce y mira los gizmos del SonarSystem.");
        }

        /// <summary>Crea el asset de configuración del sónar si no existe.</summary>
        private static SonarConfig EnsureSonarConfig()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets", "Data");

            SonarConfig config = AssetDatabase.LoadAssetAtPath<SonarConfig>(SonarConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SonarConfig>();
                AssetDatabase.CreateAsset(config, SonarConfigPath);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        /// <summary>Dos hazards placeholder bajo el WorldRoot para verificar contactos.</summary>
        private static void EnsureHazards(int hazardLayer)
        {
            GameObject worldRoot = GameObject.Find("WorldRoot");
            if (worldRoot == null)
            {
                Debug.LogWarning("Hito3Setup: no hay WorldRoot; sin hazards placeholder.");
                return;
            }

            Transform placeholder = worldRoot.transform.Find("WorldPlaceholder");
            if (placeholder == null)
            {
                Debug.LogWarning("Hito3Setup: no hay mundo placeholder; sin hazards de prueba.");
                return;
            }

            Vector3[] positions =
            {
                new Vector3(-8f, -4.75f, 40f),
                new Vector3(12f, -4.5f, 65f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                string objectName = "Mine" + i;
                if (placeholder.Find(objectName) != null) continue;

                GameObject mine = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Undo.RegisterCreatedObjectUndo(mine, "Crear hazard placeholder");
                mine.name = objectName;
                mine.layer = hazardLayer;
                mine.transform.SetParent(placeholder, false);
                mine.transform.localPosition = positions[i];
                mine.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
            }
        }
    }
}
