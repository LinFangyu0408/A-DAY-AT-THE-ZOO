using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using ADayAtTheZoo.Monkeys;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC.Editor
{
    public static partial class ZooDayTwoBuilder
    {
        [MenuItem("Zoo/Day Two/Build dedicated Day Two scene")]
        public static void Setup()
        {
            Directory.CreateDirectory(Root + "Generated"); Directory.CreateDirectory(Root + "Scenes"); AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(SourceScene);
            var oldSession = UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>(FindObjectsInactive.Include);
            var originalMonkeys = oldSession.monkeys.ToArray();
            var bananaSource = oldSession.items.First(i => i.kind == CareNeed.Hunger);
            var scratchSource = oldSession.items.First(i => i.kind == CareNeed.Itch);
            var banana = MakeToolPrefab(bananaSource.gameObject, "Banana");
            var scratcher = MakeToolPrefab(scratchSource.gameObject, "BackScratcher");
            var library = originalMonkeys[0].library;
            var controller = BuildController(library);
            foreach (var oldPlayer in UnityEngine.Object.FindObjectsByType<DesktopPlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) oldPlayer.gameObject.SetActive(false);
            foreach (var switcher in UnityEngine.Object.FindObjectsByType<VrPlatformSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(switcher);
            foreach (var rig in UnityEngine.Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None)) rig.gameObject.SetActive(false);
            foreach (var manager in UnityEngine.Object.FindObjectsByType<OVRManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) manager.enabled = false;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.enabled = false;
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None)) listener.enabled = false;
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour && behaviour.GetType().Namespace == "ADayAtTheZoo.Monkeys.Care") behaviour.enabled = false;
            foreach (var item in oldSession.items) item.gameObject.SetActive(false);
            foreach (var point in UnityEngine.Object.FindObjectsByType<CareTeleportPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)) point.gameObject.SetActive(false);
            oldSession.gameObject.SetActive(false);
            // This inherited marker has a scene-local MonoScript and cannot
            // be serialized reliably into a player. Day 02 provides its own
            // supply marker, so remove the legacy object from this PC copy.
            foreach (var item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item && item.name == "Banana Pickup Marker") UnityEngine.Object.DestroyImmediate(item.gameObject);

            var root = new GameObject("DAY 02 - Lakeside Care");
            var session = root.AddComponent<ZooDayTwoSession>();
            var rules = AssetDatabase.LoadAssetAtPath<ZooDayTwoRules>(Root + "Generated/DayTwoRules.asset");
            if (!rules) { rules = ScriptableObject.CreateInstance<ZooDayTwoRules>(); AssetDatabase.CreateAsset(rules, Root + "Generated/DayTwoRules.asset"); }
            rules.monkeyCount = 5; rules.duration = 180; rules.oneStar = 7; rules.twoStars = 14; rules.threeStars = 20;
            EditorUtility.SetDirty(rules);
            session.rules = rules; session.bananaPrefab = banana; session.scratcherPrefab = scratcher;
            session.bananaIcon = Resources.Load<Texture2D>("UI/Backpack/banana");
            if (!session.bananaIcon) session.bananaIcon = Resources.Load<Texture2D>("UI/GameUI2/banana");
            session.scratcherIcon = Resources.Load<Texture2D>("UI/Backpack/back_scratcher");
            var player = new GameObject("Day 02 PC keeper", typeof(CharacterController)); player.transform.SetParent(root.transform);
            player.transform.position = Ground(new Vector3(0, 0, -24)) + Vector3.up * .05f;
            var character = player.GetComponent<CharacterController>(); character.height = 1.8f; character.radius = .3f; character.center = Vector3.up * .9f;
            var playerObstacle = player.AddComponent<UnityEngine.AI.NavMeshObstacle>(); playerObstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
            playerObstacle.center = Vector3.up * .9f; playerObstacle.height = 1.8f; playerObstacle.radius = .4f; playerObstacle.carving = false;
            var cameraGo = new GameObject("Day 02 keeper camera", typeof(Camera), typeof(AudioListener)); cameraGo.transform.SetParent(player.transform, false);
            cameraGo.transform.localPosition = Vector3.up * 1.65f; cameraGo.tag = "MainCamera";
            session.playerCamera = cameraGo.GetComponent<Camera>(); session.playerCamera.fieldOfView = 65; session.playerCamera.nearClipPlane = .06f; session.playerCamera.farClipPlane = 1500;
            session.player = player.AddComponent<DesktopPlayerController>();
            var stand = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Banana Market Stand");
            session.supply = stand;
            var wood = Material("Care stand wood", new Color(.46f, .29f, .12f));
            session.monkeys = new ZooDayTwoNpc[rules.monkeyCount];
            var troop = originalMonkeys.Concat(new[] { UnityEngine.Object.Instantiate(originalMonkeys[0]), UnityEngine.Object.Instantiate(originalMonkeys[2]) }).ToArray();
            var positions = new[] { new Vector3(-5.1f, 0, -16.1f), new Vector3(0, 0, -13.36f), new Vector3(5.8f, 0, -16.3f), new Vector3(-3.5f, 0, -20), new Vector3(3.8f, 0, -20) };
            for (int i = 0; i < troop.Length; i++)
            {
                var old = troop[i]; var model = old.gameObject; var animator = old.animator;
                model.name = "Day 02 monkey " + (i + 1); model.SetActive(true);
                var point = old.teleportPoint; if (point) point.gameObject.SetActive(false);
                UnityEngine.Object.DestroyImmediate(old);
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var ground = Ground(positions[i]);
                model.transform.position = ground + Vector3.up * .025f;
                model.transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, -20) - new Vector3(positions[i].x, 0, positions[i].z));
                animator.runtimeAnimatorController = controller;
                var driver = model.GetComponent<MonkeyNpcAnimator>(); if (!driver) driver = model.AddComponent<MonkeyNpcAnimator>(); driver.Configure(library); driver.enabled = true;
                var npc = model.AddComponent<ZooDayTwoNpc>(); npc.animationDriver = driver; session.monkeys[i] = npc;
                npc.roams = i == 0 || i >= 3; npc.walkSpeed = .45f + i * .025f; npc.roamRadius = 6;
                var colliderObject = new GameObject("Day 02 animal body", typeof(CapsuleCollider)); colliderObject.transform.SetParent(model.transform, false);
                var body = colliderObject.GetComponent<CapsuleCollider>(); body.center = Vector3.up * .75f; body.height = 1.4f; body.radius = .45f;
            }
            var toolStand = GameObject.CreatePrimitive(PrimitiveType.Cube); toolStand.name = "Day 02 reusable tool station"; toolStand.transform.SetParent(root.transform);
            toolStand.transform.position = Ground(new Vector3(-2.6f, 0, -22.6f)) + Vector3.up * .48f; toolStand.transform.localScale = new Vector3(.85f, .95f, .55f); toolStand.GetComponent<Renderer>().sharedMaterial = wood;
            var display = UnityEngine.Object.Instantiate(scratcher, root.transform); display.name = "Back scratcher on tool station";
            display.transform.position = toolStand.transform.position + Vector3.up * .65f; display.transform.localScale = Vector3.one * .45f; display.transform.rotation = Quaternion.Euler(0, 20, -25);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { light.color = new Color(1, .94f, .82f); light.intensity = Mathf.Clamp(light.intensity, .9f, 1.2f); }
            BuildNavigation(session);
            var assets = Resources.Load<ZooMenuAssets>("ZooPCMenu/MenuAssets");
            assets.dayTwoScene = Root + "Scenes/PC_DayTwoLakeside.unity";
            assets.dayTwoRules = rules;
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, assets.dayTwoScene);

            CaptureLocation(session);
            assets.dayTwoPhoto=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Generated/DayTwoLakeside.png");

            // Capture the new collection entry from the supplied 3D tool, then discard studio changes.
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.75f, .75f, .7f); RenderSettings.fog = false;
            ZooPCMenuPolishBuilder.Capture(scratcher, "CollectionBackScratcher", false);
            AssetDatabase.ImportAsset("Assets/ZooPCMenu/Art/CollectionBackScratcher.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/ZooPCMenu/Art/CollectionBackScratcher.png");
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            assets.facilities[1] = new ZooCollectionEntry { id = "back-scratcher", displayName = "Back scratcher", subtitle = "Reusable care tool", portrait = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ZooPCMenu/Art/CollectionBackScratcher.png"), discoveredDay = 2 };
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(assets.menuScene);
            Debug.Log("[Zoo Day 2] Dedicated PC scene, rules, tools, journal route and collection asset ready.");
        }
        public static void SetupBatch() { Setup(); EditorApplication.Exit(0); }
        public static void SetupAndPreview() { Setup(); ZooDayTwoValidation.Preview(); }
        public static void SetupAndValidate() { Setup(); ZooDayTwoValidation.Full(); }
        static void CaptureLocation(ZooDayTwoSession session)
        {
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MonkeyCare/Generated/Care_StandingIdle.anim");
            AnimationMode.StartAnimationMode();
            foreach(var npc in session.monkeys)
            {
                AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(npc.gameObject,idle,.65f);AnimationMode.EndSampling();
                foreach(var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh);var baked=new GameObject("Photo pose",typeof(MeshFilter),typeof(MeshRenderer));baked.transform.SetParent(skin.transform,false);
                    baked.GetComponent<MeshFilter>().sharedMesh=mesh;baked.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
                }
            }
            var go=new GameObject("Day Two location photograph",typeof(Camera));var camera=go.GetComponent<Camera>();
            camera.transform.position=new Vector3(0,3.6f,-25);camera.transform.LookAt(new Vector3(0,1,-15));camera.fieldOfView=64;camera.nearClipPlane=.1f;camera.farClipPlane=1500;
            var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Root+"Generated/DayTwoLakeside.png",pixels.EncodeToPNG());RenderTexture.active=previous;
            AnimationMode.StopAnimationMode();UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);AssetDatabase.ImportAsset(Root+"Generated/DayTwoLakeside.png");
        }
        static GameObject MakeToolPrefab(GameObject source, string name)
        {
            var wrapper = new GameObject(name); var model = UnityEngine.Object.Instantiate(source, wrapper.transform);
            model.SetActive(true); model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = source.transform.lossyScale;
            foreach (var b in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(b);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            var bounds = BoundsOf(wrapper); model.transform.position -= bounds.center;
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)); wrapper.transform.localScale = Vector3.one / Mathf.Max(.001f, size);
            if (name == "BackScratcher")
            {
                // Calibrate against the central metal claw, not the handle pivot.
                var vertices = wrapper.GetComponentsInChildren<MeshFilter>().SelectMany(m => m.sharedMesh.vertices.Select(v => m.transform.TransformPoint(v))).ToArray();
                var normalized = BoundsOf(wrapper);
                var head = vertices.Where(v => v.y > normalized.min.y + normalized.size.y * .8f && Mathf.Abs(v.x - normalized.center.x) < normalized.size.y * .035f).ToArray();
                float front = head.Min(v => v.z);
                var tips = head.Where(v => v.z <= front + .008f).ToArray();
                Vector3 tip = Vector3.zero; foreach (var v in tips) tip += v; tip /= tips.Length;
                var contact = new GameObject("Scratch contact tip"); contact.transform.SetParent(wrapper.transform); contact.transform.position = tip;
            }
            SetLayer(wrapper, 2);
            var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, Root + "Generated/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(wrapper); return prefab;
        }
        static RuntimeAnimatorController BuildController(MonkeyAnimationLibrary library)
        {
            string path = Root + "Generated/DayTwoMonkey.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var entry in library.Entries)
            {
                if (!entry.clip) continue;
                var state = machine.AddState(entry.action.ToString()); state.motion = entry.clip;
                if (entry.action == MonkeyAction.StandingIdle) machine.defaultState = state;
            }
            return controller;
        }
        static Material Material(string name, Color color)
        {
            string path = Root + "Generated/" + name.Replace(" ", "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Standard")); material.color = color; material.SetFloat("_Glossiness", .05f); AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        static Vector3 Ground(Vector3 at)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(new Vector3(at.x, 5, at.z), Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
            foreach (var hit in hits) if (hit.normal.y > .65f && hit.point.y < 2 && !hit.collider.GetComponentInParent<MonkeyCareNpc>() && !hit.collider.GetComponentInParent<ZooDayTwoNpc>()) return hit.point;
            return new Vector3(at.x, .08f, at.z);
        }
        static void SetLayer(GameObject go, int layer) { go.layer = layer; foreach (Transform child in go.transform) SetLayer(child.gameObject, layer); }
    }
}
