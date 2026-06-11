using UnityEngine;

/// <summary>
/// Exercise 3 - stop line at a regulated intersection.
///
/// Penalties:
///   100 - ran a red / red+yellow
///    20 - took > 20 sec to cross on a permissive signal
///   100 - same, but > 30 sec
///     5 - didn't signal while turning
///
/// Flow:
///   - Car enters the stop line
///   - If red -> record a penalty
///   - If green -> start the timer
///   - If the car waits in the trigger and the light turns green -> also start the timer
/// </summary>
public class RedLightDetector : MonoBehaviour
{
    public enum BlinkerCheck { None, Left, Right, Any }

    [Header("Linked traffic light")]
    public TrafficLight linkedTrafficLight;

    [Header("Activation - link this intersection's CheckGreenLight")]
    [Tooltip("The trigger is active only while the matching GreenLightTimer is active.\nLeave empty - always active.")]
    public GreenLightTimer linkedGreenLightTimer;

    [Header("Turn-signal check")]
    [Tooltip("Which signal must be on when crossing the intersection.\nNone - don't check.\nLeft/Right - specific.\nAny - either.")]
    public BlinkerCheck requiredBlinker = BlinkerCheck.None;

    [Header("Pedestrian exercise (TF1_Check only)")]
    public PedestrianExercise pedestrianExercise;

    private bool  _carInZone         = false;
    private bool  _timerRunning      = false;
    private bool  _exerciseStarted   = false;
    private bool  _penalizedRedLight = false;
    private bool  _penalty20Given    = false;
    private bool  _penalty30Given    = false;

    private float _timeInIntersection = 0f; // time on a permissive signal (overtime checks)
    private float _timeInZone         = 0f; // total time physically in the zone (signal check)

    private CarIndicators _indicators;
    private bool _blinkerUsed = false;

    void Start()
    {
        _indicators = FindAnyObjectByType<CarIndicators>();
    }

    void Update()
    {
        if (!_carInZone) return;

        // Turn-signal check is independent of the green-light timer: track time in the zone and
        // whether the required signal was ever on, from the moment the car enters - so crossing
        // without waiting for green (light in any state) is still evaluated for the signal.
        _timeInZone += Time.deltaTime;
        if (IsBlinkerOn()) _blinkerUsed = true;

        bool isGreen = IsGreenLight();

        // Car waits in the zone - wait for green to start the crossing timer
        if (!_timerRunning && isGreen && !_penalizedRedLight)
        {
            _timerRunning         = true;
            _timeInIntersection   = 0f;
            _penalty20Given       = false;
            _penalty30Given       = false;
            GameLog.Info("RedLightDetector: green signal - timer started");
        }

        if (!_timerRunning) return;

        _timeInIntersection += Time.deltaTime;

        if (!_penalty20Given && _timeInIntersection > 20f)
        {
            _penalty20Given = true;
            ExamManager.Instance?.AddPenaltyOnce(
                "Took more than 20 seconds to cross the regulated intersection",
                ExamManager.P3_OVERTIME_20, 3);
        }

        if (!_penalty30Given && _timeInIntersection > 30f)
        {
            _penalty30Given = true;
            ExamManager.Instance?.AddPenaltyOnce(
                "Took more than 30 seconds to cross the regulated intersection",
                ExamManager.P3_OVERTIME_30, 3);
        }
    }

    bool IsActivated => linkedGreenLightTimer == null || linkedGreenLightTimer.IsActivated;

    void OnTriggerEnter(Collider other)
    {
        if (!IsActivated) return;
        if (other.GetComponentInParent<Car>() == null) return;
        if (ExamManager.Instance != null && !ExamManager.Instance.IsExerciseUnlocked(3)) return;

        _carInZone   = true;
        _timeInZone  = 0f;
        _blinkerUsed = false;

        if (!_exerciseStarted)
        {
            _exerciseStarted = true;
            ExamManager.Instance?.SetExerciseActive(3);
        }

        // Check for red at the moment of entry
        if (IsRedLight() && !_penalizedRedLight)
        {
            _penalizedRedLight = true;
            ExamManager.Instance?.AddPenalty(
                "Entered the intersection / crossed the stop line on a prohibiting signal",
                ExamManager.P3_RED_LIGHT, 3);
            ExamManager.Instance?.MarkExerciseFailed(3);
            return;
        }

        // If already green - start the timer immediately
        if (IsGreenLight())
        {
            _timerRunning       = true;
            _timeInIntersection = 0f;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;
        if (!_carInZone) return;

        _carInZone    = false;
        _timerRunning = false;

        if (!_penalizedRedLight)
        {
            // Signal check - only if a specific one is set in the inspector (not None).
            // Uses time in the zone (not the green timer), so it fires regardless of light state.
            if (requiredBlinker != BlinkerCheck.None && _timeInZone > 1f && !_blinkerUsed)
                ExamManager.Instance?.AddPenalty(
                    $"Didn't signal ({BlinkerName()}) while turning (Ex.3)",
                    ExamManager.P3_NO_BLINKER, 3);

            if (!_penalty30Given)
                ExamManager.Instance?.CompleteExercise(3);
        }

        // Activate the pedestrian exercise (TF1_Check only)
        pedestrianExercise?.Activate();

        _penalizedRedLight = false;
    }

    bool IsBlinkerOn()
    {
        if (_indicators == null || requiredBlinker == BlinkerCheck.None) return false;
        return requiredBlinker switch
        {
            BlinkerCheck.Left  => _indicators.LeftIndicatorOn,
            BlinkerCheck.Right => _indicators.RightIndicatorOn,
            BlinkerCheck.Any   => _indicators.LeftIndicatorOn || _indicators.RightIndicatorOn,
            _                  => false
        };
    }

    string BlinkerName() => requiredBlinker switch
    {
        BlinkerCheck.Left  => "left",
        BlinkerCheck.Right => "right",
        BlinkerCheck.Any   => "any",
        _                  => ""
    };

    bool IsRedLight()
    {
        if (linkedTrafficLight == null) return false;
        return linkedTrafficLight.currentState == TrafficLight.LightState.Red ||
               linkedTrafficLight.currentState == TrafficLight.LightState.RedYellow;
    }

    bool IsGreenLight()
    {
        if (linkedTrafficLight == null) return false;
        return linkedTrafficLight.currentState == TrafficLight.LightState.Green ||
               linkedTrafficLight.currentState == TrafficLight.LightState.BlinkingGreen ||
               linkedTrafficLight.currentState == TrafficLight.LightState.Yellow;
        // Yellow counts too - the car has already started moving
    }

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        bool activated = linkedGreenLightTimer == null || linkedGreenLightTimer.IsActivated;
        Color c = !activated    ? new Color(0.3f, 0.3f, 0.3f, 0.12f)
                : _timerRunning ? new Color(0f,   1f,   0f,   0.3f)
                                : new Color(1f,   0f,   0f,   0.3f);

        Gizmos.color  = c;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);

#if UNITY_EDITOR
        if (Application.isPlaying && !activated)
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "LOCKED");
#endif
    }
}
