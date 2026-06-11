using UnityEngine;
using System.Collections;

/// <summary>
/// Exercise 8 - Emergency stop.
/// After entering the zone, a signal is given after a random delay.
/// Required: stop within 2s + turn on hazards within 3s.
/// After the signal ends: turn off hazards and continue.
/// </summary>
public class EmergencyStop : MonoBehaviour
{
    [Header("Settings")]
    public float minDelay    = 3f;
    public float maxDelay    = 10f;
    public float resumeDelay = 5f;

    [Header("Visual signal (optional)")]
    public GameObject signalIndicator;

    [Header("Audio signal (optional)")]
    public AudioSource signalSound;
    public AudioClip   signalClip;

    [Header("Timing (tunable)")]
    [Tooltip("Max time after the signal to come to a full stop (sec). " +
             "Must allow reaction + braking from up to 20 km/h, so keep it generous.")]
    public float StopTimeLimit   = 4f;
    [Tooltip("Max time after stopping to switch on the hazard lights (sec)")]
    public float HazardTimeLimit = 3f;
    [Tooltip("Speed below which the car counts as stopped (m/s). Keep it above idle creep " +
             "so a car gently creeping in Drive still registers as stopped.")]
    public float MinStopSpeed    = 0.5f;

    [Header("Activation")]
    [Tooltip("Exercise number that must be completed before EmergencyStop (7 = railway crossing)")]
    public int activateAfterExercise = 7;

    private bool _activated = false;
    private bool _triggered  = false;
    private bool _completed  = false;

    private CarIndicators _indicators;
    private Rigidbody     _carRb;

    void Start()
    {
        Car car = FindAnyObjectByType<Car>();
        if (car != null)
        {
            _carRb = car.rb;
            if (_carRb == null) _carRb = car.GetComponentInParent<Rigidbody>();
            if (_carRb == null) _carRb = car.GetComponentInChildren<Rigidbody>();
        }
        _indicators = FindAnyObjectByType<CarIndicators>();

        if (activateAfterExercise <= 0)
        {
            _activated = true; // no prerequisite
        }
        else if (ExamManager.Instance != null)
        {
            // Already completed before start?
            if (ExamManager.Instance.ExerciseStatuses[activateAfterExercise - 1]
                    == ExamManager.ExerciseStatus.Completed)
                _activated = true;
            else
                ExamManager.Instance.OnExerciseComplete.AddListener(OnExerciseDone);
        }

        GameLog.Info($"EmergencyStop: Rigidbody={_carRb != null}, Indicators={_indicators != null}, activated={_activated}");
    }

    void OnDestroy()
    {
        if (ExamManager.Instance != null)
            ExamManager.Instance.OnExerciseComplete.RemoveListener(OnExerciseDone);
    }

    void OnExerciseDone(int exercise)
    {
        if (!_activated && exercise == activateAfterExercise)
        {
            _activated = true;
            GameLog.Info($"EmergencyStop: activated after Ex.{exercise} finished");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Active if the legacy path fired (activateAfterExercise finished)
        // OR the ExamManager prerequisite is FINISHED (passed or not doesn't matter).
        if (!_activated &&
            (ExamManager.Instance == null || !ExamManager.Instance.IsExerciseUnlocked(8)))
            return;
        if (_triggered || _completed) return;
        if (other.GetComponentInParent<Car>() == null) return;

        _triggered = true;
        GameLog.Info("EmergencyStop: car entered the zone");
        StartCoroutine(EmergencyRoutine());
    }

    IEnumerator EmergencyRoutine()
    {
        // Random delay before the signal
        float delay = Random.Range(minDelay, maxDelay);
        GameLog.Info($"EmergencyStop: signal in {delay:F1} sec...");
        yield return new WaitForSeconds(delay);

        // ——— Signal ON ———
        ExamManager.Instance?.SetExerciseActive(8);
        SetSignal(true);
        GameLog.Info("EmergencyStop: SIGNAL! Stop and turn on the hazards!");

        // Wait for the stop (max 2 sec)
        float stopTimer = 0f;
        bool  stopped   = false;

        while (stopTimer < StopTimeLimit)
        {
            if (_carRb != null && _carRb.linearVelocity.magnitude <= MinStopSpeed)
            {
                stopped = true;
                GameLog.Info("EmergencyStop: car stopped ✓");
                break;
            }
            stopTimer += Time.deltaTime;
            yield return null;
        }

        // Wait for hazards (max 3 sec)
        float hazardTimer = 0f;
        bool  hazardsOn   = false;

        while (hazardTimer < HazardTimeLimit)
        {
            if (_indicators != null && _indicators.HazardLightsOn)
            {
                hazardsOn = true;
                GameLog.Info("EmergencyStop: hazards on ✓");
                break;
            }
            hazardTimer += Time.deltaTime;
            yield return null;
        }

        if (!stopped || !hazardsOn)
        {
            string reason = !stopped
                ? "Didn't stop in time after the emergency-stop signal"
                : "Didn't switch on the hazard lights after stopping (Ex.8)";
            ExamManager.Instance?.AddPenalty(reason, ExamManager.P8_LATE_STOP_OR_HAZARDS, 8);
        }

        // Hold the signal for another resumeDelay sec
        yield return new WaitForSeconds(resumeDelay);

        // ——— Signal OFF ———
        SetSignal(false);
        GameLog.Info("EmergencyStop: signal cleared - turn off hazards and drive");

        // Wait for movement to start (max 30 sec)
        float waitTimer = 0f;
        while (waitTimer < 30f)
        {
            if (_carRb != null && _carRb.linearVelocity.magnitude > MinStopSpeed)
            {
                if (_indicators != null && _indicators.HazardLightsOn)
                    ExamManager.Instance?.AddPenalty(
                        "Didn't turn off the hazards before starting to move (Ex.8)",
                        ExamManager.P8_HAZARDS_NOT_OFF, 8);
                break;
            }
            waitTimer += Time.deltaTime;
            yield return null;
        }

        _completed = true;
        ExamManager.Instance?.CompleteExercise(8);
    }

    void SetSignal(bool on)
    {
        if (signalIndicator != null) signalIndicator.SetActive(on);
        if (signalSound != null)
        {
            if (on)
            {
                if (signalClip != null) signalSound.clip = signalClip;
                signalSound.loop = true;
                signalSound.Play();
            }
            else
            {
                signalSound.Stop();
            }
        }
    }

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Color c = !_activated  ? new Color(0.3f, 0.3f, 0.3f, 0.12f)  // gray - locked
                : _triggered   ? new Color(1f,   0f,   1f,   0.4f)    // magenta - active
                               : new Color(1f,   0f,   1f,   0.15f);  // pale - waiting

        Gizmos.color  = c;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            string status = !_activated ? $"LOCKED (waiting for Ex.{activateAfterExercise})"
                          : _triggered  ? "TRIGGERED"
                                        : "ready";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, status);
        }
#endif
    }
}
