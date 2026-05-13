using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Отправляет результаты экзамена в CRM после завершения.
/// Повесь на тот же объект что и ExamManager.
/// Задай studentName в инспекторе или через PlayerPrefs (сохраняется между сессиями).
/// </summary>
public class ExamResultSender : MonoBehaviour
{
    [Header("CRM")]
    [Tooltip("URL CRM API, например http://localhost:3000/api/attempts")]
    public string apiUrl = "http://localhost:3000/api/attempts";

    [Header("Курсант")]
    [Tooltip("Имя курсанта — задаётся инструктором перед экзаменом")]
    public string studentName = "";

    [Header("Запись трека")]
    [Tooltip("Частота записи позиций (кадров/сек). 5 достаточно для 2D реплея.")]
    public float trackFPS = 5f;

    [System.Serializable] class LightEvent
    {
        public float t;
        public int   id;
        public string phaseA, phaseB;
        public float duration;
    }
    [System.Serializable] class LightPos
    {
        public int id;
        public float x, z;
    }

    private ExamManager  _exam;
    private Car          _car;
    private TrafficIntersection[] _lights;
    private string[]    _lastPhaseA;
    private string[]    _lastPhaseB;
    private List<LightEvent> _lightEvents    = new List<LightEvent>();
    private List<LightPos>   _lightPositions = new List<LightPos>();
    // маппинг: _intersectionIds[i] = все posId для i-й TrafficIntersection
    private List<int>[] _intersectionIds;
    private bool         _sent         = false;
    private bool         _wasFinished  = false;
    private float        _trackTimer   = 0f;
    private float        _elapsed      = 0f;
    private int          _lastPenCount = 0;
    private List<TrackPoint>  _track          = new List<TrackPoint>();
    private List<float>       _penaltyTimes   = new List<float>();
    private List<Vector3>     _penaltyPositions = new List<Vector3>();

    [System.Serializable] class TrackPoint
    {
        public float x, z, rot, speed, rpm, t;
    }

    void Awake()
    {
        _exam = GetComponent<ExamManager>();
        _car  = FindAnyObjectByType<Car>();

        // Загружаем имя из PlayerPrefs если не задано
        if (string.IsNullOrEmpty(studentName))
            studentName = PlayerPrefs.GetString("StudentName", "Курсант");

    }

    void Start()
    {
        _lights = FindObjectsByType<TrafficIntersection>(FindObjectsSortMode.None);
        _lastPhaseA      = new string[_lights.Length];
        _lastPhaseB      = new string[_lights.Length];
        _intersectionIds = new List<int>[_lights.Length];

        int posId = 0;
        for (int i = 0; i < _lights.Length; i++)
        {
            _lastPhaseA[i]      = "";
            _intersectionIds[i] = new List<int>();

            foreach (var tl in _lights[i].sideA)
                if (tl != null)
                {
                    _intersectionIds[i].Add(posId);
                    _lightPositions.Add(new LightPos { id = posId++, x = tl.transform.position.x, z = tl.transform.position.z });
                }
            foreach (var tl in _lights[i].sideB)
                if (tl != null)
                {
                    _intersectionIds[i].Add(posId);
                    _lightPositions.Add(new LightPos { id = posId++, x = tl.transform.position.x, z = tl.transform.position.z });
                }
        }
        Debug.Log($"[ExamResultSender] Найдено светофоров: {_lightPositions.Count}");
    }

    void Update()
    {
        if (_exam == null || _sent) return;

        // Записываем трек пока экзамен идёт
        if (_exam.State == ExamManager.ExamState.InProgress)
        {
            _elapsed += Time.deltaTime;
            _trackTimer += Time.deltaTime;
            if (_trackTimer >= 1f / trackFPS)
            {
                _trackTimer = 0f;
                RecordPoint();
            }

            // Записываем события смены фазы для каждого физического светофора
            for (int i = 0; i < _lights.Length; i++)
            {
                var l = _lights[i];
                if (l == null) continue;
                if (l.PhaseNameA != _lastPhaseA[i] || l.PhaseNameB != _lastPhaseB[i])
                {
                    _lastPhaseA[i] = l.PhaseNameA;
                    _lastPhaseB[i] = l.PhaseNameB;
                    int sideACount = l.sideA.Count;
                    for (int j = 0; j < _intersectionIds[i].Count; j++)
                    {
                        int pid  = _intersectionIds[i][j];
                        bool isA = j < sideACount;
                        // phaseA = фаза именно этого светофора
                        _lightEvents.Add(new LightEvent
                        {
                            t = _elapsed, id = pid,
                            phaseA   = isA ? l.PhaseNameA : l.PhaseNameB,
                            phaseB   = isA ? l.PhaseNameB : l.PhaseNameA,
                            duration = l.PhaseDuration
                        });
                    }
                }
            }

            // Фиксируем время и точную позицию машины в момент каждой новой ошибки
            int count = _exam.Penalties.Count;
            while (_lastPenCount < count)
            {
                _penaltyTimes.Add(_elapsed);
                _penaltyPositions.Add(_car != null ? _car.transform.position : Vector3.zero);
                _lastPenCount++;
            }
        }

        // Экзамен завершился — отправляем
        if (!_wasFinished && _exam.State == ExamManager.ExamState.Finished)
        {
            _wasFinished = true;
            StartCoroutine(SendResults());
        }
    }

