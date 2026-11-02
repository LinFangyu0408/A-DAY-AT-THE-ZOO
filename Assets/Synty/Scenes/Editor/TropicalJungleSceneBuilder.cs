#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Synty.SceneTools
{
    // Source GUID metadata is restored before building so prefab variants resolve.
    [InitializeOnLoad]
    public static class TropicalJungleSceneBuilder
    {
        const string ScenePath = "Assets/Synty/Scenes/TropicalJungle_Reference.unity";
        const string Root = "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle";
        const string Prefabs = Root + "/Prefabs";
        const string Materials = Root + "/Materials";
        static bool dependencyRefreshAttempted;

        static TropicalJungleSceneBuilder()
        {
            EditorApplication.delayCall += AutoBuild;
        }

        static void AutoBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoBuild;
                return;
            }

            Build();
        }

        [MenuItem("Synty/Build Tropical Jungle Reference Scene")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("Synty scene build postponed while Unity finishes importing.");
                EditorApplication.delayCall += Build;
                return;
            }

            EnsureFolder("Assets/Synty/Scenes");

            Scene scene = FindLoadedScene(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                    UnityEngine.Object.DestroyImmediate(rootObject);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                scene.name = "TropicalJungle_Reference";
            }

            SceneManager.SetActiveScene(scene);

            Transform layout = Group(scene, "Tropical Jungle Layout");
            Transform terrain = Group(scene, "01 Sand Clearing");
            Transform lagoon = Group(scene, "02 Lagoon and Waterfall");
            Transform cliffs = Group(scene, "03 Cliffs and Rocks");
            Transform structures = Group(scene, "04 Structures");
            Transform foliage = Group(scene, "05 Tropical Foliage");
            Transform lighting = Group(scene, "06 Lighting and Camera");

            BuildGround(terrain);
            BuildLagoon(lagoon);
            BuildCliffs(cliffs);
            BuildStructures(structures);
            BuildFoliage(foliage);
            BuildLighting(lighting);
            FixInvalidMaterials(scene);

            ParentAll(layout, terrain, lagoon, cliffs, structures, foliage, lighting);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("SYNTY_SCENE_BUILT:" + ScenePath);
        }

        static void BuildGround(Transform parent)
        {
            Material sand = GetOrCreateMaterial(
                "Assets/Synty/Scenes/Materials/TropicalSand.mat",
                new Color(0.72f, 0.61f, 0.46f),
                0.12f,
                false);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            MoveToScene(ground, parent.gameObject.scene);
            ground.name = "Sand Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, 0f, 2f);
            ground.transform.localScale = new Vector3(5.8f, 1f, 4.8f);
            SetMaterial(ground, sand);

            GameObject rearRise = GameObject.CreatePrimitive(PrimitiveType.Plane);
            MoveToScene(rearRise, parent.gameObject.scene);
            rearRise.name = "Rear Rock Shelf";
            rearRise.transform.SetParent(parent, false);
            rearRise.transform.position = new Vector3(0f, 0.08f, 19f);
            rearRise.transform.localScale = new Vector3(5.2f, 1f, 1.4f);
            SetMaterial(rearRise, sand);
        }

        static void BuildLagoon(Transform parent)
        {
            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            MoveToScene(pool, parent.gameObject.scene);
            pool.name = "Turquoise Lagoon";
            pool.transform.SetParent(parent, false);
            pool.transform.position = new Vector3(0f, 0.14f, 6.2f);
            pool.transform.localScale = new Vector3(15f, 0.04f, 10.5f);
            SetMaterial(pool, GetOrCreateMaterial(
                "Assets/Synty/Scenes/Materials/TropicalLagoon.mat",
                new Color(0.08f, 0.62f, 0.74f, 0.78f),
                0.92f,
                true));
            UnityEngine.Object.DestroyImmediate(pool.GetComponent<Collider>());

            Place(Prefabs + "/SM_Waterfall_Mesh_01.prefab", "Tall Waterfall", parent,
                new Vector3(0f, 6.4f, 17.2f), new Vector3(0f, 180f, 0f), new Vector3(3.2f, 1.8f, 1.6f));

            Place(Root + "/FX/FX_Prefabs/FX_Waterfall_Impact_01.prefab", "Waterfall Impact", parent,
                new Vector3(0f, 0.65f, 16.2f), Vector3.zero, new Vector3(1.8f, 1f, 1.8f));

            int rockIndex = 0;
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                float x = Mathf.Cos(a) * 8.2f;
                float z = 6.2f + Mathf.Sin(a) * 5.8f;
                string rock = Prefabs + (rockIndex % 3 == 0
                    ? "/SM_Env_Rock_Flat_01.prefab"
                    : rockIndex % 3 == 1
                        ? "/SM_Env_Rock_02.prefab"
                        : "/SM_Env_Rock_03.prefab");
                Place(rock, "Lagoon Rim Rock " + (i + 1), parent,
                    new Vector3(x, 0.15f, z),
                    new Vector3(0f, i * 31f, 0f),
                    Vector3.one * (0.65f + (i % 4) * 0.13f));
                rockIndex++;
            }

            Place(Prefabs + "/SM_Env_Rock_04.prefab", "Lagoon Island Rock", parent,
                new Vector3(-1.5f, 0.25f, 6.2f), new Vector3(0f, 22f, 0f), Vector3.one * 0.55f);
            Place(Prefabs + "/SM_Env_Rock_Flat_01.prefab", "Lagoon Island Rock 2", parent,
                new Vector3(1.3f, 0.1f, 5.8f), new Vector3(0f, 77f, 0f), new Vector3(0.45f, 0.35f, 0.45f));
        }

        static void BuildCliffs(Transform parent)
        {
            Vector3[] wall =
            {
                new Vector3(-24f, 1.2f, 20f),
                new Vector3(-17f, 1.8f, 22f),
                new Vector3(-10f, 1.4f, 20.5f),
                new Vector3(-4f, 1.2f, 21.5f),
                new Vector3(4f, 1.4f, 21f),
                new Vector3(10f, 1.7f, 21.8f),
                new Vector3(17f, 1.5f, 20.5f),
                new Vector3(24f, 1.2f, 20f)
            };

            for (int i = 0; i < wall.Length; i++)
            {
                string cliff = Prefabs + "/SM_Env_Rock_Cliff_0" + ((i % 3) + 1) + ".prefab";
                Place(cliff, "Back Cliff " + (i + 1), parent, wall[i],
                    new Vector3(0f, (i % 2 == 0 ? -8f : 12f), 0f),
                    new Vector3(1.55f, 1.45f, 1.55f));

                if (i % 2 == 0)
                {
                    Place(cliff, "Cliff Crown " + (i + 1), parent,
                        wall[i] + new Vector3(2f, 4.8f, 0.6f),
                        new Vector3(0f, i * 21f, 0f),
                        new Vector3(1.15f, 1.25f, 1.15f));
                }
            }

            Place(Prefabs + "/SM_Env_Rock_Cliff_02.prefab", "Left Cliff Shelf", parent,
                new Vector3(-16f, 3.2f, 12f), new Vector3(0f, -18f, 0f), new Vector3(1.45f, 1.35f, 1.45f));
            Place(Prefabs + "/SM_Env_Rock_Cliff_03.prefab", "Right Cliff Shelf", parent,
                new Vector3(16f, 3.4f, 12.8f), new Vector3(0f, 18f, 0f), new Vector3(1.55f, 1.4f, 1.55f));

            Vector3[] foreground =
            {
                new Vector3(-11f, 0.1f, -8f),
                new Vector3(9f, 0.1f, -8.5f),
                new Vector3(-5f, 0.1f, -12f),
                new Vector3(5f, 0.1f, -13f),
                new Vector3(19f, 0.1f, -4f),
                new Vector3(-20f, 0.1f, -4f)
            };
            for (int i = 0; i < foreground.Length; i++)
            {
                string rock = Prefabs + (i % 2 == 0 ? "/SM_Env_Rock_Pile_0" + ((i % 4) + 1) + ".prefab"
                    : "/SM_Env_Rock_0" + ((i % 5) + 1) + ".prefab");
                Place(rock, "Foreground Rock " + (i + 1), parent, foreground[i],
                    new Vector3(0f, i * 38f, 0f), Vector3.one * (0.75f + (i % 3) * 0.15f));
            }
        }

        static void BuildStructures(Transform parent)
        {
            Transform lookout = GroupInScene(parent, "Left Lookout");
            Place(Prefabs + "/SM_Env_Dock_01.prefab", "Lookout Deck", lookout,
                new Vector3(-14.5f, 4.1f, 8.6f), new Vector3(0f, 12f, 0f), new Vector3(1.25f, 1f, 1.25f));
            Place(Prefabs + "/SM_Bld_Shack_Pirate_01.prefab", "Elevated Jungle Shack", lookout,
                new Vector3(-14.5f, 4.25f, 8.6f), new Vector3(0f, 12f, 0f), new Vector3(1.25f, 1.25f, 1.25f));
            Place(Prefabs + "/SM_Env_Dock_Stairs_01.prefab", "Lookout Stairs", lookout,
                new Vector3(-11.6f, 1.15f, 4.9f), new Vector3(0f, 28f, 0f), new Vector3(1.2f, 1f, 1.2f));

            Transform bridge = GroupInScene(parent, "Right Rope Bridge");
            Place(Prefabs + "/SM_Bld_RopeBridge_01.prefab", "Rope Bridge", bridge,
                new Vector3(8.8f, 6.3f, 11.3f), new Vector3(0f, 18f, 0f), new Vector3(1.7f, 1f, 1.4f));
            Place(Prefabs + "/SM_Env_Dock_02.prefab", "Right Cliff Landing", bridge,
                new Vector3(14.5f, 5.5f, 11.5f), new Vector3(0f, 18f, 0f), new Vector3(1.15f, 1f, 1.05f));
        }

        static void BuildFoliage(Transform parent)
        {
            string[] broadTrees =
            {
                "SM_Env_Tree_Pohutukawa_01.prefab",
                "SM_Env_Tree_Pohutukawa_02.prefab",
                "SM_Env_Tree_Pohutukawa_03.prefab",
                "SM_Env_Tree_Pohutukawa_04.prefab",
                "SM_Env_Tree_Forest_01.prefab",
                "SM_Env_Tree_Banana_02.prefab"
            };
            string[] palms =
            {
                "SM_Env_Tree_Palm_01.prefab",
                "SM_Env_Tree_Palm_02.prefab",
                "SM_Env_Tree_Palm_03.prefab",
                "SM_Env_Tree_Palm_04.prefab"
            };

            Vector3[] treePositions =
            {
                new Vector3(-24f, 0f, -10f), new Vector3(-20f, 0f, -2f),
                new Vector3(-24f, 0f, 8f), new Vector3(-19f, 0f, 16f),
                new Vector3(-10f, 0f, 19f), new Vector3(9f, 0f, 19.5f),
                new Vector3(18.5f, 0f, 17f), new Vector3(23f, 0f, 9f),
                new Vector3(24f, 0f, -1f), new Vector3(19f, 0f, -10f),
                new Vector3(12f, 0f, -14f), new Vector3(-13f, 0f, -14f)
            };

            for (int i = 0; i < treePositions.Length; i++)
            {
                string prefab = Prefabs + "/" + broadTrees[i % broadTrees.Length];
                float s = 0.75f + (i % 3) * 0.12f;
                Place(prefab, "Broadleaf Tree " + (i + 1), parent, treePositions[i],
                    new Vector3(0f, i * 47f, 0f), new Vector3(s, s, s));
            }

            Vector3[] palmPositions =
            {
                new Vector3(-18f, 0f, 6f), new Vector3(-12f, 0f, 14f),
                new Vector3(-7f, 0f, 13f), new Vector3(7f, 0f, 15f),
                new Vector3(13f, 0f, 16f), new Vector3(19f, 0f, 5f),
                new Vector3(16f, 0f, -7f), new Vector3(-17f, 0f, -9f),
                new Vector3(-7f, 0f, -15f), new Vector3(5f, 0f, -16f)
            };

            for (int i = 0; i < palmPositions.Length; i++)
            {
                string prefab = Prefabs + "/" + palms[i % palms.Length];
                float s = 0.7f + (i % 4) * 0.09f;
                Place(prefab, "Palm " + (i + 1), parent, palmPositions[i],
                    new Vector3(0f, i * 41f, 0f), new Vector3(s, s, s));
            }

            string[] shrubs =
            {
                "SM_Env_Bush_Tropical_01.prefab",
                "SM_Env_Bush_Tropical_02.prefab",
                "SM_Env_Bush_Tropical_03.prefab",
                "SM_Env_Bush_Tropical_04.prefab",
                "SM_Env_Bush_Palm_01.prefab",
                "SM_Env_Bush_Palm_02.prefab",
                "SM_Env_Fern_01.prefab",
                "SM_Env_Fern_03.prefab"
            };

            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                float x = Mathf.Cos(a) * (11f + (i % 3) * 1.4f);
                float z = 3f + Mathf.Sin(a) * (10f + (i % 4) * 1.15f);
                string prefab = Prefabs + "/" + shrubs[i % shrubs.Length];
                float s = 0.48f + (i % 5) * 0.08f;
                Place(prefab, "Tropical Shrub " + (i + 1), parent,
                    new Vector3(x, 0.05f, z), new Vector3(0f, i * 29f, 0f), new Vector3(s, s, s));
            }

            Vector3[] foregroundFoliage =
            {
                new Vector3(-20f, 0f, -15f), new Vector3(-14f, 0f, -17f),
                new Vector3(13f, 0f, -17f), new Vector3(20f, 0f, -14f),
                new Vector3(-8f, 0f, 14.5f), new Vector3(8f, 0f, 14.5f)
            };
            for (int i = 0; i < foregroundFoliage.Length; i++)
            {
                string prefab = Prefabs + "/" + shrubs[i % shrubs.Length];
                Place(prefab, "Jungle Edge " + (i + 1), parent, foregroundFoliage[i],
                    new Vector3(0f, i * 52f, 0f), Vector3.one * (0.8f + (i % 2) * 0.18f));
            }
        }

        static void BuildLighting(Transform parent)
        {
            Material sky = LoadMaterial(Materials + "/Skybox_Tropical_Day.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.68f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.50f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.72f, 0.83f, 0.9f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            DynamicGI.UpdateEnvironment();

            GameObject sun = new GameObject("Warm Sun");
            MoveToScene(sun, parent.gameObject.scene);
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.88f, 0.72f);
            sunLight.intensity = 1.05f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 0.82f;

            GameObject fill = new GameObject("Cool Sky Fill");
            MoveToScene(fill, parent.gameObject.scene);
            fill.transform.SetParent(parent, false);
            fill.transform.rotation = Quaternion.Euler(52f, 145f, 0f);
            Light fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.55f, 0.72f, 1f);
            fillLight.intensity = 0.22f;
            fillLight.shadows = LightShadows.None;

            GameObject cameraObject = new GameObject("Reference Camera");
            MoveToScene(cameraObject, parent.gameObject.scene);
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, 9.2f, -24f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 4.2f, 3.5f) - cameraObject.transform.position);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 47f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.tag = "MainCamera";
        }

        static GameObject Place(string path, string name, Transform parent, Vector3 position,
            Vector3 eulerRotation, Vector3 scale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                if (!dependencyRefreshAttempted)
                {
                    ImportAssetOptions importOptions =
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ImportRecursive;
                    AssetDatabase.ImportAsset(Prefabs, importOptions);
                    AssetDatabase.ImportAsset(Root + "/Models", importOptions);
                    dependencyRefreshAttempted = true;
                }

                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null && path.StartsWith(Prefabs + "/") && path.EndsWith(".prefab"))
                {
                    string modelPath = path.Replace(Prefabs, Root + "/Models");
                    modelPath = modelPath.Substring(0, modelPath.Length - ".prefab".Length) + ".fbx";
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                }

                if (prefab == null)
                {
                    Debug.LogError("Missing Synty prefab: " + path);
                    return null;
                }
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.eulerAngles = eulerRotation;
            instance.transform.localScale = scale;
            return instance;
        }

        static Material LoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                Debug.LogWarning("Missing Synty material: " + path);
            return material;
        }

        static Material GetOrCreateMaterial(string path, Color color, float smoothness, bool transparent)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                ConfigureMaterial(material, color, smoothness, transparent);
                EditorUtility.SetDirty(material);
                return material;
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            Shader shader = Shader.Find("Standard");
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path),
                color = color
            };
            ConfigureMaterial(material, color, smoothness, transparent);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void ConfigureMaterial(Material material, Color color, float smoothness, bool transparent)
        {
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);

            if (!transparent)
                return;

            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        static void FixInvalidMaterials(Scene scene)
        {
            Material rockFallback = GetOrCreateMaterial(
                "Assets/Synty/Scenes/Materials/TropicalRockFallback.mat",
                new Color(0.63f, 0.54f, 0.42f),
                0.08f,
                false);
            Material foliageFallback = GetOrCreateMaterial(
                "Assets/Synty/Scenes/Materials/TropicalFoliageFallback.mat",
                new Color(0.30f, 0.52f, 0.19f),
                0.15f,
                false);
            Material waterFallback = GetOrCreateMaterial(
                "Assets/Synty/Scenes/Materials/TropicalWaterfallFallback.mat",
                new Color(0.72f, 0.92f, 1f, 0.72f),
                0.92f,
                true);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material material = materials[i];
                        if (material != null && material.shader != null &&
                            !material.shader.name.Contains("InternalErrorShader"))
                            continue;

                        string label = (renderer.name + " " + (material != null ? material.name : ""))
                            .ToLowerInvariant();
                        if (label.Contains("water") || label.Contains("fx_"))
                            materials[i] = waterFallback;
                        else if (label.Contains("leaf") || label.Contains("tree") ||
                                 label.Contains("bush") || label.Contains("fern") ||
                                 label.Contains("grass") || label.Contains("plant"))
                            materials[i] = foliageFallback;
                        else
                            materials[i] = rockFallback;
                        changed = true;
                    }

                    if (changed)
                        renderer.sharedMaterials = materials;
                }
            }
        }

        static void SetMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
        }

        static Transform Group(Scene scene, string name)
        {
            GameObject go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        static Transform GroupInScene(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void ParentAll(Transform root, params Transform[] children)
        {
            foreach (Transform child in children)
                child.SetParent(root, false);
        }

        static void MoveToScene(GameObject target, Scene scene)
        {
            if (target.scene != scene)
                SceneManager.MoveGameObjectToScene(target, scene);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                throw new InvalidOperationException("Invalid asset folder path: " + path);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Scene FindLoadedScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == path)
                    return scene;
            }

            return default;
        }
    }
}
#endif
