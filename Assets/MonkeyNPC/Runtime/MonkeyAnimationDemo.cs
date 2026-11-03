using UnityEngine;

namespace ADayAtTheZoo.Monkeys
{
    public sealed class MonkeyAnimationDemo : MonoBehaviour
    {
        [SerializeField] private MonkeyNpcAnimator monkey;

        private void OnGUI()
        {
            if (monkey == null)
                return;
            GUILayout.BeginArea(new Rect(20, 20, 270, 600), GUI.skin.box);
            GUILayout.Label("Monkey Animation Library");
            GUILayout.Label("Current: " + monkey.CurrentAction);
            foreach (MonkeyAction action in System.Enum.GetValues(typeof(MonkeyAction)))
            {
                GUI.enabled = monkey.Library != null && monkey.Library.HasAction(action);
                if (GUILayout.Button(action.ToString(), GUILayout.Height(34)))
                    monkey.TryPlay(action);
            }
            GUI.enabled = monkey.Library != null && monkey.Library.HasAction(MonkeyAction.Eating)
                && monkey.Library.HasAction(MonkeyAction.Happy);
            if (GUILayout.Button("Feed: Eating > Happy > Idle", GUILayout.Height(38)))
                monkey.Feed();
            GUI.enabled = true;
            GUILayout.Label("Walk/run play in place. Movement is controlled by gameplay code.");
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        public void Configure(MonkeyNpcAnimator value) => monkey = value;
#endif
    }
}
