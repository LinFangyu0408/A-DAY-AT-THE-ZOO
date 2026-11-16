#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using ADayAtTheZoo.Monkeys;
using ADayAtTheZoo.Monkeys.Care;

public sealed partial class CarePlayTestDriver
{
    float Now => MonkeyCareSession.GameTime;
    static Collider Contact(MonkeyCareNpc npc) => npc.transform.Find("Care Contact").GetComponent<BoxCollider>();

    IEnumerator RecordRound()
    {
        session.recordingHand = previewHand;
        SetHandToView();
        while (!session.Begun || Now < 3.4f) yield return null;

        yield return FrameSupply(banana);
        yield return GrabForVideo(banana);
        SetHandToView();
        yield return new WaitForSeconds(1.4f);
        session.recordingGrip = false;
        yield return new WaitForSeconds(1.2f);
        Check(!banana.Held && session.Round.Bananas == 12, "drop banana without consuming inventory");
        banana.transform.position = new Vector3(0, -5, 0);
        yield return null;
        yield return null;
        Check(Vector3.Distance(banana.transform.position, session.roundArtwork.bananaAnchor.position) < 2, "lost banana returns to supply");
        yield return ArcForVideo();
        yield return FrameSupply(banana);
        while (Now < 14.9f) yield return null;
        yield return GrabForVideo(banana);
        Aim(feed); SetHandToView();
        while (!feed.Accepts(CareNeed.Hunger)) yield return null;

        var point = feed.teleportPoint;
        var rigStart = session.locomotion.transform.position;
        previewHand.LookAt(point.GetComponent<Collider>().bounds.center);
        yield return new WaitForSeconds(.55f);
        session.recordingTrigger = true;
        yield return null;
        session.recordingTrigger = false;
        yield return null;
        Check(Vector3.Distance(rigStart, session.locomotion.transform.position) > .2f && banana.Held, "trigger teleports to demand point while retaining banana");
        var back = Vector3.ProjectOnPlane(point.Landing - feed.transform.position, Vector3.up).normalized;
        camera.transform.position = point.Landing + back * 1.35f + Vector3.up * 1.55f;
        camera.transform.LookAt(Contact(feed).bounds.center + Vector3.up * .15f);
        SetHandToView();
        previewHand.LookAt(Contact(feed).bounds.center);
        yield return new WaitForSeconds(2.1f);
        Check(feed.Progress.Fed > 1 && session.RayNpc == feed, "held straight ray starts feeding");
        previewHand.position = camera.transform.position + camera.transform.right * 1.25f;
        previewHand.rotation = Quaternion.LookRotation(camera.transform.right);
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.35f);
        float retained = feed.Progress.Fed;
        yield return new WaitForSeconds(1.7f);
        Check(Mathf.Abs(feed.Progress.Fed - retained) < .06f, "looking away pauses and retains feeding progress");
        previewHand.rotation = Quaternion.LookRotation(camera.transform.right);
        PutInContact(banana, feed);
        float limit = Now + 12;
        while (feed.HasDemand && Now < limit) yield return null;
        Check(feed.Celebrating && session.Round.Bananas == 11, "physical contact completes feed and spends one banana");
        session.recordingGrip = false;
        while (feed.Celebrating) yield return null;
        Check(banana.Available, "banana replenishes during celebration");
        yield return FrameSupply(banana);
        yield return new WaitForSeconds(1.1f);

        yield return ScratchForVideo(itch, false);
        yield return FrameSupply(scratcher);
        yield return new WaitForSeconds(1);
        Check(scratcher.Available, "scratcher replenishes during celebration");

        Aim(third); SetHandToView();
        while (Now < CareRound.Cutoff && (!third.Accepts(CareNeed.Hunger) || !third.Progress.Late)) yield return null;
        Check(third.HasDemand && third.Progress.Late, "third monkey displays hunger timeout and rescue requirement");
        yield return new WaitForSeconds(1.5f);
        yield return FrameSupply(banana);
        yield return GrabForVideo(banana);
        Aim(third); SetHandToView();
        previewHand.LookAt(Contact(third).bounds.center);
        limit = Now + third.lateFeedingSeconds + 3;
        while (third.HasDemand && Now < limit) yield return null;
        Check(third.Celebrating && Mathf.Approximately(third.Progress.Fed, third.lateFeedingSeconds),
            "timeout feeding completes at the scene's configured rescue duration");
        session.recordingGrip = false;
        while (third.Celebrating) yield return null;

