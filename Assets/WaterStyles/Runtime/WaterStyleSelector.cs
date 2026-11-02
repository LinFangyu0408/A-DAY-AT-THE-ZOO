using UnityEngine;

namespace ADayAtTheZoo.WaterStyles
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class WaterStyleSelector : MonoBehaviour
    {
        public enum Style
        {
            ClearSparklingLagoon = 0,
            ToonShallowPool = 1,
            TranslucentLowPolyBay = 2,
            TealUnderwaterTone = 3,
            BrightSparklingShallows = 4,
            MidnightLowPolySea = 5
        }

        [SerializeField] Style selectedStyle = Style.ClearSparklingLagoon;
        [SerializeField] Renderer waterRenderer;
        [SerializeField] Material[] waterMaterials = new Material[6];
        [SerializeField] bool applyInEditMode = true;
        [SerializeField] string preferredRendererObjectName = "samor_4077_600";
        [SerializeField] string generatedPreviewObjectName = "Water Surface - choose style on parent";

        public Style CurrentStyle
        {
            get => selectedStyle;
        }

        public Renderer WaterRenderer
        {
            get => waterRenderer;
            set => waterRenderer = value;
        }

        public Material[] WaterMaterials
        {
            get => waterMaterials;
            set => waterMaterials = value;
        }

        void OnEnable()
        {
            ResolveRenderer();
            ApplyCurrentStyle();
            EnsureCameraDepthTexture();
        }

        void OnValidate()
        {
            ResolveRenderer();
            if (applyInEditMode && !Application.isPlaying)
            {
                ApplyCurrentStyle();
            }
        }

        public void ApplyCurrentStyle()
        {
            ResolveRenderer();
            if (waterRenderer == null || waterMaterials == null)
            {
                return;
            }

            int index = Mathf.Clamp((int)selectedStyle, 0, waterMaterials.Length - 1);
            if (waterMaterials[index] != null)
            {
                waterRenderer.sharedMaterial = waterMaterials[index];
            }
        }

        public void ResolveRenderer()
        {
            Transform searchRoot = transform.root;
            foreach (Transform candidate in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != preferredRendererObjectName)
                {
                    continue;
                }

                Renderer preferredRenderer = candidate.GetComponent<Renderer>();
                if (preferredRenderer == null)
                {
                    preferredRenderer = candidate.GetComponentInChildren<Renderer>(true);
                }

                if (preferredRenderer != null)
                {
                    waterRenderer = preferredRenderer;
                    HideGeneratedPreview(searchRoot);
                    return;
                }
            }

            if (waterRenderer != null)
            {
                return;
            }

            waterRenderer = GetComponent<Renderer>();
            if (waterRenderer == null)
            {
                waterRenderer = GetComponentInChildren<Renderer>(true);
            }
            if (waterRenderer == null)
            {
                waterRenderer = GetComponentInParent<Renderer>();
            }
        }

        public void UsePreferredWaterTarget()
        {
            waterRenderer = null;
            ResolveRenderer();
            ApplyCurrentStyle();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        public void SelectStyle(Style style)
        {
            selectedStyle = style;
            ApplyCurrentStyle();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        public void NextStyle()
        {
            SelectStyle((Style)(((int)selectedStyle + 1) % waterMaterials.Length));
        }

        public static string GetDisplayName(Style style)
        {
            switch (style)
            {
                case Style.ClearSparklingLagoon: return "01 · 清透波光";
                case Style.ToonShallowPool: return "02 · 卡通浅池";
                case Style.TranslucentLowPolyBay: return "03 · 半透明低模海湾";
                case Style.TealUnderwaterTone: return "04 · 青绿低模水色";
                case Style.BrightSparklingShallows: return "05 · 强闪光浅海";
                case Style.MidnightLowPolySea: return "06 · 深色月光海面";
                default: return style.ToString();
            }
        }

        static void EnsureCameraDepthTexture()
        {
            foreach (Camera camera in Camera.allCameras)
            {
                camera.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        void HideGeneratedPreview(Transform searchRoot)
        {
            foreach (Transform candidate in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != generatedPreviewObjectName)
                {
                    continue;
                }

                Renderer previewRenderer = candidate.GetComponent<Renderer>();
                if (previewRenderer == null)
                {
                    previewRenderer = candidate.GetComponentInChildren<Renderer>(true);
                }

                if (previewRenderer != null && previewRenderer != waterRenderer)
                {
                    previewRenderer.enabled = false;
                }
            }
        }
    }
}
