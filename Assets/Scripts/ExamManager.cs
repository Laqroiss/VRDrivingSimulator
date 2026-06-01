using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public class ExamManager : MonoBehaviour
{
    public static ExamManager Instance { get; private set; }

    [Header("Exam settings")]
    public float examDuration = 1200f; // 20 minutes
    public float maxSpeedKmh  = 20f;   // > 20 km/h - penalty every 5 sec

    [Header("Car reference")]
    public Car car;

    [Header("Exercise prerequisites (0 = no prerequisite)")]
    [Tooltip("For each exercise 1..10 - the exercise that must be COMPLETED before it activates. If the previous one isn't done, the next won't start.")]
    public int[] exercisePrerequisite = { 0, 0, 0, 3, 4, 5, 6, 7, 8, 0 };

    // ——— State ———
    public enum ExamState { WaitingStart, InProgress, Finished }
    public ExamState State { get; private set; } = ExamState.WaitingStart;

    // ——— 10 exercises ———
    public enum ExerciseStatus { Pending, Active, Completed, Failed }
    public ExerciseStatus[] ExerciseStatuses { get; private set; } = new ExerciseStatus[10];

    // ——— Penalty constants ———

    // Ex.1 - Start
    public const int P1_NO_MOVEMENT_30  = 100;
    public const int P1_NO_MOVEMENT_20  = 25;
    public const int P1_NO_SEATBELT     = 5;
    public const int P1_NO_LEFT_BLINKER = 5;
    public const int P1_BLINKER_NOT_OFF = 5;

    // Ex.2 - Unregulated intersections
    public const int P2_WHEEL_ON_LINE   = 20;
    public const int P2_OVERTIME        = 15;

    // Ex.3 - Regulated intersection
    public const int P3_RED_LIGHT       = 100;
    public const int P3_OVERTIME_30     = 100;
    public const int P3_OVERTIME_20     = 20;
    public const int P3_NO_BLINKER      = 5;

    // Ex.4 - Pedestrian crossing
    public const int P4_ON_MARKING      = 25;
    public const int P4_EARLY_START     = 25;
    public const int P4_NO_MOVEMENT     = 25;

    // Ex.5 - U-turn and reverse parking
    public const int P5_NO_FIXATION     = 20;
    public const int P5_WHEEL_ON_LINE   = 20;
    public const int P5_OVERTIME        = 15;

    // Ex.6 - Parallel parking
    public const int P6_NO_FIXATION     = 20;
    public const int P6_WHEEL_ON_LINE   = 20;
    public const int P6_OVERTIME        = 15;

    // Ex.7 - Railway crossing
    public const int P7_ON_STOP_LINE    = 25;
    public const int P7_EARLY_START     = 25;

    // Ex.8 - Emergency stop
    public const int P8_LATE_STOP_OR_HAZARDS = 20;
    public const int P8_HAZARDS_NOT_OFF      = 10;

    // Ex.9 - Steep hill up and down
    public const int P9_WRONG_POSITION  = 25;
    public const int P9_EARLY_START     = 25;
    public const int P9_NO_MOVEMENT     = 25;
    public const int P9_ROLLBACK        = 20;

    // Ex.10 - Finish
    public const int P10_NO_BLINKER     = 5;

    // General violations
    public const int PG_OVERTIME        = 100;
    public const int PG_SKIPPED         = 100;
    public const int PG_COLLISION       = 100;
    public const int PG_STALL           = 20;
    public const int PG_SPEED_5SEC      = 5;

    // ——— Penalty record ———
    [System.Serializable]
    public class PenaltyRecord
    {
        public string description;
        public int    points;
        public int    exerciseNum; // 1-10; 0 = general violation
    }

    public List<PenaltyRecord> Penalties           { get; private set; } = new List<PenaltyRecord>();
    public int                  TotalPenaltyPoints  { get; private set; }
    public float                ExamTimeLeft        { get; private set; }

    // Names of activated "gates" (traffic-light checkpoints, pedestrian crossing, etc.).
    // Saved to the DB and restored on resume, so intersections don't stay LOCKED.
    public List<string> ActivatedGates { get; private set; } = new List<string>();
    public void MarkGateActivated(string key)
    {
        if (!string.IsNullOrEmpty(key) && !ActivatedGates.Contains(key))
            ActivatedGates.Add(key);
    }

    // Seconds since the exam started. Survives resume, since ExamTimeLeft is restored.
    public float ExamElapsed => Mathf.Max(0f, examDuration - ExamTimeLeft);

    // Activation moment (ExamElapsed) for each exercise; -1 = not activated yet. Saved to the DB
    // and restored - so exercise timers (e.g. the 2-min parking limit) keep counting after
    // resume instead of resetting.
    public float[] ExerciseActivatedAt { get; private set; } = MakeActivatedAt();
    static float[] MakeActivatedAt() { var a = new float[10]; for (int i = 0; i < 10; i++) a[i] = -1f; return a; }

    /// <summary>Exam seconds elapsed since the exercise activated (0 if not activated yet).</summary>
    public float GetTimeSinceActivation(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10 || ExerciseActivatedAt[idx] < 0f) return 0f;
        return Mathf.Max(0f, ExamElapsed - ExerciseActivatedAt[idx]);
    }

    // Arbitrary named timers (start moment in exam seconds). For "budgets" that start later
    // than exercise activation - e.g. the regulated-intersection crossing countdown
    // (CheckGreenLight). They survive resume.
    private Dictionary<string, float> _namedTimers = new Dictionary<string, float>();

    /// <summary>Starts a named timer (if not already started/restored).</summary>
    public void StartNamedTimer(string key)
    {
        if (!string.IsNullOrEmpty(key) && !_namedTimers.ContainsKey(key))
            _namedTimers[key] = ExamElapsed;
    }
    public bool  HasNamedTimer(string key) => key != null && _namedTimers.ContainsKey(key);
    public float GetNamedTimer(string key)
        => (key != null && _namedTimers.TryGetValue(key, out var s)) ? Mathf.Max(0f, ExamElapsed - s) : 0f;
    public void  StopNamedTimer(string key) { if (key != null) _namedTimers.Remove(key); }

    public IReadOnlyDictionary<string, float> NamedTimers => _namedTimers;
    public void RestoreNamedTimers(string[] keys, float[] starts)
    {
        _namedTimers.Clear();
        if (keys == null || starts == null) return;
        int n = Mathf.Min(keys.Length, starts.Length);
        for (int i = 0; i < n; i++)
            if (!string.IsNullOrEmpty(keys[i])) _namedTimers[keys[i]] = starts[i];
    }

    private float _speedViolationTimer;
    private float _penaltyGraceUntil;   // after resume, suppress penalties while the car settles

    // ——— Events ———
    public UnityEvent             OnExamStart        = new UnityEvent();
    public UnityEvent             OnExamFinish       = new UnityEvent();
    public UnityEvent<string,int> OnPenalty          = new UnityEvent<string,int>();
    public UnityEvent<int>        OnExerciseActivate = new UnityEvent<int>();
    public UnityEvent<int>        OnExerciseComplete = new UnityEvent<int>();

    public static readonly string[] ExerciseNames =
    {
        "Ex.1  - Start",
        "Ex.2  - Turns at unregulated intersections",
        "Ex.3  - Crossing a regulated intersection",
        "Ex.4  - Pedestrian crossing",
        "Ex.5  - U-turn and reverse parking",
        "Ex.6  - Reverse parallel parking",
        "Ex.7  - Unregulated railway crossing",
        "Ex.8  - Emergency stop",
        "Ex.9  - Steep hill up and down",
        "Ex.10 - Finish"
    };

    public static string GetExerciseName(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= ExerciseNames.Length) return $"Ex.{exerciseNum}";
        return ExerciseNames[idx];
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        for (int i = 0; i < 10; i++)
            ExerciseStatuses[i] = ExerciseStatus.Pending;
    }

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<Car>();
    }

    void Update()
    {
        if (State != ExamState.InProgress) return;

        ExamTimeLeft -= Time.deltaTime;
        if (ExamTimeLeft <= 0f)
        {
            ExamTimeLeft = 0f;
            AddPenalty("Exceeded total exam time (20 minutes)", PG_OVERTIME, 0);
            FinishExam();
            return;
        }

        if (car != null && car.rb != null)
        {
            float kmh = car.rb.linearVelocity.magnitude * 3.6f;
            if (kmh > maxSpeedKmh)
            {
                _speedViolationTimer += Time.deltaTime;
                if (_speedViolationTimer >= 5f)
                {
                    _speedViolationTimer = 0f;
                    AddPenalty($"Speeding (>{maxSpeedKmh} km/h)", PG_SPEED_5SEC, 0);
                }
            }
            else
            {
                _speedViolationTimer = 0f;
            }
        }
    }

    // ——— Public methods ———

    public void StartExam()
    {
        if (State != ExamState.WaitingStart) return;
        State = ExamState.InProgress;
        ExamTimeLeft = examDuration;
        Penalties.Clear();
        TotalPenaltyPoints = 0;
        ActivatedGates.Clear();
        for (int i = 0; i < ExerciseActivatedAt.Length; i++) ExerciseActivatedAt[i] = -1f;
        _namedTimers.Clear();
        _speedViolationTimer = 0f;
        OnExamStart.Invoke();
        Debug.Log("ExamManager: Exam started!");
    }

    /// <summary>
    /// Resumes the exam from a DB snapshot: restores elapsed time, the penalty list,
    /// total points and exercise statuses, and sets the exam to InProgress.
    /// That way the tablet (StatusPanel) and route ribbon show the correct state right away.
    /// Called from ExamResume after teleporting the car to the save point.
    /// </summary>
    public void ResumeExam(float elapsedSeconds, int totalPenaltyPoints,
                           List<PenaltyRecord> penalties, ExerciseStatus[] statuses,
                           List<string> activatedGates = null, float[] activatedAt = null)
    {
        State        = ExamState.InProgress;
        ExamTimeLeft = Mathf.Max(5f, examDuration - elapsedSeconds);

        Penalties          = penalties ?? new List<PenaltyRecord>();
        TotalPenaltyPoints = totalPenaltyPoints;
        ActivatedGates     = activatedGates ?? new List<string>();

        if (activatedAt != null)
            for (int i = 0; i < ExerciseActivatedAt.Length && i < activatedAt.Length; i++)
                ExerciseActivatedAt[i] = activatedAt[i];

        if (statuses != null)
            for (int i = 0; i < ExerciseStatuses.Length && i < statuses.Length; i++)
                ExerciseStatuses[i] = statuses[i];

        _speedViolationTimer = 0f;
        _penaltyGraceUntil   = Time.time + 2.5f;   // 2.5s without penalties: the car lands and settles
        OnExamStart.Invoke();
        Debug.Log($"ExamManager: Exam RESUMED - penalty {TotalPenaltyPoints} pts, " +
                  $"{elapsedSeconds:F0}s elapsed, {ExamTimeLeft:F0}s left");
    }

    /// <summary>
    /// Whether an exercise is unlocked: its prerequisite (see exercisePrerequisite) is FINISHED -
    /// it reached a final status of Completed OR Failed (passed or not doesn't matter).
    /// While the previous one is still Pending/Active, the next won't activate.
    /// </summary>
    public bool IsExerciseUnlocked(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10) return true;
        int pre = (exercisePrerequisite != null && idx < exercisePrerequisite.Length)
                  ? exercisePrerequisite[idx] : 0;
        if (pre <= 0) return true;
        int pidx = pre - 1;
        if (pidx < 0 || pidx >= 10) return true;
        return ExerciseStatuses[pidx] == ExerciseStatus.Completed
            || ExerciseStatuses[pidx] == ExerciseStatus.Failed;
    }

    public void SetExerciseActive(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10) return;
        if (ExerciseStatuses[idx] == ExerciseStatus.Pending)
        {
            ExerciseStatuses[idx] = ExerciseStatus.Active;
            if (ExerciseActivatedAt[idx] < 0f) ExerciseActivatedAt[idx] = ExamElapsed;
            OnExerciseActivate.Invoke(exerciseNum);
            Debug.Log($"ExamManager: {GetExerciseName(exerciseNum)} - started");
        }
    }

    public void CompleteExercise(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10) return;
        if (ExerciseStatuses[idx] == ExerciseStatus.Completed) return; // already passed - ignore
        ExerciseStatuses[idx] = ExerciseStatus.Completed;
        OnExerciseComplete.Invoke(exerciseNum);
        Debug.Log($"ExamManager: {GetExerciseName(exerciseNum)} - PASSED �");
    }

    public void MarkExerciseFailed(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10) return;
        if (ExerciseStatuses[idx] != ExerciseStatus.Completed)
        {
            ExerciseStatuses[idx] = ExerciseStatus.Failed;
            Debug.LogWarning($"ExamManager: {GetExerciseName(exerciseNum)} - failed");
        }
    }

    public void AddPenalty(string description, int points, int exerciseNum)
    {
        // Grace window right after resume - don't penalize (the car teleports and settles)
        if (Time.time < _penaltyGraceUntil) return;

        Penalties.Add(new PenaltyRecord
        {
            description = description,
            points      = points,
            exerciseNum = exerciseNum
        });
        TotalPenaltyPoints += points;
        OnPenalty.Invoke(description, points);

        string prefix = exerciseNum > 0 ? GetExerciseName(exerciseNum) : "General violation";
        Debug.LogWarning($"PENALTY | {prefix} | {description} - {points} pts (total: {TotalPenaltyPoints} pts)");
    }

    /// <summary>
    /// Adds a penalty ONLY once: if a penalty with the same description and exercise already
    /// exists (including one restored from the DB on resume), it's skipped. For one-off penalties
    /// like "exercise time exceeded", so Resume doesn't add them again.
    /// </summary>
    public bool AddPenaltyOnce(string description, int points, int exerciseNum)
    {
        foreach (var p in Penalties)
            if (p.exerciseNum == exerciseNum && p.description == description) return false;
        AddPenalty(description, points, exerciseNum);
        return true;
    }

    public void AddCollision() => AddPenalty("Collision with an obstacle or another vehicle", PG_COLLISION, 0);
    public void AddStall()     => AddPenalty("Engine stalled", PG_STALL, 0);

    public void FinishExam()
    {
        if (State == ExamState.Finished) return;
        State = ExamState.Finished;

        // Penalty for skipped exercises 2-9
        int[] mandatory = { 3, 4, 5, 6, 7, 8, 9 }; // 2 removed if there are no unregulated intersections
        foreach (int n in mandatory)
            if (ExerciseStatuses[n - 1] == ExerciseStatus.Pending)
                AddPenalty($"Skipped: {GetExerciseName(n)}", PG_SKIPPED, 0);

        OnExamFinish.Invoke();
        bool passed = TotalPenaltyPoints < 100;
        Debug.Log($"ExamManager: Finish. Penalty: {TotalPenaltyPoints} pts. Result: {(passed ? "PASSED" : "FAILED")}");
    }

    // ——— Proxies for backward compatibility with old scripts ———

    public void FinishExam(bool _)             => FinishExam();
    public void AddError(string msg)           => AddPenalty(msg, PG_COLLISION, 0);
    public void StartParking(bool isParallel)  => SetExerciseActive(isParallel ? 6 : 5);
    public void CompleteParking(bool isParallel) => CompleteExercise(isParallel ? 6 : 5);
    public void CompleteRailwayCrossing()      => CompleteExercise(7);
    public void StartEmergencyStop()           => SetExerciseActive(8);
    public void CompleteEmergencyStop()        => CompleteExercise(8);

    public bool RearParkingDone     => ExerciseStatuses[4] == ExerciseStatus.Completed;
    public bool ParallelParkingDone => ExerciseStatuses[5] == ExerciseStatus.Completed;
    public bool RailwayCrossingDone => ExerciseStatuses[6] == ExerciseStatus.Completed;
    public bool EmergencyStopDone   => ExerciseStatuses[7] == ExerciseStatus.Completed;

    public float ParkingTimeUsed  { get; set; }
    public float parkingTimeLimit => 120f;

}
