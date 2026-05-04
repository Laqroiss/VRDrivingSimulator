using UnityEngine;
using System.Collections;

/// <summary>
/// Упражнение №1 — Старт.
/// Логика:
///   1. Перед стартом: пристегнуть ремень (клавиша B), отрегулировать зеркала (M), завести двигатель (E).
///   2. После нажатия кнопки «Старт» (или автоматически) — 20-секундный отсчёт.
///   3. Машина должна начать движение в течение 20 с (25 б.) / 30 с (100 б.).
///   4. При пересечении линии старта должен гореть левый поворотник (5 б. если нет).
///   5. В течение 10 м после линии старта — выключить левый поворотник (5 б. если нет).
/// </summary>
public class Exercise1_Start : MonoBehaviour
{
    [Header("Стартовая линия (BoxCollider trigger)")]
    public BoxCollider startLine;

    [Header("Настройки")]
    public float moveTimeout20  = 20f;  // предупреждение если нет движения
    public float moveTimeout30  = 30f;  // критично если нет движения
    public float blinkerOffDist = 10f;  // метров после старта для выключения поворотника
    public float movingSpeed    = 0.3f; // скорость «начал движение»

    private Car           _car;
    private CarIndicators _indicators;
    private Rigidbody     _rb;

    private bool _seatbeltOn   = false;
    private bool _mirrorsSet   = false;
    private bool _engineOn     = false;

    private bool _goSignalGiven  = false;
    private bool _startedMoving  = false;
    private bool _crossedStart   = false;
    private bool _exerciseDone   = false;

    private float _goTimer       = 0f;
    private bool  _penalty20Given = false;

    private Vector3 _startLinePos;

    void Start()
    {
        _car        = FindAnyObjectByType<Car>();
        _indicators = FindAnyObjectByType<CarIndicators>();
    }

    // rb получаем лениво — Car.Start() может выполниться позже нашего Start()
    void EnsureRb()
    {
        if (_rb != null) return;
        if (_car == null) _car = FindAnyObjectByType<Car>();
        if (_car != null) _rb = _car.rb != null ? _car.rb : _car.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (_exerciseDone) return;
        EnsureRb();

        HandlePreStartInput();

        if (!_goSignalGiven) return;

        // Таймер отсчёта после команды «Старт»
        if (!_startedMoving)
        {
            _goTimer += Time.deltaTime;

            bool moving = _rb != null && _rb.linearVelocity.magnitude > movingSpeed;
            if (moving)
            {
                _startedMoving = true;
            }
            else
            {
                if (!_penalty20Given && _goTimer > moveTimeout20)
                {
                    _penalty20Given = true;
                    ExamManager.Instance?.AddPenalty(
                        "Не начал движение в течение 20 секунд после сигнала «Старт»",
                        ExamManager.P1_NO_MOVEMENT_20, 1);
                }
                if (_goTimer > moveTimeout30)
                {
                    ExamManager.Instance?.AddPenalty(
                        "Не начал движение в течение 30 секунд после сигнала «Старт»",
                        ExamManager.P1_NO_MOVEMENT_30, 1);
                    ExamManager.Instance?.MarkExerciseFailed(1);
                    _exerciseDone = true;
                    return;
                }
            }
        }

        // Проверка дистанции после пересечения линии старта
        if (_crossedStart && !_exerciseDone && _indicators != null)
        {
            float dist = Vector3.Distance(
                new Vector3(_rb.position.x, 0, _rb.position.z),
                new Vector3(_startLinePos.x, 0, _startLinePos.z));

            if (dist >= blinkerOffDist)
            {
                if (_indicators.LeftIndicatorOn)
                {
                    ExamManager.Instance?.AddPenalty(
                        "Не выключил левый поворотник в течение 10 м после линии старта",
                        ExamManager.P1_BLINKER_NOT_OFF, 1);
                }
                _exerciseDone = true;
                ExamManager.Instance?.CompleteExercise(1);
            }
        }
    }

    void HandlePreStartInput()
    {
        // B — пристегнуть ремень
        if (LegacyInput.GetKeyDown(KeyCode.B) && !_seatbeltOn)
        {
            _seatbeltOn = true;
            Debug.Log("Exercise1: Ремень пристёгнут");
        }
        // M — зеркала
        if (LegacyInput.GetKeyDown(KeyCode.M) && !_mirrorsSet)
        {
            _mirrorsSet = true;
            Debug.Log("Exercise1: Зеркала отрегулированы");
        }
        // E — завести двигатель (или автоматически)
        if (LegacyInput.GetKeyDown(KeyCode.E) && !_engineOn)
        {
            _engineOn = true;
            Debug.Log("Exercise1: Двигатель запущен");
        }
    }

    /// <summary>
    /// Вызвать когда машина пересекает стартовую линию.
    /// Обычно вызывается из ExamTrigger.
    /// </summary>
    public void OnCrossStartLine()
    {
        if (_crossedStart) return;
        _crossedStart  = true;
        _goSignalGiven = true;
        EnsureRb();
        _startLinePos  = _rb != null ? _rb.position : transform.position;

        // Проверяем ремень
        if (!_seatbeltOn)
            ExamManager.Instance?.AddPenalty(
                "Не пристёгнут ремень безопасности",
                ExamManager.P1_NO_SEATBELT, 1);

        // Проверяем левый поворотник
        if (_indicators == null || !_indicators.LeftIndicatorOn)
            ExamManager.Instance?.AddPenalty(
                "Пересёк линию «Старт» с выключенным левым указателем поворота",
                ExamManager.P1_NO_LEFT_BLINKER, 1);

        Debug.Log("Exercise1: Пересёк линию старта");
    }

    /// <summary>
    /// Даёт сигнал «Старт» и запускает экзамен.
    /// Вызывается кнопкой UI или автоматически при загрузке сцены.
    /// </summary>
    public void GiveStartSignal()
    {
        if (_goSignalGiven) return;
        _goSignalGiven = true;
        _goTimer = 0f;

        ExamManager.Instance?.SetExerciseActive(1);
        ExamManager.Instance?.StartExam();
        Debug.Log("Exercise1: Сигнал «СТАРТ» дан!");
    }

    public bool SeatbeltOn => _seatbeltOn;
    public bool MirrorsSet => _mirrorsSet;
    public bool EngineOn   => _engineOn;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        if (startLine != null)
        {
            Gizmos.matrix = startLine.transform.localToWorldMatrix;
            Gizmos.DrawCube(startLine.center, startLine.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(startLine.center, startLine.size);
        }
    }
}
