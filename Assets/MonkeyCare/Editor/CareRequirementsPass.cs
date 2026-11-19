using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ADayAtTheZoo.Monkeys.Care;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CareRequirementsPass
{
    const string Work = "work/monkey-care/";
    const string Request = Work + "requirements.request";
    const string Result = Work + "requirements-result.txt";
    const string Audit = Work + "requirements-audit.txt";

    static CareRequirementsPass()
    {
        Directory.CreateDirectory(Work);
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists(Request)) return;
        File.Delete(Request);
        try
        {
            Apply();
            File.WriteAllText(Result, "SUCCESS " + DateTime.Now.ToString("s") + "\n" + ReadText(Audit));
        }
        catch (Exception exception)
        {
            File.WriteAllText(Result, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/猴子照顾/应用最新交互与场景需求")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != MonkeyCareInstaller.ScenePath)
            throw new Exception("请先打开 Highland Lake 喂香蕉场景。");

        string backup = "Assets/MonkeyCare/Backups/HighlandLake_BeforeRequirements_" +
                        DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!AssetDatabase.CopyAsset(scene.path, backup))
            throw new Exception("无法在修改前备份场景。");

        var session = UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>(FindObjectsInactive.Include);
        if (!session) throw new Exception("没有找到 MonkeyCareSession。");
        session.hapticAmplitude = .62f;
        session.hapticFrequency = .55f;
        session.showTutorial = true;
        session.feedingOnly = true;
        EditorUtility.SetDirty(session);

        GameObject farm = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Monkey Care - Farm");
        if (!farm)
        {
            var found = GameObject.Find("Monkey Care - Farm");
            farm = found ? found : new GameObject("Monkey Care - Farm");
        }

        BuildMarketStand(session, farm.transform);
        EnsureVisible("Background Windmill");
        RemoveSackObjects();
        HideOriginalBananaSupplies();
        RemoveBananaPiles(farm.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new Exception("Unity 无法保存场景。");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WriteAudit(scene, session);
        CareSelfTests.Run();
        Debug.Log("[Monkey Care] 最新需求已应用到场景。备份: " + backup);
    }

    static void BuildMarketStand(MonkeyCareSession session, Transform farmRoot)
    {
        GameObject oldBarn = FindIncluding("Start Open Barn");
        if (oldBarn) Undo.DestroyObjectImmediate(oldBarn);
        GameObject oldStand = FindIncluding("Banana Market Stand");
        if (oldStand) Undo.DestroyObjectImmediate(oldStand);
        GameObject oldStack = FindIncluding("Banana Crate Stack");
        if (oldStack) Undo.DestroyObjectImmediate(oldStack);

        Camera camera = UnityEngine.Object.FindFirstObjectByType<DesktopPlayerController>(
                FindObjectsInactive.Include)?.GetComponentInChildren<Camera>(true);
        Transform player = camera ? camera.transform : session.rig ? session.rig.transform : null;
        if (!player) throw new Exception("无法找到玩家位置。");
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 plan = player.position - forward * CareFarmEnvironment.MarketStandDistance;
        float groundY = GroundY(plan + Vector3.up * 1.5f);
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

        const string importedPath = "Assets/MonkeyCare/Resources/Farm/MarketStand_1.fbx";
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(importedPath);
        GameObject stand;
        if (template)
        {
            stand = PrefabUtility.InstantiatePrefab(template, farmRoot) as GameObject;
            if (!stand) throw new Exception("无法实例化小摊模型。");
            stand.name = "Banana Market Stand";
            stand.transform.SetPositionAndRotation(plan, facing);
            stand.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(stand, "导入集市小摊");
            if (!BoundsOf(stand, out Bounds sourceBounds))
                throw new Exception("小摊模型没有可见 Renderer。");
            float scale = Mathf.Min(3.4f / Mathf.Max(.01f, sourceBounds.size.x),
                3.0f / Mathf.Max(.01f, sourceBounds.size.y));
            stand.transform.localScale = Vector3.one * scale;
            Physics.SyncTransforms();
            AlignBottom(stand, groundY + .015f);
            CenterXZ(stand, new Vector3(plan.x, 0, plan.z));
            AddMeshColliders(stand);
            Debug.Log("[Monkey Care] Imported Drive market stand from " + importedPath);
        }
        else
        {
            stand = BuildFallbackMarketStand(farmRoot, plan, facing);
            Debug.Log("[Monkey Care] MarketStand_1.fbx is missing; using the editable fallback stand.");
        }

        stand.SetActive(true);
        Physics.SyncTransforms();
        AlignBottom(stand, groundY + .015f);
        CenterXZ(stand, new Vector3(plan.x, 0f, plan.z));
        Physics.SyncTransforms();
        foreach (var transform in stand.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = 0;
        if (BoundsOf(stand, out Bounds standBounds))
            ClearVegetationAround(stand);
        BuildTwoCrates(stand, farmRoot);
        EditorUtility.SetDirty(stand);
    }

    static GameObject BuildFallbackMarketStand(Transform parent, Vector3 position, Quaternion rotation)
    {
        Material wood = EnsureMaterial("Market Wood", new Color(.38f, .18f, .07f, 1f));
        Material cloth = EnsureMaterial("Market Awning Orange", new Color(.92f, .34f, .08f, 1f));
        Material cream = EnsureMaterial("Market Awning Cream", new Color(1f, .88f, .54f, 1f));
        var stand = new GameObject("Banana Market Stand");
        stand.transform.SetParent(parent, false);
        stand.transform.SetPositionAndRotation(position, rotation);
        Undo.RegisterCreatedObjectUndo(stand, "创建集市小摊");

        Primitive(stand.transform, "Counter", new Vector3(0, 1.03f, 0),
            new Vector3(3.2f, .16f, 1.3f), wood);
        Primitive(stand.transform, "Counter Surface", new Vector3(0, 1.13f, .02f),
            new Vector3(3.05f, .05f, 1.18f), cream);
        Primitive(stand.transform, "Front Panel", new Vector3(0, .56f, .55f),
            new Vector3(3.0f, .95f, .12f), wood);
        Primitive(stand.transform, "Back Support", new Vector3(0, 1.42f, -.56f),
            new Vector3(3.1f, .12f, .12f), wood);
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -1.52f : 1.52f;
            float z = i < 2 ? -.52f : .52f;
            Primitive(stand.transform, "Post " + (i + 1), new Vector3(x, 1.4f, z),
                new Vector3(.14f, 2.8f, .14f), wood);
        }
        for (int i = 0; i < 5; i++)
        {
            Primitive(stand.transform, "Awning stripe " + (i + 1),
                new Vector3((i - 2f) * .72f, 2.72f, 0),
                new Vector3(.7f, .16f, 1.8f), i % 2 == 0 ? cloth : cream);
        }
        return stand;
    }

    static void BuildTwoCrates(GameObject stand, Transform farmRoot)
    {
        Transform source = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == "banana box");
        if (!source) throw new Exception("找不到原始香蕉箱模型 banana box。");
        if (!VisualBounds(source.gameObject, out _)) throw new Exception("香蕉箱没有可见 Renderer。");

        var stack = new GameObject("Banana Crate Stack");
        stack.transform.SetParent(farmRoot, false);
        Undo.RegisterCreatedObjectUndo(stack, "在摊位摆放两箱香蕉");

        Transform counter = stand.transform.Find("Counter Surface");
        float counterY = BoundsOf(stand, out Bounds standBounds)
            ? standBounds.min.y + 1.18f
            : GroundY(stand.transform.position) + 1.18f;
        if (counter && BoundsOf(counter.gameObject, out Bounds surfaceBounds))
            counterY = surfaceBounds.max.y + .015f;
        Vector3 center = new(stand.transform.position.x, counterY, stand.transform.position.z);
        stack.transform.SetPositionAndRotation(center,
            Quaternion.Euler(0, stand.transform.eulerAngles.y, 0));

        var first = CreateCrate(source.gameObject, stack.transform,
            source.eulerAngles.y, "left");
        var second = CreateCrate(source.gameObject, stack.transform,
            source.eulerAngles.y, "right");
        float width = Mathf.Max(.2f, VisualBounds(first).size.x);
        float pitch = width + Mathf.Max(.06f, width * .10f);
        Vector3 right = stand.transform.right;
        PlaceOnY(first, center - right * (pitch * .5f), counterY);
        PlaceOnY(second, center + right * (pitch * .5f), counterY);
        EnsureStackCollider(stack);
        EnsurePickupMarker(stack);
        EditorUtility.SetDirty(stack);
    }

    static GameObject Primitive(Transform parent, string name, Vector3 localPosition,
        Vector3 localScale, Material material)
    {
        var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        if (material) piece.GetComponent<Renderer>().sharedMaterial = material;
        Undo.RegisterCreatedObjectUndo(piece, "创建摊位部件");
        return piece;
    }

    static Material EnsureMaterial(string name, Color color)
    {
        const string folder = "Assets/MonkeyCare/Materials";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/MonkeyCare", "Materials");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = SurfaceShader();
        if (!material)
        {
            if (!shader) return null;
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader && material.shader != shader)
        {
            material.shader = shader;
        }
        string texturePath = name.StartsWith("Market Wood", StringComparison.Ordinal)
            ? "Assets/MonkeyCare/Resources/Farm/MarketWood.png"
            : "Assets/MonkeyCare/Resources/Farm/MarketFabric.png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", name.StartsWith("Market Wood", StringComparison.Ordinal)
                ? Color.white : color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", name.StartsWith("Market Wood", StringComparison.Ordinal)
                ? Color.white : color);
        if (texture)
        {
            material.mainTexture = texture;
            material.mainTextureScale = new Vector2(1.5f, 1.5f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Shader SurfaceShader()
    {
        bool usesScriptableRenderPipeline =
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader shader = Shader.Find(usesScriptableRenderPipeline
            ? "Universal Render Pipeline/Lit" : "Standard");
        if (!shader) shader = Shader.Find(usesScriptableRenderPipeline
            ? "Standard" : "Universal Render Pipeline/Lit");
        return shader;
    }

    static GameObject FindIncluding(string name) =>
        UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == name)?.gameObject;

    static void RemoveBananaPiles(Transform farmRoot)
    {
        foreach (string name in new[] { "Banana Pile A", "Banana Pile B" })
        {
            var pile = farmRoot.Find(name);
            if (pile) Undo.DestroyObjectImmediate(pile.gameObject);
        }
    }

    static void RebuildBarnIfInvalid(Transform farmRoot)
    {
        GameObject barn = GameObject.Find("Start Open Barn");
        if (barn && BoundsOf(barn, out Bounds existing) &&
            existing.size.x >= 2.5f && existing.size.y >= 1.8f)
            return;

        Vector3 position = barn ? barn.transform.position : Vector3.zero;
        Quaternion rotation = barn ? barn.transform.rotation : Quaternion.identity;
        if (barn) Undo.DestroyObjectImmediate(barn);

        const string path = "Assets/MonkeyCare/Resources/Farm/OpenBarn.fbx";
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!template) throw new Exception("找不到小屋模型: " + path);
        barn = PrefabUtility.InstantiatePrefab(template, farmRoot) as GameObject;
        if (!barn) throw new Exception("无法实例化小屋模型。");
        barn.name = "Start Open Barn";
        barn.transform.position = position;
        barn.transform.rotation = rotation;
        barn.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(barn, "重建并缩放小屋");

        if (!BoundsOf(barn, out Bounds sourceBounds))
            throw new Exception("小屋模型没有可见 Renderer。");
        float scale = Mathf.Min(8f / Mathf.Max(.01f, sourceBounds.size.x),
            5f / Mathf.Max(.01f, sourceBounds.size.y));
        barn.transform.localScale = Vector3.one * scale;
        Physics.SyncTransforms();
        if (BoundsOf(barn, out Bounds scaled))
        {
            barn.transform.position += new Vector3(position.x - scaled.center.x, 0f,
                position.z - scaled.center.z);
            Physics.SyncTransforms();
            if (BoundsOf(barn, out scaled))
                barn.transform.position += Vector3.up * (position.y - scaled.min.y + .015f);
        }

        foreach (var filter in barn.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || filter.GetComponent<MeshCollider>()) continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }
        ClearVegetationAround(barn);
        EditorUtility.SetDirty(barn);
    }

    static void PositionCrateStackNearBarn()
    {
        var stack = GameObject.Find("Banana Crate Stack");
        var barn = GameObject.Find("Start Open Barn");
        if (!stack || !barn || !BoundsOf(barn, out Bounds barnBounds)) return;
        Vector3 forward = barn.transform.forward;
        Vector3 right = barn.transform.right;
        Vector3 center = barnBounds.center - forward * (barnBounds.extents.z * .2f) -
                         right * (barnBounds.extents.x * .34f);
        stack.transform.position = new Vector3(center.x, barnBounds.min.y + .03f, center.z);
        stack.transform.rotation = Quaternion.Euler(0f, barn.transform.eulerAngles.y, 0f);
        Physics.SyncTransforms();

        var marker = stack.GetComponentInChildren<CarePickupMarker>(true);
        if (marker) Undo.DestroyObjectImmediate(marker.gameObject);
        EnsureBananaPickupMarker();
        EditorUtility.SetDirty(stack);
    }

    static void ClearVegetationAround(GameObject root)
    {
        if (!BoundsOf(root, out Bounds clearing)) return;
        clearing.Expand(new Vector3(1.2f, .4f, 1.2f));
        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!transform) continue;
            string name = transform.name;
            if (!(name.StartsWith("Tree_") || name.StartsWith("BananaTree") ||
                  name.StartsWith("Bush_"))) continue;
            if (!BoundsOf(transform.gameObject, out Bounds vegetation) ||
                !vegetation.Intersects(clearing)) continue;
            transform.gameObject.SetActive(false);
            EditorUtility.SetDirty(transform.gameObject);
        }
    }

    static void EnsureVisible(string objectName)
    {
        var root = GameObject.Find(objectName);
        if (!root) throw new Exception("场景中缺少模型: " + objectName);
        root.SetActive(true);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = true;
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = 0;
        EditorUtility.SetDirty(root);
    }

    static void EnsureBananaPickupMarker()
    {
        var stack = GameObject.Find("Banana Crate Stack");
        if (!stack) throw new Exception("场景中缺少 Banana Crate Stack。");
        stack.SetActive(true);
        foreach (var renderer in stack.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;

        var marker = stack.GetComponentInChildren<CarePickupMarker>(true);
        if (marker) return;
        if (!BoundsOf(stack, out Bounds bounds)) return;
        marker = CarePickupMarker.Create(stack.transform,
            bounds.center + Vector3.up * (bounds.extents.y + 1.05f));
        Undo.RegisterCreatedObjectUndo(marker.gameObject, "恢复香蕉拾取提示");
    }

    static void RemoveSackObjects()
    {
        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!transform) continue;
            string name = transform.name;
            if (name.IndexOf("Sack", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("麻袋", StringComparison.OrdinalIgnoreCase) < 0) continue;
            Undo.DestroyObjectImmediate(transform.gameObject);
        }
    }

    static void HideOriginalBananaSupplies()
    {
        foreach (var item in UnityEngine.Object.FindObjectsByType<CareItem>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!item || item.kind != CareNeed.Hunger) continue;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var collider in item.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            EditorUtility.SetDirty(item);
        }
    }

    static void RebuildTwoBananaPiles(Transform farmRoot)
    {
        foreach (string name in new[] { "Banana Pile A", "Banana Pile B" })
        {
            var existing = farmRoot.Find(name);
            if (existing) Undo.DestroyObjectImmediate(existing.gameObject);
        }

        CareItem source = UnityEngine.Object.FindObjectsByType<CareItem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item && item.kind == CareNeed.Hunger &&
                                    item.GetComponentsInChildren<Renderer>(true).Any(r => r));
        GameObject barn = GameObject.Find("Start Open Barn");
        if (!source || !barn || !BoundsOf(barn, out Bounds barnBounds))
            throw new Exception("无法从香蕉源模型和小屋创建两堆装饰香蕉。");

        Vector3 floor = new(barnBounds.center.x, barnBounds.min.y, barnBounds.center.z);
        Vector3 right = barn.transform.right;
        Vector3 leftPoint = floor - right * (barnBounds.extents.x * .82f) + Vector3.up * 1.15f;
        Vector3 rightPoint = floor + right * (barnBounds.extents.x * .82f) + Vector3.up * 1.15f;
        CreatePile(source.gameObject, farmRoot, "Banana Pile A", leftPoint);
        CreatePile(source.gameObject, farmRoot, "Banana Pile B", rightPoint);
    }

    static void CreatePile(GameObject source, Transform parent, string name, Vector3 position)
    {
        var pile = new GameObject(name);
        pile.transform.SetParent(parent, false);
        pile.transform.position = position;
        Undo.RegisterCreatedObjectUndo(pile, "创建香蕉装饰堆");

        var offsets = new[]
        {
            new Vector3(-.13f, -.04f, .03f),
            new Vector3(-.04f, .04f, -.03f),
            new Vector3(.06f, -.02f, .04f),
            new Vector3(.13f, .05f, -.02f),
            new Vector3(0f, .10f, .02f)
        };
        for (int i = 0; i < offsets.Length; i++)
        {
            var copy = UnityEngine.Object.Instantiate(source, pile.transform, false);
            copy.name = "Banana " + (i + 1);
            copy.transform.localPosition = offsets[i];
            copy.transform.localRotation = Quaternion.Euler(0f, i * 33f, i % 2 == 0 ? 8f : -8f);
            foreach (var item in copy.GetComponentsInChildren<CareItem>(true))
                UnityEngine.Object.DestroyImmediate(item);
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body);
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
            foreach (var transform in copy.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = 0;
        }
    }

    static bool VisualBounds(GameObject root, out Bounds bounds) => BoundsOf(root, out bounds);

    static Bounds VisualBounds(GameObject root)
    {
        BoundsOf(root, out Bounds bounds);
        return bounds;
    }

    static float GroundY(Vector3 point)
    {
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 120f, Vector3.down, 240f,
                     ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore)
                 .Where(item => item.normal.y >= .45f)
                 .OrderBy(item => item.point.y))
        {
            return hit.point.y;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain && terrain.terrainData)
        {
            Vector3 position = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 local = point - position;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
                return terrain.SampleHeight(point) + position.y;
        }
        return point.y;
    }

    static void AlignBottom(GameObject root, float groundY)
    {
        if (!BoundsOf(root, out Bounds bounds)) return;
        root.transform.position += Vector3.up * (groundY - bounds.min.y);
    }

    static void CenterXZ(GameObject root, Vector3 target)
    {
        if (!BoundsOf(root, out Bounds bounds)) return;
        root.transform.position += new Vector3(target.x - bounds.center.x, 0f,
            target.z - bounds.center.z);
    }

    static void PlaceOnY(GameObject item, Vector3 horizontal, float bottomY)
    {
        item.transform.position = new Vector3(horizontal.x, 0f, horizontal.z);
        if (!BoundsOf(item, out Bounds bounds)) return;
        item.transform.position += new Vector3(horizontal.x - bounds.center.x, 0f,
            horizontal.z - bounds.center.z);
        BoundsOf(item, out bounds);
        item.transform.position += Vector3.up * (bottomY - bounds.min.y);
    }

    static void AddMeshColliders(GameObject root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || filter.GetComponent<MeshCollider>()) continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            Undo.RegisterCreatedObjectUndo(collider, "添加小摊碰撞");
        }
    }

    static GameObject CreateCrate(GameObject source, Transform parent, float yaw, string suffix)
    {
        GameObject copy = UnityEngine.Object.Instantiate(source, parent, false);
        Undo.RegisterCreatedObjectUndo(copy, "复制香蕉箱");
        copy.name = "Banana Crate " + suffix;
        copy.transform.localScale = source.transform.lossyScale;
        copy.transform.rotation = Quaternion.Euler(0, yaw, 0);
        foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;
        return copy;
    }

    static void EnsureStackCollider(GameObject stack)
    {
        var collider = stack.GetComponent<BoxCollider>();
        if (!collider) collider = stack.AddComponent<BoxCollider>();
        if (!BoundsOf(stack, out Bounds bounds)) return;
        collider.center = stack.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size;
    }

    static void EnsurePickupMarker(GameObject stack)
    {
        if (stack.GetComponentInChildren<CarePickupMarker>(true)) return;
        if (!BoundsOf(stack, out Bounds bounds)) return;
        var marker = CarePickupMarker.Create(stack.transform,
            bounds.center + Vector3.up * (bounds.extents.y + 1.05f));
        Undo.RegisterCreatedObjectUndo(marker.gameObject, "添加香蕉箱拾取提示");
    }

    static void WriteAudit(Scene scene, MonkeyCareSession session)
    {
        var report = new StringBuilder();
        report.AppendLine("Scene: " + scene.path);
        report.AppendLine("Bananas=" + CareRound.StartingBananas +
                          " PickupAmount=" + CareRound.BananaPickupAmount +
                          " Haptics=" + session.hapticAmplitude.ToString("F2"));
        report.AppendLine("Score=feedings10-penalties clamping 0..10; pass>=6; stars 6/8/10");
        report.AppendLine("OldBarn=" + Describe("Start Open Barn"));
        report.AppendLine("MarketStand=" + Describe("Banana Market Stand"));
        report.AppendLine("Windmill=" + Describe("Background Windmill"));
        report.AppendLine("CrateStack=" + Describe("Banana Crate Stack"));
        report.AppendLine("PickupMarker=" + Describe("Banana Pickup Marker"));
        report.AppendLine("BananaPileA=" + Describe("Banana Pile A"));
        report.AppendLine("BananaPileB=" + Describe("Banana Pile B"));
        report.AppendLine();

        report.AppendLine("CareItem supplies:");
        foreach (var item in UnityEngine.Object.FindObjectsByType<CareItem>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None)
                 .OrderBy(item => Hierarchy(item.transform)))
        {
            bool visible = item && item.GetComponentsInChildren<Renderer>(true).Any(r => r && r.enabled);
            string bounds = item && BoundsOf(item.gameObject, out Bounds itemBounds)
                ? itemBounds.center.ToString("F2")
                : "none";
            report.AppendLine("  " + (item ? item.kind.ToString() : "NULL") +
                              " | path=" + (item ? Hierarchy(item.transform) : "-") +
                              " | active=" + (item && item.gameObject.activeInHierarchy) +
                              " | visible=" + visible + " | center=" + bounds);
        }

        report.AppendLine();
        report.AppendLine("Sack objects:");
        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None)
                 .Where(item => item && item.name.IndexOf("Sack", StringComparison.OrdinalIgnoreCase) >= 0))
            report.AppendLine("  " + Hierarchy(transform));

        File.WriteAllText(Audit, report.ToString());
    }

    static string Describe(string name)
    {
        var root = GameObject.Find(name);
        if (!root) return "MISSING";
        bool bounds = BoundsOf(root, out Bounds value);
        return "active=" + root.activeInHierarchy +
               " renderers=" + root.GetComponentsInChildren<Renderer>(true).Count(r => r && r.enabled) +
               " center=" + (bounds ? value.center.ToString("F2") : "none") +
               " size=" + (bounds ? value.size.ToString("F2") : "none");
    }

    static bool BoundsOf(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    static string Hierarchy(Transform transform)
    {
        if (!transform) return "-";
        return transform.parent ? Hierarchy(transform.parent) + "/" + transform.name : transform.name;
    }

    static string ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;
}
