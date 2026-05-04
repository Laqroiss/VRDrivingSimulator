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

        StartCoroutine(TrainLoop());
    }

    // Approach zone (this object)
    void OnTriggerEnter(Collider other)
    {
        if (_completed || _active) return;
        if (other.GetComponentInParent<Car>() == null) return;

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
