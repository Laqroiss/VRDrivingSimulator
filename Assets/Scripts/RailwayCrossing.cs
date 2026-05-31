using UnityEngine;
using System.Collections;

/// <summary>
/// Exercise 7 - Crossing an unregulated railway crossing.
///
/// Flow:
///   1. Car enters the zone -> must stop BEFORE the stop line.
///   2. Stop and hold for 3 seconds.
///   3. Penalties:
///       25 - drove onto or across the stop line BEFORE stopping
///       25 - started moving less than 3 seconds after stopping
///
/// Two objects:
///   - This object: the approach zone (large area before the crossing)
///   - stopLineCollider: a thin trigger on the stop line
/// </summary>
public class RailwayCrossing : MonoBehaviour
{
    [Header("Stop line (BoxCollider trigger)")]
    [Tooltip("A thin trigger right on the stop line")]
    public Collider stopLineCollider;

    [Header("Settings")]
    public float stopWaitTime  = 3f;   // how long to wait after stopping
    public float maxStopSpeed  = 0.3f; // "stopped" speed

    [Header("Activation prerequisite")]
    [Tooltip("Activate the exercise only after parallel parking (Ex.6) is done - guards against accidental triggers")]
    public bool requireParallelParkingFirst = true;

    [Header("Train object (optional)")]
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
        // Find the car's Rigidbody ahead of time
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

    // Approach zone (this object)
    void OnTriggerEnter(Collider other)
    {
        if (_completed || _active) return;
        if (other.GetComponentInParent<Car>() == null) return;

        // Don't activate until the previous exercise is FINISHED (prerequisite from ExamManager,
        // by default Ex.6 - parallel parking; passed or not doesn't matter, only that it's finished).
        if (requireParallelParkingFirst &&
            ExamManager.Instance != null && !ExamManager.Instance.IsExerciseUnlocked(7))
        {
            Debug.Log("RailwayCrossing: zone ignored - previous exercise not finished yet");
            return;
        }

        _active = true;
        if (_carRb == null) _carRb = other.GetComponentInParent<Rigidbody>();
        ExamManager.Instance?.SetExerciseActive(7);
        StartCoroutine(CheckCrossing());
        Debug.Log("RailwayCrossing: car in the crossing zone");
    }

    IEnumerator CheckCrossing()
    {
        // Wait for the stop (max 10 sec)
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
                "Drove onto or across the \"Stop\" line before stopping (Ex.7)",
                ExamManager.P7_ON_STOP_LINE, 7);
        }

        if (_carRb == null || _carRb.linearVelocity.magnitude > maxStopSpeed)
        {
            // Didn't stop at all - the stop-line penalty was already added if crossed
            _completed = true;
            ExamManager.Instance?.MarkExerciseFailed(7);
            yield break;
        }

        // Stopped - wait 3 seconds
        float standTimer = 0f;
        bool earlyStart  = false;

        while (standTimer < stopWaitTime)
        {
            if (_carRb.linearVelocity.magnitude > maxStopSpeed)
            {
                // Started moving too early
                earlyStart = true;
                break;
            }
            standTimer += Time.deltaTime;
            yield return null;
        }

        if (earlyStart)
        {
            ExamManager.Instance?.AddPenalty(
                "Started moving less than 3 seconds after stopping (Ex.7)",
                ExamManager.P7_EARLY_START, 7);
            ExamManager.Instance?.MarkExerciseFailed(7);
        }
        else
        {
            ExamManager.Instance?.CompleteExercise(7);
        }

        _completed = true;
    }

    // Called from StopLineTrigger (attach a separate script to the stop line)
    public void OnStopLineCrossed()
    {
        // The stop line only counts while the exercise is actually active -
        // otherwise it would fire on an accidental crossing before the exercise starts.
        if (!_active || _completed) return;
        if (_crossedStopLine) return;
        _crossedStopLine = true;

        if (_carRb != null && _carRb.linearVelocity.magnitude > maxStopSpeed && !_stopLinePenalty)
        {
            _stopLinePenalty = true;
            ExamManager.Instance?.AddPenalty(
                "Drove onto or across the \"Stop\" line before stopping (Ex.7)",
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
/// Helper script - attach to the crossing's stop line.
/// Notifies RailwayCrossing when the car crosses it.
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
