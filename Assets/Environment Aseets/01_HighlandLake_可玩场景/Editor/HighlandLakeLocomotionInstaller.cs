using System;
using System.IO;
using System.Text;
using Oculus.Interaction.DistanceReticles;
using Oculus.Interaction.Locomotion;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

[InitializeOnLoad]
public static class HighlandLakeLocomotionInstaller
{
    private const string ScenePath = "Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake.unity";
    private const string BackupPath = "Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake_BeforeVR.unity";
    private const string MarkerPath = "Library/HighlandLakeLocomotionInstalled_v2.txt";
    private const string RigPrefabName = "OVRComprehensiveInteractionRig";

    static HighlandLakeLocomotionInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
    }

    [MenuItem("Tools/Environment/Setup VR Walking and Teleport")]
    public static void SetupFromMenu()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Install(active);
    }

    public static void SetupVRScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Install(active);
    }

    public static void SetupAndValidate()
    {
        SetupVRScene();
        ValidateVRScene();
    }

    [MenuItem("Tools/Environment/Validate VR Walking and Teleport")]
    public static void ValidateVRScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("Highland Lake VR validation");
        report.AppendLine("Scene: " + scene.path);

        GameObject desktop = FindInScene(scene, "Desktop Walking Player");
        GameObject vr = FindInScene(scene, "Meta XR Interaction");
        GameObject cameraRig = FindInScene(scene, "[BuildingBlock] Camera Rig");
        GameObject surface = FindInScene(scene, "LocomotionInteractable");
        GameObject waterBlocker = FindInScene(scene, "Teleport Blocker - Lake");
        MovingSetting moving = FindInSceneComponent<MovingSetting>(scene);
        FirstPersonLocomotor locomotor = FindInSceneComponent<FirstPersonLocomotor>(scene);
        TeleportInteractable terrainTeleport = surface != null
            ? surface.GetComponentInChildren<TeleportInteractable>(true)
            : null;

        report.AppendLine("Desktop player: " + (desktop != null));
        report.AppendLine("Desktop CharacterController: " +
                          (desktop != null && desktop.GetComponent<UnityEngine.CharacterController>() != null));
        report.AppendLine("Desktop controller script: " +
                          (desktop != null && desktop.GetComponent<DesktopPlayerController>() != null));
        report.AppendLine("Meta XR player: " + (vr != null));
        report.AppendLine("OVR Camera Rig: " + (cameraRig != null));
        report.AppendLine("FirstPersonLocomotor: " + (locomotor != null));
        report.AppendLine("MovingSetting: " + (moving != null));
        report.AppendLine("Meta movement mode: " +
                          (moving == null ? "missing" : moving.ControllerMovement.Value.ToString()));
        Locomotion dreamcore =
            FindInSceneComponent<Locomotion>(scene);
        report.AppendLine("Dreamcore locomotion: " + (dreamcore != null));
        if (dreamcore != null)
        {
            report.AppendLine("Dreamcore move speed: " + dreamcore.moveSpeed.ToString("F2") + " m/s");
            report.AppendLine("Dreamcore snap turn: " + dreamcore.turnAngle.ToString("F0") + " degrees");
            report.AppendLine("Dreamcore teleport launch: " + dreamcore.launchSpeed.ToString("F2"));
        }
        report.AppendLine("Flat meadow height: " + GroundHeight(0f, -20f).ToString("F3") + " m");
        report.AppendLine("Lake water height: -0.180 m");
        report.AppendLine("Lake floor height: " + GroundHeight(0f, 7f).ToString("F3") + " m");
        GameObject windGrass = FindInScene(scene, "Low Poly Wind Grass");
        report.AppendLine("Animated wind grass: " + (windGrass != null));
        report.AppendLine("Terrain teleport surface: " +
                          (terrainTeleport != null && terrainTeleport.Surface != null));
        report.AppendLine("Water teleport blocker: " + (waterBlocker != null));
        report.AppendLine("Active AudioListeners: " +
                          UnityEngine.Object.FindObjectsByType<AudioListener>(
                              FindObjectsInactive.Exclude,
                              FindObjectsSortMode.None).Length);
        if (locomotor != null)
        {
            SerializedObject serialized = new SerializedObject(locomotor);
            report.AppendLine("Locomotor Player Origin: " +
                              (serialized.FindProperty("_playerOrigin").objectReferenceValue != null));
            report.AppendLine("Locomotor Player Eyes: " +
                              (serialized.FindProperty("_playerEyes").objectReferenceValue != null));
        }

        Camera desktopCamera = desktop != null
            ? desktop.GetComponentInChildren<Camera>(true)
            : null;
        if (desktopCamera != null)
        {
            const int width = 1600;
            const int height = 920;
            RenderTexture target = RenderTexture.GetTemporary(
                width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            desktopCamera.targetTexture = target;
            desktopCamera.Render();
            RenderTexture.active = target;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(
                "Assets/Environment Aseets/01_HighlandLake_可玩场景/Preview_VR_DesktopSpawn.png",
                image.EncodeToPNG());
            desktopCamera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }

        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Logs",
            "HighlandLakeVrValidation.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report.ToString());
        Debug.Log("VR validation complete: " + path);
    }

    private static void AutoInstall()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        string marker = Path.Combine(Directory.GetCurrentDirectory(), MarkerPath);
        if (File.Exists(marker))
        {
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Install(active);
    }

    private static void Install(Scene scene)
    {
        if (!AssetDatabase.CopyAsset(ScenePath, BackupPath) && !File.Exists(BackupPath))
        {
            Debug.LogWarning("Could not create a separate pre-VR scene backup; continuing with the current scene.");
        }

        EditorSceneManager.SaveScene(scene);
        Undo.RegisterFullObjectHierarchyUndo(scene.GetRootGameObjects()[0], "Setup VR Walking and Teleport");

        GameObject presentationCamera = FindInScene(scene, "Presentation Camera");
        if (presentationCamera != null)
        {
            presentationCamera.SetActive(false);
        }

        GameObject playerSystems = FindInScene(scene, "Player and Locomotion");
        if (playerSystems == null)
        {
            playerSystems = new GameObject("Player and Locomotion");
            SceneManager.MoveGameObjectToScene(playerSystems, scene);
        }

        GameObject desktopPlayer = CreateDesktopPlayer(scene, playerSystems.transform);
        GameObject vrPlayer = CreateVrPlayer(scene, playerSystems.transform);
        CreateTeleportSurfaces(scene, playerSystems.transform);
        CreatePlatformSwitcher(scene, desktopPlayer, vrPlayer, presentationCamera);

        foreach (MovingSetting moving in UnityEngine.Object.FindObjectsByType<MovingSetting>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (moving.gameObject.scene == scene)
            {
                moving.ControllerMovement.Value = MovingSetting.MovementStyle.Teleport;
                EditorUtility.SetDirty(moving);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        string marker = Path.Combine(Directory.GetCurrentDirectory(), MarkerPath);
        Directory.CreateDirectory(Path.GetDirectoryName(marker));
        File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        Debug.Log("Highland Lake VR walking and teleport setup completed.");
    }

    private static GameObject CreateDesktopPlayer(Scene scene, Transform parent)
    {
        GameObject oldPlayer = FindInScene(scene, "Desktop Walking Player");
        if (oldPlayer != null)
        {
            UnityEngine.Object.DestroyImmediate(oldPlayer);
        }

        GameObject player = new GameObject("Desktop Walking Player");
        SceneManager.MoveGameObjectToScene(player, scene);
        player.transform.SetParent(parent, true);

        UnityEngine.CharacterController controller =
            player.AddComponent<UnityEngine.CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0, 0.9f, 0);
        controller.stepOffset = 0.35f;
        controller.slopeLimit = 45f;
        controller.minMoveDistance = 0f;

        GameObject cameraObject = new GameObject("Desktop Camera");
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 1.7f, 0);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 250f;
        camera.fieldOfView = 60f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.5f, 0.82f, 0.99f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";

        DesktopPlayerController movement = player.AddComponent<DesktopPlayerController>();
        SerializedObject serialized = new SerializedObject(movement);
        serialized.FindProperty("view").objectReferenceValue = cameraObject.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        player.transform.rotation = Quaternion.Euler(0, 0, 0);
        Physics.SyncTransforms();
        Vector3 origin = new Vector3(0, 20f, -20f);
        float groundY = 0.1f;
        GameObject meadow = FindInScene(scene, "Meadow and Outer Slopes");
        MeshCollider meadowCollider = meadow != null ? meadow.GetComponent<MeshCollider>() : null;
        if (meadowCollider != null &&
            meadowCollider.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, 40f))
        {
            groundY = hit.point.y;
        }

        player.transform.position = new Vector3(0, groundY + 0.03f, -20f);
        return player;
    }

    private static GameObject CreateVrPlayer(Scene scene, Transform parent)
    {
        GameObject cameraRig = FindInScene(scene, "[BuildingBlock] Camera Rig");
        if (cameraRig == null)
        {
            string cameraRigPath =
                "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
            GameObject cameraRigPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(cameraRigPath);
            if (cameraRigPrefab == null)
            {
                string[] rigGuids = AssetDatabase.FindAssets("OVRCameraRig t:Prefab");
                foreach (string guid in rigGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == "OVRCameraRig")
                    {
                        cameraRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        break;
                    }
                }
            }

            if (cameraRigPrefab == null)
            {
                throw new InvalidOperationException(
                    "The Meta OVRCameraRig prefab could not be loaded.");
            }

            cameraRig = PrefabUtility.InstantiatePrefab(cameraRigPrefab, scene) as GameObject;
            if (cameraRig == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate the Meta OVRCameraRig prefab.");
            }
            cameraRig.name = "[BuildingBlock] Camera Rig";
        }

        GameObject oldInteraction = FindInScene(scene, "Meta XR Interaction");
        if (oldInteraction != null)
        {
            UnityEngine.Object.DestroyImmediate(oldInteraction);
        }
        GameObject oldRawRig = FindInScene(scene, "Meta XR Player");
        if (oldRawRig != null)
        {
            UnityEngine.Object.DestroyImmediate(oldRawRig);
        }

        string[] guids = AssetDatabase.FindAssets(RigPrefabName + " t:Prefab");
        string prefabPath = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == RigPrefabName)
            {
                prefabPath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(prefabPath))
        {
            throw new InvalidOperationException("Meta XR comprehensive interaction rig was not found.");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject rig = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (rig == null)
        {
            throw new InvalidOperationException("Could not instantiate the Meta XR comprehensive interaction rig.");
        }

        rig.name = "Meta XR Interaction";
        rig.transform.SetParent(cameraRig.transform, false);
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        rig.transform.localScale = Vector3.one;

        Transform leftEye = FindChildByName(cameraRig.transform, "LeftEyeAnchor");
        Transform rightEye = FindChildByName(cameraRig.transform, "RightEyeAnchor");
        Transform centerEye = FindChildByName(cameraRig.transform, "CenterEyeAnchor");
        Camera centerEyeCamera = centerEye != null ? centerEye.GetComponent<Camera>() : null;
        Component ovrCameraRigComponent = null;
        foreach (Component component in cameraRig.GetComponents<Component>())
        {
            if (component != null && component.GetType().Name == "OVRCameraRig")
            {
                ovrCameraRigComponent = component;
                break;
            }
        }

        if (leftEye == null || rightEye == null || centerEye == null ||
            centerEyeCamera == null || ovrCameraRigComponent == null)
        {
            throw new InvalidOperationException("The OVR Camera Rig eye anchors or camera are incomplete.");
        }

        SetReference(rig, "_leftEyeAnchor", leftEye);
        SetReference(rig, "_rightEyeAnchor", rightEye);
        SetReference(rig, "_centerEyeCamera", centerEyeCamera);
        SetReference(rig, "_playerOrigin", cameraRig.transform);
        SetReference(rig, "_playerEyes", centerEye);
        SetReference(rig, "_ovrCameraRig", ovrCameraRigComponent);
        ConfigureDreamcoreLocomotion(cameraRig);

        GameObject meadow = FindInScene(scene, "Meadow and Outer Slopes");
        MeshCollider collider = meadow != null ? meadow.GetComponent<MeshCollider>() : null;
        Vector3 origin = new Vector3(0, 20f, -20f);
        float groundY = 0.1f;
        if (collider != null &&
            collider.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, 40f))
        {
            groundY = hit.point.y;
        }

        cameraRig.transform.SetParent(parent, true);
        cameraRig.transform.position = new Vector3(0, groundY + 0.03f, -20f);
        cameraRig.transform.rotation = Quaternion.identity;
        cameraRig.transform.localScale = Vector3.one;
        cameraRig.SetActive(true);
        return cameraRig;
    }

    private static void ConfigureDreamcoreLocomotion(GameObject cameraRig)
    {
        foreach (Component component in cameraRig.GetComponentsInChildren<Component>(true))
        {
            if (component != null &&
                component.GetType().FullName ==
                "Oculus.Interaction.Locomotion.FirstPersonLocomotor")
            {
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty enabled = serialized.FindProperty("m_Enabled");
                if (enabled != null)
                {
                    enabled.boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        Transform oldArc = cameraRig.transform.Find("Dreamcore Teleport Arc");
        if (oldArc != null)
        {
            UnityEngine.Object.DestroyImmediate(oldArc.gameObject);
        }
        Transform oldMarker = cameraRig.transform.Find("Dreamcore Landing Marker");
        if (oldMarker != null)
        {
            UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
        }

        Material arcMaterial = LoadOrCreateMaterial(
            "Assets/Environment Aseets/01_HighlandLake_可玩场景/Materials/Dreamcore Teleport Arc.mat",
            "Sprites/Default",
            new Color(0.45f, 0.9f, 0.9f, 1f));
        Material markerMaterial = LoadOrCreateMaterial(
            "Assets/Environment Aseets/01_HighlandLake_可玩场景/Materials/Dreamcore Landing Marker.mat",
            "Unlit/Color",
            new Color(0.35f, 0.9f, 0.88f, 1f));

        GameObject arcObject = new GameObject("Dreamcore Teleport Arc");
        arcObject.transform.SetParent(cameraRig.transform, false);
        LineRenderer arc = arcObject.AddComponent<LineRenderer>();
        arc.sharedMaterial = arcMaterial;
        arc.widthMultiplier = 0.018f;
        arc.useWorldSpace = true;
        arc.numCapVertices = 4;
        arc.numCornerVertices = 3;
        arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arc.receiveShadows = false;
        arc.enabled = false;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Dreamcore Landing Marker";
        marker.transform.SetParent(cameraRig.transform, false);
        marker.transform.localScale = new Vector3(0.6f, 0.008f, 0.6f);
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;
        marker.GetComponent<MeshRenderer>().shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        marker.SetActive(false);

        Locomotion locomotion =
            cameraRig.GetComponent<Locomotion>();
        if (locomotion == null)
        {
            locomotion = cameraRig.AddComponent<Locomotion>();
        }

        locomotion.rig = cameraRig.GetComponent<OVRCameraRig>();
        locomotion.arc = arc;
        locomotion.landingMarker = marker.transform;
        locomotion.launchSpeed = 7f;
        locomotion.turnAngle = 30f;
        locomotion.smoothMoveEnabled = true;
        locomotion.moveSpeed = 1.2f;
        locomotion.moveDeadzone = 0.2f;
        locomotion.bodyEyeHeight = 1.7f;
        EditorUtility.SetDirty(locomotion);
    }

    private static Material LoadOrCreateMaterial(string path, string shaderName, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static float GroundHeight(float x, float z)
    {
        float lake = Mathf.Sqrt((x / 23f) * (x / 23f) + ((z - 7f) / 19f) * ((z - 7f) / 19f));
        float t = Mathf.Clamp01((lake - 0.8f) / 0.22f);
        float bank = t * t * (3f - 2f * t);
        return Mathf.Lerp(-1.25f, 0f, bank);
    }

    private static void SetReference(GameObject root, string propertyName, UnityEngine.Object value)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void CreateTeleportSurfaces(Scene scene, Transform parent)
    {
        foreach (GameObject old in FindAllInScene(scene, "LocomotionInteractable"))
        {
            UnityEngine.Object.DestroyImmediate(old);
        }

        GameObject meadow = FindInScene(scene, "Meadow and Outer Slopes");
        if (meadow == null)
        {
            throw new InvalidOperationException("The meadow terrain collider was not found.");
        }

        MeshCollider meadowCollider = meadow.GetComponent<MeshCollider>();
        if (meadowCollider == null)
        {
            throw new InvalidOperationException("The meadow MeshCollider was not found.");
        }

        GameObject locomotionRoot = new GameObject("LocomotionInteractable");
        locomotionRoot.transform.SetParent(parent, false);
        GameObject colliderInteractable = new GameObject("ColliderInteractable");
        colliderInteractable.transform.SetParent(locomotionRoot.transform, false);
        GameObject colliderObject = new GameObject("Collider");
        colliderObject.transform.SetParent(colliderInteractable.transform, false);

        MeshCollider surfaceCollider = colliderObject.AddComponent<MeshCollider>();
        surfaceCollider.sharedMesh = meadowCollider.sharedMesh;
        ColliderSurface surface = colliderObject.AddComponent<ColliderSurface>();
        surface.InjectCollider(surfaceCollider);

        TeleportInteractable teleport = colliderInteractable.AddComponent<TeleportInteractable>();
        teleport.InjectAllTeleportInteractable(surface);
        colliderInteractable.AddComponent<ReticleDataTeleport>();

        foreach (GameObject oldBlocker in FindAllInScene(scene, "Teleport Blocker - Lake"))
        {
            UnityEngine.Object.DestroyImmediate(oldBlocker);
        }

        CreateWaterBlocker(parent);
    }

    private static void CreateWaterBlocker(Transform parent)
    {
        GameObject blockerRoot = new GameObject("Teleport Blocker - Lake");
        blockerRoot.transform.SetParent(parent, false);
        GameObject colliderObject = new GameObject("Water Surface Blocker");
        colliderObject.transform.SetParent(blockerRoot.transform, false);
        colliderObject.transform.position = new Vector3(0, 0.45f, 7f);

        BoxCollider box = colliderObject.AddComponent<BoxCollider>();
        box.size = new Vector3(46f, 0.2f, 38f);
        box.isTrigger = true;
        ColliderSurface surface = colliderObject.AddComponent<ColliderSurface>();
        surface.InjectCollider(box);

        TeleportInteractable teleport = blockerRoot.AddComponent<TeleportInteractable>();
        teleport.InjectAllTeleportInteractable(surface);
        teleport.AllowTeleport = false;
        colliderObject.AddComponent<ReticleDataTeleport>();
    }

    private static void CreatePlatformSwitcher(
        Scene scene,
        GameObject desktopPlayer,
        GameObject vrPlayer,
        GameObject presentationCamera)
    {
        GameObject oldSwitcher = FindInScene(scene, "XR Platform Switcher");
        if (oldSwitcher != null)
        {
            UnityEngine.Object.DestroyImmediate(oldSwitcher);
        }

        GameObject switcherObject = new GameObject("XR Platform Switcher");
        SceneManager.MoveGameObjectToScene(switcherObject, scene);
        VrPlatformSwitcher switcher = switcherObject.AddComponent<VrPlatformSwitcher>();

        SerializedObject serialized = new SerializedObject(switcher);
        serialized.FindProperty("desktopPlayer").objectReferenceValue = desktopPlayer;
        serialized.FindProperty("vrPlayer").objectReferenceValue = vrPlayer;
        serialized.FindProperty("presentationCamera").objectReferenceValue = presentationCamera;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Keep the OVR rig active in the saved scene so its tracking components
        // initialize before the runtime switcher decides whether a headset exists.
        desktopPlayer.SetActive(false);
        vrPlayer.SetActive(true);
        if (presentationCamera != null)
        {
            presentationCamera.SetActive(false);
        }
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

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static System.Collections.Generic.List<GameObject> FindAllInScene(Scene scene, string name)
    {
        System.Collections.Generic.List<GameObject> matches =
            new System.Collections.Generic.List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    matches.Add(transform.gameObject);
                }
            }
        }

        return matches;
    }

    private static T FindInSceneComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
