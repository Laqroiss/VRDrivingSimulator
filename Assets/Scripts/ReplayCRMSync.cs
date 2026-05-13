using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Text;

/// <summary>
/// Полная запись и воспроизведение сцены через CRM:
/// — Машина (позиция, поворот, скорость, огни)
/// — Светофоры (фаза sideA / sideB каждого перекрёстка)
/// — Поезд (позиция, активен ли)
///
/// Привяжи к тому же GameObject что ExamManager + ExamResultSender.
/// Перетащи ReplaySystem в поле replaySystem.
/// </summary>
[RequireComponent(typeof(ExamManager))]
public class ReplayCRMSync : MonoBehaviour
{
    // ── Форматы данных ───────────────────────────────────────────────────────

    [System.Serializable]
    public class CRMFrame
    {
        // Машина
        public float x, y, z, qx, qy, qz, qw;
        public float speed, rpm;
        public int   gear;
        public bool  bl, rl, lb, rb, bp;       // brake/reverse/left/right blink/blinkPhase
        // Поезд
        public float tx, ty, tz;
        public bool  trainActive;
    }

    // Событие смены фазы светофора
    [System.Serializable]
    public class LightChange
    {
        public float t;     // время от начала экзамена
        public int   idx;   // индекс TrafficIntersection в массиве
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

    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Ссылки")]
    public ReplaySystem   replaySystem;

    [Header("CRM")]
    public string crmUrl     = "http://localhost:3000";
    public int    replayPort = 7779;
    public float  recordFPS  = 15f;

    // ── Runtime ──────────────────────────────────────────────────────────────

    private ExamManager          _exam;
    private Car                  _car;
    private CarIndicators        _indicators;
    private TrafficIntersection[] _intersections;
    private RailwayCrossing      _railway;

    // Запись
    private List<CRMFrame>    _frames      = new List<CRMFrame>();
    private List<LightChange> _lightChanges = new List<LightChange>();
    private string[]          _lastPhaseA;   // предыдущая фаза каждого перекрёстка
    private string[]          _lastPhaseB;
    private bool              _recording   = false;
    private float             _elapsed     = 0f;
    private float             _timer       = 0f;

    // Воспроизведение
    private HttpListener      _listener;
    private bool              _launchReplay;
    private CRMReplay         _pendingReplay;
    private bool              _replayRunning;
    private Coroutine         _sceneReplayCoroutine;

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _exam         = GetComponent<ExamManager>();
        _car          = FindAnyObjectByType<Car>();
        _railway      = FindAnyObjectByType<RailwayCrossing>();
        _intersections = FindObjectsByType<TrafficIntersection>(FindObjectsSortMode.None);
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

            // Записываем события смены фаз светофоров
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

        // Запуск повтора должен быть в главном потоке
        if (_launchReplay && _pendingReplay != null)
        {
            _launchReplay = false;
            StartFullReplay(_pendingReplay);
            _pendingReplay = null;
        }
    }

