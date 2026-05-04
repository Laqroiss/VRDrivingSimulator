using UnityEngine;
using System.Collections;

/// <summary>
/// Упражнение №9 — Крутой подъём и спуск.
///
/// Логика:
///   1. Машина должна остановиться на участке между линией фиксации и линией «Стоп».
///   2. Зафиксироваться на 3 секунды.
///   3. После 3 секунд — продолжить движение без отката назад > 0.2 м.
///
/// Штрафы:
///   25 б — при остановке не пересёк линию фиксации ИЛИ пересёк линию «Стоп»
///   25 б — начал движение ранее чем через 3 секунды после остановки
///   25 б — не начал движение в течение 30 секунд после остановки
///   20 б — откат > 0.2 метра
///
/// Настройка: поместить этот объект (с BoxCollider trigger) на допустимую зону остановки
/// между линией фиксации и линией «Стоп».
/// </summary>
public class HillStartExercise : MonoBehaviour
{
    [Header("Настройки")]
    public float requiredStopTime   = 3f;
    public float stopSpeedThreshold = 0.3f;
    public float maxRollbackMeters  = 0.2f;
    public float noMovementTimeout  = 30f;

    [Header("Линия «Стоп» (отдельный триггер, необязательно)")]
    [Tooltip("Если задан — пересечение этого коллайдера до остановки = штраф 25 б.")]
    public Collider stopLineCollider;

    private bool  _active         = false;
    private bool  _completed      = false;
    private bool  _carInZone      = false;
    private bool  _hasStopped     = false;
    private bool  _holdComplete   = false;

    private bool  _wrongPosPenalty  = false;
    private bool  _earlyStartPenalty = false;
    private bool  _noMovePenalty    = false;
    private bool  _rollbackPenalty  = false;
    private bool  _crossedStopLine  = false;

    private float _stopTimer       = 0f;
    private float _holdTimer       = 0f;
    private float _noMoveTimer     = 0f;

    private Rigidbody _carRb;
    private Vector3   _stopPosition;

    void Start()
    {
        Car car = FindAnyObjectByType<Car>();
        if (car != null)
        {
            _carRb = car.rb;
            if (_carRb == null) _carRb = car.GetComponentInParent<Rigidbody>();
            if (_carRb == null) _carRb = car.GetComponentInChildren<Rigidbody>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_completed) return;
        var car = other.GetComponentInParent<Car>();
        if (car == null) return;

        _carInZone = true;
        _active    = true;
        car.hillHoldAllowed = true;
        car.fullStopHold    = true;     // жёсткая фиксация — не даёт скатываться назад

        ExamManager.Instance?.SetExerciseActive(9);
        Debug.Log("HillStartExercise: машина в зоне подъёма/спуска (full stop hold ON)");
    }

    void OnTriggerExit(Collider other)
    {
        var car = other.GetComponentInParent<Car>();
        if (car == null) return;
        car.hillHoldAllowed = false;
        car.fullStopHold    = false;
        _carInZone = false;

        if (_active && !_completed)
        {
            // Выехал не остановившись — не дошёл до линии фиксации
            if (!_hasStopped && !_wrongPosPenalty)
            {
                _wrongPosPenalty = true;
                ExamManager.Instance?.AddPenalty(
                    "При остановке не пересёк линию фиксации или пересёк линию «Стоп» (Упр.9)",
                    ExamManager.P9_WRONG_POSITION, 9);
            }

            if (_holdComplete)
            {
                _completed = true;
                ExamManager.Instance?.CompleteExercise(9);
            }
        }
    }

    void Update()
    {
        if (!_active || _completed || _carRb == null) return;

        float speed = _carRb.linearVelocity.magnitude;

        // ——— Фаза 1: ждём остановки ———
        if (!_hasStopped)
        {
            if (speed <= stopSpeedThreshold)
            {
                _stopTimer += Time.deltaTime;
                if (_stopTimer >= 0.5f)
                {
                    _hasStopped   = true;
                    _stopPosition = _carRb.position;
                    _holdTimer    = 0f;
                    _noMoveTimer  = 0f;
                    Debug.Log("HillStartExercise: машина остановилась");
                }
            }
            else
            {
                _stopTimer = 0f;

                // Пересёк стоп-линию на ходу
                if (_crossedStopLine && !_wrongPosPenalty)
                {
                    _wrongPosPenalty = true;
                    ExamManager.Instance?.AddPenalty(
                        "При остановке не пересёк линию фиксации или пересёк линию «Стоп» (Упр.9)",
                        ExamManager.P9_WRONG_POSITION, 9);
                }
            }
            return;
        }

        // ——— Фаза 2: удерживаем 3 секунды ———
        if (!_holdComplete)
        {
            if (speed <= stopSpeedThreshold)
            {
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= requiredStopTime)
                {
                    _holdComplete = true;
                    _noMoveTimer  = 0f;
                    Debug.Log("HillStartExercise: 3 секунды выдержаны, можно ехать");
                }
            }
            else
            {
                // Тронулся раньше
                if (_holdTimer < requiredStopTime && !_earlyStartPenalty)
                {
                    _earlyStartPenalty = true;
                    ExamManager.Instance?.AddPenalty(
                        "Начал движение ранее чем через 3 секунды после остановки (Упр.9)",
                        ExamManager.P9_EARLY_START, 9);
                }
                _holdComplete = true; // всё равно переходим дальше
            }
            return;
        }

        // ——— Фаза 3: после фиксации — ждём движения, контроль отката ———
        if (speed > stopSpeedThreshold)
        {
            // Откат: машина движется назад от точки остановки
            float distFromStop = Vector3.Distance(_stopPosition, _carRb.position);
            Vector3 dirFromStop = (_carRb.position - _stopPosition).normalized;
            float   dotBack     = Vector3.Dot(dirFromStop, -_carRb.transform.forward);

            if (dotBack > 0.3f && distFromStop > maxRollbackMeters && !_rollbackPenalty)
            {
                _rollbackPenalty = true;
                ExamManager.Instance?.AddPenalty(
                    "Откат автомобиля назад более 0.2 метра (Упр.9)",
                    ExamManager.P9_ROLLBACK, 9);
            }

            if (!_carInZone)
            {
                _completed = true;
                ExamManager.Instance?.CompleteExercise(9);
            }
        }
        else
        {
            // Стоит — считаем таймер «не начал движение»
            _noMoveTimer += Time.deltaTime;
            if (!_noMovePenalty && _noMoveTimer > noMovementTimeout)
            {
                _noMovePenalty = true;
                ExamManager.Instance?.AddPenalty(
                    "Не начал движение в течение 30 секунд после остановки (Упр.9)",
                    ExamManager.P9_NO_MOVEMENT, 9);
            }
        }
    }

    /// <summary>Вызвать из HillStopLineTrigger когда машина пересекает стоп-линию</summary>
    public void OnStopLineCrossed()
    {
        _crossedStopLine = true;
        if (!_hasStopped && !_wrongPosPenalty)
        {
            _wrongPosPenalty = true;
            ExamManager.Instance?.AddPenalty(
                "При остановке не пересёк линию фиксации или пересёк линию «Стоп» (Упр.9)",
                ExamManager.P9_WRONG_POSITION, 9);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.7f, 0f, 0.25f);
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.7f, 0f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

/// <summary>
/// Вспомогательный скрипт — вешается на линию «Стоп» подъёма.
/// </summary>
public class HillStopLineTrigger : MonoBehaviour
{
    public HillStartExercise hillExercise;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;
        hillExercise?.OnStopLineCrossed();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
