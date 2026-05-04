using UnityEngine;
using System.Collections;

/// <summary>
/// Exercise 1 - Start.
/// Flow:
///   1. Before start: buckle seatbelt (B), adjust mirrors (M), start the engine (E).
///   2. After pressing "Start" (or automatically) - a 20-second countdown.
///   3. The car must start moving within 20s (25 pts) / 30s (100 pts).
///   4. The left turn signal must be on when crossing the start line (5 pts if not).
///   5. Within 10m after the start line - turn the left signal off (5 pts if not).
/// </summary>
public class Exercise1_Start : MonoBehaviour
{
    [Header("Start line (BoxCollider trigger)")]
    public BoxCollider startLine;

    [Header("Settings")]
    public float moveTimeout20  = 20f;  // warning if no movement
    public float moveTimeout30  = 30f;  // critical if no movement
    public float blinkerOffDist = 10f;  // meters after start to turn the signal off
    public float movingSpeed    = 0.3f; // "started moving" speed

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

    // Get rb lazily - Car.Start() may run after our Start()
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

        // Countdown timer after the "Start" command
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
                        "Didn't start moving within 20 seconds of the \"Start\" signal",
                        ExamManager.P1_NO_MOVEMENT_20, 1);
                }
                if (_goTimer > moveTimeout30)
                {
                    ExamManager.Instance?.AddPenalty(
                        "Didn't start moving within 30 seconds of the \"Start\" signal",
                        ExamManager.P1_NO_MOVEMENT_30, 1);
                    ExamManager.Instance?.MarkExerciseFailed(1);
                    _exerciseDone = true;
                    return;
                }
            }
        }

        // Distance check after crossing the start line
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
                        "Didn't turn off the left signal within 10m after the start line",
                        ExamManager.P1_BLINKER_NOT_OFF, 1);
                }
                _exerciseDone = true;
                ExamManager.Instance?.CompleteExercise(1);
            }
        }
    }

    void HandlePreStartInput()
    {
        // B - buckle the seatbelt
        if (LegacyInput.GetKeyDown(KeyCode.B) && !_seatbeltOn)
        {
            _seatbeltOn = true;
            Debug.Log("Exercise1: Seatbelt fastened");
        }
        // M - mirrors
        if (LegacyInput.GetKeyDown(KeyCode.M) && !_mirrorsSet)
        {
            _mirrorsSet = true;
            Debug.Log("Exercise1: Mirrors adjusted");
        }
        // E - start the engine (or automatically)
        if (LegacyInput.GetKeyDown(KeyCode.E) && !_engineOn)
        {
            _engineOn = true;
            Debug.Log("Exercise1: Engine started");
        }
    }

    /// <summary>
    /// Call when the car crosses the start line.
    /// Normally called from ExamTrigger.
    /// </summary>
    public void OnCrossStartLine()
    {
        if (_crossedStart) return;
        _crossedStart  = true;
        _goSignalGiven = true;
        EnsureRb();
        _startLinePos  = _rb != null ? _rb.position : transform.position;

        // Check the seatbelt
        if (!_seatbeltOn)
            ExamManager.Instance?.AddPenalty(
                "Seatbelt not fastened",
                ExamManager.P1_NO_SEATBELT, 1);

        // Check the left turn signal
        if (_indicators == null || !_indicators.LeftIndicatorOn)
            ExamManager.Instance?.AddPenalty(
                "Crossed the \"Start\" line with the left turn signal off",
                ExamManager.P1_NO_LEFT_BLINKER, 1);

        Debug.Log("Exercise1: Crossed the start line");
    }

    /// <summary>
    /// Gives the "Start" signal and starts the exam.
    /// Called by a UI button or automatically on scene load.
    /// </summary>
    public void GiveStartSignal()
    {
        if (_goSignalGiven) return;
        _goSignalGiven = true;
        _goTimer = 0f;

        ExamManager.Instance?.SetExerciseActive(1);
        ExamManager.Instance?.StartExam();
        Debug.Log("Exercise1: \"START\" signal given!");
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