        yield return ScratchForVideo(feed, true);
        while (Now < 110) yield return null;
        // Keep the interaction footage at 1.2x and remove only uneventful waiting.
        CareDemoRecorder.Section = "pause";
        Time.timeScale = 14;
        while (Now < 168) yield return null;
        Time.timeScale = 1.2f;
        Aim(feed); SetHandToView();
        CareDemoRecorder.Section = "core";
        while (!session.Round.Frozen) yield return null;
        Check(session.Round.CompletedTotal == 4 && session.Round.Bananas == 10, "result reflects both normal and rescue care for both tools");
        while (Now < CareRound.Duration) yield return null;
        Check(FindObjectsByType<CareResultButton>(FindObjectsSortMode.None).Length >= 2, "results and buttons shown");
        yield return new WaitForSecondsRealtime(1.1f);
        Check(!report.ToString().Contains("RUNTIME ERROR"), "no care runtime errors");
    }

    IEnumerator FrameSupply(CareItem item)
    {
        var center = item.GetComponent<Collider>().bounds.center;
        camera.transform.position = center + new Vector3(.45f, 1.25f, -1.85f);
        camera.transform.LookAt(center);
        SetHandToView();
        yield return new WaitForSeconds(.65f);
    }

    IEnumerator ArcForVideo()
    {
        var floor = GameObject.Find("Island_1").GetComponent<Collider>();
        var origin = session.roundArtwork.bananaAnchor.position + new Vector3(2, 1.7f, 1);
        camera.transform.position = origin;
        var arc = session.transform.Find("Care Teleport Arc").GetComponent<LineRenderer>();
        foreach (var offset in new[] { new Vector3(0, 0, 2.4f), new Vector3(-2, 0, 1.6f), new Vector3(1.2f, 0, 2.4f), new Vector3(0, 0, -2.4f) })
        {
            if (!floor.Raycast(new Ray(origin + offset + Vector3.up * 4, Vector3.down), out var hit, 10)) continue;
            camera.transform.LookAt(hit.point + Vector3.up * .3f);
            SetHandToView();
            previewHand.LookAt(hit.point);
            session.recordingTrigger = session.recordingGrip = true;
            yield return null;
            if (!arc.enabled || arc.startColor != Color.cyan)
            {
                session.recordingTrigger = session.recordingGrip = false;
                yield return null;
                continue;
            }
            var before = session.locomotion.transform.position;
            yield return new WaitForSeconds(.7f);
            session.recordingTrigger = session.recordingGrip = false;
            yield return null;
            var travel = session.locomotion.transform.position - before;
            Check(travel.sqrMagnitude > .04f, "empty-hand arc teleports on release");
            camera.transform.position += travel;
            yield return new WaitForSeconds(.5f);
            yield break;
        }
        Check(false, "could not find clear ground for the arc demonstration");
    }

    IEnumerator GrabForVideo(CareItem item)
    {
        var center = item.GetComponent<Collider>().bounds.center;
        previewHand.position = center + new Vector3(0, .75f, 0);
        previewHand.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        Physics.SyncTransforms();
        session.recordingTrigger = session.recordingGrip = true;
        yield return null;
        yield return null;
        session.recordingTrigger = false;
        Check(session.HeldItem == item && item.Held, "grab " + item.kind + " with trigger and grip");
    }

    void SetHandToView()
    {
        previewHand.localPosition = new Vector3(.26f, -.25f, .42f);
        previewHand.localRotation = Quaternion.identity;
    }

    void PutInContact(CareItem item, MonkeyCareNpc npc)
    {
        Physics.SyncTransforms();
        previewHand.position += Contact(npc).bounds.center - item.GetComponent<Collider>().bounds.center;
        Physics.SyncTransforms();
    }

    IEnumerator ScratchForVideo(MonkeyCareNpc npc, bool late)
    {
        Aim(npc); SetHandToView();
        while (!npc.Accepts(CareNeed.Itch) || (late && !npc.Progress.Late)) yield return null;
        yield return new WaitForSeconds(.7f);
        yield return FrameSupply(scratcher);
        yield return GrabForVideo(scratcher);
        Aim(npc); SetHandToView();
        PutInContact(scratcher, npc);
        yield return new WaitForSeconds(.5f);
        Check(scratcher.Caring && npc.Progress.Strokes == 0, "stationary contact does not count as scratching");
        var top = previewHand.position;
        var bottom = top - Vector3.up * (Mathf.Max(npc.strokeDistance, npc.resetDistance) + .065f);
        int count = npc.Progress.StrokeTarget;
        for (int i = 0; i < count; i++)
        {
            yield return MoveHand(top, bottom, .35f);
            Check(npc.Progress.Strokes == i + 1, "one downward Box Trigger stroke counted");
            if (i + 1 < count)
            {
                yield return new WaitForSeconds(.28f);
                yield return MoveHand(bottom, top, .4f);
                yield return new WaitForSeconds(.14f);
            }
        }
        Check(npc.Celebrating && npc.Phase == MonkeyAction.Cute1, "scratch completes with Cute1");
        session.recordingGrip = false;
        SetHandToView();
        while (npc.Celebrating) yield return null;
    }

    IEnumerator MoveHand(Vector3 from, Vector3 to, float duration)
    {
        float start = Now;
        while (Now - start < duration)
        {
            previewHand.position = Vector3.Lerp(from, to, (Now - start) / duration);
            yield return null;
        }
        previewHand.position = to;
        yield return null;
    }
}
#endif
