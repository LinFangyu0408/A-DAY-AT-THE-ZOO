using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ADayAtTheZoo.WaterStyles.Editor
{
    [InitializeOnLoad]
    public static class WaterStylesInstaller
    {
        const string RootFolder = "Assets/WaterStyles";
        const string MaterialFolder = RootFolder + "/Materials";
        const string ShaderPath = RootFolder + "/Shaders/MM_WaterStyles.shader";
        const string SelectorName = "WATER STYLE - SELECT HERE";

        static WaterStylesInstaller()
        {
            EditorApplication.delayCall += InstallForOpenScene;
        }

        [MenuItem("Tools/A Day at the Zoo/Install Water Styles")]
        public static void InstallForOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += InstallForOpenScene;
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.name.Contains("Monekey Mountain"))
            {
                return;
            }

            MigrateExistingSelector(scene);

            if (GameObject.Find(SelectorName) != null)
            {
                return;
            }

            Install(scene);
        }

        static void Install(Scene scene)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder("Assets", "WaterStyles");
            EnsureFolder(RootFolder, "Materials");
            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("[Water Styles] 无法加载 MM_WaterStyles.shader。");
                return;
            }

            Material[] materials =
            {
                CreateMaterial(shader, "01_Clear_Sparkling_Lagoon",
                    new Color(0.38f, 0.94f, 0.91f), new Color(0.025f, 0.32f, 0.70f),
                    opacity: 0.53f, depthFade: 6.0f, foam: 0.68f, foamDepth: 0.60f,
                    specular: 2.1f, sparkle: 0.36f, facets: 0.25f, waveHeight: 0.10f,
                    waveSpeed: 0.28f, flow: new Vector4(1f, 0.15f, 0f, 0f)),

                CreateMaterial(shader, "02_Toon_Shallow_Pool",
                    new Color(0.16f, 0.72f, 0.95f), new Color(0.02f, 0.18f, 0.74f),
                    opacity: 0.84f, depthFade: 4.1f, foam: 1.15f, foamDepth: 0.38f,
                    specular: 1.4f, sparkle: 0.06f, facets: 0.12f, waveHeight: 0.055f,
                    waveSpeed: 0.20f, flow: new Vector4(1f, -0.08f, 0f, 0f), toon: 1f),

                CreateMaterial(shader, "03_Translucent_LowPoly_Bay",
                    new Color(0.20f, 0.62f, 0.68f), new Color(0.055f, 0.23f, 0.37f),
                    opacity: 0.62f, depthFade: 8.5f, foam: 0.38f, foamDepth: 0.28f,
                    specular: 2.7f, sparkle: 0.22f, facets: 1.0f, waveHeight: 0.22f,
                    waveSpeed: 0.24f, flow: new Vector4(1f, 0.42f, 0f, 0f)),

                CreateMaterial(shader, "04_Teal_LowPoly_Water_Tone",
                    new Color(0.08f, 0.46f, 0.49f), new Color(0.015f, 0.09f, 0.16f),
                    opacity: 0.84f, depthFade: 1.7f, foam: 0.12f, foamDepth: 0.18f,
                    specular: 0.45f, sparkle: 0.04f, facets: 0.75f, waveHeight: 0.08f,
                    waveSpeed: 0.18f, flow: new Vector4(0.8f, 0.18f, 0f, 0f)),

                CreateMaterial(shader, "05_Bright_Sparkling_Shallows",
                    new Color(0.19f, 0.78f, 1.0f), new Color(0.015f, 0.30f, 0.76f),
                    opacity: 0.47f, depthFade: 6.8f, foam: 0.78f, foamDepth: 0.72f,
                    specular: 4.7f, sparkle: 1.05f, facets: 0.08f, waveHeight: 0.11f,
                    waveSpeed: 0.34f, flow: new Vector4(1f, 0.30f, 0f, 0f)),

                CreateMaterial(shader, "06_Midnight_LowPoly_Sea",
                    new Color(0.035f, 0.13f, 0.24f), new Color(0.005f, 0.018f, 0.055f),
                    opacity: 0.84f, depthFade: 7.0f, foam: 0.58f, foamDepth: 0.42f,
                    specular: 2.4f, sparkle: 0.28f, facets: 0.55f, waveHeight: 0.16f,
                    waveSpeed: 0.22f, flow: new Vector4(0.85f, 0.38f, 0f, 0f),
                    sky: new Color(0.18f, 0.22f, 0.36f), sparkleColor: new Color(0.85f, 0.92f, 1f))
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject room = GameObject.Find("Moneky room");
            Vector3 center = room != null
                ? room.transform.position
                : new Vector3(-4619.163f, -16.03435f, 47513.31f);

            var selectorRoot = new GameObject(SelectorName);
            Undo.RegisterCreatedObjectUndo(selectorRoot, "Create Water Style Selector");
            selectorRoot.transform.position = center + new Vector3(0f, 4.2f, 0f);
            selectorRoot.transform.rotation = Quaternion.identity;
            selectorRoot.transform.localScale = Vector3.one;

            var surface = new GameObject("Water Surface - use WATER STYLE component");
            surface.transform.SetParent(selectorRoot.transform, false);
            surface.transform.localPosition = Vector3.zero;
            surface.transform.localRotation = Quaternion.identity;

            var meshFilter = surface.AddComponent<MeshFilter>();
            var meshRenderer = surface.AddComponent<MeshRenderer>();
            var surfaceMesh = surface.AddComponent<WaterSurfaceMesh>();
            surfaceMesh.width = 18f;
            surfaceMesh.length = 12f;
            surfaceMesh.EnsureMesh();

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = materials[0];

            var selector = selectorRoot.AddComponent<WaterStyleSelector>();
            selector.WaterRenderer = meshRenderer;
            selector.WaterMaterials = materials;
            selector.SelectStyle(WaterStyleSelector.Style.ClearSparklingLagoon);

            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.depthTextureMode |= DepthTextureMode.Depth;
                EditorUtility.SetDirty(camera);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = selectorRoot;
            EditorGUIUtility.PingObject(selectorRoot);

            Debug.Log(
                "[Water Styles] 已加入当前场景。单击 Hierarchy 中最上层的 “WATER STYLE - SELECT HERE”，" +
                "在 Inspector 的 Water Style 下拉框或六个样式按钮中切换。");
        }

        static void MigrateExistingSelector(Scene scene)
        {
            WaterStyleSelector[] selectors =
                Object.FindObjectsByType<WaterStyleSelector>(FindObjectsSortMode.None);

            foreach (WaterStyleSelector selector in selectors)
            {
                if (selector == null)
                {
                    continue;
                }

                Transform target = null;
                foreach (Transform candidate in selector.transform.root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == "samor_4077_600")
                    {
                        target = candidate;
                        break;
                    }
                }

                if (target == null)
                {
                    continue;
                }

                if (WaterStyleSelectorEditor.MoveSelectorToPreferredWater(selector))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[Water Styles] 已将水效选择器迁移到 samor_4077_600，并停用重复预览水面。");
                }
                break;
            }
        }

        static Material CreateMaterial(
            Shader shader,
            string name,
            Color shallow,
            Color deep,
            float opacity,
            float depthFade,
            float foam,
            float foamDepth,
            float specular,
            float sparkle,
            float facets,
            float waveHeight,
            float waveSpeed,
            Vector4 flow,
            float toon = 0f,
            Color? sky = null,
            Color? sparkleColor = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_ShallowColor", shallow);
            material.SetColor("_DeepColor", deep);
            material.SetColor("_SkyColor", sky ?? new Color(0.58f, 0.82f, 1f));
            material.SetColor("_SparkleColor", sparkleColor ?? Color.white);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_DepthFade", depthFade);
            material.SetVector("_FlowDirection", flow.normalized);
            material.SetFloat("_WaveAmplitude", waveHeight);
            material.SetFloat("_WaveFrequency", 2.6f);
            material.SetFloat("_WaveSpeed", waveSpeed);
            material.SetFloat("_NormalStrength", 0.18f);
            material.SetFloat("_FacetStrength", facets);
            material.SetFloat("_SparkleStrength", sparkle);
            material.SetFloat("_SparkleScale", 0.52f);
            material.SetFloat("_SparkleSpeed", 0.72f);
            material.SetFloat("_FoamStrength", foam);
            material.SetFloat("_FoamDepth", foamDepth);
            material.SetFloat("_FoamScale", 1.9f);
            material.SetFloat("_SpecularStrength", specular);
            material.SetFloat("_SpecularPower", toon > 0.5f ? 45f : 120f);
            material.SetFloat("_ReflectionStrength", 0.32f);
            material.SetFloat("_ToonLighting", toon);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string parent, string child)
        {
            string path = Path.Combine(parent, child).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
