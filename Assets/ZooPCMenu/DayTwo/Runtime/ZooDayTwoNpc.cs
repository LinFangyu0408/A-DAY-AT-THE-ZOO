using UnityEngine;
using UnityEngine.UI;
using ADayAtTheZoo.Monkeys;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC
{
    public sealed class ZooDayTwoNpc : MonoBehaviour
    {
        public MonkeyNpcAnimator animationDriver;
        public int index;
        public ZooDayTwoSession session;
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
        public Vector3 LabelPoint => transform.position + Vector3.up * labelHeight;
        float aimHeight = .8f, labelHeight = 1.7f;
        RectTransform plate, patienceFill;
        Text request, progress, celebration;
        ZooCareState previous = (ZooCareState)(-1);
        MonkeyFace face;
        float popupAt;

        public void Initialize(ZooDayTwoSession owner, int number)
        {
            session = owner; index = number;
            if (!animationDriver) animationDriver = GetComponent<MonkeyNpcAnimator>();
            face = GetComponent<MonkeyFace>();
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                aimHeight = Mathf.Max(.4f, bounds.center.y - transform.position.y);
                labelHeight = Mathf.Max(1.2f, bounds.max.y - transform.position.y + .3f);
            }
            var canvasObject = new GameObject("Day 02 animal request", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            plate = canvasObject.GetComponent<RectTransform>(); plate.SetParent(transform, false);
            plate.sizeDelta = new Vector2(900, 240); plate.pivot = new Vector2(.5f, .5f);
            // Preserve a consistent world size even when a source monkey is scaled.
            plate.localScale = new Vector3(.0028f / transform.lossyScale.x, .0028f / transform.lossyScale.y, .0028f / transform.lossyScale.z);
            ZooMenuUI.Panel("Request card", plate, 0, 0, 900, 240, ZooMenuUI.Cream, 35);
            request = ZooMenuUI.Label("Need", plate, "", 35, 14, 830, 75, 54, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            progress = ZooMenuUI.Label("Action progress", plate, "", 35, 94, 830, 60, 38, ZooMenuUI.Brown, TextAnchor.MiddleCenter);
            ZooMenuUI.Panel("Patience track", plate, 45, 178, 810, 29, ZooMenuUI.Hex("D5CAAC"), 14);
            patienceFill = ZooMenuUI.Panel("Patience", plate, 45, 178, 810, 29, ZooMenuUI.Leaf, 14).rectTransform;
            celebration = ZooMenuUI.Label("Care reward", plate, "+1 CARE", 80, -105, 740, 90, 63, ZooMenuUI.Cream, TextAnchor.MiddleCenter, true);
            var shadow = celebration.gameObject.AddComponent<Shadow>(); shadow.effectColor = ZooMenuUI.Ink; shadow.effectDistance = new Vector2(2, -3);
            plate.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!session || session.Round == null) return;
            var a = session.Round.animals[index];
            if (a.state != previous)
            {
                var action = MonkeyAction.StandingIdle;
                if (a.state == ZooCareState.Waiting) action = a.need == CareNeed.Hunger ? MonkeyAction.Hungry : MonkeyAction.Scratching;
                if (a.state == ZooCareState.Eating) action = MonkeyAction.Eating;
                if (a.state == ZooCareState.Celebrating)
                {
                    action = a.need == CareNeed.Hunger ? MonkeyAction.Happy : MonkeyAction.Cute1;
                    popupAt = Time.time; face?.ShowReaction(action, 3);
                    session.PlayCareSound(a.need == CareNeed.Hunger ? "feedSuccess" : "scratchSuccess", AimPoint);
                }
                if (animationDriver) animationDriver.TryPlay(action);
                previous = a.state;
            }
            bool pending = a.state == ZooCareState.Waiting || a.state == ZooCareState.Eating;
            bool happy = a.state == ZooCareState.Celebrating;
            plate.gameObject.SetActive(session.Round.Started && !session.Round.Finished && (pending || happy));
            if (!plate.gameObject.activeSelf) return;
            request.text = happy ? "THANK YOU!" : a.need == CareNeed.Hunger ? "BANANA, PLEASE" : "ITCHY!";
            if (a.state == ZooCareState.Eating) progress.text = "MUNCHING...";
            else if (happy) progress.text = "A LITTLE CARE GOES A LONG WAY";
            else if (a.need == CareNeed.Itch) progress.text = a.strokes + " / " + session.Round.StrokeGoal(a) + " STROKES" + (a.late ? "  •  NEEDS EXTRA CARE" : "");
            else progress.text = a.late ? "NEEDS EXTRA CARE" : "FEED WITH A BANANA";
            patienceFill.sizeDelta = new Vector2(810 * (happy ? 1 : session.Round.Patience(a)), 29);
            patienceFill.GetComponent<ZooRoundedPanel>().color = a.late ? ZooMenuUI.Orange : ZooMenuUI.Leaf;
            celebration.gameObject.SetActive(happy && Time.time - popupAt < 1.4f);
            celebration.rectTransform.anchoredPosition = new Vector2(80, 105 + Mathf.Min(1.4f, Time.time - popupAt) * 45);
        }

        void LateUpdate()
        {
            if (!plate || !session || !session.playerCamera) return;
            var camera = session.playerCamera;
            Vector3 viewport = camera.WorldToViewportPoint(LabelPoint);
            if (viewport.z > 0 && viewport.x > 0 && viewport.x < 1) viewport.y = Mathf.Min(viewport.y, .78f);
            plate.position = camera.ViewportToWorldPoint(viewport);
            // Keep the request readable at a distance without turning it into a
            // huge banner over the fixed HUD when the player walks up to scratch.
            float scale = .0028f * Mathf.Clamp(Vector3.Distance(camera.transform.position, AimPoint) / 6, .15f, 1.6f);
            plate.localScale = new Vector3(scale / transform.lossyScale.x, scale / transform.lossyScale.y, scale / transform.lossyScale.z);
            plate.rotation = Quaternion.LookRotation(plate.position - session.playerCamera.transform.position);
        }
    }
}
