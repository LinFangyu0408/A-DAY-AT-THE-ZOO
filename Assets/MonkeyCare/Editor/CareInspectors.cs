using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;

[CustomPropertyDrawer(typeof(CareAnimation))]
public sealed class CareAnimationDrawer : PropertyDrawer
{
    static List<string> Fields(SerializedProperty p)
    {
        var list = new List<string> { "action", "clip", "speed", "finish" };
        int mode = p.FindPropertyRelative("finish").enumValueIndex;
        if (mode == 1) list.Add("seconds"); if (mode == 2) list.Add("repeats"); return list;
    }
    public override float GetPropertyHeight(SerializedProperty p, GUIContent label) => (p.isExpanded ? Fields(p).Count + 1 : 1) * 22;
    public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
    {
        r.height = 20; p.isExpanded = EditorGUI.Foldout(r, p.isExpanded, label, true);
        if (!p.isExpanded) return; EditorGUI.indentLevel++;
        foreach (var name in Fields(p)) { r.y += 22; EditorGUI.PropertyField(r, p.FindPropertyRelative(name)); }
        EditorGUI.indentLevel--;
    }
}

[CustomPropertyDrawer(typeof(CareEffect))]
public sealed class CareEffectDrawer : PropertyDrawer
{
    static List<string> Fields(SerializedProperty p)
    {
        var list = new List<string> { "prefab", "anchor", "position", "rotation", "scale", "tint" };
        int tint = p.FindPropertyRelative("tint").enumValueIndex;
        if (tint == 1) list.Add("color"); if (tint == 2) list.Add("gradient"); list.Add("duration"); return list;
    }
    static bool Unsupported(SerializedProperty p)
    {
        if (p.FindPropertyRelative("tint").enumValueIndex == 0) return false;
        var prefab = p.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
        if (!prefab) return false;
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            if (!(renderer is ParticleSystemRenderer))
                foreach (var m in renderer.sharedMaterials)
                    if (m && !m.HasProperty("_Color") && !m.HasProperty("_BaseColor") && !m.HasProperty("_TintColor")) return true;
        return false;
    }
    public override float GetPropertyHeight(SerializedProperty p, GUIContent label) =>
        p.isExpanded ? (Fields(p).Count + 1) * 22 + (Unsupported(p) ? 44 : 0) : 22;
    public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
    {
        r.height = 20; p.isExpanded = EditorGUI.Foldout(r, p.isExpanded, label, true);
        if (!p.isExpanded) return; EditorGUI.indentLevel++;
        foreach (var name in Fields(p)) { r.y += 22; EditorGUI.PropertyField(r, p.FindPropertyRelative(name)); }
        if (Unsupported(p)) { r.y += 22; r.height = 40; EditorGUI.HelpBox(r, "部分网格Shader没有可覆盖颜色属性，将保留原色。", MessageType.Warning); }
        EditorGUI.indentLevel--;
    }
}

[CustomEditor(typeof(MonkeyCareNpc)), CanEditMultipleObjects]
public sealed class MonkeyCareNpcEditor : Editor
{
    readonly Dictionary<string, bool> expanded = new();
    void Group(string title, string[] fields, string[] labels = null)
    {
        expanded.TryGetValue(title, out bool open);
        open = EditorGUILayout.Foldout(open, title, true); expanded[title] = open;
        if (!open) return;
        EditorGUI.indentLevel++;
        for (int i = 0; i < fields.Length; i++)
        {
            var p = serializedObject.FindProperty(fields[i]);
            if (labels == null) EditorGUILayout.PropertyField(p, true);
            else EditorGUILayout.PropertyField(p, new GUIContent(labels[i]), true);
        }
        EditorGUI.indentLevel--;
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("每只NPC独立配置。满意度按正常/超时分段；动画时长不会绕过照顾要求。", MessageType.Info);
        Group("需求时间", new[] { "firstNeed", "firstDelay", "hungerInterval", "itchInterval", "recoveryWait", "patienceSeconds" });
        Group("喂食与挠痒", new[] { "feedingSeconds", "lateFeedingSeconds", "scratchCount", "lateScratchCount", "strokeDistance", "resetDistance" });
        Group("动画选择与播放", new[] { "wave", "hungry", "eating", "scratching", "victory", "cute" }, new[] { "Wave 等待", "Hungry 饥饿", "Eating 进食", "Scratch 挠痒", "Victory 喂食成功", "Cute1 挠痒成功" });
        Group("特效", new[] { "hungerEffect", "itchEffect", "smokeEffect" }, new[] { "饥饿特效", "发痒特效", "首次进食烟雾" });
        Group("UI和表情", new[] { "ui" }, new[] { "图片与位置" });
        Group("场景引用", new[] { "animator", "library", "teleportPoint" }, new[] { "Animator", "动作库", "传送点" });
        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying && target is MonkeyCareNpc npc)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("当前动作", npc.Phase.ToString());
            if (npc.Progress != null)
            {
                EditorGUILayout.LabelField("需求 / 超时", npc.Need + " / " + npc.Progress.Late);
                EditorGUILayout.LabelField("喂食秒数", npc.Progress.Fed.ToString("F2") + " / " + npc.Progress.FeedTarget);
                EditorGUILayout.LabelField("挠痒次数", npc.Progress.Strokes + " / " + npc.Progress.StrokeTarget);
            }
            else EditorGUILayout.LabelField("下次需求等待", npc.NextIn.ToString("F1") + "秒");
            Repaint();
        }
    }
}
