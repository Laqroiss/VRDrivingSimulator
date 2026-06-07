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
    // ── Data formats ──────────────────────────────────────────────────────────

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
        // Real exam time when the frame was recorded (seconds from start)
        public float t;
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

    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    public ReplaySystem   replaySystem;

    [Header("CRM")]
    public string crmUrl     = "http://localhost:3000";
    public int    replayPort = 7779;
    public float  recordFPS  = 30f;

    // The HUD is created in code - nothing to drag into the Inspector
    private GameObject    _hudRoot;
    private TMP_Text      hudNameText;
    private TMP_Text      hudResultText;
    private TMP_Text      hudScoreText;
    private TMP_Text      hudTimeText;
    private RectTransform _errorContainer;

    // ── Runtime ──────────────────────────────────────────────────────────────

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
    private volatile bool     _replayLoadFailed;   // replay load failed (404/network)
    private CRMReplay         _pendingReplay;
    private AttemptMeta       _pendingMeta;
    private bool              _replayRunning;
    private Coroutine         _sceneReplayCoroutine;

    /// <summary>Fires when replay playback finishes.</summary>
    public static event System.Action OnReplayFinished;

    /// <summary>Launch a 3D replay of an attempt by its id (from the cabinet). Network request runs in the background.</summary>
    public void PlayReplayById(string attemptId)
    {
        if (string.IsNullOrEmpty(attemptId)) { Debug.LogWarning("[ReplayCRMSync] PlayReplayById: empty id"); return; }
        Debug.Log($"[ReplayCRMSync] Launching replay from cabinet: id='{attemptId}'  ->  {crmUrl}/api/attempts/{attemptId}/replay");
        ThreadPool.QueueUserWorkItem(_ => FetchAndQueueReplay(attemptId));
    }

    // ── Unity ────────────────────────────────────────────────────────────────

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

        // Replay load failed - notify (so the menu comes back)
        if (_replayLoadFailed)
        {
            _replayLoadFailed = false;
            OnReplayFinished?.Invoke();
        }
    }

    // ── Recording ──────────────────────────────────────────────────────────────

    void OnExamStart()
    {
        _frames.Clear();
        _lightChanges.Clear();
        _elapsed = 0f;
        _timer   = 0f;

        // Replay auto-recording can be turned off in the settings menu ("Record Replay")
        // to avoid cluttering the DB with test attempts. When off, frames aren't collected,
        // so OnResultSent uploads nothing (frames is empty).
        if (!MenuUIToolkit.AutoRecordExam)
        {
            _recording = false;
            Debug.Log("[ReplayCRMSync] Replay auto-recording disabled in settings - skipping");
            return;
        }

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
            t     = _elapsed,   // real exam time - the key for syncing penalties
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

    // ── Upload to CRM ──────────────────────────────────────────────────────────

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

    // ── Scene playback ─────────────────────────────────────────────────────────

    void StartFullReplay(CRMReplay replay, AttemptMeta meta)
    {
        if (_sceneReplayCoroutine != null) StopCoroutine(_sceneReplayCoroutine);

        // HUD
        InitHUD(meta);

        // Car - via ReplaySystem
        replaySystem?.StartReplayFromCRMData(replay.frames, replay.fps);

        // Scene - separate coroutine
        _replayRunning = true;
        _sceneReplayCoroutine = StartCoroutine(SceneReplayRoutine(replay, meta));
    }

    void BuildHUD()
    {
        if (_hudRoot != null) Destroy(_hudRoot);

        // Canvas
        var canvasGO = new GameObject("ReplayCRMHUD");
        DontDestroyOnLoad(canvasGO);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
            UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        _hudRoot = canvasGO;

        // ── Main panel (middle-right) ───────────────────────────────────────
        var panel = MakePanel(_hudRoot.transform, new Vector2(340, 220),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20, 0), new Color(0.05f, 0.07f, 0.12f, 0.88f));
        panel.pivot = new Vector2(1f, 0.5f);

        // Thin blue accent strip on the left
        var accent = MakeImage(panel.transform, new Vector2(4, 220),
            new Vector2(0,0.5f), new Vector2(0,0.5f), new Vector2(2,0),
            new Color(0.25f, 0.55f, 1f, 1f));

        float y = 82f;

        hudNameText   = MakeText(panel.transform, "—",           16, FontStyles.Bold,
                                 Color.white,           new Vector2(0,y));  y -= 26f;
        hudResultText = MakeText(panel.transform, "",            14, FontStyles.Bold,
                                 new Color(0.55f,0.85f,0.55f,1f), new Vector2(0,y)); y -= 26f;

        // Separator
        MakeImage(panel.transform, new Vector2(290, 1),
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0, y),
            new Color(0.3f, 0.4f, 0.6f, 0.5f));
        y -= 18f;

        hudScoreText  = MakeText(panel.transform, "0 pts",       13, FontStyles.Normal,
                                 new Color(0.9f,0.9f,0.9f,1f),  new Vector2(0,y));  y -= 22f;
        hudTimeText   = MakeText(panel.transform, "0:00",        13, FontStyles.Normal,
                                 new Color(0.6f,0.7f,0.9f,1f),  new Vector2(0,y));

        // ── Error container (below the main panel, grows downward) ─────────
        var cntGO = new GameObject("ErrorContainer");
        cntGO.transform.SetParent(_hudRoot.transform, false);
        // Image gives a RectTransform; make it transparent
        cntGO.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
        _errorContainer = cntGO.GetComponent<RectTransform>();
        _errorContainer.anchorMin        = _errorContainer.anchorMax = new Vector2(1f, 0.5f);
        _errorContainer.pivot            = new Vector2(1f, 1f);   // grows downward
        _errorContainer.anchoredPosition = new Vector2(-20f, -120f); // 10px below the panel
        _errorContainer.sizeDelta        = new Vector2(340f, 0f);

        var vlg = cntGO.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.spacing              = 4f;
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth    = false;
        vlg.childControlHeight   = false;

        var csf = cntGO.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    }

    // ── UI creation helpers ────────────────────────────────────────────────

    static RectTransform MakePanel(Transform parent, Vector2 size,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Color color)
    {
        var go  = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        // Rounded corners via sprite if available
        return rt;
    }

    static RectTransform MakeImage(Transform parent, Vector2 size,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Color color)
    {
        var go  = new GameObject("Img");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return rt;
    }

    static TMP_Text MakeText(Transform parent, string text, float size,
        FontStyles style, Color color, Vector2 pos)
    {
        var go  = new GameObject("Txt");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300, 24);
        rt.anchoredPosition = pos;
        return tmp;
    }

    void InitHUD(AttemptMeta meta)
    {
        BuildHUD();
        if (hudNameText  != null) hudNameText.text  = meta?.studentName ?? "—";
        if (hudScoreText != null) hudScoreText.text = "0 pts";
        if (hudTimeText  != null) hudTimeText.text  = "0:00";
        if (hudResultText != null)
            hudResultText.text = meta == null ? "" : meta.passed
                ? "<color=#22c55e>PASSED</color>"
                : "<color=#ef4444>FAILED</color>";
    }

    void HideHUD()
    {
        ClearErrors();
        if (_hudRoot != null) { Destroy(_hudRoot); _hudRoot = null; }
    }

    // Destroys all current error cards (when scrubbing back)
    void ClearErrors()
    {
        if (_errorContainer == null) return;
        for (int i = _errorContainer.childCount - 1; i >= 0; i--)
            Destroy(_errorContainer.GetChild(i).gameObject);
    }

    // Creates a new error card; it lives on its own and fades out by itself
    void SpawnError(PenaltyData p, int accumulatedScore)
    {
        if (_errorContainer == null) return;
        Debug.Log($"[ReplayCRMSync] SpawnError: penalty.t={p.t:F1}s  frameT={replaySystem?.CurrentReplayTime:F1}s  '{p.description}'");
        if (hudScoreText != null) hudScoreText.text = $"{accumulatedScore} pts";

        // Card background
        var itemRT = MakeImage(_errorContainer, new Vector2(340, 54),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Color(0.55f, 0.08f, 0.08f, 0.88f));

        // Left strip
        MakeImage(itemRT, new Vector2(4, 54),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, 0f),
            new Color(1f, 0.3f, 0.3f, 1f));

        // Error description
        string exStr = p.exerciseNum > 0 ? $"Ex.{p.exerciseNum} • " : "";
        var desc = MakeText(itemRT, $"{exStr}{p.description}", 11, FontStyles.Normal,
                            Color.white, new Vector2(-30f, 2f));
        var descRT = desc.GetComponent<RectTransform>();
        descRT.sizeDelta         = new Vector2(230f, 46f);
        desc.alignment           = TextAlignmentOptions.Left;
        desc.textWrappingMode    = TextWrappingModes.Normal;

        // Penalty points on the right
        var pts = MakeText(itemRT, $"−{p.points}", 15, FontStyles.Bold,
                           new Color(1f, 0.45f, 0.45f, 1f), new Vector2(130f, 0f));
        pts.alignment = TextAlignmentOptions.Right;

        // LayoutElement so the container's ContentSizeFitter accounts for the height
        var le = itemRT.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        le.preferredHeight = 54f;
        le.preferredWidth  = 340f;

        var cg = itemRT.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        StartCoroutine(ErrorItemLifetime(itemRT.gameObject, cg));
    }

    IEnumerator ErrorItemLifetime(GameObject item, CanvasGroup cg)
    {
        // Fade in
        float t = 0f;
        while (t < 0.15f && item != null) { t += Time.deltaTime; cg.alpha = t / 0.15f; yield return null; }
        if (item == null) yield break;
        cg.alpha = 1f;

        yield return new WaitForSeconds(3f);

        // Fade out
        t = 0f;
        while (t < 0.3f && item != null) { t += Time.deltaTime; cg.alpha = 1f - t / 0.3f; yield return null; }
        if (item != null) Destroy(item);
    }

    IEnumerator SceneReplayRoutine(CRMReplay replay, AttemptMeta meta)
    {
        // Stop the scene automation
        foreach (var ti in _intersections) ti?.StopCycle();
        _railway?.PauseTrain();

        float duration  = replay.frames.Count / replay.fps;
        var penalties   = meta?.penalties;
        float prevElapsed = -1f;

        // Computes the correct nextPenalty and accumulated score for a given time
        (int idx, int pts) PenaltyStateAt(float t)
        {
            int idx = 0, pts = 0;
            if (penalties == null) return (0, 0);
            while (idx < penalties.Count && penalties[idx].t <= t)
            { pts += penalties[idx].points; idx++; }
            return (idx, pts);
        }

        int nextPenalty    = 0;
        int accumulatedPts = 0;

        while (_replayRunning)
        {
            // Replay stopped early (Stop button) - exit to bring the menu back
            if (replaySystem != null && !replaySystem.IsReplaying) break;

            float replayTime = replaySystem != null ? replaySystem.CurrentReplayTime : 0f;
            if (replayTime >= duration) break;

            int frameIdx = Mathf.Clamp(Mathf.FloorToInt(replayTime * replay.fps), 0, replay.frames.Count - 1);
            var frame    = replay.frames[frameIdx];
            // Use the frame's real recorded time - it matches penalty.t exactly
            float elapsed = frame.t > 0f ? frame.t : replayTime;

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

            // Errors - sync with the current replay time
            if (penalties != null)
            {
                // Scrubbed back - recompute position from scratch
                if (elapsed < prevElapsed - 0.1f)
                {
                    ClearErrors();
                    (nextPenalty, accumulatedPts) = PenaltyStateAt(elapsed);
                    if (hudScoreText != null) hudScoreText.text = $"{accumulatedPts} pts";
                }

                // Advance through penalties - all new ones go into the queue
                while (nextPenalty < penalties.Count && elapsed >= penalties[nextPenalty].t)
                {
                    var pen = penalties[nextPenalty];
                    accumulatedPts += pen.points;
                    SpawnError(pen, accumulatedPts);
                    nextPenalty++;
                }
            }

            prevElapsed = elapsed;
            yield return null;
        }

        // Resume the automation
        foreach (var ti in _intersections) ti?.ResumeCycle();
        _railway?.ResumeTrain();
        _replayRunning = false;
        HideHUD();
        OnReplayFinished?.Invoke();

        Debug.Log("[ReplayCRMSync] Playback finished");
    }

    // ── HTTP listener ────────────────────────────────────────────────────────

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

                string html = "<html><body style='font-family:sans-serif;text-align:center;padding:40px'><h2>▶ Replay starting...</h2><p>You can close this window</p></body></html>";
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
            { Debug.LogWarning("[ReplayCRMSync] Replay is empty"); _replayLoadFailed = true; return; }

            // 2. Attempt metadata (student name, penalties)
            AttemptMeta meta = null;
            try
            {
                var metaTask = client.GetStringAsync($"{crmUrl}/api/attempts/{attemptId}");
                metaTask.Wait();
                meta = JsonUtility.FromJson<AttemptMeta>(metaTask.Result);
                var sb = new System.Text.StringBuilder();
                sb.Append($"[ReplayCRMSync] Metadata: student={meta?.studentName}, penalties={meta?.penalties?.Count ?? 0}\n");
                if (meta?.penalties != null)
                    foreach (var pen in meta.penalties)
                        sb.Append($"  t={pen.t:F1}s  {pen.points}pts  {pen.description}\n");
                Debug.Log(sb.ToString());
            }
            catch (System.Exception me)
            {
                Debug.LogError($"[ReplayCRMSync] Failed to load metadata: {me.Message}");
            }

            _pendingReplay = replay;
            _pendingMeta   = meta;
            _launchReplay  = true;
            Debug.Log($"[ReplayCRMSync] Replay ready: {replay.frames.Count} frames | student: {meta?.studentName ?? "?"} | penalties: {meta?.penalties?.Count ?? 0}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ReplayCRMSync] Error fetching replay (no replay recorded for this attempt?): {e.Message}");
            _replayLoadFailed = true;
        }
    }
}
