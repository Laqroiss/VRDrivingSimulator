using UnityEngine;

/// <summary>
/// Exercise 5 (reverse parking) or 6 (parallel parking).
/// Entry/exit is detected by checking the car position directly (no OnTriggerEnter/Exit).
///
/// Scene structure:
///   ParkingLine_Rear          <- parent, holds the fixation-line BoxCollider
///     RearParkingArea1        <- this object: the large trigger zone + ParkingZone script
///
/// Setup:
///   - Parking Type      = Rear / Parallel
///   - Fixation Collider = the fixation-line BoxCollider (ParkingLine_Rear)
///   - Time Limit        = 120 (2 minutes)
/// </summary>
public class ParkingZone : MonoBehaviour
{
    public enum ParkingType { Rear, Parallel }
    public enum ParallelSide { Right, Left }

    [Header("Parking type")]
    public ParkingType  parkingType  = ParkingType.Rear;
    public ParallelSide parallelSide = ParallelSide.Right;

    [Header("Time limit (seconds)")]
    public float timeLimit = 120f;

    [Header("Fixation criteria")]
    [Tooltip("How many seconds to hold the wheels on the fixation line")]
    public float holdTime        = 2.0f;
    [Tooltip("Max speed to count as \"stopped\"")]
    public float holdSpeedMax    = 0.3f;
    [Tooltip("Tolerance for going past the fixation-line collider bounds")]
    public float fixationTolerance = 0.35f;

    [Header("Fixation line (Rear - one collider)")]
    [Tooltip("For Rear parking: the fixation-line BoxCollider.")]
    public BoxCollider fixationCollider;

    [Header("Fixation lines (Parallel - up to 3 spots)")]
    [Tooltip("For Parallel parking: the car may park in ANY of these spots.")]
    public BoxCollider[] parallelFixationColliders = new BoxCollider[3];

    // ——— Private state ———

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
    private BoxCollider _zoneBounds; // zone collider (for IsCarInZone)

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
            _wheel1 = 0; // rear right
            _wheel2 = 3; // rear left
        }
        else if (parallelSide == ParallelSide.Right)
        {
            _wheel1 = 0; // rear right
            _wheel2 = 1; // front right
        }
        else
        {
            _wheel1 = 2; // front left
            _wheel2 = 3; // rear left
        }
    }

    void Update()
    {
        if (_car == null || _carRb == null || _zoneBounds == null) return;

        bool inZone = IsCarInZone();

        // ——— Entering the zone ———
        if (_phase == Phase.Idle && inZone)
        {
            // Don't start until the previous exercise is finished
            if (ExamManager.Instance != null && !ExamManager.Instance.IsExerciseUnlocked(_exNum)) return;

            _phase        = Phase.Active;
            _timer        = 0f;
            _holdTimer    = 0f;
            _parked       = false;
            _fixationMet  = false;
            _overtimeGiven = false;

            ExamManager.Instance?.SetExerciseActive(_exNum);
            Debug.Log($"ParkingZone: {ExamManager.GetExerciseName(_exNum)} - entered, timer started");
        }

        if (_phase != Phase.Active) return;

        // ——— Exercise timer ———
        // Use time since activation from ExamManager (not a local counter): it survives
        // Resume, so the 2-min limit doesn't reset if you leave at 1:59 and continue.
        _timer = ExamManager.Instance != null
            ? ExamManager.Instance.GetTimeSinceActivation(_exNum)
            : _timer + Time.deltaTime;
        if (!_overtimeGiven && _timer > timeLimit)
        {
            _overtimeGiven = true;
            // AddPenaltyOnce - so Resume (when _overtimeGiven is reset) doesn't add it again.
            ExamManager.Instance?.AddPenaltyOnce(
                $"Took more than 2 minutes on exercise {_exNum}",
                _exNum == 5 ? ExamManager.P5_OVERTIME : ExamManager.P6_OVERTIME,
                _exNum);
        }

        // ——— Fixation-line check ———
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

                    Debug.Log($"ParkingZone: {ExamManager.GetExerciseName(_exNum)} - fixed �  ({_timer:F1}s)");
                    ExamManager.Instance?.CompleteExercise(_exNum);
                }
            }
            else
            {
                _holdTimer = 0f;
            }
        }

        // ——— Left the zone without fixation - exercise FAILED ———
        if (_phase == Phase.Active && !inZone)
        {
            _phase     = Phase.Done;
            _holdTimer = 0f;
            Debug.Log($"ParkingZone: {ExamManager.GetExerciseName(_exNum)} - left the zone without fixation -> FAILED");
            ExamManager.Instance?.MarkExerciseFailed(_exNum);
        }
    }

    // ——— Helper methods ———

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
            // Check each of the three spots - parking in any one is enough
            foreach (var col in parallelFixationColliders)
            {
                if (col == null) continue;
                if (IsWheelInBox(w1, col) && IsWheelInBox(w2, col))
                    return true;
            }
            // Fallback if the array is empty
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

        // Fixation lines
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

        // Wheel positions
        if (Application.isPlaying && _car != null && _phase == Phase.Active)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color  = _fixationMet ? Color.green : Color.red;
            Gizmos.DrawSphere(_car.GetWheelPosition(_wheel1), 0.15f);
            Gizmos.DrawSphere(_car.GetWheelPosition(_wheel2), 0.15f);
        }
    }
}
