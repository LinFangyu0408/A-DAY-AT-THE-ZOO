using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ADayAtTheZoo.Monkeys.Care;

[InitializeOnLoad]
public static class CareProgressStarCheck
{
    const string Work = "work/progress-star/";
    static CareProgressStarCheck() => EditorApplication.update += Tick;
    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || !File.Exists(Work + "check.request")) return;
        File.Delete(Work + "check.request");
        try { Run(); }
        catch (Exception e) { File.WriteAllText(Work + "result.txt", "FAIL " + e); Debug.LogException(e); }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Patch(CareUiCustomization config)
    {
        var source = config.elements.First(e => e.group == "Score" && e.source.name == "Score star badge").source;
        var fill = config.elements.First(e => e.group == "Score" && e.source.name == "Progress fill").source.GetComponent<Image>();
        var follower = source.GetComponent<CareProgressStarFollower>();
        if (!follower) follower = Undo.AddComponent<CareProgressStarFollower>(source.gameObject);
        follower.progressFill = fill;
        PrefabUtility.RecordPrefabInstancePropertyModifications(follower);
        EditorUtility.SetDirty(follower);
    }
    static void Run()
    {
        var root = new GameObject("Progress marker verification", typeof(RectTransform));
        int checks = 0;
        try
        {
            root.transform.localScale = new Vector3(1.4f, .8f, 1f);
            root.transform.localRotation = Quaternion.Euler(0, 0, 17);
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = fillObject.GetComponent<Image>(); fill.type = Image.Type.Filled;
            fill.transform.SetParent(root.transform, false);
            fill.rectTransform.sizeDelta = new Vector2(872, 80);
            fill.rectTransform.anchoredPosition = new Vector2(21, -31);
            var marker = new GameObject("Star", typeof(RectTransform), typeof(CareProgressStarFollower));
            marker.transform.SetParent(root.transform, false);
            var follower = marker.GetComponent<CareProgressStarFollower>(); follower.progressFill = fill;
            foreach (float ratio in new[] { 0f, .22f, .6f, 1f })
            {
                fill.fillAmount = ratio;
                marker.transform.localPosition = new Vector3(-400, 0, 0); // Simulate an editable layout pinning it.
                typeof(CareProgressStarFollower).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(follower, null);
                var actual = fill.rectTransform.InverseTransformPoint(marker.transform.position);
                var rect = fill.rectTransform.rect;
                Check(Mathf.Abs(actual.x - Mathf.Lerp(rect.xMin, rect.xMax, ratio)) < .001f && Mathf.Abs(actual.y - rect.center.y) < .001f,
                    "Star follows the fill after layout at " + ratio);
                checks++;
            }
            fill.rectTransform.sizeDelta = new Vector2(1110, 110);
            fill.rectTransform.anchoredPosition = new Vector2(-80, 45);
            follower.offset = new Vector2(3, 7); fill.fillAmount = .5f; follower.Follow();
            var changed = fill.rectTransform.InverseTransformPoint(marker.transform.position);
            Check(Vector2.Distance(changed, new Vector2(3, 7)) < .001f, "Resized/repositioned fill and inspector offset"); checks++;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var config = UnityEngine.Object.FindFirstObjectByType<CareUiCustomization>();
        Check(config, "Editable UI exists in the open game scene");
        if (!File.Exists(Work + "scene-before.unity")) File.Copy(config.gameObject.scene.path, Work + "scene-before.unity");
        string prefabPath = "Assets/MonkeyCare/Generated/EditableUI/ZooEditableUI.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try { Patch(prefab.GetComponent<CareUiCustomization>()); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        config = UnityEngine.Object.FindFirstObjectByType<CareUiCustomization>();
        Patch(config);
        EditorSceneManager.MarkSceneDirty(config.gameObject.scene);
        EditorSceneManager.SaveScene(config.gameObject.scene);
        Selection.activeGameObject = config.elements.First(e => e.group == "Score" && e.source.name == "Score star badge").source.gameObject;
        File.WriteAllText(Work + "result.txt", "PASS " + checks + " checks: 0/22/60/100 percent, layout overwrite, transformed/resized/repositioned bar and offset. Scene and prefab saved.\n" + DateTime.Now.ToString("s"));
    }
}
