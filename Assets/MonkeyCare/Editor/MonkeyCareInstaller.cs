using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using ADayAtTheZoo.Monkeys;
using ADayAtTheZoo.Monkeys.Care;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MonkeyCareInstaller
{
    public const string ScenePath = "Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake_Backup_2026-11-03.unity";
    public const string Generated = "Assets/MonkeyCare/Generated";
    const string UiFolder = "Assets/Interaction Assets/UI切图-游戏界面/";
    const string Work = "work/monkey-care/";
    static MonkeyCareInstaller() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Work + "refresh.request"))
        {
            File.Delete(Work + "refresh.request"); AssetDatabase.Refresh(); return;
        }
        if (!File.Exists(Work + "install.request")) return;
        SessionState.SetBool("MonkeyCarePlayTestActive", false);
        File.Delete(Work + "install.request");
        try { Install(); } catch (Exception ex) { File.WriteAllText(Work + "install-result.txt", ex.ToString()); Debug.LogException(ex); }
    }
    [MenuItem("Tools/猴子照顾/配置当前HighlandLake场景")]
    public static void Install()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("当前场景不是指定HighlandLake场景，未做修改。");
        Directory.CreateDirectory(Work); Directory.CreateDirectory(Generated); AssetDatabase.Refresh();
        var report = new StringBuilder();
        string backupRoot = File.ReadAllText(Work + "backup-path.txt").Trim();
        string snapshot = "Assets/MonkeyCare/Backups/HighlandLake_BeforeCare_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        Directory.CreateDirectory("Assets/MonkeyCare/Backups");
        if (!EditorSceneManager.SaveScene(scene, snapshot, true)) throw new IOException("无法备份编辑器场景，已停止。");
        File.Copy(snapshot, Path.Combine(backupRoot, Path.GetFileName(snapshot)), false);
        report.AppendLine("Editor scene snapshot: " + snapshot);

        string[] names = { "Monkey_Wave_01 (1)", "02奶咖色猴子Wave", "Monkey_Wave_01" };
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var targets = names.Select(n => all.Single(t => t.name == n)).ToArray();
        foreach (var t in targets)
        {
            var a = t.GetComponent<Animator>();
            if (!a || !a.avatar || !a.avatar.isValid || !a.avatar.isHuman) throw new InvalidOperationException("NPC不是有效Humanoid: " + t.name);
        }
        var library = BuildAnimations();
        var controller = Load<RuntimeAnimatorController>(Generated + "/Care.controller");
        var session = Object.FindFirstObjectByType<MonkeyCareSession>(FindObjectsInactive.Include);
        if (!session) session = new GameObject("Monkey Care - 核心交互").AddComponent<MonkeyCareSession>();
        session.rig = Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
        session.locomotion = Object.FindFirstObjectByType<Locomotion>(FindObjectsInactive.Include);
        if (!session.rig || !session.locomotion) throw new InvalidOperationException("缺少现有VR Rig或Locomotion组件。");
        session.trackingSpace = session.rig.trackingSpace;
        bool migrateRules = session.rulesVersion < 1;
        session.readyGo = Sprite("readygo.png");
        session.roundArtwork.scorePanel = Sprite("UI SCORE.png");
        session.roundArtwork.bananaPanel = Sprite("banana.png");
        session.roundArtwork.level1 = Sprite("1.png"); session.roundArtwork.level2 = Sprite("2.png"); session.roundArtwork.level3 = Sprite("3.png");
        const string resultsFolder = "Assets/Interaction Assets/UI切图-结算界面/UI切图-结算界面/";
        session.roundArtwork.resultsPanel = SpriteAt(resultsFolder + "Results Screen.png");
        session.roundArtwork.star = SpriteAt(resultsFolder + "star .png");
        session.roundArtwork.continueButton = SpriteAt(resultsFolder + "continue.png");
        session.roundArtwork.restartButton = SpriteAt(resultsFolder + "restart.png");
        session.readyEffect.prefab = null;
        session.readySeconds = 3;
        session.roundArtwork.bananaAnchor = all.Single(t => t.name == "Wooden crate_Lowpoly_Textured (1)");
        var crateBounds = BoundsOf(session.roundArtwork.bananaAnchor.gameObject);
        session.roundArtwork.bananaOffset = new Vector3(-.18f, crateBounds.max.y - session.roundArtwork.bananaAnchor.position.y + .43f, 0);
        session.pointerMaterial = Material("Pointer", Shader.Find("Sprites/Default"), Color.white);
        var markerMaterial = Material("Teleport", Shader.Find("Sprites/Default"), new Color(.15f, .85f, .85f, .7f));
        var ghostMaterial = Material("Ghost", Shader.Find("Sprites/Default"), new Color(.65f, 1, 1, .25f));

        session.monkeys = new MonkeyCareNpc[3];
        float[] delays = { 15, 25, 35 };
        for (int i = 0; i < targets.Length; i++)
        {
            var go = targets[i].gameObject;
            var npc = go.GetComponent<MonkeyCareNpc>(); bool fresh = !npc;
            if (fresh) npc = go.AddComponent<MonkeyCareNpc>();
            npc.animator = go.GetComponent<Animator>(); npc.animator.enabled = true;
            npc.animator.runtimeAnimatorController = controller; npc.animator.applyRootMotion = false;
            npc.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; npc.library = library;
            var driver = go.GetComponent<MonkeyNpcAnimator>(); if (!driver) driver = go.AddComponent<MonkeyNpcAnimator>();
            driver.Configure(library);
            if (fresh || migrateRules)
            {
                npc.firstDelay = delays[i]; npc.firstNeed = i == 1 ? CareNeed.Itch : CareNeed.Hunger;
                npc.hungerInterval = npc.itchInterval = 18; npc.recoveryWait = 4; npc.patienceSeconds = 30;
                npc.feedingSeconds = 8; npc.lateFeedingSeconds = 10; npc.scratchCount = 3; npc.lateScratchCount = 4;
                npc.victory.repeats = 3; npc.cute.repeats = 2;
            }
            // Temporarily disabled demand effects (StarBurst / StunExplosion).
            // npc.hungerEffect.prefab = Fx("Combat/Explosions/- Misc/StarBurst");
            // npc.hungerEffect.position = new Vector3(0, .7f, 0); npc.hungerEffect.scale = Vector3.one * .25f;
            // npc.itchEffect.prefab = Fx("Combat/Explosions/- Misc/StunExplosion");
            // npc.itchEffect.position = new Vector3(0, .7f, 0); npc.itchEffect.scale = Vector3.one * .3f;
            npc.smokeEffect.prefab = Fx("Environment/Smoke/White/SmokeExplosionWhite"); npc.smokeEffect.scale = Vector3.one * .25f;
            npc.ui.stamina = Sprite("STAMINA.png"); npc.ui.satisfaction = Sprite("SATISFACTION.png");
            npc.ui.track = Sprite("progress bar background.png");
            npc.ui.staminaFill = Sprite("progress bar background STAMINA.png");
            npc.ui.satisfactionFill = Sprite("progress bar background SATISFACTION.png");
            npc.ui.staminaLogo = Sprite("STAMINA LOGO.png"); npc.ui.satisfactionLogo = Sprite("SATISFACTION LOGO.png");
            npc.ui.sad = Sprite("sad.png"); npc.ui.happy = Sprite("happy.png");
            Bounds bounds = BoundsOf(go);
            if (fresh)
            {
                float height = bounds.max.y - go.transform.position.y;
                npc.ui.barOffset.y = height + .3f;
                npc.ui.sadOffset.y = npc.ui.happyOffset.y = height;
            }
            var contact = go.transform.Find("Care Contact");
            if (!contact) { contact = new GameObject("Care Contact").transform; contact.SetParent(go.transform, false); }
            var box = contact.GetComponent<BoxCollider>(); if (!box) box = contact.gameObject.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            var scale = go.transform.lossyScale;
            box.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);
            box.isTrigger = true;
            if (!npc.teleportPoint)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name = "Care Teleport - " + names[i];
                marker.transform.SetParent(session.transform); marker.transform.localScale = new Vector3(.38f, .012f, .38f);
                marker.GetComponent<Collider>().isTrigger = true;
                npc.teleportPoint = marker.AddComponent<CareTeleportPoint>(); npc.teleportPoint.owner = npc;
                Vector3 landing = go.transform.position + Vector3.ProjectOnPlane(go.transform.forward, Vector3.up).normalized * 1.2f;
                var ground = all.First(t => t.name == "Island_1").GetComponent<Collider>();
                if (!ground.Raycast(new Ray(landing + Vector3.up * 10, Vector3.down), out var hit, 30)) throw new Exception("传送点未找到地面: " + names[i]);
                marker.transform.position = hit.point + Vector3.up * .025f;
                marker.GetComponent<Renderer>().sharedMaterial = markerMaterial; marker.SetActive(false);
            }
            if (!session.locomotion.IsLandingClear(npc.teleportPoint.Landing))
            {
                var ground = all.First(t => t.name == "Island_1").GetComponent<Collider>();
                bool found = false;
                foreach (float radius in new[] { 1.4f, 1.8f, 2.2f, 2.6f, 3f })
                {
                    foreach (float angle in new[] { 0f, -25f, 25f, -50f, 50f, -75f, 75f })
                    {
                        Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.ProjectOnPlane(go.transform.forward, Vector3.up).normalized;
                        Vector3 candidate = go.transform.position + direction * radius;
                        if (ground.Raycast(new Ray(candidate + Vector3.up * 10, Vector3.down), out var safeGround, 30) &&
                            safeGround.normal.y >= .45f && safeGround.point.y >= -.12f && session.locomotion.IsLandingClear(safeGround.point))
                        { npc.teleportPoint.transform.position = safeGround.point + Vector3.up * .025f; found = true; break; }
                    }
                    if (found) break;
                }
                if (!found) throw new Exception("猴子前方没有安全传送落点: " + names[i] + " point=" + npc.teleportPoint.Landing + " blockers=" + string.Join(",", Physics.OverlapCapsule(npc.teleportPoint.Landing + Vector3.up * .56f, npc.teleportPoint.Landing + Vector3.up * 1.79f, .3f, ~0, QueryTriggerInteraction.Ignore).Select(c => Hierarchy(c.transform))));
            }
            session.monkeys[i] = npc;
            EditorUtility.SetDirty(npc); EditorUtility.SetDirty(driver); PrefabUtility.RecordPrefabInstancePropertyModifications(npc.animator);
            report.AppendLine(names[i] + " first=" + npc.firstNeed + " delay=" + npc.firstDelay + " avatar=" + npc.animator.avatar.name);
        }
        if (session.items == null || session.items.Length != 2) session.items = new CareItem[2];
        string[] itemNames = { "base_basic_shaded (9)", "base_basic_shaded (10)" };
        for (int i = 0; i < 2; i++)
        {
            var existingItem = session.items != null && session.items.Length > i ? session.items[i] : null;
            var go = existingItem ? existingItem.gameObject : all.Single(t => t.name == itemNames[i] && (i != 0 || (t.parent && t.parent.name == "banana box"))).gameObject;
            var item = go.GetComponent<CareItem>(); if (!item) item = go.AddComponent<CareItem>();
            item.kind = i == 0 ? CareNeed.Hunger : CareNeed.Itch; item.ghostMaterial = ghostMaterial;
            var body = go.GetComponent<Rigidbody>(); if (!body) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = true; body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var box = go.GetComponent<BoxCollider>(); if (!box) box = go.AddComponent<BoxCollider>();
            Bounds bounds = BoundsOf(go);
            box.center = go.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = go.transform.InverseTransformVector(bounds.size);
            // World AABB projected onto local axes overestimates rotated props, so use mesh-local bounds below.
            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 1 && filters[0].transform == go.transform) { box.center = filters[0].sharedMesh.bounds.center; box.size = filters[0].sharedMesh.bounds.size; }
            else
            {
                Bounds localBounds = new Bounds(go.transform.InverseTransformPoint(bounds.center), Vector3.zero);
                foreach (var filter in filters)
                {
                    var b = filter.sharedMesh.bounds;
                    for (int c = 0; c < 8; c++) localBounds.Encapsulate(go.transform.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)))));
                }
                box.center = localBounds.center; box.size = localBounds.size;
            }
            item.gripPosition = i == 0 ? new Vector3(0, -.02f, .16f) : new Vector3(0, -.02f, .2f);
            session.items[i] = item; EditorUtility.SetDirty(item);
            report.AppendLine(itemNames[i] + " bounds=" + bounds.size.ToString("F3"));
        }
        foreach (var behaviour in session.rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!behaviour) continue;
            string type = behaviour.GetType().Name;
            string path = Hierarchy(behaviour.transform);
            bool right = path.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0;
            if (right && (type.Contains("Interactor") || type.Contains("Reticle")) && !type.Contains("ControllerRef"))
            { behaviour.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour); report.AppendLine("Disabled old right input: " + path + " / " + type); }
        }
        foreach (var old in Object.FindObjectsByType<ChibiMonkeyHiRayInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None)) old.enabled = false;
        session.rulesVersion = 1;
        EditorUtility.SetDirty(session); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("保存场景失败");
        CareSelfTests.Run();
        File.WriteAllText(Work + "install-result.txt", "SUCCESS\n" + report);
        Selection.activeGameObject = session.gameObject;
        Debug.Log("[Monkey Care] 场景配置完成，备份：" + snapshot);
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) throw new Exception("模型没有Renderer: " + go.name);
        var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;
    static GameObject Fx(string path) => Load<GameObject>("Assets/Epic Toon FX/Prefabs/" + path + ".prefab");
    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ? AssetDatabase.LoadAssetAtPath<T>(path) : throw new Exception("缺少素材: " + path);
    static Sprite Sprite(string name)
        => SpriteAt(UiFolder + name);
    static Sprite SpriteAt(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return Load<Sprite>(path);
    }
    static Material Material(string name, Shader shader, Color color)
    {
        string path = Generated + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; EditorUtility.SetDirty(mat); return mat;
    }
    static MonkeyAnimationLibrary BuildAnimations()
    {
        var source = Load<MonkeyAnimationLibrary>("Assets/MonkeyNPC/Generated/MonkeyAnimationLibrary.asset");
        var entries = new System.Collections.Generic.List<MonkeyAnimationLibrary.Entry>();
        foreach (MonkeyAction action in Enum.GetValues(typeof(MonkeyAction)))
        {
            var clip = source.GetClip(action);
            if (action == MonkeyAction.Cute1) clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Suriyun/Animations/Pspsps Animations/Character Animations/Anim_Chibi@Cute1.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            if (!clip || !clip.isHumanMotion) throw new Exception("缺少Humanoid动画: " + action);
            var copy = Object.Instantiate(clip); copy.name = "Care_" + action;
            var settings = AnimationUtility.GetAnimationClipSettings(copy); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(copy, settings);
            string path = Generated + "/Care_" + action + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing) { EditorUtility.CopySerialized(copy, existing); Object.DestroyImmediate(copy); copy = existing; EditorUtility.SetDirty(copy); }
            else AssetDatabase.CreateAsset(copy, path);
            entries.Add(new MonkeyAnimationLibrary.Entry { action = action, clip = copy, source = AssetDatabase.GetAssetPath(clip) });
        }
        var library = AssetDatabase.LoadAssetAtPath<MonkeyAnimationLibrary>(Generated + "/CareLibrary.asset");
        if (!library) { library = ScriptableObject.CreateInstance<MonkeyAnimationLibrary>(); AssetDatabase.CreateAsset(library, Generated + "/CareLibrary.asset"); }
        library.SetEntries(entries.ToArray()); EditorUtility.SetDirty(library);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/Care.controller");
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(Generated + "/Care.controller");
        var machine = controller.layers[0].stateMachine;
        foreach (var entry in entries)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == entry.action.ToString());
            if (!state) state = machine.AddState(entry.action.ToString());
            foreach (var transition in state.transitions) state.RemoveTransition(transition);
            state.motion = entry.clip; state.writeDefaultValues = false;
            if (entry.action == MonkeyAction.Hi) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller); return library;
    }
}

