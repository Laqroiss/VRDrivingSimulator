using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Вешается на CheckGreenLight1/2/3/4.
/// Не реагирует пока не активирован (activated = false).
///
/// Режимы активации (ActivationMode):
///   AlwaysActive        — активен сразу (обратная совместимость)
///   OnExerciseComplete  — активируется когда ExamManager.OnExerciseComplete(exerciseNumber)
///   OnCheckpointPass    — активируется через GreenLightActivator на чекпоинт-триггере
/// </summary>
public class GreenLightTimer : MonoBehaviour
{
    public enum ActivationMode { AlwaysActive, OnExerciseComplete, OnCheckpointPass }

    [Header("Активация")]
    public ActivationMode activationMode = ActivationMode.AlwaysActive;

    [Tooltip("Номер упражнения (1-10) при режиме OnExerciseComplete")]
    public int activateOnExercise = 1;

    [Header("Светофор этого направления")]
    public TrafficLight linkedTrafficLight;

    // ── runtime ──────────────────────────────────────────────────────────
    private bool  _activated    = false; // разрешено ли вообще реагировать
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
                Debug.LogWarning($"GreenLightTimer [{name}]: ExamManager.Instance == null в Start");
        }
        // OnCheckpointPass — активируется снаружи через Activate()
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
            Debug.Log($"GreenLightTimer [{name}]: активирован после завершения Упр.{completedExercise}");
        }
    }

    /// Вызывается из GreenLightActivator (чекпоинт) или другого скрипта
    public void Activate()
    {
        if (_activated) return;
        _activated = true;
        Debug.Log($"GreenLightTimer [{name}]: активирован внешним вызовом");
    }

    // ── Trigger (CheckGreenLight зона) ────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!_activated) return;
        if (other.GetComponentInParent<Car>() == null) return;
        _carInZone = true;
        TryStartTimer("въезд");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;
        _carInZone = false;
        // Таймер не останавливаем — он работает до выезда из IntersectionPass
    }

    // ── Update ────────────────────────────────────────────────────────────

    void Update()
    {
        // Если машина стоит в зоне на красный — ждём пока загорится зелёный
        if (_carInZone && !_timerActive)
            TryStartTimer("свет переключился");

        if (_timerActive)
        {
            _timer += Time.deltaTime;
            if (!_penalty20Done && _timer > 20f) Apply20Penalty();
            if (!_penalty30Done && _timer > 30f) Apply30Penalty();
        }
    }

    // ── Логика запуска таймера ────────────────────────────────────────────

    void TryStartTimer(string source)
    {
        if (_timerActive) return;

        if (linkedTrafficLight != null)
        {
            var s = linkedTrafficLight.currentState;
            bool greenOk = s == TrafficLight.LightState.Green
                        || s == TrafficLight.LightState.BlinkingGreen;
            if (!greenOk) return; // тихо ждём
            StartTimer(source, s.ToString());
        }
        else
        {
            StartTimer(source, "no light");
        }
    }

    void StartTimer(string source, string lightState)
    {
        _timerActive   = true;
        _timer         = 0f;
        _penalty20Done = false;
        _penalty30Done = false;
        Debug.Log($"GreenLightTimer [{name}]: таймер запущен ({source}, свет={lightState})");
    }

    // ── Вызов из IntersectionPassRelay при выезде из зоны перекрёстка ─────

    public void OnExitIntersectionPass()
    {
        if (!_timerActive) return;

        Debug.Log($"GreenLightTimer [{name}]: выезд из IntersectionPass — итого {_timer:F1} сек");

        if (!_penalty20Done && _timer > 20f) Apply20Penalty();
        if (!_penalty30Done && _timer > 30f) Apply30Penalty();

        _timerActive = false;
    }

    // ── Штрафы ────────────────────────────────────────────────────────────

    void Apply20Penalty()
    {
        _penalty20Done = true;
        ExamManager.Instance?.AddPenalty(
            "Затратил на проезд регулируемого перекрёстка более 20 секунд",
            ExamManager.P3_OVERTIME_20, 3);
    }

    void Apply30Penalty()
    {
        _penalty30Done = true;
        ExamManager.Instance?.AddPenalty(
            "Затратил на проезд регулируемого перекрёстка более 30 секунд",
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
            c = new Color(0.3f, 0.3f, 0.3f, 0.12f);       // серый  — ещё не активирован
        else if (_timerActive)
            c = _timer > 20f
                ? new Color(1f, 0.2f, 0.2f, 0.4f)          // красный — превышение
                : new Color(0.2f, 1f, 0.2f, 0.35f);         // зелёный — таймер идёт
        else if (_carInZone)
            c = new Color(1f, 0.9f, 0.1f, 0.35f);           // жёлтый  — машина ждёт зелёного
        else
            c = new Color(0f, 0.6f, 1f, 0.18f);             // синий   — активен, ждёт машину

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
