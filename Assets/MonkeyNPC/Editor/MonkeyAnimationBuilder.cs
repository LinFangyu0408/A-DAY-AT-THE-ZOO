using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADayAtTheZoo.Monkeys;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ADayAtTheZoo.Monkeys.Editor
{
    public static class MonkeyAnimationBuilder
    {
        public const string Output = "Assets/MonkeyNPC/Generated";
        public const string LibraryPath = Output + "/MonkeyAnimationLibrary.asset";
        public const string ControllerPath = Output + "/MonkeyNPC.controller";
        public const string PrefabPath = Output + "/MonkeyNPC.prefab";
        private const string VendorAnimations = "Assets/Suriyun/Animations/Pspsps Animations/Character Animations/";
        private const string VendorPrefab = "Assets/Suriyun/Pspsps/Prefab/Characters/Monkey/Chibi_Monkey_01.prefab";
        private const string MixamoInput = "Assets/MonkeyNPC/Animations/Mixamo/";

        private static readonly Dictionary<MonkeyAction, string> Sources = new Dictionary<MonkeyAction, string>
        {
            { MonkeyAction.StandingIdle, VendorAnimations + "Anim_Chibi@IdleA.fbx" },
            { MonkeyAction.SittingIdle, VendorAnimations + "Anim_Chibi@Sit.fbx" },
            { MonkeyAction.Hungry, MixamoInput + "Hungry.fbx" },
            { MonkeyAction.Eating, MixamoInput + "Eating.fbx" },
            { MonkeyAction.Happy, VendorAnimations + "Anim_Chibi@Victory.fbx" },
            { MonkeyAction.Walking, VendorAnimations + "Anim_Chibi@Walk.fbx" },
            { MonkeyAction.Running, VendorAnimations + "Anim_Chibi@Run.fbx" },
            { MonkeyAction.Scratching, MixamoInput + "Scratching.fbx" },
            { MonkeyAction.Hi, VendorAnimations + "Anim_Chibi@Hi.fbx" }
        };

        [MenuItem("Tools/Monkey NPC/Build Complete Library")]
        public static void BuildComplete()
        {
            Build(true);
        }

        [MenuItem("Tools/Monkey NPC/Preview Available Animations")]
        public static void BuildPreview()
        {
            Build(false);
        }

        private static bool Loops(MonkeyAction action)
        {
            return action == MonkeyAction.StandingIdle || action == MonkeyAction.Hungry ||
                action == MonkeyAction.Walking || action == MonkeyAction.Running;
        }

        public static void Build(bool requireComplete)
        {
            // Check inputs before changing generated assets. A missing action is never substituted.
            string[] missing = Sources.Values.Where(p => !File.Exists(p)).ToArray();
            if (requireComplete && missing.Length > 0)
                throw new InvalidOperationException("Missing animation FBX files:\n" + string.Join("\n", missing));

            foreach (string path in Sources.Values.Where(p => p.StartsWith(MixamoInput) && File.Exists(p)))
                ConfigureMixamoImporter(path);

            var sources = new Dictionary<MonkeyAction, AnimationClip>();
            foreach (var pair in Sources)
            {
                if (!File.Exists(pair.Value))
                    continue;
                AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(pair.Value)
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (source == null || !source.isHumanMotion)
                    throw new InvalidOperationException(pair.Value + " has no valid Humanoid animation clip.");
                sources.Add(pair.Key, source);
            }

            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var entries = new List<MonkeyAnimationLibrary.Entry>();
            foreach (var pair in sources)
            {
                AnimationClip copy = Object.Instantiate(pair.Value);
                copy.name = pair.Key.ToString();
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = Loops(pair.Key);
                AnimationUtility.SetAnimationClipSettings(copy, settings);
                string path = Output + "/" + pair.Key + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    AssetDatabase.CreateAsset(copy, path);
                    clip = copy;
                }
                else
                {
                    EditorUtility.CopySerialized(copy, clip);
                    Object.DestroyImmediate(copy);
                    EditorUtility.SetDirty(clip);
                }
                entries.Add(new MonkeyAnimationLibrary.Entry { action = pair.Key, clip = clip, source = Sources[pair.Key] });
            }

            MonkeyAnimationLibrary library = AssetDatabase.LoadAssetAtPath<MonkeyAnimationLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MonkeyAnimationLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(library);
            AnimatorController controller = BuildController(entries);
            BuildPrefab(library, controller);
            AssetDatabase.SaveAssets();
            Validate(library, controller, requireComplete);
            string unavailable = string.Join(", ", library.MissingActions());
            Debug.Log(unavailable.Length == 0 ? "[Monkey NPC] Complete library built and validated."
                : "[Monkey NPC] Preview built. Still missing: " + unavailable);
        }

        private static void ConfigureMixamoImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not import " + path);
            if (importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel || !importer.importAnimation)
            {
                // Mixamo skeletons create their own Avatar. The monkey Avatar must not be
                // copied onto a skeleton with different bone names.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("Invalid Mixamo Humanoid Avatar: " + path);
        }

        private static AnimatorController BuildController(List<MonkeyAnimationLibrary.Entry> entries)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            var states = new Dictionary<MonkeyAction, AnimatorState>();
            foreach (var child in machine.states)
            {
                if (!entries.Any(e => e.action.ToString() == child.state.name))
                    machine.RemoveState(child.state);
            }
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
                machine.RemoveAnyStateTransition(transition);
            foreach (var entry in entries)
            {
                AnimatorState state = machine.states.Select(s => s.state)
                    .FirstOrDefault(s => s.name == entry.action.ToString());
                if (state == null)
                    state = machine.AddState(entry.action.ToString(), new Vector3(300, (int)entry.action * 75, 0));
                foreach (AnimatorStateTransition transition in state.transitions)
                    state.RemoveTransition(transition);
                state.motion = entry.clip;
                state.speed = 1f;
                state.writeDefaultValues = false;
                states.Add(entry.action, state);
            }
            machine.defaultState = states[MonkeyAction.StandingIdle];
            AddExit(states, MonkeyAction.Eating, MonkeyAction.Happy);
            AddExit(states, MonkeyAction.Happy, MonkeyAction.StandingIdle);
            AddExit(states, MonkeyAction.Scratching, MonkeyAction.StandingIdle);
            AddExit(states, MonkeyAction.Hi, MonkeyAction.StandingIdle);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(machine);
            return controller;
        }

        private static void AddExit(Dictionary<MonkeyAction, AnimatorState> states,
            MonkeyAction from, MonkeyAction to)
        {
            if (!states.ContainsKey(from) || !states.ContainsKey(to))
                return;
            AnimatorStateTransition transition = states[from].AddTransition(states[to]);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = 0.15f;
            transition.canTransitionToSelf = false;
        }

        private static void BuildPrefab(MonkeyAnimationLibrary library, AnimatorController controller)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(VendorPrefab);
            if (source == null)
                throw new InvalidOperationException("Monkey source prefab not found: " + VendorPrefab);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = "MonkeyNPC";
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Monkey prefab needs a valid Humanoid Avatar.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                MonkeyNpcAnimator driver = instance.AddComponent<MonkeyNpcAnimator>();
                driver.Configure(library);
                ConfigureFace(instance.AddComponent<MonkeyFace>());
                instance.AddComponent<MonkeyHandContact>();
                if (instance.GetComponent<Collider>() == null)
                {
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        foreach (Renderer renderer in renderers.Skip(1))
                            bounds.Encapsulate(renderer.bounds);
                        Vector3 scale = instance.transform.lossyScale;
                        var collider = instance.AddComponent<BoxCollider>();
                        collider.center = instance.transform.InverseTransformPoint(bounds.center);
                        collider.size = new Vector3(bounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                            bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                            bounds.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static void Validate(MonkeyAnimationLibrary library, AnimatorController controller, bool requireComplete)
        {
            if (requireComplete && library.MissingActions().Any())
                throw new InvalidOperationException("Animation library is incomplete.");
            foreach (var entry in library.Entries)
            {
                if (!entry.clip.isHumanMotion || entry.clip.length <= 0f)
                    throw new InvalidOperationException("Invalid animation clip: " + entry.action);
                var state = controller.layers[0].stateMachine.states.Select(s => s.state)
                    .Single(s => s.name == entry.action.ToString());
                if (state.motion != entry.clip)
                    throw new InvalidOperationException("Incorrect state clip: " + entry.action);
                if (AnimationUtility.GetAnimationClipSettings(entry.clip).loopTime != Loops(entry.action))
                    throw new InvalidOperationException("Incorrect loop setting: " + entry.action);
            }
        }

        private static void ConfigureFace(MonkeyFace face)
        {
            const string directory = "Assets/Suriyun/Pspsps/Materials/Emotions/";
            Material Load(int id) => AssetDatabase.LoadAssetAtPath<Material>(directory + $"M_Chibi_Emo_{id:00}.mat");
            face.Configure(Load(1), Load(5), Load(20), Load(2), Load(3), Load(14), Load(22), Load(8));
            EditorUtility.SetDirty(face);
        }

        [MenuItem("Tools/Monkey NPC/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build(false);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject monkey = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            monkey.transform.position = Vector3.zero;
            var preview = new GameObject("Animation Controls").AddComponent<MonkeyAnimationDemo>();
            preview.Configure(monkey.GetComponent<MonkeyNpcAnimator>());
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Preview Ground";
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(2f, 1.2f, 2.3f);
            camera.transform.LookAt(new Vector3(0, 0.5f, 0));
            EditorSceneManager.SaveScene(scene, Output + "/MonkeyAnimationDemo.unity");
        }

        [MenuItem("Tools/Monkey NPC/Apply Library to Selected Monkeys")]
        public static void ApplyToSelected()
        {
            Build(true);
            var library = AssetDatabase.LoadAssetAtPath<MonkeyAnimationLibrary>(LibraryPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            foreach (Animator animator in Selection.gameObjects
                .SelectMany(o => o.GetComponentsInChildren<Animator>(true)).Distinct())
            {
                if (animator.avatar == null || !AssetDatabase.GetAssetPath(animator.avatar).EndsWith("Chibi_Monkey.fbx"))
                    continue;
                Undo.RecordObject(animator, "Apply Monkey Animation Library");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var driver = animator.GetComponent<MonkeyNpcAnimator>();
                if (driver == null)
                    driver = Undo.AddComponent<MonkeyNpcAnimator>(animator.gameObject);
                Undo.RecordObject(driver, "Configure Monkey Animation Library");
                driver.Configure(library);
                var face = animator.GetComponent<MonkeyFace>();
                if (face == null)
                    face = Undo.AddComponent<MonkeyFace>(animator.gameObject);
                Undo.RecordObject(face, "Configure Monkey Expressions");
                ConfigureFace(face);
                PrefabUtility.RecordPrefabInstancePropertyModifications(face);
                if (animator.GetComponent<MonkeyHandContact>() == null)
                    Undo.AddComponent<MonkeyHandContact>(animator.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
                EditorUtility.SetDirty(driver);
                EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
            }
        }
    }
}
