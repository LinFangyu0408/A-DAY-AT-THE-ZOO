using UnityEngine;
using UnityEngine.EventSystems;

namespace ADayAtTheZoo.PC
{
    public sealed partial class ZooDayTwoSession
    {
        ZooMonkeyAttackView attackView;
        float attackGraceUntil;
        public int AttacksStarted { get; private set; }
        public ZooMonkeyAttackView AttackView => attackView;

        void BeginAttack()
        {
            CancelScratch(); Target = null;
            UpdateViewModels(); UpdateTargetMarker();
            hud.gameObject.SetActive(false);
            player.inputLocked = true;
            EventSystem.current?.SetSelectedGameObject(null);
            attackView = gameObject.AddComponent<ZooMonkeyAttackView>();
            attackView.Begin(monkeys[Round.AttackIndex], playerCamera, interfaceRoot);
            AttacksStarted++;
        }
        void AdvanceAttack(float delta)
        {
            if (attackView && !attackView.Advance(delta)) return;
            StopAttackPresentation(); Round.EndAttack();
            attackGraceUntil = Time.time + 1.25f;
            inputArmedAt = Time.unscaledTime + .25f;
            player.inputLocked = !IsRunning;
            hud.gameObject.SetActive(true);
            UpdateInterface(); UpdateViewModels();
            Notify("BACK TO WORK  •  DON'T KEEP THEM WAITING!", 3);
        }
        void StopAttackPresentation()
        {
            if (!attackView) return;
            attackView.Cleanup(); Destroy(attackView); attackView = null;
        }
        void OnDisable() { StopAttackPresentation(); }
    }
}
