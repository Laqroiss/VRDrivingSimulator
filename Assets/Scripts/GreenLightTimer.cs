using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to CheckGreenLight1/2/3/4.
/// Does nothing until activated (activated = false).
///
/// Activation modes (ActivationMode):
///   AlwaysActive        - active right away (backward compatibility)
///   OnExerciseComplete  - activates on ExamManager.OnExerciseComplete(exerciseNumber)
///   OnCheckpointPass    - activates via GreenLightActivator on a checkpoint trigger
/// </summary>
public class GreenLightTimer : MonoBehaviour
{
    public enum ActivationMode { AlwaysActive, OnExerciseComplete, OnCheckpointPass }

    [Header("Activation")]
    public ActivationMode activationMode = ActivationMode.AlwaysActive;

    [Tooltip("Exercise number (1-10) for OnExerciseComplete mode")]
    public int activateOnExercise = 1;

    [Header("Traffic light for this direction")]
    public TrafficLight linkedTrafficLight;

    // ── runtime ──────────────────────────────────────────────────────────
    private bool  _activated    = false; // whether it's allowed to react at all
    private bool  _carInZone    = false;
    private bool  _timerActive  = false;
    private float _timer        = 0f;
    private bool  _penalty20Done = false;
    private bool  _penalty30Done = false;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (activationMode == ActivationMode.AlwaysActive)
        {
            _activated = true;
        }
        else if (activationMode == ActivationMode.OnExerciseComplete)
        {
            if (ExamManager.Instance != null)
                ExamManager.Instance.OnExerciseComplete.AddListener(OnExerciseDone);
            else
                GameLog.Warn($"GreenLightTimer [{name}]: ExamManager.Instance == null in Start");
        }
        // OnCheckpointPass - activated externally via Activate()
    }

    void OnDestroy()
    {
        if (ExamManager.Instance != null)
            ExamManager.Instance.OnExerciseComplete.RemoveListener(OnExerciseDone);
    }

    void OnExerciseDone(int completedExercise)
    {
        if (!_activated && completedExercise == activateOnExercise)
        {
            _activated = true;
            ExamManager.Instance?.MarkGateActivated(gameObject.name);
            GameLog.Info($"GreenLightTimer [{name}]: activated after Ex.{completedExercise} finished");
        }
    }

    /// Called from GreenLightActivator (checkpoint) or another script
    public void Activate()
    {
        if (_activated) return;
        _activated = true;
        ExamManager.Instance?.MarkGateActivated(gameObject.name);
        GameLog.Info($"GreenLightTimer [{name}]: activated by external call");
    }

    // ── Trigger (CheckGreenLight zone) ────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!_activated) return;
        if (other.GetComponentInParent<Car>() == null) return;
        _carInZone = true;
        TryStartTimer("entry");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;
        _carInZone = false;
        // Don't stop the timer - it runs until exiting IntersectionPass
    }

    // ── Update ────────────────────────────────────────────────────────────

    void Update()
    {
        // If the car waits in the zone on red - wait until it turns green
        if (_carInZone && !_timerActive)
            TryStartTimer("light switched");

        if (_timerActive)
        {
            // Crossing time comes from the ExamManager named timer - survives Resume.
            _timer = ExamManager.Instance != null
                ? ExamManager.Instance.GetNamedTimer(gameObject.name)
                : _timer + Time.deltaTime;
            if (!_penalty20Done && _timer > 20f) Apply20Penalty();
            if (!_penalty30Done && _timer > 30f) Apply30Penalty();
        }
    }

    // ── Timer-start logic ──────────────────────────────────────────────────

    void TryStartTimer(string source)
    {
        if (_timerActive) return;

        if (linkedTrafficLight != null)
        {
            var s = linkedTrafficLight.currentState;
            bool greenOk = s == TrafficLight.LightState.Green
                        || s == TrafficLight.LightState.BlinkingGreen;
            if (!greenOk) return; // wait quietly
            StartTimer(source, s.ToString());
        }
        else
        {
            StartTimer(source, "no light");
        }
    }

    void StartTimer(string source, string lightState)
    {
        _timerActive = true;
        // Named timer in ExamManager (by object name) - on Resume it keeps the start moment,
        // so the crossing count doesn't reset. StartNamedTimer leaves a restored start untouched.
        ExamManager.Instance?.StartNamedTimer(gameObject.name);
        _timer = ExamManager.Instance != null ? ExamManager.Instance.GetNamedTimer(gameObject.name) : 0f;
        _penalty20Done = false;
        _penalty30Done = false;
        GameLog.Info($"GreenLightTimer [{name}]: timer started ({source}, light={lightState})");
    }

    // ── Called from IntersectionPassRelay when exiting the intersection zone ─

    public void OnExitIntersectionPass()
    {
        if (!_timerActive) return;

        if (ExamManager.Instance != null) _timer = ExamManager.Instance.GetNamedTimer(gameObject.name);
        GameLog.Info($"GreenLightTimer [{name}]: exited IntersectionPass - total {_timer:F1} sec");

        if (!_penalty20Done && _timer > 20f) Apply20Penalty();
        if (!_penalty30Done && _timer > 30f) Apply30Penalty();

        _timerActive = false;
        ExamManager.Instance?.StopNamedTimer(gameObject.name);
    }

    // ── Penalties ───────────────────────────────────────────────────────────

    void Apply20Penalty()
    {
        _penalty20Done = true;
        ExamManager.Instance?.AddPenaltyOnce(
            "Took more than 20 seconds to cross the regulated intersection",
            ExamManager.P3_OVERTIME_20, 3);
    }

    void Apply30Penalty()
    {
        _penalty30Done = true;
        ExamManager.Instance?.AddPenaltyOnce(
            "Took more than 30 seconds to cross the regulated intersection",
            ExamManager.P3_OVERTIME_30, 3);
    }

    // ── Debug Gizmos ──────────────────────────────────────────────────────

    public bool IsActivated  => _activated;
    public bool IsActive     => _timerActive;
    public float CurrentTime => _timer;

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Color c;
        if (!_activated)
            c = new Color(0.3f, 0.3f, 0.3f, 0.12f);       // gray   - not activated yet
        else if (_timerActive)
            c = _timer > 20f
                ? new Color(1f, 0.2f, 0.2f, 0.4f)          // red    - overtime
                : new Color(0.2f, 1f, 0.2f, 0.35f);         // green  - timer running
        else if (_carInZone)
            c = new Color(1f, 0.9f, 0.1f, 0.35f);           // yellow - car waiting for green
        else
            c = new Color(0f, 0.6f, 1f, 0.18f);             // blue   - active, waiting for the car

        Gizmos.color  = c;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(c.r, c.g, c.b, 1f);
        Gizmos.DrawWireCube(box.center, box.size);

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            string status = !_activated ? "LOCKED"
                          : _timerActive ? $"{_timer:F1}s"
                          : _carInZone   ? "waiting green"
                          : "ready";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, status);
        }
#endif
    }
}
