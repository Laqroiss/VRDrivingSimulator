using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Text;

/// <summary>
/// Full scene recording and playback via CRM:
/// - Car (position, rotation, speed, lights)
/// - Traffic lights (sideA / sideB phase of each intersection)
/// - Train (position, whether active)
///
/// Attach to the same GameObject as ExamManager + ExamResultSender.
/// Drag ReplaySystem into the replaySystem field.
/// </summary>
[RequireComponent(typeof(ExamManager))]
public class ReplayCRMSync : MonoBehaviour
{
    // вв Data formats вввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    [System.Serializable]
    public class CRMFrame
    {
        // Car
        public float x, y, z, qx, qy, qz, qw;
        public float speed, rpm;
        public int   gear;
        public bool  bl, rl, lb, rb, bp;       // brake/reverse/left/right blink/blinkPhase
        // Train
        public float tx, ty, tz;
        public bool  trainActive;
    }

    // Traffic-light phase change event
    [System.Serializable]
    public class LightChange
    {
        public float t;     // time from exam start
        public int   idx;   // TrafficIntersection index in the array
        public string pA, pB;
    }

    [System.Serializable]
    class CRMReplay
    {
        public float             fps;
        public List<CRMFrame>    frames;
        public List<LightChange> lightChanges;
    }

    [System.Serializable]
    class AttemptIdResponse { public string id; }

    // Attempt metadata (loaded together with the replay)
    [System.Serializable]
    public class PenaltyData
    {
        public string description;
        public int    points;
        public int    exerciseNum;
        public float  t;   // time from exam start
    }

    [System.Serializable]
    class AttemptMeta
    {
        public string            studentName;
        public bool              passed;
        public int               totalPenaltyPoints;
        public List<PenaltyData> penalties;
    }

    // вв Inspector вввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    [Header("References")]
    public ReplaySystem   replaySystem;

    [Header("CRM")]
    public string crmUrl     = "http://localhost:3000";
    public int    replayPort = 7779;
    public float  recordFPS  = 30f;

    [Header("HUD ()")]
    public Canvas     hudCanvas;      // Screen Space Overlay,   
    public TMP_Text   hudNameText;    // "Sartayev Miras"
    public TMP_Text   hudResultText;  // " /    вЂў  10 ."
    public TMP_Text   hudTimeText;    //  
    public GameObject hudErrorPanel;  //   
    public TMP_Text   hudErrorText;   //  
    public TMP_Text   hudErrorPoints; // "в€’5 ."

    // вв Runtime вввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    private ExamManager          _exam;
    private Car                  _car;
    private CarIndicators        _indicators;
    private TrafficIntersection[] _intersections;
    private RailwayCrossing      _railway;

    // Recording
    private List<CRMFrame>    _frames      = new List<CRMFrame>();
    private List<LightChange> _lightChanges = new List<LightChange>();
    private string[]          _lastPhaseA;   // previous phase of each intersection
    private string[]          _lastPhaseB;
    private bool              _recording   = false;
    private float             _elapsed     = 0f;
    private float             _timer       = 0f;

    // Playback
    private HttpListener      _listener;
    private bool              _launchReplay;
    private CRMReplay         _pendingReplay;
    private AttemptMeta       _pendingMeta;
    private bool              _replayRunning;
    private Coroutine         _sceneReplayCoroutine;
    private Coroutine         _errorCoroutine;

    // вв Unity вввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void Awake()
    {
        _exam         = GetComponent<ExamManager>();
        _car          = FindAnyObjectByType<Car>();
        _railway      = FindAnyObjectByType<RailwayCrossing>();
        _intersections = FindObjectsByType<TrafficIntersection>(FindObjectsInactive.Exclude);
        if (_car != null) _indicators = _car.GetComponent<CarIndicators>();
        _lastPhaseA = new string[_intersections.Length];
        _lastPhaseB = new string[_intersections.Length];
    }

