#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;

public sealed class CareDemoRecorder : MonoBehaviour
{
    public const int FrameRate = 24;
    public static string Section = "core";
    public static string Folder;
    static CareDemoRecorder active;
    Camera preview;
    RenderTexture target;
    Texture2D pixels;
    StreamWriter timeline;
    int frame;

    void Start()
    {
        preview = GetComponent<Camera>();
        target = new RenderTexture(960, 720, 24);
        pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
        Time.captureFramerate = FrameRate;
        Folder = "work/monkey-care/video-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(Folder + "/core");
        Directory.CreateDirectory(Folder + "/results");
        timeline = new StreamWriter(Folder + "/sounds.tsv") { AutoFlush = true };
        active = this;
        File.WriteAllText("work/monkey-care/video-folder.txt", Folder);
    }

    public static void RecordSound(AudioSource source, AudioClip clip)
    {
        if (!active || Section == "pause") return;
        var offset = source.transform.position - active.preview.transform.position;
        float distance = offset.magnitude;
        float attenuation = 1 - Mathf.InverseLerp(source.minDistance, source.maxDistance, distance);
        float pan = Vector3.Dot(active.preview.transform.right, offset.normalized);
        active.timeline.WriteLine(FormattableString.Invariant($"{active.frame}\t{clip.name}\t{source.volume * attenuation}\t{source.pitch}\t{pan}\t{source.GetInstanceID()}"));
    }

    void LateUpdate()
    {
        if (Section == "pause") return;
        string part = MonkeyCareSession.Instance && MonkeyCareSession.Instance.Round.Frozen ? "results" : "core";
        var previousActive = RenderTexture.active;
        var previousTarget = preview.targetTexture;
        try
        {
            Canvas.ForceUpdateCanvases();
            preview.targetTexture = target;
            preview.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Folder + "/" + part + "/" + (frame++).ToString("D06") + ".jpg", pixels.EncodeToJPG(91));
        }
        finally
        {
            preview.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
        }
    }

    void OnDestroy()
    {
        if (active == this) active = null;
        timeline?.Dispose();
        Time.captureFramerate = 0;
        if (target) Destroy(target);
        if (pixels) Destroy(pixels);
    }
}
#endif
