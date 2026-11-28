using UnityEngine;

namespace ADayAtTheZoo.PC
{
    public sealed partial class ZooDayTwoSession
    {
        LineRenderer targetMarker;
        Material markerMaterial;
        public Transform TargetMarker => targetMarker ? targetMarker.transform : null;
        void BuildTargetMarker()
        {
            var go = new GameObject("Selected monkey marker"); go.layer = 2; go.transform.SetParent(transform);
            targetMarker = go.AddComponent<LineRenderer>(); targetMarker.useWorldSpace = false; targetMarker.loop = true;
            targetMarker.widthMultiplier = .028f; targetMarker.positionCount = 48;
            markerMaterial = new Material(Shader.Find("Sprites/Default")); targetMarker.sharedMaterial = markerMaterial;
            targetMarker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; targetMarker.receiveShadows = false;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2 / 48;
                targetMarker.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .62f);
            }
            go.SetActive(false);
        }
        void UpdateTargetMarker()
        {
            if (!targetMarker) return;
            bool visible = IsRunning && Target;
            targetMarker.gameObject.SetActive(visible);
            if (!visible) return;
            targetMarker.transform.position = Target.transform.position + Vector3.up * .03f;
            targetMarker.transform.rotation = Quaternion.identity;
            Color color = SelectedTool == 1 ? new Color(.48f, .92f, 1, .9f) : new Color(1, .86f, .4f, .9f);
            targetMarker.startColor = targetMarker.endColor = color;
        }
        void OnDestroy() { StopAttackPresentation(); if (markerMaterial) Destroy(markerMaterial); }
    }
}