    void Start()
    {
        _exam.OnExamStart.AddListener(OnExamStart);
        _exam.OnExamFinish.AddListener(OnExamFinish);
        ExamResultSender.OnResultSent += OnResultSent;
        StartHTTPListener();
    }

    void OnDestroy()
    {
        _exam.OnExamStart.RemoveListener(OnExamStart);
        _exam.OnExamFinish.RemoveListener(OnExamFinish);
        ExamResultSender.OnResultSent -= OnResultSent;
        _listener?.Stop();
    }

    void Update()
    {
        if (_recording)
        {
            _elapsed += Time.deltaTime;
            _timer   += Time.deltaTime;

            if (_timer >= 1f / recordFPS)
            {
                _timer = 0f;
                RecordFrame();
            }

            // Record traffic-light phase-change events
            for (int i = 0; i < _intersections.Length; i++)
            {
                var ti = _intersections[i];
                if (ti == null) continue;
                if (ti.PhaseNameA != _lastPhaseA[i] || ti.PhaseNameB != _lastPhaseB[i])
                {
                    _lastPhaseA[i] = ti.PhaseNameA;
                    _lastPhaseB[i] = ti.PhaseNameB;
                    _lightChanges.Add(new LightChange { t = _elapsed, idx = i, pA = ti.PhaseNameA, pB = ti.PhaseNameB });
                }
            }
        }

        // Replay launch must be on the main thread
        if (_launchReplay && _pendingReplay != null)
        {
            _launchReplay = false;
            StartFullReplay(_pendingReplay, _pendingMeta);
            _pendingReplay = null;
            _pendingMeta   = null;
        }
    }

    // вв Recording вввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void OnExamStart()
    {
        _frames.Clear();
        _lightChanges.Clear();
        _elapsed = 0f;
        _timer   = 0f;
        _recording = true;
        for (int i = 0; i < _intersections.Length; i++)
        {
            _lastPhaseA[i] = "";
            _lastPhaseB[i] = "";
        }
        replaySystem?.StartRecording("Exam");
        Debug.Log("[ReplayCRMSync] Recording started");
    }

    void OnExamFinish()
    {
        _recording = false;
        replaySystem?.StopRecording();
        Debug.Log($"[ReplayCRMSync] Frames recorded: {_frames.Count}, light changes: {_lightChanges.Count}");
    }

    void RecordFrame()
    {
        if (_car == null) return;
        var t = _car.transform;
        var q = t.rotation;
        var f = new CRMFrame
        {
            x = t.position.x, y = t.position.y, z = t.position.z,
            qx = q.x, qy = q.y, qz = q.z, qw = q.w,
            speed = _car.rb != null ? _car.rb.linearVelocity.magnitude * 3.6f : 0f,
            rpm   = _car.e?.getRPM()         ?? 0f,
            gear  = _car.e?.getCurrentGear() ?? 0,
            bl    = _car.BrakeLightsOn,
            rl    = _car.ReverseLightsOn,
            lb    = _indicators != null && (_indicators.LeftIndicatorOn  || _indicators.HazardLightsOn),
            rb    = _indicators != null && (_indicators.RightIndicatorOn || _indicators.HazardLightsOn),
            bp    = _indicators != null && _indicators.BlinkVisible,
        };

        // Train
        if (_railway != null && _railway.TrainActive)
        {
            var tp = _railway.TrainPosition;
            f.tx = tp.x; f.ty = tp.y; f.tz = tp.z;
            f.trainActive = true;
        }

        _frames.Add(f);
    }

    // вв Upload to CRM вввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void OnResultSent(string attemptId)
    {
        if (_frames.Count == 0) return;
        StartCoroutine(UploadReplay(attemptId));
    }

