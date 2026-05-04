using UnityEngine;

/// <summary>
/// Упражнение 5 (задняя парковка) или 6 (параллельная парковка).
/// Определение въезда/выезда — через прямую проверку позиции машины (без OnTriggerEnter/Exit).
///
/// Структура в сцене:
///   ParkingLine_Rear          ← родитель, содержит BoxCollider линии фиксации
///     RearParkingArea1        ← этот объект: большой триггер-зона + ParkingZone скрипт
///
/// Настройка:
///   - Parking Type      = Rear / Parallel
///   - Fixation Collider = BoxCollider линии фиксации (ParkingLine_Rear)
///   - Time Limit        = 120 (2 минуты)
/// </summary>
public class ParkingZone : MonoBehaviour
{
    public enum ParkingType { Rear, Parallel }
    public enum ParallelSide { Right, Left }

    [Header("Тип парковки")]
    public ParkingType  parkingType  = ParkingType.Rear;
    public ParallelSide parallelSide = ParallelSide.Right;

    [Header("Время на упражнение (секунды)")]
    public float timeLimit = 120f;

    [Header("Критерии фиксации")]
    [Tooltip("Сколько секунд держать колёса на линии фиксации")]
    public float holdTime        = 2.0f;
    [Tooltip("Максимальная скорость для состояния «стоит»")]
    public float holdSpeedMax    = 0.3f;
    [Tooltip("Допуск выхода за границы коллайдера линии фиксации")]
    public float fixationTolerance = 0.35f;

    [Header("Линия фиксации (Rear — один коллайдер)")]
    [Tooltip("Для Rear парковки: BoxCollider линии фиксации.")]
    public BoxCollider fixationCollider;

    [Header("Линии фиксации (Parallel — до 3 мест)")]
    [Tooltip("Для Parallel парковки: машина встаёт в ЛЮБОЕ из этих мест.")]
    public BoxCollider[] parallelFixationColliders = new BoxCollider[3];

    // ——— Приватное состояние ———

    private enum Phase { Idle, Active, Done }
    private Phase _phase = Phase.Idle;

    private int _exNum;
    private int _wheel1, _wheel2;

    private bool  _parked        = false;
    private bool  _fixationMet   = false;
    private bool  _overtimeGiven = false;

    private float _timer     = 0f;
    private float _holdTimer = 0f;

    private Car         _car;
    private Rigidbody   _carRb;
    private BoxCollider _zoneBounds; // коллайдер зоны (для IsCarInZone)

    void Start()
    {
        _car        = FindAnyObjectByType<Car>();
        _zoneBounds = GetComponent<BoxCollider>();

        if (_car != null)
        {
            _carRb = _car.rb;
            if (_carRb == null) _carRb = _car.GetComponentInParent<Rigidbody>();
            if (_carRb == null) _carRb = _car.GetComponentInChildren<Rigidbody>();
            if (_carRb == null) _carRb = FindAnyObjectByType<Rigidbody>();
        }

        _exNum = parkingType == ParkingType.Rear ? 5 : 6;


        if (parkingType == ParkingType.Rear)
        {
            _wheel1 = 0; // заднее правое
            _wheel2 = 3; // заднее левое
        }
        else if (parallelSide == ParallelSide.Right)
        {
            _wheel1 = 0; // заднее правое
            _wheel2 = 1; // переднее правое
        }
        else
        {
            _wheel1 = 2; // переднее левое
            _wheel2 = 3; // заднее левое
        }
    }

