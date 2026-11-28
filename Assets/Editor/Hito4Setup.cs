using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonarCrew.Environment;

namespace SonarCrew.EditorTools
{
    /// <summary>
    /// Montaje del hito 4 (persona B): rehace el interior con el gancho y su
    /// pantalla (hito 2) y monta en `Bootstrap` el tesoro único bajo el `WorldRoot`,
    /// justo debajo del gancho para poder probar la victoria.
    ///
    /// El gancho cuelga del suelo del casco en el marco estático del interior, así
    /// que su cota es fija; el tesoro es contenido del mapa y se coloca con la
    /// traslación entre el marco dibujado (el casco en el origen) y el mapa (la
    /// posición de arranque del proxy), que es la que aplica `SubmarineFrame`.
    /// Es idempotente: reejecutarlo rehace tesoro y gancho sin duplicarlos.
    /// Menú: SonarCrew > Hito 4 (persona B) > Montar escena.
    /// </summary>
    public static class Hito4Setup
    {
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        private const string TreasurePrefabPath = "Assets/Prefabs/Treasure.prefab";
        private const string TreasureMaterialPath = "Assets/Materials/World/Treasure.mat";
        private const string TreasureRootName = "Tesoro";
        private const string GroundPath = "WorldPlaceholder/Ground";

        /// <summary>Cota del centro del cofre sobre el fondo del mapa.</summary>
        private const float TreasureHeight = 0.35f;

        [MenuItem("SonarCrew/Hito 4 (persona B)/Montar escena", false, 40)]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Hito 4] Sal del modo Play antes de montar la escena.");
                return;
            }

            // El gancho y la pantalla viven en la escena del interior (hito 2).
            SubmarineInteriorBuilder.BuildInterior();

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != BootstrapPath)
                scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);

            GameObject submarine = GameObject.Find("Submarine");
            if (submarine == null)
            {
                Debug.LogError("[Hito 4] No hay un objeto 'Submarine' en Bootstrap. Abortado.");
                return;
            }

            GameObject worldRootObject = GameObject.Find("WorldRoot");
            if (worldRootObject == null)
            {
                Debug.LogError("[Hito 4] No hay 'WorldRoot'. Ejecuta antes el montaje del hito 1.");
                return;
            }

            Transform worldRoot = worldRootObject.transform;
            Transform ground = worldRoot.Find(GroundPath);
            if (ground == null)
            {
                Debug.LogError("[Hito 4] No se encontró " + GroundPath + " bajo el WorldRoot. Abortado.");
                return;
            }

            GameObject treasurePrefab = EnsureTreasurePrefab();
            if (treasurePrefab == null) return;

            PlaceTreasure(worldRoot, ground, submarine.transform.position, treasurePrefab);
            EnsureSceneInBuildSettings(BootstrapPath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Hito 4] Gancho montado en SubmarineInterior y tesoro colocado bajo el gancho. " +
                      "Entra en la sala del gancho, ocupa la estación y suelta cable con [X] o el stick derecho.");
        }

        /// <summary>
        /// Cuelga el tesoro del WorldRoot justo debajo del gancho. El casco se dibuja
        /// en el origen y el mundo con la pose inversa del proxy, así que la
        /// coordenada de mapa que queda bajo el gancho es la posición de arranque
        /// del proxy en horizontal y la cota del fondo en vertical.
        /// </summary>
        private static void PlaceTreasure(Transform worldRoot, Transform ground, Vector3 spawn, GameObject prefab)
        {
            Transform existing = worldRoot.Find(TreasureRootName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, worldRoot);
            instance.name = TreasureRootName;
            instance.transform.localPosition = new Vector3(spawn.x, ground.localPosition.y + TreasureHeight, spawn.z);
            instance.transform.localRotation = Quaternion.identity;
        }

        private static GameObject EnsureTreasurePrefab()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Materials/World");

            GameObject prototype = BuildTreasurePrototype(EnsureTreasureMaterial());
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prototype, TreasurePrefabPath);
            Object.DestroyImmediate(prototype);

            if (prefab == null)
            {
                Debug.LogError("[Hito 4] No se pudo guardar el prefab " + TreasurePrefabPath);
                return null;
            }

            return prefab;
        }

        /// <summary>Cofre de pruebas: cuerpo, tapa, cerradura y una luz que lo delata en la cámara del gancho.</summary>
        private static GameObject BuildTreasurePrototype(Material material)
        {
            GameObject root = new GameObject("Treasure");
            root.AddComponent<TreasureCollectible>();

            CreateBox(root.transform, "Cuerpo", new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.5f, 0.6f), Quaternion.identity, material);
            CreateBox(root.transform, "Tapa", new Vector3(0f, 0.32f, 0f), new Vector3(0.94f, 0.16f, 0.64f), Quaternion.identity, material);
            CreateBox(root.transform, "Cerradura", new Vector3(0f, 0.04f, 0.32f), new Vector3(0.16f, 0.18f, 0.06f), Quaternion.identity, material);

            GameObject lightObject = new GameObject("Brillo");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            Light glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 8f;
            glow.intensity = 3f;
            glow.color = new Color(1f, 0.82f, 0.42f);
            glow.shadows = LightShadows.None;

            // El tesoro no es física: se captura por distancia desde el gancho.
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            // Es contenido del mapa: capa World, la misma que ven el sónar y el gancho.
            int worldLayer = LayerMask.NameToLayer("World");
            if (worldLayer < 0)
            {
                Debug.LogWarning("[Hito 4] No existe la capa 'World'; el tesoro se queda en Default.");
                return root;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = worldLayer;

            return root;
        }

        private static Material EnsureTreasureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TreasureMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("[Hito 4] No se encontró el shader URP/Lit; revisa el render pipeline.");
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, TreasureMaterialPath);
            }

            material.SetColor("_BaseColor", new Color(0.86f, 0.66f, 0.2f));
            material.SetFloat("_Metallic", 0.85f);
            material.SetFloat("_Smoothness", 0.6f);
            EditorUtility.SetDirty(material);
            return material;
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

            if (material != null)
            {
                Renderer renderer = box.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }

            return box;
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

        /// <summary>
        /// Deja la escena de arranque en Build Settings: `GameFlow` la recarga para
        /// reiniciar la partida tras la victoria.
        /// </summary>
        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            if (!File.Exists(scenePath)) return;

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == scenePath) return;
            }

            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[current.Length + 1];
            System.Array.Copy(current, updated, current.Length);
            updated[current.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;
            Debug.Log("[Hito 4] " + scenePath + " añadida a Build Settings (necesaria para reiniciar la partida).");
        }
    }
}