    IEnumerator UploadReplay(string attemptId)
    {
        var replay = new CRMReplay { fps = recordFPS, frames = _frames, lightChanges = _lightChanges };
        string json = JsonUtility.ToJson(replay);
        Debug.Log($"[ReplayCRMSync] Uploading replay ({_frames.Count} frames, {json.Length / 1024} KB)...");

        var req = new UnityWebRequest($"{crmUrl}/api/attempts/{attemptId}/replay", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("[ReplayCRMSync] Replay uploaded to CRM");
        else
            Debug.LogError($"[ReplayCRMSync] Upload error: {req.error}");
    }

    // вв Scene playback ввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void StartFullReplay(CRMReplay replay, AttemptMeta meta)
    {
        if (_sceneReplayCoroutine != null) StopCoroutine(_sceneReplayCoroutine);

        // HUD
        InitHUD(meta);

        // Car вЂ”  ReplaySystem
        replaySystem?.StartReplayFromCRMData(replay.frames, replay.fps);

        // Scene - separate coroutine
        _replayRunning = true;
        _sceneReplayCoroutine = StartCoroutine(SceneReplayRoutine(replay, meta));
    }

    void InitHUD(AttemptMeta meta)
    {
        if (hudCanvas != null) hudCanvas.gameObject.SetActive(true);
        if (hudErrorPanel != null) hudErrorPanel.SetActive(false);

        if (meta == null)
        {
            if (hudNameText   != null) hudNameText.text   = "";
            if (hudResultText != null) hudResultText.text = "";
            return;
        }

        if (hudNameText != null)
            hudNameText.text = meta.studentName ?? "";

        if (hudResultText != null)
        {
            string res = meta.passed ? "<color=#22c55e></color>" : "<color=#ef4444> </color>";
            hudResultText.text = $"{res}  вЂў  {meta.totalPenaltyPoints} .";
        }
    }

    void HideHUD()
    {
        if (hudCanvas   != null) hudCanvas.gameObject.SetActive(false);
        if (_errorCoroutine != null) { StopCoroutine(_errorCoroutine); _errorCoroutine = null; }
        if (hudErrorPanel != null) hudErrorPanel.SetActive(false);
    }

    IEnumerator ShowError(PenaltyData p)
    {
        if (hudErrorPanel == null) yield break;

        if (hudErrorText   != null)
        {
            string exStr = p.exerciseNum > 0 ? $". {p.exerciseNum}  вЂў  " : "";
            hudErrorText.text = $"{exStr}{p.description}";
        }
        if (hudErrorPoints != null)
            hudErrorPoints.text = $"в€’{p.points} .";

        hudErrorPanel.SetActive(true);

        //    CanvasGroup  
        var cg = hudErrorPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            float t = 0f;
            while (t < 0.25f) { t += Time.deltaTime; cg.alpha = t / 0.25f; yield return null; }
            cg.alpha = 1f;
        }

        yield return new WaitForSeconds(3f);

        //  
        if (cg != null)
        {
            float t = 0f;
            while (t < 0.4f) { t += Time.deltaTime; cg.alpha = 1f - t / 0.4f; yield return null; }
        }

        hudErrorPanel.SetActive(false);
        _errorCoroutine = null;
    }