    void RecordPoint()
    {
        if (_car == null) return;
        var t = _car.transform;
        _track.Add(new TrackPoint
        {
            x     = t.position.x,
            z     = t.position.z,
            rot   = t.eulerAngles.y,
            speed = _car.rb != null ? _car.rb.linearVelocity.magnitude * 3.6f : 0f,
            rpm   = _car.e.getRPM(),
            t     = _elapsed,
        });
    }

    IEnumerator SendResults()
    {
        // Собираем статусы упражнений как строки
        var statuses = new List<string>();
        foreach (var s in _exam.ExerciseStatuses)
            statuses.Add(s.ToString());

        // Собираем ошибки с позицией машины в момент нарушения
        // (позиция уже записана в track — берём ближайшую по времени точку)
        var penaltiesJson = new List<string>();
        for (int i = 0; i < _exam.Penalties.Count; i++)
        {
            var p   = _exam.Penalties[i];
            float pt = i < _penaltyTimes.Count    ? _penaltyTimes[i]         : 0f;
            var   pos = i < _penaltyPositions.Count ? _penaltyPositions[i]   : Vector3.zero;
            penaltiesJson.Add(
                $"{{\"description\":\"{Escape(p.description)}\",\"points\":{p.points}," +
                $"\"exerciseNum\":{p.exerciseNum},\"t\":{F(pt)}," +
                $"\"x\":{F(pos.x)},\"z\":{F(pos.z)}}}"
            );
        }

        var trackJson = new List<string>();
        foreach (var pt in _track)
        {
            trackJson.Add(
                $"{{\"x\":{F(pt.x)},\"z\":{F(pt.z)},\"rot\":{F(pt.rot)},\"speed\":{F(pt.speed)},\"rpm\":{F(pt.rpm)},\"t\":{F(pt.t)}}}"
            );
        }

        string statusesJson = "[\"" + string.Join("\",\"", statuses) + "\"]";

        var lightEventsJson = new List<string>();
        foreach (var e in _lightEvents)
            lightEventsJson.Add(
                $"{{\"t\":{F(e.t)},\"id\":{e.id}," +
                $"\"phaseA\":\"{e.phaseA}\",\"phaseB\":\"{e.phaseB}\"," +
                $"\"duration\":{F(e.duration)}}}"
            );

        var lightPosJson = new List<string>();
        foreach (var p in _lightPositions)
            lightPosJson.Add($"{{\"id\":{p.id},\"x\":{F(p.x)},\"z\":{F(p.z)}}}");

        string json = "{" +
            $"\"studentName\":\"{Escape(studentName)}\"," +
            $"\"timestamp\":\"{System.DateTime.UtcNow:O}\"," +
            $"\"passed\":{(_exam.TotalPenaltyPoints < 100 ? "true" : "false")}," +
            $"\"totalPenaltyPoints\":{_exam.TotalPenaltyPoints}," +
            $"\"examDuration\":{F(_elapsed)}," +
            $"\"exerciseStatuses\":{statusesJson}," +
            $"\"penalties\":[{string.Join(",", penaltiesJson)}]," +
            $"\"track\":[{string.Join(",", trackJson)}]," +
            $"\"lightEvents\":[{string.Join(",", lightEventsJson)}]," +
            $"\"lightPositions\":[{string.Join(",", lightPosJson)}]" +
        "}";

        var req = new UnityWebRequest(apiUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            _sent = true;
            Debug.Log($"[ExamResultSender] Результат отправлен в CRM. Ответ: {req.downloadHandler.text}");
        }
        else
        {
            Debug.LogError($"[ExamResultSender] Ошибка отправки: {req.error}");
        }
    }

static string F(float v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    // Публичный метод — сменить курсанта из UI
    public void SetStudentName(string name)
    {
        studentName = name;
        PlayerPrefs.SetString("StudentName", name);
    }
}
