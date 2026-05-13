using UnityEngine;
using System.Collections;

/// <summary>
/// Упражнение №7 — Проезд нерегулируемого железнодорожного переезда.
///
/// Логика:
///   1. Машина въезжает в зону → должна остановиться ДО стоп-линии.
///   2. Остановиться и выдержать 3 секунды.
///   3. Штрафы:
///       25 б — наехал на стоп-линию или пересёк её ДО остановки
///       25 б — тронулся раньше чем через 3 секунды после остановки
///
/// Два объекта:
///   - Этот объект: зона «подъезда» (большая зона перед переездом)
///   - stopLineCollider: тонкий триггер на стоп-линии
/// </summary>
public class RailwayCrossing : MonoBehaviour
{
    [Header("Стоп-линия (BoxCollider trigger)")]
    [Tooltip("Тонкий триггер ровно по стоп-линии")]
    public Collider stopLineCollider;

    [Header("Настройки")]
    public float stopWaitTime  = 3f;   // сколько стоять после остановки
    public float maxStopSpeed  = 0.3f; // скорость «стоит»

    [Header("Объект поезда (необязательно)")]
    public GameObject trainObject;
    public Transform  trainStart;
    public Transform  trainEnd;
    public float      trainSpeed    = 20f;
    public float      trainInterval = 12f;

    private bool  _active            = false;
    private bool  _completed         = false;
    private bool  _crossedStopLine   = false;
    private bool  _stoppedBeforeLine = false;
    private bool  _stopLinePenalty   = false;

    private Rigidbody _carRb;

    void Start()
    {
        // Ищем Rigidbody машины заранее
        Car car = FindAnyObjectByType<Car>();
        if (car != null)
        {
            _carRb = car.rb;
            if (_carRb == null) _carRb = car.GetComponentInParent<Rigidbody>();
            if (_carRb == null) _carRb = car.GetComponentInChildren<Rigidbody>();
        }

        if (trainObject != null)
            trainObject.SetActive(false);

        _trainLoopCoroutine = StartCoroutine(TrainLoop());
    }

    private Coroutine _trainLoopCoroutine;

    public void PauseTrain()
    {
        if (_trainLoopCoroutine != null) { StopCoroutine(_trainLoopCoroutine); _trainLoopCoroutine = null; }
    }

    public void ResumeTrain()
    {
        if (_trainLoopCoroutine == null) _trainLoopCoroutine = StartCoroutine(TrainLoop());
    }

    public bool  TrainActive   => trainObject != null && trainObject.activeSelf;
    public Vector3 TrainPosition => trainObject != null ? trainObject.transform.position : Vector3.zero;

    public void SetTrainState(float tx, float ty, float tz, bool active)
    {
        if (trainObject == null) return;
        trainObject.SetActive(active);
        if (active) trainObject.transform.position = new Vector3(tx, ty, tz);
    }

    // Зона подъезда (сам объект)
    void OnTriggerEnter(Collider other)
    {
        if (_completed || _active) return;
        if (other.GetComponentInParent<Car>() == null) return;

        _active = true;
        if (_carRb == null) _carRb = other.GetComponentInParent<Rigidbody>();
        ExamManager.Instance?.SetExerciseActive(7);
        StartCoroutine(CheckCrossing());
        Debug.Log("RailwayCrossing: машина в зоне переезда");
    }

    IEnumerator CheckCrossing()
    {
        // Ждём остановки (макс 10 сек)
        float elapsed = 0f;
        while (elapsed < 10f)
        {
            if (_carRb != null && _carRb.linearVelocity.magnitude <= maxStopSpeed)
            {
                _stoppedBeforeLine = !_crossedStopLine;
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_crossedStopLine && !_stopLinePenalty)
        {
            _stopLinePenalty = true;
            ExamManager.Instance?.AddPenalty(
                "Наехал на линию «Стоп» или пересёк её до остановки (Упр.7)",
                ExamManager.P7_ON_STOP_LINE, 7);
        }

        if (_carRb == null || _carRb.linearVelocity.magnitude > maxStopSpeed)
        {
            // Не остановился совсем — штраф за стоп-линию уже добавлен если пересёк
            _completed = true;
            ExamManager.Instance?.MarkExerciseFailed(7);
            yield break;
        }

        // Стоим — ждём 3 секунды
        float standTimer = 0f;
        bool earlyStart  = false;

        while (standTimer < stopWaitTime)
        {
            if (_carRb.linearVelocity.magnitude > maxStopSpeed)
            {
                // Тронулся раньше
                earlyStart = true;
                break;
            }
            standTimer += Time.deltaTime;
            yield return null;
        }

        if (earlyStart)
        {
            ExamManager.Instance?.AddPenalty(
                "Начал движение ранее чем через 3 секунды после остановки (Упр.7)",
                ExamManager.P7_EARLY_START, 7);
            ExamManager.Instance?.MarkExerciseFailed(7);
        }
        else
        {
            ExamManager.Instance?.CompleteExercise(7);
        }

        _completed = true;
    }

    // Вызывается из StopLineTrigger (вешать отдельный скрипт на стоп-линию)
    public void OnStopLineCrossed()
    {
        if (_crossedStopLine) return;
        _crossedStopLine = true;

        if (_carRb != null && _carRb.linearVelocity.magnitude > maxStopSpeed && !_stopLinePenalty)
        {
            _stopLinePenalty = true;
            ExamManager.Instance?.AddPenalty(
                "Наехал на линию «Стоп» или пересёк её до остановки (Упр.7)",
                ExamManager.P7_ON_STOP_LINE, 7);
        }
    }

    IEnumerator TrainLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(trainInterval);
            yield return StartCoroutine(RunTrain());
        }
    }

    IEnumerator RunTrain()
    {
        if (trainObject == null) yield break;

        trainObject.SetActive(true);
        if (trainStart != null)
            trainObject.transform.position = trainStart.position;

        while (trainEnd != null &&
               Vector3.Distance(trainObject.transform.position, trainEnd.position) > 0.5f)
        {
            trainObject.transform.position = Vector3.MoveTowards(
                trainObject.transform.position, trainEnd.position, trainSpeed * Time.deltaTime);
            yield return null;
        }

        trainObject.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

/// <summary>
/// Вспомогательный скрипт — вешается на стоп-линию переезда.
/// При пересечении машиной уведомляет RailwayCrossing.
/// </summary>
public class RailwayStopLineTrigger : MonoBehaviour
{
    public RailwayCrossing crossing;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;
        crossing?.OnStopLineCrossed();
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