    IEnumerator SceneReplayRoutine(CRMReplay replay, AttemptMeta meta)
    {
        // Stop the scene automation
        foreach (var ti in _intersections) ti?.StopCycle();
        _railway?.PauseTrain();

        float startTime = Time.time;
        float duration  = replay.frames.Count / replay.fps;

        //     
        var penalties = meta?.penalties;
        int nextPenalty = 0;

        while (_replayRunning)
        {
            float elapsed = Time.time - startTime;
            if (elapsed >= duration) break;

            int frameIdx = Mathf.Clamp(Mathf.FloorToInt(elapsed * replay.fps), 0, replay.frames.Count - 1);
            var frame = replay.frames[frameIdx];

            // HUD timer
            if (hudTimeText != null)
            {
                int m = Mathf.FloorToInt(elapsed / 60f);
                int s = Mathf.FloorToInt(elapsed % 60f);
                hudTimeText.text = $"{m}:{s:00}";
            }

            // Traffic lights
            for (int i = 0; i < _intersections.Length; i++)
            {
                if (_intersections[i] == null) continue;
                string pA = null, pB = null;
                foreach (var lc in replay.lightChanges)
                    if (lc.idx == i && lc.t <= elapsed) { pA = lc.pA; pB = lc.pB; }
                if (pA != null) _intersections[i].ForcePhase(pA, pB);
            }

            // Train
            _railway?.SetTrainState(frame.tx, frame.ty, frame.tz, frame.trainActive);

            //  вЂ”       
            if (penalties != null)
            {
                while (nextPenalty < penalties.Count && penalties[nextPenalty].t > 0f
                       && elapsed >= penalties[nextPenalty].t)
                {
                    if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
                    _errorCoroutine = StartCoroutine(ShowError(penalties[nextPenalty]));
                    nextPenalty++;
                }
            }

            yield return null;
        }

        // Resume the automation
        foreach (var ti in _intersections) ti?.ResumeCycle();
        _railway?.ResumeTrain();
        _replayRunning = false;
        HideHUD();

        Debug.Log("[ReplayCRMSync] Playback finished");
    }

    // вв HTTP listener вввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void StartHTTPListener()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{replayPort}/");
            _listener.Start();
            ThreadPool.QueueUserWorkItem(_ => ListenLoop());
            Debug.Log($"[ReplayCRMSync] Listening for replay commands on port {replayPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ReplayCRMSync] Failed to start HTTP listener: {e.Message}");
        }
    }

    void ListenLoop()
    {
        while (_listener != null && _listener.IsListening)
        {
            try
            {
                var ctx = _listener.GetContext();
                string id = ctx.Request.QueryString["id"];

                string html = "<html><body style='font-family:sans-serif;text-align:center;padding:40px'><h2>в Replay starting...</h2><p>You can close this window</p></body></html>";
                var buf = Encoding.UTF8.GetBytes(html);
                ctx.Response.ContentType     = "text/html; charset=utf-8";
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                ctx.Response.OutputStream.Close();

                if (!string.IsNullOrEmpty(id))
                    ThreadPool.QueueUserWorkItem(_ => FetchAndQueueReplay(id));
            }
            catch (HttpListenerException) { break; }
            catch (System.Exception e) { Debug.LogWarning($"[ReplayCRMSync] {e.Message}"); }
        }
    }

    void FetchAndQueueReplay(string attemptId)
    {
        try
        {
            var client = new System.Net.Http.HttpClient();

            // 1. Replay frames
            var replayTask = client.GetStringAsync($"{crmUrl}/api/attempts/{attemptId}/replay");
            replayTask.Wait();
            var replay = JsonUtility.FromJson<CRMReplay>(replayTask.Result);
            if (replay?.frames == null || replay.frames.Count == 0)
            { Debug.LogWarning("[ReplayCRMSync]  "); return; }

            // 2. Attempt metadata (student name, penalties)
            AttemptMeta meta = null;
            try
            {
                var metaTask = client.GetStringAsync($"{crmUrl}/api/attempts/{attemptId}");
                metaTask.Wait();
                meta = JsonUtility.FromJson<AttemptMeta>(metaTask.Result);
            }
            catch (System.Exception me)
            {
                Debug.LogWarning($"[ReplayCRMSync]    : {me.Message}");
            }

            _pendingReplay = replay;
            _pendingMeta   = meta;
            _launchReplay  = true;
            Debug.Log($"[ReplayCRMSync] Replay ready: {replay.frames.Count} frames | student: {meta?.studentName ?? "?"} | penalties: {meta?.penalties?.Count ?? 0}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ReplayCRMSync]   : {e.Message}");
        }
    }
}
