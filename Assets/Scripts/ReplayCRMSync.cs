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

    // The HUD is created in code - nothing to drag into the Inspector
    private GameObject  _hudRoot;
    private TMP_Text    hudNameText;
    private TMP_Text    hudResultText;
    private TMP_Text    hudScoreText;
    private TMP_Text    hudTimeText;
    private CanvasGroup hudErrorGroup;
    private TMP_Text    hudErrorText;
    private TMP_Text    hudErrorPoints;

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

        // вв Main panel (middle-right) ввввввввввввввввввввввввввввввввввввввв
        var panel = MakePanel(_hudRoot.transform, new Vector2(340, 220),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20, 0), new Color(0.05f, 0.07f, 0.12f, 0.88f));
        panel.pivot = new Vector2(1f, 0.5f);

        // Thin blue accent strip on the left
        var accent = MakeImage(panel.transform, new Vector2(4, 220),
            new Vector2(0,0.5f), new Vector2(0,0.5f), new Vector2(2,0),
            new Color(0.25f, 0.55f, 1f, 1f));

        float y = 82f;

        hudNameText   = MakeText(panel.transform, "вЂ”",           16, FontStyles.Bold,
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

        // вв   (  ) вввввввввввввввввввввввввввв
        var errGO = new GameObject("ErrorSection");
        errGO.transform.SetParent(panel.transform, false);
        hudErrorGroup = errGO.AddComponent<CanvasGroup>();
        hudErrorGroup.alpha = 0f;

        var errRect = errGO.AddComponent<RectTransform>();
        errRect.anchorMin = errRect.anchorMax = new Vector2(0.5f, 0f);
        errRect.sizeDelta = new Vector2(310, 52);
        errRect.anchoredPosition = new Vector2(0, -86);

        //  
        var errBg = errGO.AddComponent<UnityEngine.UI.Image>();
        errBg.color = new Color(0.6f, 0.1f, 0.1f, 0.7f);

        hudErrorText   = MakeText(errGO.transform, "",  12, FontStyles.Normal,
                                  Color.white,           new Vector2(-20, 8));
        hudErrorText.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 40);
        hudErrorText.alignment = TextAlignmentOptions.Left;

        hudErrorPoints = MakeText(errGO.transform, "",  14, FontStyles.Bold,
                                  new Color(1f, 0.4f, 0.4f, 1f), new Vector2(118, 0));
        hudErrorPoints.alignment = TextAlignmentOptions.Right;
    }

    // вв UI creation helpers вввввввввввввввввввввввввввввввввввввввввввввввв

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

        if (hudErrorGroup != null) hudErrorGroup.alpha = 0f;
        if (hudNameText   != null) hudNameText.text    = meta?.studentName ?? "вЂ”";
        if (hudScoreText  != null) hudScoreText.text   = "0 .";
        if (hudTimeText   != null) hudTimeText.text    = "0:00";

        if (hudResultText != null)
            hudResultText.text = meta == null ? "" : meta.passed
                ? "<color=#22c55e>PASSED</color>"
                : "<color=#ef4444>FAILED</color>";
    }

    void HideHUD()
    {
        if (_errorCoroutine != null) { StopCoroutine(_errorCoroutine); _errorCoroutine = null; }
        if (_hudRoot != null) { Destroy(_hudRoot); _hudRoot = null; }
    }

    IEnumerator ShowError(PenaltyData p, int accumulatedScore)
    {
        if (hudErrorGroup == null) yield break;

        //   
        if (hudErrorText != null)
        {
            string exStr = p.exerciseNum > 0 ? $". {p.exerciseNum}  вЂў  " : "";
            hudErrorText.text = $"{exStr}{p.description}";
        }
        if (hudErrorPoints != null)
            hudErrorPoints.text = $"в€’{p.points} .";

        //   
        if (hudScoreText != null)
            hudScoreText.text = $"{accumulatedScore} .";

        //  
        hudErrorGroup.gameObject.SetActive(true);
        hudErrorGroup.alpha = 0f;
        float t = 0f;
        while (t < 0.2f) { t += Time.deltaTime; hudErrorGroup.alpha = t / 0.2f; yield return null; }
        hudErrorGroup.alpha = 1f;

        yield return new WaitForSeconds(3f);

        //  
        t = 0f;
        while (t < 0.35f) { t += Time.deltaTime; hudErrorGroup.alpha = 1f - t / 0.35f; yield return null; }
        hudErrorGroup.gameObject.SetActive(false);
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
        var penalties     = meta?.penalties;
        int nextPenalty   = 0;
        int accumulatedPts = 0;

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
                while (nextPenalty < penalties.Count
                       && elapsed >= penalties[nextPenalty].t)
                {
                    var pen = penalties[nextPenalty];
                    accumulatedPts += pen.points;
                    if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
                    _errorCoroutine = StartCoroutine(ShowError(pen, accumulatedPts));
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
                Debug.Log($"[ReplayCRMSync] : ={meta?.studentName}, ={meta?.penalties?.Count ?? 0}");
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
            Debug.LogError($"[ReplayCRMSync]   : {e.Message}");
        }
    }
}
