using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Text;

/// <summary>
/// Связывает систему повторов с CRM:
/// 1. Автоматически записывает 3D повтор во время экзамена (15fps).
/// 2. После получения ID попытки — загружает повтор в CRM.
/// 3. Слушает HTTP на порту replayPort и запускает повтор по команде из браузера.
///
/// Повесь на тот же GameObject что ExamManager и ExamResultSender.
/// Перетащи ReplaySystem в Inspector.
/// </summary>
[RequireComponent(typeof(ExamManager))]
public class ReplayCRMSync : MonoBehaviour
{
    // ── Формат одного кадра CRM-повтора ─────────────────────────────────────
    [System.Serializable]
    public class CRMFrame
    {
        public float x, y, z;           // позиция
        public float qx, qy, qz, qw;   // поворот
        public float speed, rpm;
        public int   gear;
        public bool  bl, rl, lb, rb, bp; // brake/reverse/left/right blink / blinkPhase
    }

    [System.Serializable]
    class CRMReplay
    {
        public float fps;
        public List<CRMFrame> frames;
    }

    [System.Serializable]
    class ReplayIdResponse { public string id; }

    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Ссылки")]
    public ReplaySystem replaySystem;

    [Header("CRM")]
    public string crmUrl    = "http://localhost:3000";
    public int    replayPort = 7779;
    public float  recordFPS  = 15f;

    // ── Runtime ──────────────────────────────────────────────────────────────

    private ExamManager     _exam;
    private Car             _car;
    private CarIndicators   _indicators;

    private List<CRMFrame>  _frames     = new List<CRMFrame>();
    private bool            _recording  = false;
    private float           _timer      = 0f;

    private HttpListener    _listener;
    private string          _pendingReplayId; // id загруженного но ещё не запущенного повтора
    private List<CRMFrame>  _pendingFrames;
    private float           _pendingFps;
    private bool            _launchReplay;

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _exam = GetComponent<ExamManager>();
        _car  = FindAnyObjectByType<Car>();
        if (_car != null) _indicators = _car.GetComponent<CarIndicators>();
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
            _timer += Time.deltaTime;
            if (_timer >= 1f / recordFPS)
            {
                _timer = 0f;
                RecordFrame();
            }
        }

        // Запуск повтора должен быть в главном потоке
        if (_launchReplay && _pendingFrames != null)
        {
            _launchReplay = false;
            replaySystem?.StartReplayFromCRMData(_pendingFrames, _pendingFps);
            _pendingFrames = null;
        }
    }

    // ── Запись ───────────────────────────────────────────────────────────────

    void OnExamStart()
    {
        _frames.Clear();
        _recording = true;
        _timer     = 0f;
        replaySystem?.StartRecording("Экзамен");
        Debug.Log("[ReplayCRMSync] Запись начата");
    }

    void OnExamFinish()
    {
        _recording = false;
        replaySystem?.StopRecording();
        Debug.Log($"[ReplayCRMSync] Записано кадров: {_frames.Count}");
    }

    void RecordFrame()
    {
        if (_car == null) return;
        var t = _car.transform;
        var q = t.rotation;
        _frames.Add(new CRMFrame
        {
            x = t.position.x, y = t.position.y, z = t.position.z,
            qx = q.x, qy = q.y, qz = q.z, qw = q.w,
            speed = _car.rb != null ? _car.rb.linearVelocity.magnitude * 3.6f : 0f,
            rpm   = _car.e?.getRPM()          ?? 0f,
            gear  = _car.e?.getCurrentGear()  ?? 0,
            bl    = _car.BrakeLightsOn,
            rl    = _car.ReverseLightsOn,
            lb    = _indicators != null && (_indicators.LeftIndicatorOn  || _indicators.HazardLightsOn),
            rb    = _indicators != null && (_indicators.RightIndicatorOn || _indicators.HazardLightsOn),
            bp    = _indicators != null && _indicators.BlinkVisible,
        });
    }

    // ── Загрузка в CRM ───────────────────────────────────────────────────────

    void OnResultSent(string attemptId)
    {
        if (_frames.Count == 0) return;
        StartCoroutine(UploadReplay(attemptId));
    }

    IEnumerator UploadReplay(string attemptId)
    {
        Debug.Log($"[ReplayCRMSync] Загрузка повтора для попытки {attemptId} ({_frames.Count} кадров)...");

        var replay = new CRMReplay { fps = recordFPS, frames = _frames };
        string json = JsonUtility.ToJson(replay);

        var req = new UnityWebRequest($"{crmUrl}/api/attempts/{attemptId}/replay", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[ReplayCRMSync] Повтор загружен в CRM. Размер: {json.Length / 1024} KB");
        else
            Debug.LogError($"[ReplayCRMSync] Ошибка загрузки повтора: {req.error}");
    }

    // ── HTTP-слушатель (команды из браузера) ─────────────────────────────────

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
                var query = ctx.Request.QueryString;
                string id = query["id"];

                // Ответ браузеру
                string html = "<html><body><p style='font-family:sans-serif;text-align:center;margin-top:40px'>▶ Повтор запускается в игре...</p></body></html>";
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
            string json = task.Result;

            var data = JsonUtility.FromJson<CRMReplay>(json);
            if (data?.frames == null || data.frames.Count == 0)
            {
                Debug.LogWarning("[ReplayCRMSync] Повтор не найден или пуст");
                return;
            }

            _pendingFrames = data.frames;
            _pendingFps    = data.fps > 0 ? data.fps : 15f;
            _launchReplay  = true;
            Debug.Log($"[ReplayCRMSync] Получен повтор: {data.frames.Count} кадров @ {data.fps}fps");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ReplayCRMSync] Ошибка получения повтора: {e.Message}");
        }
    }
}
