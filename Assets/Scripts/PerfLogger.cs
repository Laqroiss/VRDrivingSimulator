using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lightweight performance logger for diagnosis. Once per second it appends a CSV row with
/// frame timing (avg/worst), CPU/GPU split (FrameTimingManager), draw-call stats (Editor only),
/// memory, and the current VR / MSAA / render-scale state.
///
/// Writes to <c>Application.persistentDataPath/perf_log.csv</c> (path printed to the console on
/// start). Runs only in the Editor and Development builds, so release builds are unaffected.
/// Bootstraps itself - no scene wiring needed.
/// </summary>
public class PerfLogger : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return; // never in release
        if (FindAnyObjectByType<PerfLogger>() != null) return;
        var go = new GameObject("PerfLogger");
        go.AddComponent<PerfLogger>();
        DontDestroyOnLoad(go);
    }

    const float Interval = 1f;

    float _t, _accum, _worst;
    int   _frames;
    StreamWriter _w;
    readonly FrameTiming[] _timings = new FrameTiming[1];

    void Start()
    {
        string path = Path.Combine(Application.persistentDataPath, "perf_log.csv");
        _w = new StreamWriter(path, false) { AutoFlush = true };
        _w.WriteLine("t_s,avg_fps,frame_ms,worst_ms,cpu_ms,gpu_ms,draw,setpass,tris,verts,mem_MB,vr,eyeW,eyeH,msaa,renderScale,quality");
        Debug.Log($"[PerfLogger] logging performance to: {path}");
    }

    void Update()
    {
        _frames++;
        float dt = Time.unscaledDeltaTime;
        _accum += dt;
        float ms = dt * 1000f;
        if (ms > _worst) _worst = ms;

        _t += dt;
        if (_t < Interval) return;

        float avgFps  = _frames / Mathf.Max(_accum, 1e-5f);
        float frameMs = (_accum / Mathf.Max(_frames, 1)) * 1000f;

        // CPU/GPU split (requires Frame Timing Stats enabled in Player settings).
        float cpuMs = 0f, gpuMs = 0f;
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
        {
            cpuMs = (float)_timings[0].cpuFrameTime;
            gpuMs = (float)_timings[0].gpuFrameTime;
        }

        int draw = 0, setpass = 0, tris = 0, verts = 0;
#if UNITY_EDITOR
        draw    = UnityStats.drawCalls;
        setpass = UnityStats.setPassCalls;
        tris    = UnityStats.triangles;
        verts   = UnityStats.vertices;
#endif
        long memMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

        bool vr   = UnityEngine.XR.XRSettings.isDeviceActive;
        int  eyeW = UnityEngine.XR.XRSettings.eyeTextureWidth;
        int  eyeH = UnityEngine.XR.XRSettings.eyeTextureHeight;

        int   msaa = 1;
        float rs   = 1f;
        if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp)
        {
            msaa = urp.msaaSampleCount;
            rs   = urp.renderScale;
        }

        // Invariant culture: force '.' as the decimal separator, otherwise a locale that uses ','
        // (e.g. Russian Windows) would split every float into two CSV columns and corrupt the data.
        _w.WriteLine(System.FormattableString.Invariant(
            $"{Time.realtimeSinceStartup:F0},{avgFps:F1},{frameMs:F2},{_worst:F2},{cpuMs:F2},{gpuMs:F2},{draw},{setpass},{tris},{verts},{memMB},{(vr ? 1 : 0)},{eyeW},{eyeH},{msaa},{rs:F2},{QualitySettings.GetQualityLevel()}"));

        _t = 0f; _frames = 0; _accum = 0f; _worst = 0f;
    }

    void OnDestroy()
    {
        _w?.Flush();
        _w?.Close();
    }
}
