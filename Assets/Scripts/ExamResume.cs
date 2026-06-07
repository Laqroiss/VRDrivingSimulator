using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Resumes an exam from the last checkpoint saved in the DB.
///
/// Autosave (ExamResultSender) writes an attempt snapshot to CRM every few seconds with
/// completed:false. If the game crashed or the student left before the finish, that
/// snapshot stays in the DB. Here we find it, teleport the car to the last recorded
/// position and restore penalties / points / exercise statuses and the timer, then the
/// exam continues and the same DB record keeps updating.
///
/// Setup: attach to the same object as ExamManager + ExamResultSender. MenuManager calls
/// LoadResumable(id) when an attempt is picked in the cabinet, then
/// TeleportCarToCheckpoint()/RestoreExamState() on entering the cockpit.
/// </summary>
public class ExamResume : MonoBehaviour
{
    [Header("CRM")]
    [Tooltip("CRM API base, e.g. http://localhost:3000")]
    public string crmUrl = "http://localhost:3000";

    [Header("Teleport")]
    [Tooltip("Fallback body ride height above ground for OLD records without a saved height (m)")]
    public float fallbackRideHeight = 0.4f;

    // ── /api/attempts/resume response ─────────────────────────────────────────
    [Serializable] class ResumeResponse
    {
        public bool   found;
        public string _id;
        public int    totalPenaltyPoints;
        public float  examDuration;
        public string[] exerciseStatuses;
        public string[] activatedGates;
        public float[]  exerciseActivatedAt;
        public string[] namedTimerKeys;
        public float[]  namedTimerStarts;
        public ExamResultSender.PenaltySeed[] penalties;
        public ExamResultSender.TrackSeed[]   track;
    }

    private ResumeResponse _data;

    /// <summary>Whether a resumable attempt is loaded.</summary>
    public bool HasResumable => _data != null && _data.found;

    private Car _car;
    Car GetCar() => _car != null ? _car : (_car = FindAnyObjectByType<Car>());

    // ── Loading a specific attempt to continue ──────────────────────────────────
    /// <summary>
    /// Loads the state of a specific (cabinet-selected) attempt from CRM by its id.
    /// cb(true) - the data is usable for continuing (has a track). MenuManager then
    /// calls TeleportCarToCheckpoint() and RestoreExamState().
    /// </summary>
    public void LoadResumable(string attemptId, Action<bool> cb)
    {
        _data = null;
        if (string.IsNullOrEmpty(attemptId)) { cb?.Invoke(false); return; }
        StartCoroutine(LoadRoutine(attemptId, cb));
    }

    IEnumerator LoadRoutine(string attemptId, Action<bool> cb)
    {
        using var req = UnityWebRequest.Get($"{crmUrl}/api/attempts/resume?id={UnityWebRequest.EscapeURL(attemptId)}");
        yield return req.SendWebRequest();

        bool ok = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { _data = JsonUtility.FromJson<ResumeResponse>(req.downloadHandler.text); }
            catch (Exception e) { Debug.LogWarning($"[ExamResume] Response parse: {e.Message}"); }

            ok = _data != null && _data.found && _data.track != null && _data.track.Length > 0;
            if (!ok) _data = null;
        }
        else
        {
            Debug.LogWarning($"[ExamResume] CRM unreachable while loading attempt: {req.error}");
        }

