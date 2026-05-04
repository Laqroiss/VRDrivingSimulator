using UnityEngine;

/// <summary>
/// Упражнение №4 — Пешеходный переход.
/// Активируется через Activate() из RedLightDetector (TF1_Check).
/// Определение въезда/выезда через прямую проверку позиции машины — без OnTriggerEnter/Exit.
/// </summary>
public class PedestrianExercise : MonoBehaviour
{
    [Header("Настройки")]
    public float requiredStopTime   = 3f;
    public float stopSpeedThreshold = 0.3f;  // ниже = стоит
    public float movingSpeedThreshold = 0.8f; // выше = реально тронулся (игнорируем покачивание)
    public float noMovementTimeout  = 30f;

    private enum Phase { WaitingActivation, WaitingEntry, CarInZone, Done }
    private Phase _phase = Phase.WaitingActivation;

    private bool  _hasStopped      = false;
    private bool  _earlyStartGiven = false;

    private float _stopTimer = 0f;
    private float _holdTimer = 0f;

    private Rigidbody   _carRb;
    private BoxCollider _zone;

    void Start()
    {
        Car car = FindAnyObjectByType<Car>();
        _carRb = car != null ? car.rb : null;
        _zone  = GetComponent<BoxCollider>();
    }

    /// <summary>Вызывается из RedLightDetector при проезде TF1_Check.</summary>
    public void Activate()
    {
        if (_phase != Phase.WaitingActivation) return;
        _phase = Phase.WaitingEntry;
        ExamManager.Instance?.SetExerciseActive(4);
        Debug.Log("PedestrianExercise: активировано — жди пешеходный переход");
    }

    void Update()
    {
        if (_phase == Phase.WaitingActivation || _phase == Phase.Done) return;
        if (_carRb == null || _zone == null) return;

        bool inZone = IsCarInZone();

        // ——— Въезд в зону ———
        if (_phase == Phase.WaitingEntry && inZone)
        {
            _phase      = Phase.CarInZone;
            _hasStopped = false;
            _stopTimer  = 0f;
            _holdTimer  = 0f;
            Debug.Log("PedestrianExercise: машина въехала в зону перехода");
        }

        // ——— Машина в зоне ———
        if (_phase == Phase.CarInZone)
        {
            float speed = _carRb.linearVelocity.magnitude;

            if (!_hasStopped)
            {
                if (speed <= stopSpeedThreshold)
                {
                    _stopTimer += Time.deltaTime;
                    if (_stopTimer >= 0.3f)
                    {
                        _hasStopped = true;
                        Debug.Log("PedestrianExercise: остановился ✓");
                    }
                }
                else _stopTimer = 0f;
            }
            else
            {
                if (speed <= stopSpeedThreshold)
                {
                    _holdTimer += Time.deltaTime;
                }
                else if (speed > movingSpeedThreshold
                         && _holdTimer < requiredStopTime
                         && !_earlyStartGiven)
                {
                    // Реально тронулся (не просто покачивание) раньше 3 секунд
                    _earlyStartGiven = true;
                    ExamManager.Instance?.AddPenalty(
                        "Начал движение ранее чем через 3 секунды после остановки (Упр.4)",
                        ExamManager.P4_EARLY_START, 4);
                }

                if (_holdTimer >= requiredStopTime && !_earlyStartGiven)
                {
                    // Простоял 3 секунды — сразу зачёт, не ждём выезда
                    _phase = Phase.Done;
                    ExamManager.Instance?.CompleteExercise(4);
                    return;
                }
            }

            // ——— Выезд из зоны ———
            if (!inZone)
            {
                _phase = Phase.Done;

                if (!_hasStopped)
                {
                    ExamManager.Instance?.AddPenalty(
                        "Наехал на разметку 1.14.3 или пересёк её при остановке",
                        ExamManager.P4_ON_MARKING, 4);
                    ExamManager.Instance?.MarkExerciseFailed(4);
                }
                else if (_holdTimer < requiredStopTime && !_earlyStartGiven)
                {
                    ExamManager.Instance?.AddPenalty(
                        "Начал движение ранее чем через 3 секунды после остановки (Упр.4)",
                        ExamManager.P4_EARLY_START, 4);
                    ExamManager.Instance?.CompleteExercise(4);
                }
                else
                {
                    // Простоял достаточно — чистый зачёт
                    ExamManager.Instance?.CompleteExercise(4);
                }
            }
        }
    }

    bool IsCarInZone()
    {
        // Переводим позицию машины в локальное пространство зоны
        Vector3 local = transform.InverseTransformPoint(_carRb.position) - _zone.center;
        Vector3 half  = _zone.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.z) <= half.z;
    }

    void OnDrawGizmos()
    {
        Color c = _phase switch
        {
            Phase.WaitingActivation => Color.grey,
            Phase.WaitingEntry      => new Color(1f, 0.85f, 0f, 1f),
            Phase.CarInZone         => new Color(0f, 1f, 0.5f, 1f),
            Phase.Done              => new Color(0.3f, 0.3f, 1f, 1f),
            _                       => Color.grey
        };

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color  = new Color(c.r, c.g, c.b, 0.2f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color  = c;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
