using UnityEngine;

/// <summary>
/// Exercise 4 - Pedestrian crossing.
/// Activated via Activate() from RedLightDetector (TF1_Check).
/// Entry/exit is detected by checking the car position directly - no OnTriggerEnter/Exit.
/// </summary>
public class PedestrianExercise : MonoBehaviour
{
    [Header("Settings")]
    public float requiredStopTime   = 3f;
    public float stopSpeedThreshold = 0.3f;  // below = stopped
    public float movingSpeedThreshold = 0.8f; // above = really moving (ignore wobble)
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

    /// <summary>Called from RedLightDetector when passing TF1_Check.</summary>
    public void Activate()
    {
        if (_phase != Phase.WaitingActivation) return;
        _phase = Phase.WaitingEntry;
        ExamManager.Instance?.SetExerciseActive(4);
        Debug.Log("PedestrianExercise: activated - watch for the pedestrian crossing");
    }

    void Update()
    {
        if (_phase == Phase.WaitingActivation || _phase == Phase.Done) return;
        if (_carRb == null || _zone == null) return;

        bool inZone = IsCarInZone();

        // ——— Entering the zone ———
        if (_phase == Phase.WaitingEntry && inZone)
        {
            _phase      = Phase.CarInZone;
            _hasStopped = false;
            _stopTimer  = 0f;
            _holdTimer  = 0f;
            Debug.Log("PedestrianExercise: car entered the crossing zone");
        }

        // ——— Car in the zone ———
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
                        Debug.Log("PedestrianExercise: stopped �");
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
                    // Really started moving (not just wobble) before 3 seconds
                    _earlyStartGiven = true;
                    ExamManager.Instance?.AddPenalty(
                        "Started moving less than 3 seconds after stopping (Ex.4)",
                        ExamManager.P4_EARLY_START, 4);
                }

                if (_holdTimer >= requiredStopTime && !_earlyStartGiven)
                {
                    // Stood for 3 seconds - pass immediately, no need to wait for exit
                    _phase = Phase.Done;
                    ExamManager.Instance?.CompleteExercise(4);
                    return;
                }
            }

            // ——— Exiting the zone ———
            if (!inZone)
            {
                _phase = Phase.Done;

                if (!_hasStopped)
                {
                    ExamManager.Instance?.AddPenalty(
                        "Drove onto or across the 1.14.3 marking while stopping",
                        ExamManager.P4_ON_MARKING, 4);
                    ExamManager.Instance?.MarkExerciseFailed(4);
                }
                else if (_holdTimer < requiredStopTime && !_earlyStartGiven)
                {
                    ExamManager.Instance?.AddPenalty(
                        "Started moving less than 3 seconds after stopping (Ex.4)",
                        ExamManager.P4_EARLY_START, 4);
                    ExamManager.Instance?.CompleteExercise(4);
                }
                else
                {
                    // Stood long enough - clean pass
                    ExamManager.Instance?.CompleteExercise(4);
                }
            }
        }
    }

    bool IsCarInZone()
    {
        // Convert the car position into the zone's local space
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
