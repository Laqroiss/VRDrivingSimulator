using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    [Header("Autosave")]
    [Tooltip("Autosave interval for the attempt in the DB during the exam (sec). " +
             "The record is created at start and updated each interval - " +
             "even if the exam is abandoned or the app closes, the last snapshot stays in the DB.")]
    public float autosaveInterval = 5f;

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
    private bool         _wasFinished  = false;
    private string       _attemptId    = null;  // DB record _id (received when created on the server)
    private bool         _attemptBegun = false; // local attempt session started
    private bool         _creating     = false; // server-side creation in progress
    private bool         _saving       = false; // a save is in progress
    private bool         _finalDone    = false; // final save confirmed by the server
    private bool         _lastNetOk    = false; // last network request succeeded
    private string       _localGuid    = null;  // local backup filename for the current attempt
    private float        _autosaveTimer = 0f;
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

        // Resend attempts left in the local backup from previous sessions
        // (e.g. the exam ran while the server was down).
        StartCoroutine(ResendPending());
    }

    void Update()
    {
        if (_exam == null) return;

        // Record the track while the exam is running
        if (_exam.State == ExamManager.ExamState.InProgress)
        {
            // Begin the attempt session once at exam start:
            // immediately write the local backup and try to create the server record.
            if (!_attemptBegun)
                BeginAttempt();

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

            // Periodically save the current snapshot: local file first, then the server
            if (_attemptBegun && !_saving)
            {
                _autosaveTimer += Time.deltaTime;
                if (_autosaveTimer >= autosaveInterval)
                {
                    _autosaveTimer = 0f;
                    StartCoroutine(Persist(false));
                }
            }
        }

        // Exam finished - final save
        if (!_wasFinished && _exam.State == ExamManager.ExamState.Finished)
        {
            _wasFinished = true;
            StartCoroutine(SendFinal());
        }
    }

    // Emergency flush on minimize (Quest: headset removed) and on app quit -
    // synchronously write the last snapshot so nothing is lost.
    void OnApplicationPause(bool pause) { if (pause) FlushSync(); }
    void OnApplicationQuit()            { FlushSync(); }

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

    // Builds a JSON snapshot of the attempt's current state.
    // completed = true only for the final save (exam driven to the finish).
    // We don't send timestamp - Mongoose sets the date when the record is created.
    string BuildJson(bool completed)
    {
        // Collect exercise statuses as strings
        var statuses = new List<string>();
        foreach (var s in _exam.ExerciseStatuses)
            statuses.Add(s.ToString());

        // Collect penalties with the car position at the moment of the violation
        var penaltiesJson = new List<string>();
        for (int i = 0; i < _exam.Penalties.Count; i++)
        {
            var p   = _exam.Penalties[i];
            float pt = i < _penaltyTimes.Count      ? _penaltyTimes[i]     : 0f;
            var   pos = i < _penaltyPositions.Count ? _penaltyPositions[i] : Vector3.zero;
            penaltiesJson.Add(
                $"{{\"description\":\"{Escape(p.description)}\",\"points\":{p.points}," +
                $"\"exerciseNum\":{p.exerciseNum},\"t\":{F(pt)}," +
                $"\"x\":{F(pos.x)},\"z\":{F(pos.z)}}}"
            );
        }

        var trackJson = new List<string>();
        foreach (var pt in _track)
            trackJson.Add(
                $"{{\"x\":{F(pt.x)},\"z\":{F(pt.z)},\"rot\":{F(pt.rot)},\"speed\":{F(pt.speed)},\"rpm\":{F(pt.rpm)},\"t\":{F(pt.t)}}}"
            );

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

        string userId = PlayerPrefs.GetString(AuthManager.KEY_ID,    "");
        string phone  = PlayerPrefs.GetString(AuthManager.KEY_PHONE, "");

        return "{" +
            $"\"studentId\":\"{Escape(userId)}\"," +
            $"\"studentPhone\":\"{Escape(phone)}\"," +
            $"\"studentName\":\"{Escape(studentName)}\"," +
            $"\"completed\":{(completed ? "true" : "false")}," +
            $"\"passed\":{(_exam.TotalPenaltyPoints < 100 ? "true" : "false")}," +
            $"\"totalPenaltyPoints\":{_exam.TotalPenaltyPoints}," +
            $"\"examDuration\":{F(_elapsed)}," +
            $"\"exerciseStatuses\":{statusesJson}," +
            $"\"penalties\":[{string.Join(",", penaltiesJson)}]," +
            $"\"track\":[{string.Join(",", trackJson)}]," +
            $"\"lightEvents\":[{string.Join(",", lightEventsJson)}]," +
            $"\"lightPositions\":[{string.Join(",", lightPosJson)}]" +
        "}";
    }

    // тт Local backup тттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт
    // An attempt snapshot is ALWAYS written to a file on disk, even if the server is down.
    // The file is deleted only after a confirmed DB save. Unsent files are resent on the
    // next launch (ResendPending).

    [System.Serializable] class BackupWrapper { public string attemptId; public string payload; }

    static string BackupDir => Path.Combine(Application.persistentDataPath, "pending_attempts");

    // Start the attempt session: create the folder, the backup filename, and write the first snapshot.
    void BeginAttempt()
    {
        _attemptBegun = true;
        _localGuid    = System.Guid.NewGuid().ToString("N");
        try { Directory.CreateDirectory(BackupDir); }
        catch (System.Exception e) { Debug.LogWarning($"[ExamResultSender] No access to local backup: {e.Message}"); }
        SaveLocal(BuildJson(false));
        StartCoroutine(Persist(false));
    }

    string LocalPath => _localGuid != null ? Path.Combine(BackupDir, _localGuid + ".json") : null;

    // Writes a snapshot to the local file (synchronous, cheap). Also stores the current _attemptId,
    // so a resend updates the existing record instead of creating a duplicate.
    void SaveLocal(string payloadJson)
    {
        if (LocalPath == null) return;
        try
        {
            var wrapper = new BackupWrapper { attemptId = _attemptId ?? "", payload = payloadJson };
            File.WriteAllText(LocalPath, JsonUtility.ToJson(wrapper));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ExamResultSender] Local backup not written: {e.Message}");
        }
    }

    void DeleteLocal()
    {
        try { if (LocalPath != null && File.Exists(LocalPath)) File.Delete(LocalPath); }
        catch (System.Exception e) { Debug.LogWarning($"[ExamResultSender] Failed to delete backup: {e.Message}"); }
    }

    // тт Saving (local + server) ттттттттттттттттттттттттттттттттттттттттттттттттт

    // Saves the current snapshot: local file first (guaranteed), then the server.
    IEnumerator Persist(bool completed)
    {
        _saving = true;
        string json = BuildJson(completed);
        SaveLocal(json);                       // 1) local - always

        // 2) server: create the record or update the existing one
        if (string.IsNullOrEmpty(_attemptId))
        {
            if (!_creating) yield return CreateAttempt(json);
        }
        else
        {
            yield return PutAttempt(_attemptId, json);
        }

        // Rewrite the backup with the current _attemptId (if we just created the record)
        if (!string.IsNullOrEmpty(_attemptId)) SaveLocal(json);

        // If this is the final save and the server confirmed - the backup is no longer needed
        if (completed && _lastNetOk && !string.IsNullOrEmpty(_attemptId)) DeleteLocal();

        _saving = false;
    }

    // POST - creates the attempt record and remembers its _id.
    IEnumerator CreateAttempt(string json)
    {
        _creating  = true;
        _lastNetOk = false;

        var req = new UnityWebRequest(apiUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var resp   = JsonUtility.FromJson<AttemptResponse>(req.downloadHandler.text);
            _attemptId = resp?.id;
            _lastNetOk = !string.IsNullOrEmpty(_attemptId);
            Debug.Log($"[ExamResultSender] Attempt created in DB: {_attemptId}");
        }
        else
        {
            Debug.LogWarning($"[ExamResultSender] Server unreachable, attempt only in local backup: {req.error}");
        }
        _creating = false;
    }

    // PUT - updates the existing attempt record.
    IEnumerator PutAttempt(string id, string json)
    {
        _lastNetOk = false;
        var req = new UnityWebRequest($"{apiUrl}/{id}", "PUT");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            _lastNetOk = true;
        else
            Debug.LogWarning($"[ExamResultSender] DB save failed (kept in backup): {req.error}");
    }

    // Final save when the exam finishes + signal to load the replay.
    IEnumerator SendFinal()
    {
        if (_finalDone) yield break;
        if (!_attemptBegun) BeginAttempt();   // the exam may have finished instantly
        while (_saving || _creating) yield return null;  // wait for any in-flight autosave

        yield return Persist(true);

        if (_lastNetOk && !string.IsNullOrEmpty(_attemptId))
        {
            _finalDone = true;
            Debug.Log("[ExamResultSender] Final result saved to CRM");
            OnResultSent?.Invoke(_attemptId);
        }
        else
        {
            // Server unreachable - result saved locally, will resend on next launch
            Debug.LogWarning("[ExamResultSender] Final saved locally only - will resend on next launch");
        }
    }

    // Resends all local backups left over from previous sessions to the DB.
    IEnumerator ResendPending()
    {
        if (!Directory.Exists(BackupDir)) yield break;

        string[] files;
        try { files = Directory.GetFiles(BackupDir, "*.json"); }
        catch (System.Exception e) { Debug.LogWarning($"[ExamResultSender] Reading backups failed: {e.Message}"); yield break; }

        foreach (var file in files)
        {
            // Skip the current attempt's file
            if (LocalPath != null && file == LocalPath) continue;

            BackupWrapper w = null;
            try { w = JsonUtility.FromJson<BackupWrapper>(File.ReadAllText(file)); }
            catch { }

            if (w == null || string.IsNullOrEmpty(w.payload))
            {
                try { File.Delete(file); } catch { }   // corrupt file - remove it
                continue;
            }

            bool ok = false;
            var req = string.IsNullOrEmpty(w.attemptId)
                ? new UnityWebRequest(apiUrl, "POST")
                : new UnityWebRequest($"{apiUrl}/{w.attemptId}", "PUT");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(w.payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            // PUT to a missing id (record was deleted) - recreate it via POST
            if (req.result != UnityWebRequest.Result.Success &&
                !string.IsNullOrEmpty(w.attemptId) && req.responseCode == 404)
            {
                var post = new UnityWebRequest(apiUrl, "POST");
                post.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(w.payload));
                post.downloadHandler = new DownloadHandlerBuffer();
                post.SetRequestHeader("Content-Type", "application/json");
                yield return post.SendWebRequest();
                ok = post.result == UnityWebRequest.Result.Success;
            }
            else
            {
                ok = req.result == UnityWebRequest.Result.Success;
            }

            if (ok)
            {
                try { File.Delete(file); } catch { }
                Debug.Log($"[ExamResultSender] Resent backup saved to DB: {Path.GetFileName(file)}");
            }
            else
            {
                Debug.LogWarning($"[ExamResultSender] Backup {Path.GetFileName(file)} not resent - server unreachable");
            }
        }
    }

    // Synchronous emergency flush on quit/minimize (coroutines wouldn't finish in time here).
    void FlushSync()
    {
        if (_finalDone) return;
        if (_exam == null || _exam.State != ExamManager.ExamState.InProgress) return;
        if (!_attemptBegun) return;

        // 1) Local backup - guaranteed, synchronous
        string json = BuildJson(false);
        SaveLocal(json);

        // 2) Best effort to reach the server
        if (string.IsNullOrEmpty(_attemptId)) return; // no server record yet - will resend on launch
        try
        {
            using (var client = new System.Net.Http.HttpClient { Timeout = System.TimeSpan.FromSeconds(2) })
            {
                var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
                var resp = client.PutAsync($"{apiUrl}/{_attemptId}", content).Result;
                Debug.Log($"[ExamResultSender] Emergency save on exit: {resp.StatusCode}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ExamResultSender] Emergency network save failed (local backup exists): {e.Message}");
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
