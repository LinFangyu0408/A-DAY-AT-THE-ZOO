using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ADayAtTheZoo.Monkeys.Care;

[InitializeOnLoad]
public static class CareUiAuthoringBuilder
{
    const string Folder = "Assets/MonkeyCare/Generated/EditableUI";
    const string Work = "work/ui-authoring/";
    static readonly Dictionary<int, Sprite> savedSprites = new();
    static CareUiAuthoringBuilder() => EditorApplication.update += Tick;
    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || !File.Exists(Work + "build.request")) return;
        File.Delete(Work + "build.request");
        try { Build(); }
        catch (Exception e) { File.WriteAllText(Work + "build-result.txt", "FAIL " + e); Debug.LogException(e); }
    }
    static T Field<T>(object value, string name) => (T)value.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    [MenuItem("Tools/猴子照顾/创建可编辑 UI")]
    public static void Build()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<CareUiCustomization>();
        if (existing) { TestOverrides(existing); Selection.activeGameObject = existing.gameObject; EditorSceneManager.SaveScene(existing.gameObject.scene); File.WriteAllText(Work + "build-result.txt", "PASS: saved scene and prefab; " + existing.elements.Count + " editable elements; text/color/position/size overrides validated; live counts preserved."); return; }
        var session = UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>();
        if (!session) throw new Exception("Open the playable zoo scene first.");
        var scene = session.gameObject.scene;
        Directory.CreateDirectory(Work);
        if (!string.IsNullOrEmpty(scene.path) && !File.Exists(Work + "scene-before.unity"))
            File.Copy(scene.path, Work + "scene-before.unity");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var root = new GameObject("UI_Edit 可编辑游戏界面");
        var config = root.AddComponent<CareUiCustomization>();
        var templates = new GameObject("Editable UI (编辑后保存场景)");
        templates.transform.SetParent(root.transform, false);
        config.editableUi = templates.transform;
        var before = scene.GetRootGameObjects().ToHashSet();
        var head = new GameObject("Temporary UI preview anchor");
        var view = new CareRoundView(head.transform, session.roundArtwork);
        var bag = new CareGloveInventory(session, head.transform);
        var tutorial = head.AddComponent<CareTutorial>();
        tutorial.Begin(session, head.transform, 0);
        var bagRoot = Field<RectTransform>(bag, "root");
        UnityEngine.Object.DestroyImmediate(bagRoot.GetComponent<Canvas>());
        var preview = CareUi.OverlayRoot("Care Backpack Preview", 70);
        bagRoot.SetParent(preview, false);
        bagRoot.localScale = Vector3.one;
        bagRoot.anchoredPosition = Vector2.zero;
        var tutorialRoot = Field<RectTransform>(tutorial, "root");
        typeof(CareTutorial).GetField("root", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tutorial, null);
        UnityEngine.Object.DestroyImmediate(tutorial);
        var groups = new (string name, RectTransform rect)[] {
            ("Score", Field<RectTransform>(view, "scoreHud")),
            ("Banana counter", Field<RectTransform>(view, "bananaHud")),
            ("Results", Field<RectTransform>(view, "results")),
            ("Backpack", bagRoot), ("Bag entry", bag.BagButtonRoot), ("Tutorial", tutorialRoot) };
        foreach (var go in scene.GetRootGameObjects())
            if (!before.Contains(go) && go != head && go.GetComponent<Canvas>()) go.transform.SetParent(templates.transform, false);
        // Native sprite assets allow the saved preview/prefab to survive an editor restart.
        savedSprites.Clear();
        foreach (var image in templates.GetComponentsInChildren<Image>(true))
            if (image.sprite) image.sprite = Persist(image.sprite);
        foreach (var group in groups)
        {
            group.rect.gameObject.SetActive(true);
            config.Capture(group.name, group.rect);
        }
        foreach (var canvasGroup in templates.GetComponentsInChildren<CanvasGroup>(true)) canvasGroup.alpha = 1;
        Field<RectTransform>(view, "results").gameObject.SetActive(false);
        bagRoot.gameObject.SetActive(false);
        UnityEngine.Object.DestroyImmediate(head);
        // These templates are separate from live runtime objects and are hidden automatically in Play.
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ZooEditableUI.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Create editable zoo UI");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;
        var saved = instance.GetComponent<CareUiCustomization>();
        Check(saved.elements.Count > 60, "All editable UI elements are serialized");
        Check(saved.elements.Any(e => e.group == "Backpack" && e.source.name == "Backpack title" && e.source.GetComponent<Text>()), "BAG is editable Text");
        Check(saved.elements.Any(e => e.group == "Score" && e.source.name == "Progress title" && e.source.GetComponent<Text>()), "SCORE is editable Text");
        Check(saved.elements.Count(e => e.source.name.StartsWith("Backpack slot ")) == 15, "15 independent slots");
        TestOverrides(saved);
        File.WriteAllText(Work + "build-result.txt", "PASS: saved editable scene and prefab; " + saved.elements.Count +
            " elements; persistent sprites; editable captions; style overrides; live counts preserved.\n" + DateTime.Now.ToString("s"));
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void TestOverrides(CareUiCustomization config)
    {
        var entry = config.elements.First(e => e.source.name == "Backpack title");
        var clone = UnityEngine.Object.Instantiate(entry.source.gameObject);
        var sourceText = entry.source.GetComponent<Text>();
        var originalText = sourceText.text; var originalColor = sourceText.color;
        var originalPosition = entry.source.anchoredPosition; var originalSize = entry.source.sizeDelta;
        try
        {
            var type = typeof(CareUiCustomization).GetNestedType("Binding", BindingFlags.NonPublic);
            var binding = Activator.CreateInstance(type, true);
            var target = clone.GetComponent<RectTransform>();
            type.GetField("element").SetValue(binding, entry);
            type.GetField("target").SetValue(binding, target);
            type.GetField("initial").SetValue(binding, CareUiCustomization.State.Read(target));
            sourceText.text = "MY BAG"; sourceText.color = Color.cyan;
            entry.source.anchoredPosition += new Vector2(12, 8);
            entry.source.sizeDelta += new Vector2(30, 10);
            typeof(CareUiCustomization).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { binding });
            Check(clone.GetComponent<Text>().text == "MY BAG" && clone.GetComponent<Text>().color == Color.cyan, "Text and color edits apply");
            Check(target.sizeDelta == entry.source.sizeDelta && target.anchoredPosition == entry.source.anchoredPosition, "Position and size edits apply");
            Check(config.elements.First(e => e.source.name == "Banana count").liveValue, "Banana count stays driven by gameplay");
        }
        finally
        {
            sourceText.text = originalText; sourceText.color = originalColor;
            entry.source.anchoredPosition = originalPosition; entry.source.sizeDelta = originalSize;
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }
    static Sprite Persist(Sprite sprite)
    {
        if (EditorUtility.IsPersistent(sprite)) return sprite;
        if (savedSprites.TryGetValue(sprite.GetInstanceID(), out var existing)) return existing;
        var texture = sprite.texture;
        string name = string.Concat(sprite.name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        if (!EditorUtility.IsPersistent(texture))
        {
            string texturePath = Folder + "/" + name + ".png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(texturePath);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }
        string path = Folder + "/" + name + "_" + savedSprites.Count + ".asset";
        var copy = Sprite.Create(texture, sprite.rect, new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height),
            sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
        copy.name = sprite.name;
        AssetDatabase.CreateAsset(copy, path);
        savedSprites[sprite.GetInstanceID()] = copy;
        return copy;
    }
}



[CustomEditor(typeof(CareUiCustomization))]
public sealed class CareUiCustomizationInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("停止 Play 后，展开 Editable UI，编辑子对象的 Text、Image、RectTransform，并保存场景。数量/时间仍由游戏更新。教程文案按步骤设置，{A}/{B} 会跟随手柄切换。", MessageType.Info);
        if (Application.isPlaying) return;
        var config = (CareUiCustomization)target;
        foreach (var group in new[] { "Backpack", "Results" })
        {
            var entry = config.elements.FirstOrDefault(e => e.group == group && e.key == ".");
            if (entry == null || !entry.source) continue;
            if (GUILayout.Button(group == "Backpack" ? "显示 / 隐藏背包预览" : "显示 / 隐藏结算预览"))
            {
                Undo.RecordObject(entry.source.gameObject, "Toggle UI preview");
                entry.source.gameObject.SetActive(!entry.source.gameObject.activeSelf);
                EditorSceneManager.MarkSceneDirty(config.gameObject.scene);
            }
        }
        if (GUILayout.Button("选中背包标题 Text"))
            Selection.activeGameObject = config.elements.First(e => e.source.name == "Backpack title").source.gameObject;
    }
}
