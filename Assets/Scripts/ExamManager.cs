using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public class ExamManager : MonoBehaviour
{
    public static ExamManager Instance { get; private set; }

    [Header("Настройки экзамена")]
    public float examDuration = 1200f; // 20 минут
    public float maxSpeedKmh  = 20f;   // > 20 км/ч — штраф каждые 5 сек

    [Header("Ссылка на машину")]
    public Car car;

    // ——— Состояние ———
    public enum ExamState { WaitingStart, InProgress, Finished }
    public ExamState State { get; private set; } = ExamState.WaitingStart;

    // ——— 10 упражнений ———
    public enum ExerciseStatus { Pending, Active, Completed, Failed }
    public ExerciseStatus[] ExerciseStatuses { get; private set; } = new ExerciseStatus[10];

    // ——— Штрафные константы ———

    // Упр.1 — Старт
    public const int P1_NO_MOVEMENT_30  = 100;
    public const int P1_NO_MOVEMENT_20  = 25;
    public const int P1_NO_SEATBELT     = 5;
    public const int P1_NO_LEFT_BLINKER = 5;
    public const int P1_BLINKER_NOT_OFF = 5;

    // Упр.2 — Нерегулируемые перекрёстки
    public const int P2_WHEEL_ON_LINE   = 20;
    public const int P2_OVERTIME        = 15;

    // Упр.3 — Регулируемый перекрёсток
    public const int P3_RED_LIGHT       = 100;
    public const int P3_OVERTIME_30     = 100;
    public const int P3_OVERTIME_20     = 20;
    public const int P3_NO_BLINKER      = 5;

    // Упр.4 — Пешеходный переход
    public const int P4_ON_MARKING      = 25;
    public const int P4_EARLY_START     = 25;
    public const int P4_NO_MOVEMENT     = 25;

    // Упр.5 — Разворот и парковка задним ходом
    public const int P5_NO_FIXATION     = 20;
    public const int P5_WHEEL_ON_LINE   = 20;
    public const int P5_OVERTIME        = 15;

    // Упр.6 — Параллельная парковка
    public const int P6_NO_FIXATION     = 20;
    public const int P6_WHEEL_ON_LINE   = 20;
    public const int P6_OVERTIME        = 15;

    // Упр.7 — ЖД переезд
    public const int P7_ON_STOP_LINE    = 25;
    public const int P7_EARLY_START     = 25;

    // Упр.8 — Аварийная остановка
    public const int P8_LATE_STOP_OR_HAZARDS = 20;
    public const int P8_HAZARDS_NOT_OFF      = 10;

    // Упр.9 — Крутой подъём и спуск
    public const int P9_WRONG_POSITION  = 25;
    public const int P9_EARLY_START     = 25;
    public const int P9_NO_MOVEMENT     = 25;
    public const int P9_ROLLBACK        = 20;

    // Упр.10 — Финиш
    public const int P10_NO_BLINKER     = 5;

    // Общие нарушения
    public const int PG_OVERTIME        = 100;
    public const int PG_SKIPPED         = 100;
    public const int PG_COLLISION       = 100;
    public const int PG_STALL           = 20;
    public const int PG_SPEED_5SEC      = 5;

    // ——— Запись штрафа ———
    [System.Serializable]
    public class PenaltyRecord
    {
        public string description;
        public int    points;
        public int    exerciseNum; // 1-10; 0 = общее нарушение
    }

    public List<PenaltyRecord> Penalties           { get; private set; } = new List<PenaltyRecord>();
    public int                  TotalPenaltyPoints  { get; private set; }
    public float                ExamTimeLeft        { get; private set; }

    private float _speedViolationTimer;

    // ——— События ———
    public UnityEvent             OnExamStart        = new UnityEvent();
    public UnityEvent             OnExamFinish       = new UnityEvent();
    public UnityEvent<string,int> OnPenalty          = new UnityEvent<string,int>();
    public UnityEvent<int>        OnExerciseActivate = new UnityEvent<int>();
    public UnityEvent<int>        OnExerciseComplete = new UnityEvent<int>();

    public static readonly string[] ExerciseNames =
    {
        "Упр.1  — Старт",
        "Упр.2  — Повороты на нерег. перекрёстках",
        "Упр.3  — Проезд регулируемого перекрёстка",
        "Упр.4  — Пешеходный переход",
        "Упр.5  — Разворот и парковка задним ходом",
        "Упр.6  — Параллельная парковка задним ходом",
        "Упр.7  — Нерег. железнодорожный переезд",
        "Упр.8  — Аварийная остановка",
        "Упр.9  — Крутой подъём и спуск",
        "Упр.10 — Финиш"
    };

    public static string GetExerciseName(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= ExerciseNames.Length) return $"Упр.{exerciseNum}";
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
            AddPenalty("Превышено общее время экзамена (20 минут)", PG_OVERTIME, 0);
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
                    AddPenalty($"Превышение скорости (>{maxSpeedKmh} км/ч)", PG_SPEED_5SEC, 0);
                }
            }
            else
            {
                _speedViolationTimer = 0f;
            }
        }
    }

    // ——— Публичные методы ———

    public void StartExam()
    {
        if (State != ExamState.WaitingStart) return;
        State = ExamState.InProgress;
        ExamTimeLeft = examDuration;
        Penalties.Clear();
        TotalPenaltyPoints = 0;
        _speedViolationTimer = 0f;
        OnExamStart.Invoke();
        Debug.Log("ExamManager: Экзамен начался!");
    }

    public void SetExerciseActive(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10) return;
        if (ExerciseStatuses[idx] == ExerciseStatus.Pending)
        {
            ExerciseStatuses[idx] = ExerciseStatus.Active;
            OnExerciseActivate.Invoke(exerciseNum);
            Debug.Log($"ExamManager: {GetExerciseName(exerciseNum)} — начало");
        }
    }

    public void CompleteExercise(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10) return;
        if (ExerciseStatuses[idx] == ExerciseStatus.Completed) return; // уже зачтено — игнорируем
        ExerciseStatuses[idx] = ExerciseStatus.Completed;
        OnExerciseComplete.Invoke(exerciseNum);
        Debug.Log($"ExamManager: {GetExerciseName(exerciseNum)} — ЗАЧТЕНО ✓");
    }

    public void MarkExerciseFailed(int exerciseNum)
    {
        int idx = exerciseNum - 1;
        if (idx < 0 || idx >= 10) return;
        if (ExerciseStatuses[idx] != ExerciseStatus.Completed)
        {
            ExerciseStatuses[idx] = ExerciseStatus.Failed;
            Debug.LogWarning($"ExamManager: {GetExerciseName(exerciseNum)} — не зачтено");
        }
    }

    public void AddPenalty(string description, int points, int exerciseNum)
    {
        Penalties.Add(new PenaltyRecord
        {
            description = description,
            points      = points,
            exerciseNum = exerciseNum
        });
        TotalPenaltyPoints += points;
        OnPenalty.Invoke(description, points);

        string prefix = exerciseNum > 0 ? GetExerciseName(exerciseNum) : "Общее нарушение";
        Debug.LogWarning($"ШТРАФ | {prefix} | {description} — {points} б. (итого: {TotalPenaltyPoints} б.)");
    }

    public void AddCollision() => AddPenalty("Столкновение с препятствием или другим ТС", PG_COLLISION, 0);
    public void AddStall()     => AddPenalty("Заглох двигатель", PG_STALL, 0);

    public void FinishExam()
    {
        if (State == ExamState.Finished) return;
        State = ExamState.Finished;

        // Штраф за пропущенные упражнения 2-9
        int[] mandatory = { 3, 4, 5, 6, 7, 8, 9 }; // 2 убрано если нет нерег. перекрёстков
        foreach (int n in mandatory)
            if (ExerciseStatuses[n - 1] == ExerciseStatus.Pending)
                AddPenalty($"Пропущено: {GetExerciseName(n)}", PG_SKIPPED, 0);

        OnExamFinish.Invoke();
        bool passed = TotalPenaltyPoints < 100;
        Debug.Log($"ExamManager: Финиш. Штраф: {TotalPenaltyPoints} б. Результат: {(passed ? "СДАЛ" : "НЕ СДАЛ")}");
    }

    // ——— Прокси для обратной совместимости со старыми скриптами ———

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
