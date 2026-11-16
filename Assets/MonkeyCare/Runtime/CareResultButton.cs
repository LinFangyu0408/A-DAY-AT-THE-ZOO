using UnityEngine;
using UnityEngine.UI;
namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareResultButton : MonoBehaviour
    {
        public bool restart;
        public bool available;
        public Image image;
        public void Activate()
        {
            if (!available || !MonkeyCareSession.Instance) return;
            if (restart) MonkeyCareSession.Instance.RestartRound(); else MonkeyCareSession.Instance.ContinueExploring();
        }
    }
}
