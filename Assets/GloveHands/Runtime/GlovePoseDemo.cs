using UnityEngine;

namespace ADayAtTheZoo.Hands
{
    public sealed class GlovePoseDemo : MonoBehaviour
    {
        public GlovePoseDriver[] hands;
        public GameObject[] gripProps;
        public bool autoCycle = true;
        public float secondsPerPose = 3f;
        private GlovePose current;
        private float elapsed;

        private void Start() => Show(GlovePose.Idle);
        private void Update()
        {
            if (!autoCycle) return;
            elapsed += Time.deltaTime;
            var next = (GlovePose)(Mathf.FloorToInt(elapsed / Mathf.Max(0.5f, secondsPerPose)) % 3);
            if (next != current) Show(next);
        }
        public void Show(GlovePose pose)
        {
            current = pose;
            foreach (var hand in hands) if (hand != null) hand.SetPose(pose);
            foreach (var prop in gripProps) if (prop != null) prop.SetActive(pose == GlovePose.Grip);
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 260, 180), GUI.skin.box);
            GUILayout.Label("QUATERNIUS GLOVE / POSE CHECK");
            foreach (GlovePose pose in System.Enum.GetValues(typeof(GlovePose)))
                if (GUILayout.Button(pose.ToString().ToLowerInvariant())) { autoCycle = false; Show(pose); }
            autoCycle = GUILayout.Toggle(autoCycle, "Auto cycle");
            GUILayout.EndArea();
        }
    }
}
