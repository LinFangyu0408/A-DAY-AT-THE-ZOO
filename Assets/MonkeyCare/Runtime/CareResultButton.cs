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
            // CONTINUE leaves the round and goes back to the home/standby
            // screen; RESTART immediately deals a fresh round.
            if (restart) MonkeyCareSession.Instance.RestartRound();
            else MonkeyCareSession.Instance.ReturnHome();
        }
    }
}