        cb?.Invoke(ok);
    }

    // ── Apply ────────────────────────────────────────────────────────────────────
    /// <summary>Places the car at the last saved position. Call before the camera flies into the cockpit.</summary>
    public bool TeleportCarToCheckpoint()
    {
        if (!HasResumable || _data.track == null || _data.track.Length == 0) return false;
        var car = GetCar();
        if (car == null) return false;

        var last = _data.track[_data.track.Length - 1];

        // New records store the exact body height (y) - place the car right where it was driving
        // (a spot known to be clear of curbs). Old records without y - on the ground at natural height.
        float y = Mathf.Abs(last.y) > 0.0001f
            ? last.y
            : GroundY(last.x, last.z, car) + RideHeight(car);

        Vector3    pos = new Vector3(last.x, y, last.z);
        Quaternion rot = Quaternion.Euler(0f, last.rot, 0f);

        if (car.rb != null)
        {
            car.rb.position        = pos;
            car.rb.rotation        = rot;
            car.rb.linearVelocity  = Vector3.zero;
            car.rb.angularVelocity = Vector3.zero;
        }
        car.transform.SetPositionAndRotation(pos, rot);

        // Sync physics with the new pose so exercise triggers fire correctly when the
        // car enters their zones after the teleport.
        Physics.SyncTransforms();

        Debug.Log($"[ExamResume] Car placed at the save point ({last.x:F1}, {last.z:F1}), heading {last.rot:F0}°");
        return true;
    }

    // Natural body height above ground - measured from the car's current position (start line in menu).
    float RideHeight(Car car)
    {
        float gy = GroundY(car.transform.position.x, car.transform.position.z, car);
        float rh = car.transform.position.y - gy;
        return (rh > 0.05f && rh < 3f) ? rh : fallbackRideHeight;
    }

    // Ground height under a point (ray downward, ignoring the car itself)
    float GroundY(float x, float z, Car car)
    {
        Vector3 from = new Vector3(x, 200f, z);
        var hits = Physics.RaycastAll(from, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        bool found = false;
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.transform.IsChildOf(car.transform)) continue;
            if (h.point.y > best) { best = h.point.y; found = true; }
        }
        return found ? best : (car.rb != null ? car.rb.position.y : car.transform.position.y);
    }

    /// <summary>Restores the exam and adopts the DB record. Call after entering the cockpit.</summary>
    public void RestoreExamState()
    {
        if (!HasResumable) return;
        if (ExamManager.Instance == null) { Debug.LogWarning("[ExamResume] No ExamManager"); return; }

        // Penalties -> ExamManager.PenaltyRecord
        var penalties = new List<ExamManager.PenaltyRecord>();
        if (_data.penalties != null)
            foreach (var p in _data.penalties)
                penalties.Add(new ExamManager.PenaltyRecord
                {
                    description = p.description,
                    points      = p.points,
                    exerciseNum = p.exerciseNum,
                });

        // Exercise statuses (strings -> enum), padded to 10
        var statuses = new ExamManager.ExerciseStatus[10];
        for (int i = 0; i < 10; i++)
        {
            statuses[i] = ExamManager.ExerciseStatus.Pending;
            if (_data.exerciseStatuses != null && i < _data.exerciseStatuses.Length &&
                Enum.TryParse(_data.exerciseStatuses[i], out ExamManager.ExerciseStatus s))
                statuses[i] = s;
        }

        var gates = _data.activatedGates != null
            ? new List<string>(_data.activatedGates)
            : new List<string>();

        ExamManager.Instance.ResumeExam(_data.examDuration, _data.totalPenaltyPoints, penalties, statuses,
                                        gates, _data.exerciseActivatedAt);
        ExamManager.Instance.RestoreNamedTimers(_data.namedTimerKeys, _data.namedTimerStarts);

        // Restore "gates" (traffic lights/pedestrian crossing) exactly as recorded in the previous
        // run - from the activated list. Intersections ahead of the spawn point aren't in the list,
        // and the car opens them itself as it drives (their checkpoints aren't passed yet).
        var gateSet = new HashSet<string>(gates);
        int opened = 0;
        if (gateSet.Count > 0)
        {
            foreach (var glt in FindObjectsByType<GreenLightTimer>(FindObjectsInactive.Include))
                if (glt != null && !glt.IsActivated && gateSet.Contains(glt.gameObject.name))
                { glt.Activate(); opened++; }

            foreach (var ped in FindObjectsByType<PedestrianExercise>(FindObjectsInactive.Include))
                if (ped != null && gateSet.Contains(ped.gameObject.name))
                    ped.Activate();
        }
        if (opened > 0) Debug.Log($"[ExamResume] Traffic-light gates restored: {opened}");

        // Keep appending to the same DB record (track + penalties stay intact)
        var sender = ExamManager.Instance.GetComponent<ExamResultSender>();
        if (sender == null) sender = FindAnyObjectByType<ExamResultSender>();
        if (sender != null)
        {
            var track = _data.track != null ? new List<ExamResultSender.TrackSeed>(_data.track) : null;
            var pens  = _data.penalties != null ? new List<ExamResultSender.PenaltySeed>(_data.penalties) : null;
            sender.AdoptResumedAttempt(_data._id, _data.examDuration, track, pens);
        }

        _data = null; // one-shot
    }
}
