using UnityEngine;
using System.Collections;

/// <summary>
/// Упражнение №8 — Аварийная остановка.
/// После въезда в зону через случайное время подаётся сигнал.
/// Нужно: остановиться за 2 сек + включить аварийку за 3 сек.
/// После отключения сигнала: выключить аварийку и продолжить.
/// </summary>
public class EmergencyStop : MonoBehaviour
{
    [Header("Настройки")]
    public float minDelay    = 3f;
    public float maxDelay    = 10f;
    public float resumeDelay = 5f;

    [Header("Визуальный сигнал (опционально)")]
    public GameObject signalIndicator;

    [Header("Звуковой сигнал (опционально)")]
    public AudioSource signalSound;
    public AudioClip   signalClip;

    private const float StopTimeLimit   = 2f;
    private const float HazardTimeLimit = 3f;
    private const float MinStopSpeed    = 0.3f;

    [Header("Активация")]
    [Tooltip("Номер упражнения, которое должно быть выполнено перед EmergencyStop (7 = ЖД переезд)")]
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
            _activated = true; // без ограничений
        }
        else if (ExamManager.Instance != null)
        {
            // Уже выполнено до старта?
            if (ExamManager.Instance.ExerciseStatuses[activateAfterExercise - 1]
                    == ExamManager.ExerciseStatus.Completed)
                _activated = true;
            else
                ExamManager.Instance.OnExerciseComplete.AddListener(OnExerciseDone);
        }

        Debug.Log($"EmergencyStop: Rigidbody={_carRb != null}, Indicators={_indicators != null}, activated={_activated}");
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
            Debug.Log($"EmergencyStop: активирован после завершения Упр.{exercise}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_activated) return;
        if (_triggered || _completed) return;
        if (other.GetComponentInParent<Car>() == null) return;

        _triggered = true;
        Debug.Log("EmergencyStop: машина въехала в зону");
        StartCoroutine(EmergencyRoutine());
    }

    IEnumerator EmergencyRoutine()
    {
        // Случайная задержка перед сигналом
        float delay = Random.Range(minDelay, maxDelay);
        Debug.Log($"EmergencyStop: сигнал через {delay:F1} сек...");
        yield return new WaitForSeconds(delay);

        // ——— Сигнал ВКЛ ———
        ExamManager.Instance?.SetExerciseActive(8);
        SetSignal(true);
        Debug.Log("EmergencyStop: СИГНАЛ! Остановись и включи аварийку!");

        // Ждём остановки (макс 2 сек)
        float stopTimer = 0f;
        bool  stopped   = false;

        while (stopTimer < StopTimeLimit)
        {
            if (_carRb != null && _carRb.linearVelocity.magnitude <= MinStopSpeed)
            {
                stopped = true;
                Debug.Log("EmergencyStop: машина остановилась ✓");
                break;
            }
            stopTimer += Time.deltaTime;
            yield return null;
        }

        // Ждём аварийки (макс 3 сек)
        float hazardTimer = 0f;
        bool  hazardsOn   = false;

        while (hazardTimer < HazardTimeLimit)
        {
            if (_indicators != null && _indicators.HazardLightsOn)
            {
                hazardsOn = true;
                Debug.Log("EmergencyStop: аварийка включена ✓");
                break;
            }
            hazardTimer += Time.deltaTime;
            yield return null;
        }

        if (!stopped || !hazardsOn)
        {
            ExamManager.Instance?.AddPenalty(
                "Не остановился за 2 сек или не включил аварийку за 3 сек после остановки",
                ExamManager.P8_LATE_STOP_OR_HAZARDS, 8);
        }

        // Держим сигнал ещё resumeDelay сек
        yield return new WaitForSeconds(resumeDelay);

        // ——— Сигнал ВЫКЛ ———
        SetSignal(false);
        Debug.Log("EmergencyStop: сигнал отключён — выключи аварийку и езжай");

        // Ждём начала движения (макс 30 сек)
        float waitTimer = 0f;
        while (waitTimer < 30f)
        {
            if (_carRb != null && _carRb.linearVelocity.magnitude > MinStopSpeed)
            {
                if (_indicators != null && _indicators.HazardLightsOn)
                    ExamManager.Instance?.AddPenalty(
                        "Перед началом движения не выключил аварийную сигнализацию (Упр.8)",
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

        Color c = !_activated  ? new Color(0.3f, 0.3f, 0.3f, 0.12f)  // серый — заблокирован
                : _triggered   ? new Color(1f,   0f,   1f,   0.4f)    // пурпурный — активен
                               : new Color(1f,   0f,   1f,   0.15f);  // бледный — ждёт

        Gizmos.color  = c;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            string status = !_activated ? $"LOCKED (ждёт Упр.{activateAfterExercise})"
                          : _triggered  ? "TRIGGERED"
                                        : "ready";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, status);
        }
#endif
    }
}
