// One-shot editor installer for the Quest 2 player setup.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

public static class MonkeyMountainQuest2Installer
{
    private const string ScenePath = "Assets/Scenes/Monekey Mountain.unity";
    private const string BackupPath =
        "Assets/Scenes/Moneky Mountain_Before_Quest2Locomotion_2026-11-02.unity";
    private const string RigName = "[BuildingBlock] Camera Rig";
    private const string ArcName = "Dreamcore Teleport Arc";
    private const string MarkerName = "Dreamcore Landing Marker";
    private const string ReportPath = "Logs/MonkeyMountainQuest2Validation.txt";
    private const string StageLogPath = "Logs/MonkeyMountainQuest2Bootstrap.log";
    private const string PreviewPath =
        "Assets/Environment Aseets/01_HighlandLake_可玩场景/MonkeyMountain_Quest2_SpawnPreview.png";
    private const string ArcMaterialPath =
        "Assets/Environment Aseets/01_HighlandLake_可玩场景/Materials/Dreamcore Teleport Arc.mat";
    private const string MarkerMaterialPath =
        "Assets/Environment Aseets/01_HighlandLake_可玩场景/Materials/Dreamcore Landing Marker.mat";
    private const string OpenXRLoaderType =
        "UnityEngine.XR.OpenXR.OpenXRLoader";

    [MenuItem("Tools/A Day at the Zoo/Setup Quest 2 First Person Locomotion")]
    public static void SetupAndValidate()
    {
        LogStage("Setup requested.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        LogStage("Scene opened: " + scene.path + " dirty=" + scene.isDirty);
        if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("Could not save the current Monkey Mountain scene.");
        }

        if (!AssetDatabase.CopyAsset(ScenePath, BackupPath) &&
            !File.Exists(BackupPath))
        {
            throw new InvalidOperationException("Could not create the pre-locomotion scene backup.");
        }
        LogStage("Backup ready.");

        GameObject rig = FindInScene(scene, RigName) ?? InstantiateCameraRig(scene);
        LogStage("Camera rig acquired: " + rig.name);
        ConfigureCameraRig(scene, rig);
        LogStage("Camera rig configured.");
        int collidersAdded = EnsureWalkableTreeColliders(scene);
        LogStage("Tree colliders checked: added=" + collidersAdded);
        ConfigureLocomotion(rig);
        LogStage("Locomotion configured.");
        EnsureOpenXRLoader(BuildTargetGroup.Android);
        LogStage("Android OpenXR loader configured.");
        EnsureOpenXRLoader(BuildTargetGroup.Standalone);
        LogStage("Standalone OpenXR loader configured.");
        EnsureSceneInBuildSettings();
        LogStage("Build settings checked.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        LogStage("Scene and assets saved.");
        Validate(scene, rig, collidersAdded);
        LogStage("Validation complete.");
        Debug.Log("Quest 2 first-person locomotion installed in " + ScenePath);
    }

    private static GameObject InstantiateCameraRig(Scene scene)
    {
        const string prefabPath =
            "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            string guid = AssetDatabase.FindAssets("OVRCameraRig t:Prefab")
                .FirstOrDefault(candidate =>
                    Path.GetFileNameWithoutExtension(
                        AssetDatabase.GUIDToAssetPath(candidate)) == "OVRCameraRig");
            if (!string.IsNullOrEmpty(guid))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        if (prefab == null)
        {
            throw new InvalidOperationException("Meta OVRCameraRig prefab was not found.");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("Could not instantiate Meta OVRCameraRig.");
        }

        instance.name = RigName;
        return instance;
    }

    private static void ConfigureCameraRig(Scene scene, GameObject rig)
    {
        rig.SetActive(true);
        OVRCameraRig cameraRig = rig.GetComponent<OVRCameraRig>();
        OVRManager manager = rig.GetComponent<OVRManager>();
        if (cameraRig == null || cameraRig.centerEyeAnchor == null ||
            cameraRig.centerEyeAnchor.GetComponent<Camera>() == null)
        {
            throw new InvalidOperationException("OVR Camera Rig eye anchors are incomplete.");
        }

        if (manager != null)
        {
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            manager.usePositionTracking = true;
            manager.useRotationTracking = true;
            manager.resetTrackerOnLoad = false;
        }

        Camera centerCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
        centerCamera.enabled = true;
        centerCamera.nearClipPlane = 0.05f;
        centerCamera.farClipPlane = 2000f;
        centerCamera.stereoTargetEye = StereoTargetEyeMask.Both;

        AudioListener centerAudio = cameraRig.centerEyeAnchor.GetComponent<AudioListener>();
        if (centerAudio == null)
        {
            centerAudio = cameraRig.centerEyeAnchor.gameObject.AddComponent<AudioListener>();
        }
        centerAudio.enabled = true;

        GameObject mainCamera = FindInScene(scene, "Main Camera");
        if (mainCamera != null && mainCamera != rig)
        {
            Camera main = mainCamera.GetComponent<Camera>();
            if (main != null)
            {
                main.enabled = false;
            }
            AudioListener mainAudio = mainCamera.GetComponent<AudioListener>();
            if (mainAudio != null)
            {
                mainAudio.enabled = false;
            }
        }

        Transform spawnReference = mainCamera != null ? mainCamera.transform : null;
        Vector3 spawn = spawnReference != null
            ? spawnReference.position
            : new Vector3(-4580f, 8f, 47455f);
        float yaw = spawnReference != null ? spawnReference.eulerAngles.y : 0f;
        Vector3 rayOrigin = new Vector3(spawn.x, spawn.y + 100f, spawn.z);
        RaycastHit[] groundHits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            1000f,
            ~0,
            QueryTriggerInteraction.Ignore);
        RaycastHit? selectedGround = groundHits
            .Where(hit => hit.normal.y >= 0.45f)
            .OrderBy(hit => hit.distance)
            .Cast<RaycastHit?>()
            .FirstOrDefault();
        if (selectedGround.HasValue)
        {
            spawn.y = selectedGround.Value.point.y + 0.03f;
        }

        Undo.RecordObject(rig.transform, "Set Quest 2 spawn");
        rig.transform.SetPositionAndRotation(
            spawn,
            Quaternion.Euler(0f, yaw, 0f));
        Physics.SyncTransforms();
    }

