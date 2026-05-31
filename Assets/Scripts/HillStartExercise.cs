using UnityEngine;
using System.Collections;

/// <summary>
/// Exercise 9 - Steep hill up and down.
///
/// Flow:
///   1. Car must stop in the zone between the fixation line and the "Stop" line.
///   2. Hold for 3 seconds.
///   3. After 3 seconds - drive on without rolling back more than 0.2 m.
///
/// Penalties:
///   25 - on stopping, didn't cross the fixation line OR crossed the "Stop" line
///   25 - started moving less than 3 seconds after stopping
///   25 - didn't start moving within 30 seconds of stopping
///   20 - rolled back more than 0.2 meters
///
/// Setup: place this object (with a BoxCollider trigger) over the allowed stop zone
/// between the fixation line and the "Stop" line.
/// </summary>
public class HillStartExercise : MonoBehaviour
{
    [Header("Settings")]
    public float requiredStopTime   = 3f;
    public float stopSpeedThreshold = 0.3f;
    public float maxRollbackMeters  = 0.2f;
    public float noMovementTimeout  = 30f;

    [Header("\"Stop\" line (separate trigger, optional)")]
    [Tooltip("If set - crossing this collider before stopping = 25-pt penalty.")]
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
        if (ExamManager.Instance != null && !ExamManager.Instance.IsExerciseUnlocked(9)) return;

        _carInZone = true;
        _active    = true;
        car.hillHoldAllowed = true;
        car.fullStopHold    = true;     // hard lock - prevents rolling back

        ExamManager.Instance?.SetExerciseActive(9);
        Debug.Log("HillStartExercise: car in the hill zone (full stop hold ON)");
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
            // Left without stopping - didn't reach the fixation line
            if (!_hasStopped && !_wrongPosPenalty)
            {
                _wrongPosPenalty = true;
                ExamManager.Instance?.AddPenalty(
                    "On stopping, didn't cross the fixation line or crossed the \"Stop\" line (Ex.9)",
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

        // ——— Phase 1: wait for the stop ———
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
                    Debug.Log("HillStartExercise: car stopped");
                }
            }
            else
            {
                _stopTimer = 0f;

                // Crossed the stop line while moving
                if (_crossedStopLine && !_wrongPosPenalty)
                {
                    _wrongPosPenalty = true;
                    ExamManager.Instance?.AddPenalty(
                        "On stopping, didn't cross the fixation line or crossed the \"Stop\" line (Ex.9)",
                        ExamManager.P9_WRONG_POSITION, 9);
                }
            }
            return;
        }

        // ——— Phase 2: hold for 3 seconds ———
        if (!_holdComplete)
        {
            if (speed <= stopSpeedThreshold)
            {
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= requiredStopTime)
                {
                    _holdComplete = true;
                    _noMoveTimer  = 0f;
                    Debug.Log("HillStartExercise: 3 seconds held, OK to drive");
                }
            }
            else
            {
                // Started moving too early
                if (_holdTimer < requiredStopTime && !_earlyStartPenalty)
                {
                    _earlyStartPenalty = true;
                    ExamManager.Instance?.AddPenalty(
                        "Started moving less than 3 seconds after stopping (Ex.9)",
                        ExamManager.P9_EARLY_START, 9);
                }
                _holdComplete = true; // move on anyway
            }
            return;
        }

        // ——— Phase 3: after fixation - wait for movement, watch for rollback ———
        if (speed > stopSpeedThreshold)
        {
            // Rollback: the car moves backward from the stop point
            float distFromStop = Vector3.Distance(_stopPosition, _carRb.position);
            Vector3 dirFromStop = (_carRb.position - _stopPosition).normalized;
            float   dotBack     = Vector3.Dot(dirFromStop, -_carRb.transform.forward);

            if (dotBack > 0.3f && distFromStop > maxRollbackMeters && !_rollbackPenalty)
            {
                _rollbackPenalty = true;
                ExamManager.Instance?.AddPenalty(
                    "Car rolled back more than 0.2 meters (Ex.9)",
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
            // Stopped - count the "didn't start moving" timer
            _noMoveTimer += Time.deltaTime;
            if (!_noMovePenalty && _noMoveTimer > noMovementTimeout)
            {
                _noMovePenalty = true;
                ExamManager.Instance?.AddPenalty(
                    "Didn't start moving within 30 seconds of stopping (Ex.9)",
                    ExamManager.P9_NO_MOVEMENT, 9);
            }
        }
    }

    /// <summary>Call from HillStopLineTrigger when the car crosses the stop line</summary>
    public void OnStopLineCrossed()
    {
        _crossedStopLine = true;
        if (!_hasStopped && !_wrongPosPenalty)
        {
            _wrongPosPenalty = true;
            ExamManager.Instance?.AddPenalty(
                "On stopping, didn't cross the fixation line or crossed the \"Stop\" line (Ex.9)",
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
/// Helper script - attach to the hill's "Stop" line.
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