    void Update()
    {
        if (_car == null || _carRb == null || _zoneBounds == null) return;

        bool inZone = IsCarInZone();

        // ——— Въезд в зону ———
        if (_phase == Phase.Idle && inZone)
        {
            _phase        = Phase.Active;
            _timer        = 0f;
            _holdTimer    = 0f;
            _parked       = false;
            _fixationMet  = false;
            _overtimeGiven = false;

            ExamManager.Instance?.SetExerciseActive(_exNum);
            Debug.Log($"ParkingZone: {ExamManager.GetExerciseName(_exNum)} — въезд, таймер запущен");
        }

        if (_phase != Phase.Active) return;

        // ——— Таймер упражнения ———
        _timer += Time.deltaTime;
        if (!_overtimeGiven && _timer > timeLimit)
        {
            _overtimeGiven = true;
            ExamManager.Instance?.AddPenalty(
                $"Затратил на выполнение упражнения №{_exNum} более 2 минут",
                _exNum == 5 ? ExamManager.P5_OVERTIME : ExamManager.P6_OVERTIME,
                _exNum);
        }

        // ——— Проверка линии фиксации ———
        if (!_parked)
        {
            bool onLine   = CheckFixation();
            bool standing = _carRb.linearVelocity.magnitude <= holdSpeedMax;

            if (onLine && standing)
            {
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= holdTime)
                {
                    _parked      = true;
                    _fixationMet = true;
                    _phase       = Phase.Done;

                    Debug.Log($"ParkingZone: {ExamManager.GetExerciseName(_exNum)} — зафиксировано ✓  ({_timer:F1}с)");
                    ExamManager.Instance?.CompleteExercise(_exNum);
                }
            }
            else
            {
                _holdTimer = 0f;
            }
        }

        // ——— Выехал из зоны без парковки — сброс ———
        if (_phase == Phase.Active && !inZone)
        {
            Debug.Log($"ParkingZone: выехал без парковки — сброс, попробуй снова");
            _phase     = Phase.Idle;
            _holdTimer = 0f;
        }
    }

    // ——— Вспомогательные методы ———

    bool IsCarInZone()
    {
        Vector3 local = transform.InverseTransformPoint(_carRb.position) - _zoneBounds.center;
        Vector3 half  = _zoneBounds.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.z) <= half.z;
    }

    bool CheckFixation()
    {
        if (_car == null) return false;

        Vector3 w1 = _car.GetWheelPosition(_wheel1);
        Vector3 w2 = _car.GetWheelPosition(_wheel2);

        if (parkingType == ParkingType.Parallel)
        {
            // Проверяем каждое из трёх мест — достаточно встать в любое
            foreach (var col in parallelFixationColliders)
            {
                if (col == null) continue;
                if (IsWheelInBox(w1, col) && IsWheelInBox(w2, col))
                    return true;
            }
            // Запасной вариант если массив пуст
            if (fixationCollider != null)
                return IsWheelInBox(w1, fixationCollider) && IsWheelInBox(w2, fixationCollider);
            return false;
        }
        else
        {
            BoxCollider col = fixationCollider != null ? fixationCollider : _zoneBounds;
            if (col == null) return false;
            return IsWheelInBox(w1, col) && IsWheelInBox(w2, col);
        }
    }

    bool IsWheelInBox(Vector3 worldPos, BoxCollider col)
    {
        Vector3 local = col.transform.InverseTransformPoint(worldPos) - col.center;
        Vector3 half  = col.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x + fixationTolerance &&
               Mathf.Abs(local.z) <= half.z + fixationTolerance;
    }

    // ——— Gizmos ———

    void OnDrawGizmos()
    {
        if (_zoneBounds == null) _zoneBounds = GetComponent<BoxCollider>();

        Color zoneColor = _phase switch
        {
            Phase.Active => _parked ? new Color(0f, 1f, 0.3f, 0.2f) : new Color(0f, 0.5f, 1f, 0.2f),
            Phase.Done   => new Color(0.3f, 0.3f, 1f, 0.2f),
            _            => new Color(0.4f, 0.4f, 0.4f, 0.1f)
        };

        Gizmos.color  = zoneColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (_zoneBounds != null)
        {
            Gizmos.DrawCube(_zoneBounds.center, _zoneBounds.size);
            Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 1f);
            Gizmos.DrawWireCube(_zoneBounds.center, _zoneBounds.size);
        }

        // Линии фиксации
        void DrawFixation(BoxCollider col)
        {
            if (col == null) return;
            Gizmos.matrix = col.transform.localToWorldMatrix;
            Gizmos.color  = new Color(1f, 1f, 0f, 0.35f);
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(col.center, col.size);
        }

        if (parkingType == ParkingType.Parallel)
            foreach (var col in parallelFixationColliders) DrawFixation(col);
        else
            DrawFixation(fixationCollider);

        // Позиции колёс
        if (Application.isPlaying && _car != null && _phase == Phase.Active)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color  = _fixationMet ? Color.green : Color.red;
            Gizmos.DrawSphere(_car.GetWheelPosition(_wheel1), 0.15f);
            Gizmos.DrawSphere(_car.GetWheelPosition(_wheel2), 0.15f);
        }
    }
}