    private static void ConfigureLocomotion(GameObject rig)
    {
        Transform oldArc = rig.transform.Find(ArcName);
        if (oldArc != null)
        {
            UnityEngine.Object.DestroyImmediate(oldArc.gameObject);
        }

        Transform oldMarker = rig.transform.Find(MarkerName);
        if (oldMarker != null)
        {
            UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
        }

        Material arcMaterial = LoadOrCreateMaterial(
            ArcMaterialPath,
            "Sprites/Default",
            new Color(0.45f, 0.9f, 0.9f, 1f));
        Material markerMaterial = LoadOrCreateMaterial(
            MarkerMaterialPath,
            "Unlit/Color",
            new Color(0.35f, 0.9f, 0.88f, 1f));

        GameObject arcObject = new GameObject(ArcName);
        arcObject.transform.SetParent(rig.transform, false);
        LineRenderer arc = arcObject.AddComponent<LineRenderer>();
        arc.sharedMaterial = arcMaterial;
        arc.widthMultiplier = 0.018f;
        arc.useWorldSpace = true;
        arc.numCapVertices = 4;
        arc.numCornerVertices = 3;
        arc.shadowCastingMode = ShadowCastingMode.Off;
        arc.receiveShadows = false;
        arc.enabled = false;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = MarkerName;
        marker.transform.SetParent(rig.transform, false);
        marker.transform.localScale = new Vector3(0.6f, 0.008f, 0.6f);
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        MeshRenderer markerRenderer = marker.GetComponent<MeshRenderer>();
        markerRenderer.sharedMaterial = markerMaterial;
        markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        marker.SetActive(false);

        Locomotion locomotion =
            rig.GetComponent<Locomotion>();
        if (locomotion == null)
        {
            locomotion = rig.AddComponent<Locomotion>();
        }

        locomotion.rig = rig.GetComponent<OVRCameraRig>();
        locomotion.arc = arc;
        locomotion.landingMarker = marker.transform;
        locomotion.launchSpeed = 7f;
        locomotion.turnAngle = 30f;
        locomotion.smoothMoveEnabled = true;
        locomotion.moveSpeed = 1.2f;
        locomotion.moveDeadzone = 0.2f;
        locomotion.bodyEyeHeight = 1.7f;
        locomotion.bodyRadius = 0.3f;
        locomotion.minGroundNormalY = 0.45f;
        locomotion.groundProbeHeight = 0.45f;
        locomotion.maxStepHeight = 0.35f;
        locomotion.enabled = true;
        EditorUtility.SetDirty(locomotion);
    }

