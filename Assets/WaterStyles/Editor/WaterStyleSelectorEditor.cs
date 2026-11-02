using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ADayAtTheZoo.WaterStyles.Editor
{
    [CustomEditor(typeof(WaterStyleSelector))]
    public sealed class WaterStyleSelectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var selector = (WaterStyleSelector)target;

            EditorGUILayout.HelpBox(
                "在这里选 Water Style。选择后会立即替换这个场景水面的材质。\n" +
                "Water Renderer 留空时，脚本会自动连接同一物体、子物体或父物体上的 Renderer。\n" +
                "也可以在 Assets/WaterStyles/Materials 中把任意 .mat 直接拖给 Water Renderer。",
                MessageType.Info);

            if (selector.WaterRenderer == null)
            {
                EditorGUILayout.HelpBox(
                    "目前没有找到 Renderer。请确认这个物体或它的子物体带有 MeshRenderer。",
                    MessageType.Warning);
            }

            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("一键切换 / Quick Style Switch", EditorStyles.boldLabel);

            if (GUILayout.Button("迁移脚本并应用到 samor_4077_600", GUILayout.Height(30f)))
            {
                MoveSelectorToPreferredWater(selector);
                SceneView.RepaintAll();
            }

            for (int i = 0; i < 6; i++)
            {
                var style = (WaterStyleSelector.Style)i;
                string label = WaterStyleSelector.GetDisplayName(style);
                bool selected = selector.CurrentStyle == style;

                if (GUILayout.Button(selected ? "✓ " + label : label, GUILayout.Height(28f)))
                {
                    Undo.RecordObject(selector, "Change Water Style");
                    selector.SelectStyle(style);
                    EditorUtility.SetDirty(selector);
                    SceneView.RepaintAll();
                }
            }
        }

        internal static bool MoveSelectorToPreferredWater(WaterStyleSelector sourceSelector)
        {
            Transform target = null;
            foreach (Transform candidate in sourceSelector.transform.root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "samor_4077_600")
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
            {
                EditorUtility.DisplayDialog(
                    "没有找到水体",
                    "没有在当前层级中找到名为 samor_4077_600 的物体。",
                    "确定");
                return false;
            }

            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                targetRenderer = target.GetComponentInChildren<Renderer>(true);
            }

            if (targetRenderer == null)
            {
                EditorUtility.DisplayDialog(
                    "水面上没有 Renderer",
                    "samor_4077_600 及其子物体上没有 MeshRenderer 或 SkinnedMeshRenderer。",
                    "确定");
                return false;
            }

            WaterStyleSelector targetSelector = target.GetComponent<WaterStyleSelector>();
            if (targetSelector == null)
            {
                targetSelector = Undo.AddComponent<WaterStyleSelector>(target.gameObject);
            }

            Undo.RecordObject(targetSelector, "Move Water Style Selector");
            targetSelector.WaterRenderer = targetRenderer;
            targetSelector.WaterMaterials = sourceSelector.WaterMaterials;
            targetSelector.SelectStyle(sourceSelector.CurrentStyle);
            EditorUtility.SetDirty(targetSelector);

            foreach (Transform candidate in sourceSelector.transform.root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "Water Surface - choose style on parent")
                {
                    continue;
                }

                Renderer previewRenderer = candidate.GetComponent<Renderer>();
                if (previewRenderer == null)
                {
                    previewRenderer = candidate.GetComponentInChildren<Renderer>(true);
                }
                if (previewRenderer != null)
                {
                    Undo.RecordObject(previewRenderer, "Disable Duplicate Water Preview");
                    previewRenderer.enabled = false;
                }
            }

            if (sourceSelector.gameObject != target.gameObject)
            {
                Undo.DestroyObjectImmediate(sourceSelector);
            }

            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Selection.activeGameObject = target.gameObject;
            return true;
        }
    }
}