    // ── Запись ───────────────────────────────────────────────────────────────

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
        replaySystem?.StartRecording("Экзамен");
        Debug.Log("[ReplayCRMSync] Запись начата");
    }

    void OnExamFinish()
    {
        _recording = false;
        replaySystem?.StopRecording();
        Debug.Log($"[ReplayCRMSync] Записано кадров: {_frames.Count}, изменений светофоров: {_lightChanges.Count}");
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

        // Поезд
        if (_railway != null && _railway.trainActive)
        {
            var tp = _railway.TrainPosition;
            f.tx = tp.x; f.ty = tp.y; f.tz = tp.z;
            f.trainActive = true;
        }

        _frames.Add(f);
    }

    // ── Загрузка в CRM ───────────────────────────────────────────────────────

    void OnResultSent(string attemptId)
    {
        if (_frames.Count == 0) return;
        StartCoroutine(UploadReplay(attemptId));
    }

    IEnumerator UploadReplay(string attemptId)
    {
        var replay = new CRMReplay { fps = recordFPS, frames = _frames, lightChanges = _lightChanges };
        string json = JsonUtility.ToJson(replay);
        Debug.Log($"[ReplayCRMSync] Загрузка повтора ({_frames.Count} кадров, {json.Length / 1024} KB)...");

        var req = new UnityWebRequest($"{crmUrl}/api/attempts/{attemptId}/replay", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("[ReplayCRMSync] Повтор загружен в CRM");
        else
            Debug.LogError($"[ReplayCRMSync] Ошибка загрузки: {req.error}");
    }

    // ── Воспроизведение сцены ─────────────────────────────────────────────────

    void StartFullReplay(CRMReplay replay)
    {
        if (_sceneReplayCoroutine != null) StopCoroutine(_sceneReplayCoroutine);

        // Машина — через ReplaySystem
        replaySystem?.StartReplayFromCRMData(replay.frames, replay.fps);

        // Сцена — отдельная корутина
        _replayRunning = true;
        _sceneReplayCoroutine = StartCoroutine(SceneReplayRoutine(replay));
    }

    IEnumerator SceneReplayRoutine(CRMReplay replay)
    {
        // Останавливаем автоматику сцены
        foreach (var ti in _intersections) ti?.StopCycle();
        _railway?.PauseTrain();

        float startTime = Time.time;
        float duration  = replay.frames.Count / replay.fps;

        while (_replayRunning)
        {
            float elapsed = Time.time - startTime;
            if (elapsed >= duration) break;

            int frameIdx = Mathf.Clamp(Mathf.FloorToInt(elapsed * replay.fps), 0, replay.frames.Count - 1);
            var frame = replay.frames[frameIdx];

            // Светофоры — восстанавливаем по последнему событию до текущего времени
            for (int i = 0; i < _intersections.Length; i++)
            {
                if (_intersections[i] == null) continue;
                string pA = null, pB = null;
                foreach (var lc in replay.lightChanges)
                {
                    if (lc.idx == i && lc.t <= elapsed) { pA = lc.pA; pB = lc.pB; }
                }
                if (pA != null) _intersections[i].ForcePhase(pA, pB);
            }

            // Поезд
            _railway?.SetTrainState(frame.tx, frame.ty, frame.tz, frame.trainActive);

            yield return null;
        }

        // Возобновляем автоматику
        foreach (var ti in _intersections) ti?.ResumeCycle();
        _railway?.ResumeTrain();
        _replayRunning = false;

        Debug.Log("[ReplayCRMSync] Воспроизведение сцены завершено");
    }

    // ── HTTP-слушатель ────────────────────────────────────────────────────────

    void StartHTTPListener()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{replayPort}/");
            _listener.Start();
            ThreadPool.QueueUserWorkItem(_ => ListenLoop());
            Debug.Log($"[ReplayCRMSync] Слушаю replay-команды на порту {replayPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ReplayCRMSync] Не удалось запустить HTTP слушатель: {e.Message}");
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

                string html = "<html><body style='font-family:sans-serif;text-align:center;padding:40px'><h2>▶ Повтор запускается...</h2><p>Можете закрыть это окно</p></body></html>";
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
            var task   = client.GetStringAsync($"{crmUrl}/api/attempts/{attemptId}/replay");
            task.Wait();
            var replay = JsonUtility.FromJson<CRMReplay>(task.Result);
            if (replay?.frames == null || replay.frames.Count == 0)
            { Debug.LogWarning("[ReplayCRMSync] Повтор пуст"); return; }

            _pendingReplay = replay;
            _launchReplay  = true;
            Debug.Log($"[ReplayCRMSync] Получен повтор: {replay.frames.Count} кадров, {replay.lightChanges?.Count ?? 0} событий светофоров");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ReplayCRMSync] Ошибка получения: {e.Message}");
        }
    }
}
