using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Sends exam results to CRM after the exam finishes.
/// Attach to the same object as ExamManager.
/// Set studentName in the inspector or via PlayerPrefs (persists across sessions).
/// </summary>
public class ExamResultSender : MonoBehaviour
{
    [Header("CRM")]
    [Tooltip("CRM API URL, e.g. http://localhost:3000/api/attempts")]
    public string apiUrl = "http://localhost:3000/api/attempts";

    [Header("Student")]
    [Tooltip("Student name - set by the instructor before the exam")]
    public string studentName = "";

    [Header("Track recording")]
    [Tooltip("Position recording rate (frames/sec). 5 is enough for the 2D replay.")]
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

    /// <summary>Fires after the result is sent successfully. Passes the attempt ID.</summary>
    public static event System.Action<string> OnResultSent;

    private ExamManager  _exam;
    private Car          _car;
    private TrafficIntersection[] _lights;
    private string[]    _lastPhaseA;
    private string[]    _lastPhaseB;
    private List<LightEvent> _lightEvents    = new List<LightEvent>();
    private List<LightPos>   _lightPositions = new List<LightPos>();
    // mapping: _intersectionIds[i] = all posIds for the i-th TrafficIntersection
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

        // Take the signed-in student's data
        studentName = PlayerPrefs.GetString(AuthManager.KEY_FULL_NAME, "");
        if (string.IsNullOrEmpty(studentName))
            studentName = PlayerPrefs.GetString("StudentName", "Student");
    }

    void Start()
    {
        _lights = FindObjectsByType<TrafficIntersection>(FindObjectsInactive.Exclude);
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
        Debug.Log($"[ExamResultSender] Traffic lights found: {_lightPositions.Count}");
    }

    void Update()
    {
        if (_exam == null || _sent) return;

        // Record the track while the exam is running
        if (_exam.State == ExamManager.ExamState.InProgress)
        {
            _elapsed += Time.deltaTime;
            _trackTimer += Time.deltaTime;
            if (_trackTimer >= 1f / trackFPS)
            {
                _trackTimer = 0f;
                RecordPoint();
            }

            // Record phase-change events for each physical traffic light
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
                        // phaseA = this specific light's phase
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

            // Record the time and exact car position at each new penalty
            int count = _exam.Penalties.Count;
            while (_lastPenCount < count)
            {
                _penaltyTimes.Add(_elapsed);
                _penaltyPositions.Add(_car != null ? _car.transform.position : Vector3.zero);
                _lastPenCount++;
            }
        }

        //   — 
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
        // Collect exercise statuses as strings
        var statuses = new List<string>();
        foreach (var s in _exam.ExerciseStatuses)
            statuses.Add(s.ToString());

        // Collect penalties with the car position at the moment of the violation
        // (    track —     )
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

        string userId   = PlayerPrefs.GetString(AuthManager.KEY_ID,    "");
        string phone    = PlayerPrefs.GetString(AuthManager.KEY_PHONE, "");

        string json = "{" +
            $"\"studentId\":\"{Escape(userId)}\"," +
            $"\"studentPhone\":\"{Escape(phone)}\"," +
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
            Debug.Log($"[ExamResultSender]    CRM. : {req.downloadHandler.text}");
            var resp = JsonUtility.FromJson<AttemptResponse>(req.downloadHandler.text);
            if (!string.IsNullOrEmpty(resp?.id))
                OnResultSent?.Invoke(resp.id);
        }
        else
        {
            Debug.LogError($"[ExamResultSender]  : {req.error}");
        }
    }

    [System.Serializable] class AttemptResponse { public string id; }

static string F(float v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    // Public method - change the student from UI
    public void SetStudentName(string name)
    {
        studentName = name;
        PlayerPrefs.SetString("StudentName", name);
    }
}