    private static int EnsureWalkableTreeColliders(Scene scene)
    {
        int added = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!transform.name.StartsWith("samor_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (MeshFilter filter in
                         transform.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                    {
                        continue;
                    }

                    MeshCollider collider = filter.GetComponent<MeshCollider>();
                    if (collider == null)
                    {
                        collider = filter.gameObject.AddComponent<MeshCollider>();
                        added++;
                    }
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    collider.isTrigger = false;
                    EditorUtility.SetDirty(collider);
                }
            }
        }
        return added;
    }

    private static void EnsureOpenXRLoader(BuildTargetGroup targetGroup)
    {
        XRGeneralSettingsPerBuildTarget perTarget =
            AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        if (perTarget == null)
        {
            throw new InvalidOperationException(
                "XRGeneralSettingsPerBuildTarget asset is missing.");
        }

        if (!perTarget.HasSettingsForBuildTarget(targetGroup))
        {
            perTarget.CreateDefaultSettingsForBuildTarget(targetGroup);
        }
        if (!perTarget.HasManagerSettingsForBuildTarget(targetGroup))
        {
            perTarget.CreateDefaultManagerSettingsForBuildTarget(targetGroup);
        }

        XRGeneralSettings settings = perTarget.SettingsForBuildTarget(targetGroup);
        XRManagerSettings manager = perTarget.ManagerSettingsForBuildTarget(targetGroup);
        settings.InitManagerOnStart = true;
        manager.automaticLoading = true;
        manager.automaticRunning = true;
        XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, targetGroup);

        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(perTarget);
    }

    private static void EnsureSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes =
            EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != ScenePath))
        {
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static void Validate(
        Scene scene,
        GameObject rig,
        int collidersAdded)
    {
        Locomotion locomotion =
            rig.GetComponent<Locomotion>();
        OVRCameraRig cameraRig = rig.GetComponent<OVRCameraRig>();
        bool treeCollidersComplete = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform =>
                transform.name.StartsWith("samor_", StringComparison.OrdinalIgnoreCase))
            .SelectMany(transform =>
                transform.GetComponentsInChildren<MeshFilter>(true))
            .Where(filter => filter.sharedMesh != null)
            .All(filter => filter.GetComponent<MeshCollider>() != null);

        StringBuilder report = new StringBuilder();
        report.AppendLine("Monkey Mountain Quest 2 validation");
        report.AppendLine("Scene: " + scene.path);
        report.AppendLine("Rig active: " + rig.activeInHierarchy);
        report.AppendLine("Rig position: " + rig.transform.position.ToString("F3"));
        report.AppendLine("OVR Camera Rig: " + (cameraRig != null));
        report.AppendLine("Center eye camera: " +
                          (cameraRig != null &&
                           cameraRig.centerEyeAnchor != null &&
                           cameraRig.centerEyeAnchor.GetComponent<Camera>() != null));
        report.AppendLine("Dreamcore locomotion: " + (locomotion != null));
        report.AppendLine("Teleport arc: " + (locomotion != null && locomotion.arc != null));
        report.AppendLine("Landing marker: " +
                          (locomotion != null && locomotion.landingMarker != null));
        report.AppendLine("Walk speed: " +
                          (locomotion != null ? locomotion.moveSpeed.ToString("F2") : "-"));
        report.AppendLine("Snap turn: " +
                          (locomotion != null ? locomotion.turnAngle.ToString("F0") : "-"));
        report.AppendLine("Ground-follow probe: " +
                          (locomotion != null
                              ? locomotion.groundProbeHeight.ToString("F2")
                              : "-"));
        report.AppendLine("Tree colliders complete: " + treeCollidersComplete);
        report.AppendLine("Tree colliders added now: " + collidersAdded);
        report.AppendLine("Active cameras: " +
                          UnityEngine.Object.FindObjectsByType<Camera>(
                                  FindObjectsInactive.Exclude,
                                  FindObjectsSortMode.None)
                              .Length);
        report.AppendLine("Active audio listeners: " +
                          UnityEngine.Object.FindObjectsByType<AudioListener>(
                                  FindObjectsInactive.Exclude,
                                  FindObjectsSortMode.None)
                              .Length);

        string reportFullPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportFullPath) ?? "Logs");
        File.WriteAllText(reportFullPath, report.ToString());
        CapturePreview(cameraRig);

        if (locomotion == null || cameraRig == null ||
            locomotion.arc == null || locomotion.landingMarker == null ||
            !treeCollidersComplete)
        {
            throw new InvalidOperationException(
                "Quest 2 validation failed. See " + reportFullPath);
        }
    }

    private static void CapturePreview(OVRCameraRig cameraRig)
    {
        Camera camera = cameraRig != null && cameraRig.centerEyeAnchor != null
            ? cameraRig.centerEyeAnchor.GetComponent<Camera>()
            : null;
        if (camera == null)
        {
            return;
        }

        const int width = 1600;
        const int height = 900;
        RenderTexture target = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(PreviewPath, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static Material LoadOrCreateMaterial(
        string path,
        string shaderName,
        Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform.gameObject;
                }
            }
        }
        return null;
    }

    private static void LogStage(string message)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), StageLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Logs");
        File.AppendAllText(
            path,
            DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
    }
}

