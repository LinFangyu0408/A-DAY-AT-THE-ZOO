using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreehouseZooDiorama.Editor
{
    public static class TreehouseSceneBuilder
    {
        private const string AssetFolder = "Assets/Environment Aseets/02_第三方环境资源包/TreehouseZooDiorama";
        private const string ModelPath = AssetFolder + "/Meshes/TreehouseZooDiorama.fbx";
        private const string ScenePath = AssetFolder + "/TreehouseZooDiorama.unity";
        private const string PrefabPath = AssetFolder + "/TreehouseZooDiorama.prefab";
        private const string AutomationFolder = AssetFolder + "~/";
        private const string RequestMarker = AutomationFolder + "auto_scene_requested.marker";
        private const string CompletionMarker = AutomationFolder + "auto_scene_completed.txt";
        private const string PreviewRequestMarker = AutomationFolder + "unity_preview_requested.marker";
        private const string PreviewCompletionMarker = AutomationFolder + "unity_preview_completed.txt";

        private static readonly Dictionary<string, Color> MaterialColors =
            new Dictionary<string, Color>
            {
                { "Grass_Green", new Color(0.40f, 0.76f, 0.24f) },
                { "Grass_Sunlit", new Color(0.49f, 0.84f, 0.30f) },
                { "Grass_Shadow", new Color(0.33f, 0.65f, 0.19f) },
                { "Tree_Bark", new Color(0.48f, 0.25f, 0.12f) },
                { "Tree_Bark_Sunlit", new Color(0.62f, 0.36f, 0.20f) },
                { "Tree_Bark_Shadow", new Color(0.33f, 0.16f, 0.07f) },
                { "Foliage_Dark", new Color(0.13f, 0.34f, 0.09f) },
                { "Foliage_Mid", new Color(0.20f, 0.47f, 0.13f) },
                { "Foliage_Sunlit", new Color(0.30f, 0.60f, 0.18f) },
                { "Platform_Wood", new Color(0.54f, 0.29f, 0.13f) },
                { "Platform_Wood_Sunlit", new Color(0.70f, 0.42f, 0.22f) },
                { "Fence_Wood_Dark", new Color(0.40f, 0.19f, 0.08f) },
                { "Rock_Gray", new Color(0.46f, 0.48f, 0.49f) },
                { "Rock_Sunlit", new Color(0.63f, 0.65f, 0.66f) },
                { "Rock_Shadow", new Color(0.31f, 0.33f, 0.34f) },
                { "Distant_Mountains", new Color(0.43f, 0.62f, 0.72f) },
                { "Cloud_White", new Color(0.94f, 0.97f, 0.96f) },
                { "Wrought_Iron_Bolts", new Color(0.12f, 0.11f, 0.09f) },
            };

        [InitializeOnLoadMethod]
        private static void QueueAutomaticBuild()
        {
            // This one-shot marker lets the scene build without replacing the user's current scene.
            string requestPath = ProjectFilePath(RequestMarker);
            if (File.Exists(requestPath))
            {
                EditorApplication.delayCall += BuildSceneFromRequest;
            }

            if (File.Exists(ProjectFilePath(PreviewRequestMarker)))
            {
                EditorApplication.delayCall += RenderPreviewFromRequest;
            }
        }

        private static void BuildSceneFromRequest()
        {
            string requestPath = ProjectFilePath(RequestMarker);
            if (!File.Exists(requestPath))
            {
                return;
            }

            try
            {
                BuildScene();
                string completionPath = ProjectFilePath(CompletionMarker);
                File.WriteAllText(
                    completionPath,
                    "TREEHOUSE_UNITY_SCENE_OK\n" + ScenePath + "\n" + PrefabPath + "\n"
                );
                File.Delete(requestPath);
            }
            catch (System.Exception exception)
            {
                string failurePath = ProjectFilePath(
                    AutomationFolder + "auto_scene_failed.txt"
                );
                File.WriteAllText(failurePath, exception.ToString());
                Debug.LogException(exception);
            }
        }

        private static string ProjectFilePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(
                Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar))
            );
        }

        private static void RenderPreviewFromRequest()
        {
            string requestPath = ProjectFilePath(PreviewRequestMarker);
            if (!File.Exists(requestPath))
            {
                return;
            }

            RenderPreviewScene();
            if (File.Exists(ProjectFilePath(PreviewCompletionMarker)))
            {
                File.Delete(requestPath);
            }
        }

        public static void BuildAndVerify()
        {
            BuildScene();
            RenderPreviewScene();
        }

        private static void RenderPreviewScene()
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene previewScene = default;
            bool openedPreviewScene = false;
            RenderTexture previousTarget = null;
            Camera camera = null;
            RenderTexture renderTexture = null;
            Texture2D image = null;

            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene loadedScene = SceneManager.GetSceneAt(i);
                    if (loadedScene.path == ScenePath)
                    {
                        previewScene = loadedScene;
                        break;
                    }
                }
                if (!previewScene.IsValid())
                {
                    previewScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    openedPreviewScene = true;
                }
                SceneManager.SetActiveScene(previewScene);

                foreach (GameObject rootObject in previewScene.GetRootGameObjects())
                {
                    Camera[] cameras = rootObject.GetComponentsInChildren<Camera>(true);
                    if (cameras.Length > 0)
                    {
                        camera = cameras[0];
                        break;
                    }
                }
                if (camera == null)
                {
                    throw new InvalidDataException("Preview camera was not found in " + ScenePath);
                }

                const int width = 1440;
                const int height = 810;
                previousTarget = camera.targetTexture;
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();

                string outputPath = ProjectFilePath(
                    AutomationFolder + "QA/Treehouse_Unity_Preview.png"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                File.WriteAllText(
                    ProjectFilePath(PreviewCompletionMarker),
                    "TREEHOUSE_UNITY_PREVIEW_OK\n" + outputPath + "\n"
                );
            }
            catch (System.Exception exception)
            {
                File.WriteAllText(
                    ProjectFilePath(AutomationFolder + "unity_preview_failed.txt"),
                    exception.ToString()
                );
                Debug.LogException(exception);
            }
            finally
            {
                if (camera != null)
                {
                    camera.targetTexture = previousTarget;
                }
                RenderTexture.active = null;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }
                if (image != null)
                {
                    Object.DestroyImmediate(image);
                }
                if (openedPreviewScene && previewScene.IsValid() && previewScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(previewScene, true);
                }
                if (previousActiveScene.IsValid())
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        [MenuItem("Tools/A Day At The Zoo/Rebuild Treehouse Diorama Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(AssetFolder + "/Meshes");
            AssetDatabase.ImportAsset(
                ModelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
            );

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new InvalidDataException("Treehouse FBX could not be imported: " + ModelPath);
            }

            ConfigureModelImporter();
            Scene previousActiveScene = SceneManager.GetActiveScene();
            bool useAdditiveScene = !string.IsNullOrEmpty(previousActiveScene.path);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                useAdditiveScene ? NewSceneMode.Additive : NewSceneMode.Single
            );

            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Treehouse Zoo Diorama";
                ApplyIndependentMaterials(instance);

                CreateSun();
                CreateCamera();
                ConfigureAmbientLighting();

                EditorSceneManager.SaveScene(scene, ScenePath);
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                if (useAdditiveScene)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previousActiveScene.IsValid())
                    {
                        SceneManager.SetActiveScene(previousActiveScene);
                    }
                }
            }

            Debug.Log("TREEHOUSE_UNITY_SCENE_OK:" + ScenePath);
        }

        private static void ApplyIndependentMaterials(GameObject instance)
        {
            string materialFolder = AssetFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(materialFolder))
            {
                AssetDatabase.CreateFolder(AssetFolder, "Materials");
            }

            Dictionary<string, Material> generatedMaterials = new Dictionary<string, Material>();
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null)
                    {
                        continue;
                    }

                    string materialName = source.name.Split('.')[0];
                    if (!MaterialColors.TryGetValue(materialName, out Color color))
                    {
                        continue;
                    }

                    if (!generatedMaterials.TryGetValue(materialName, out Material material))
                    {
                        string materialPath = materialFolder + "/" + materialName + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material == null)
                        {
                            material = new Material(source.shader);
                            material.name = materialName;
                            AssetDatabase.CreateAsset(material, materialPath);
                        }
                        generatedMaterials.Add(materialName, material);
                    }

                    material.shader = source.shader;
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }
                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", color);
                    }
                    if (material.HasProperty("_Smoothness"))
                    {
                        material.SetFloat("_Smoothness", 0.12f);
                    }
                    if (material.HasProperty("_Glossiness"))
                    {
                        material.SetFloat("_Glossiness", 0.12f);
                    }
                    EditorUtility.SetDirty(material);
                    materials[i] = material;
                    changed = true;
                }
                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureModelImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
        }

        private static void CreateSun()
        {
            GameObject sunObject = new GameObject("Warm Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.89f, 0.72f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.68f;
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            GameObject fillObject = new GameObject("Sky Fill");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.54f, 0.70f, 1.0f);
            fill.intensity = 0.95f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, 148f, 0f);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.64f, 0.88f);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(12.0f, 7.3f, 20.0f);
            cameraObject.transform.LookAt(new Vector3(0f, 4.1f, 0f));
        }

        private static void ConfigureAmbientLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.82f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.68f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.36f, 0.18f);
            RenderSettings.ambientIntensity = 2.05f;
            RenderSettings.reflectionIntensity = 0.55f;
            RenderSettings.fog = false;
            RenderSettings.fogColor = new Color(0.55f, 0.73f, 0.88f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 135f;
        }
    }
}
