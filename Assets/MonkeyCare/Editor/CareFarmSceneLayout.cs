using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADayAtTheZoo.Monkeys.Care;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Commits the farm dressing to the authored scene so it is visible before
// pressing Play. The one-shot request file keeps this repeatable from the
// automation session without adding a permanent editor popup.
[InitializeOnLoad]
public static class CareFarmSceneLayout
{
    const string Work = "work/monkey-care/";
    const string Request = Work + "farm-layout.request";
    const string Result = Work + "farm-layout-result.txt";

    static CareFarmSceneLayout()
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
            File.WriteAllText(Result, "SUCCESS " + DateTime.Now.ToString("s"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(Result, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/猴子照顾/更新小摊与风车布局")]
    public static void Apply()
    {
        // Keep the historical menu/request entry point, but route all layout
        // changes through the current market-stand pass so an old barn cannot
        // be recreated by accident.
        CareRequirementsPass.Apply();
        return;
#pragma warning disable CS0162
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MonkeyCareInstaller.ScenePath)
            throw new Exception("Please open the Highland Lake care scene first.");

        GameObject playerRoot = Find("Player and Locomotion") ??
                                SceneManager.GetActiveScene().GetRootGameObjects()
                                    .FirstOrDefault(item => item.GetComponentInChildren<DesktopPlayerController>(true));
        DesktopPlayerController player = playerRoot
            ? playerRoot.GetComponentInChildren<DesktopPlayerController>(true)
            : null;
        if (!player) throw new Exception("The desktop player rig was not found in the care scene.");
        Camera camera = player.GetComponentInChildren<Camera>(true);
        Vector3 forward = camera ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : player.transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();

        string backupPath = "Assets/MonkeyCare/Backups/HighlandLake_BeforeFarm_" +
                            DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!AssetDatabase.CopyAsset(scene.path, backupPath))
            throw new Exception("Could not create a scene backup before editing.");

        GameObject farmRoot = scene.GetRootGameObjects()
            .FirstOrDefault(item => item.name == "Monkey Care - Farm");
        if (!farmRoot) farmRoot = new GameObject("Monkey Care - Farm");

        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("导入照顾关卡农场");

        GameObject barn = Find("Start Open Barn");
        if (!barn)
        {
            barn = InstantiateModel("Assets/MonkeyCare/Resources/Farm/OpenBarn.fbx",
                "Start Open Barn", farmRoot.transform);
            barn.transform.localScale = Vector3.one * CareFarmEnvironment.BarnScale;

            Vector3 plan = player.transform.position - forward * CareFarmEnvironment.BarnCenterDistance;
            float groundY = GroundY(plan);
            AlignBottom(barn, groundY + .015f);
            CenterXZ(barn, new Vector3(plan.x, 0, plan.z));
            barn.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            AddMeshColliders(barn);
        }

        if (VisualBounds(barn, out Bounds barnBounds))
            ClearVegetation(barnBounds);

        BuildCrateStack(barn);

        GameObject windmill = Find("Background Windmill");
        if (!windmill)
        {
            Vector3 point = player.transform.position + forward * 20f +
                            Vector3.Cross(Vector3.up, forward) * 7f;
            float groundY = GroundY(point);
            windmill = InstantiateModel("Assets/MonkeyCare/Resources/Farm/Windmill.fbx",
                "Background Windmill", farmRoot.transform);
            windmill.transform.localScale = Vector3.one * CareFarmEnvironment.WindmillScale;
            AlignBottom(windmill, groundY + .01f);
            Vector3 facing = player.transform.position - windmill.transform.position;
            facing.y = 0;
            windmill.transform.rotation = Quaternion.LookRotation(
                facing.sqrMagnitude > .01f ? facing.normalized : -forward, Vector3.up);
        }

        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new Exception("Unity could not save the authored farm layout.");
        AssetDatabase.Refresh();
        Debug.Log("[Monkey Care] Authored barn, five crates, and windmill are now saved in the scene. Backup: " +
                  backupPath);
#pragma warning restore CS0162
    }

    static GameObject InstantiateModel(string path, string name, Transform parent)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!template) throw new Exception("Missing model: " + path);
        var instance = PrefabUtility.InstantiatePrefab(template, parent) as GameObject;
        if (!instance) throw new Exception("Could not instantiate model: " + path);
        instance.name = name;
        Undo.RegisterCreatedObjectUndo(instance, "导入农场模型");
        return instance;
    }

    static void BuildCrateStack(GameObject barn)
    {
        GameObject existing = Find("Banana Crate Stack");
        if (existing)
        {
            EnsureStackCollider(existing);
            EnsurePickupMarker(existing);
            return;
        }

        GameObject source = Find("banana box");
        if (!source) throw new Exception("The original banana box artwork was not found.");
        if (!VisualBounds(source, out _)) throw new Exception("The banana box has no visible renderers.");
        if (!VisualBounds(barn, out Bounds barnBounds))
            throw new Exception("The barn has no visible renderers.");

        var stack = new GameObject("Banana Crate Stack");
        Undo.RegisterCreatedObjectUndo(stack, "布置香蕉箱");
        stack.transform.SetParent(barn.transform.parent, false);
        Vector3 forward = barn.transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 center = barnBounds.center - forward * barnBounds.extents.z * .28f;
        Vector3 ground = new Vector3(center.x, GroundY(center), center.z);
        stack.transform.SetPositionAndRotation(ground, barn.transform.rotation);

        float sourceYaw = source.transform.eulerAngles.y;
        var bottom = new List<GameObject>(3);
        for (int i = 0; i < 3; i++)
            bottom.Add(CreateCrate(source, stack.transform, sourceYaw, "bottom-" + (i + 1)));

        float width = Mathf.Max(.2f, VisualBounds(bottom[0]).size.x);
        float pitch = width + Mathf.Max(.045f, width * .08f);
        float floorY = ground.y + .02f;
        for (int i = 0; i < bottom.Count; i++)
            PlaceOnY(bottom[i], center + right * ((i - 1) * pitch), floorY);

        float topY = bottom.Max(item => VisualBounds(item).max.y) + .025f;
        var top = new List<GameObject>(2);
        for (int i = 0; i < 2; i++)
            top.Add(CreateCrate(source, stack.transform, sourceYaw, "top-" + (i + 1)));
        for (int i = 0; i < top.Count; i++)
        {
            Vector3 position = center + right * ((i - .5f) * pitch) - forward * .035f;
            PlaceOnY(top[i], position, topY);
        }

        EnsureStackCollider(stack);
        EnsurePickupMarker(stack);
        foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        foreach (var collider in source.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
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
        if (!VisualBounds(stack, out Bounds bounds)) return;
        collider.center = stack.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size;
    }

    static void EnsurePickupMarker(GameObject stack)
    {
        if (stack.GetComponentInChildren<CarePickupMarker>(true)) return;
        if (!VisualBounds(stack, out Bounds bounds)) return;
        var marker = CarePickupMarker.Create(stack.transform,
            bounds.center + Vector3.up * (bounds.extents.y + 1.05f));
        Undo.RegisterCreatedObjectUndo(marker.gameObject, "添加香蕉箱拾取提示");
        EditorUtility.SetDirty(marker.gameObject);
    }

    static void ClearVegetation(Bounds barn)
    {
        barn.Expand(new Vector3(.75f, .25f, .75f));
        var roots = new HashSet<Transform>();
        foreach (var item in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!(item.name.StartsWith("Tree_") || item.name.StartsWith("BananaTree") ||
                  item.name.StartsWith("Bush_"))) continue;
            Transform root = item;
            for (Transform cursor = item.parent; cursor && cursor.parent; cursor = cursor.parent)
            {
                if (!cursor.parent.name.StartsWith("03 Vegetation")) continue;
                root = cursor;
                break;
            }
            roots.Add(root);
        }

        foreach (var root in roots)
        {
            if (!root || !VisualBounds(root.gameObject, out Bounds bounds)) continue;
            if (!bounds.Intersects(barn)) continue;
            Undo.RecordObject(root.gameObject, "清理谷仓占地内的植被");
            root.gameObject.SetActive(false);
            EditorUtility.SetDirty(root.gameObject);
        }
    }

    static float GroundY(Vector3 point)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain && terrain.terrainData)
        {
            Vector3 position = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 local = point - position;
            if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
                return terrain.SampleHeight(point) + position.y;
        }
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 60f, Vector3.down, 120f,
                     ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore)
                 .OrderBy(item => item.distance))
        {
            if (hit.normal.y >= .45f) return hit.point.y;
        }
        return point.y;
    }

    static void AddMeshColliders(GameObject root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || filter.GetComponent<MeshCollider>()) continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            Undo.RegisterCreatedObjectUndo(collider, "添加谷仓碰撞");
        }
    }

    static void AlignBottom(GameObject root, float groundY)
    {
        if (!VisualBounds(root, out Bounds bounds)) return;
        root.transform.position += Vector3.up * (groundY - bounds.min.y);
    }

    static void CenterXZ(GameObject root, Vector3 target)
    {
        if (!VisualBounds(root, out Bounds bounds)) return;
        Vector3 center = bounds.center;
        root.transform.position += new Vector3(target.x - center.x, 0, target.z - center.z);
    }

    static void PlaceOnY(GameObject item, Vector3 horizontal, float bottomY)
    {
        item.transform.position = new Vector3(horizontal.x, 0, horizontal.z);
        if (!VisualBounds(item, out Bounds bounds)) return;
        item.transform.position += Vector3.up * (bottomY - bounds.min.y);
    }

    static bool VisualBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    static Bounds VisualBounds(GameObject root)
    {
        VisualBounds(root, out Bounds value);
        return value;
    }

    static GameObject Find(string name)
    {
        Transform found = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == name);
        return found ? found.gameObject : null;
    }
}