[InitializeOnLoad]
internal static class MonkeyMountainQuest2Bootstrap
{
    private const string MarkerPath =
        "Library/MonkeyMountainQuest2Installed_v1.txt";
    private static bool attempted;

    static MonkeyMountainQuest2Bootstrap()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        WriteLog("Bootstrap initialized.");
        EditorApplication.update += TryInstall;
    }

    private static void TryInstall()
    {
        string marker = Path.Combine(
            Directory.GetCurrentDirectory(),
            MarkerPath);
        if (attempted || File.Exists(marker))
        {
            EditorApplication.update -= TryInstall;
            return;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        attempted = true;
        EditorApplication.update -= TryInstall;
        WriteLog(
            "Starting installer. Active scene: " +
            SceneManager.GetActiveScene().path);
        try
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name.IndexOf(
                    "Mountain",
                    StringComparison.Ordinal) < 0)
            {
                WriteLog(
                    "Blocked: active scene is not Monkey Mountain. path='" +
                    active.path + "' name='" + active.name + "'");
                return;
            }

            if (active.isDirty)
            {
                if (!EditorSceneManager.SaveScene(active))
                {
                    throw new InvalidOperationException(
                        "Could not save the active Monkey Mountain scene.");
                }
                WriteLog("Saved active Monkey Mountain scene.");
            }

            MonkeyMountainQuest2Installer.SetupAndValidate();
            Directory.CreateDirectory(Path.GetDirectoryName(marker) ?? "Library");
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            WriteLog("Installer completed.");
        }
        catch (Exception exception)
        {
            WriteLog(exception.ToString());
            Debug.LogException(exception);
        }
    }

    private static void WriteLog(string message)
    {
        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Logs/MonkeyMountainQuest2Bootstrap.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Logs");
        File.AppendAllText(
            path,
            DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
    }
}
