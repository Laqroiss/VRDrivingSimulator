using UnityEngine;

/// <summary>
/// Упражнение №3 — стоп-линия регулируемого перекрёстка.
///
/// Штрафы:
///   100б — проехал на красный / красный+жёлтый
///    20б — на проезд перекрёстка при разрешающем сигнале затрачено > 20 сек
///   100б — то же, но > 30 сек
///     5б — не включил поворотник при повороте
///
/// Логика:
///   - Машина въезжает на стоп-линию
///   - Если красный → фиксируем штраф
///   - Если зелёный → запускаем таймер
///   - Если машина стоит в триггере и свет переключился на зелёный → тоже запускаем таймер
/// </summary>
public class RedLightDetector : MonoBehaviour
{
    public enum BlinkerCheck { None, Left, Right, Any }

    [Header("Связанный светофор")]
    public TrafficLight linkedTrafficLight;

    [Header("Активация — привяжите CheckGreenLight этого перекрёстка")]
    [Tooltip("Триггер активен только когда активен соответствующий GreenLightTimer.\nОставьте пустым — работает всегда.")]
    public GreenLightTimer linkedGreenLightTimer;

    [Header("Проверка поворотника")]
    [Tooltip("Какой поворотник должен быть включён при проезде перекрёстка.\nNone — не проверять.\nLeft/Right — конкретный.\nAny — любой.")]
    public BlinkerCheck requiredBlinker = BlinkerCheck.None;

    [Header("Упражнение с пешеходом (только для TF1_Check)")]
    public PedestrianExercise pedestrianExercise;

    private bool  _carInZone         = false;
    private bool  _timerRunning      = false;
    private bool  _exerciseStarted   = false;
    private bool  _penalizedRedLight = false;
    private bool  _penalty20Given    = false;
    private bool  _penalty30Given    = false;

    private float _timeInIntersection = 0f;

    private CarIndicators _indicators;
    private bool _blinkerUsed = false;

    void Start()
    {
        _indicators = FindAnyObjectByType<CarIndicators>();
    }

    void Update()
    {
        if (!_carInZone) return;

        bool isGreen = IsGreenLight();

        // Машина стоит в зоне — ждём зелёного чтобы запустить таймер
        if (!_timerRunning && isGreen && !_penalizedRedLight)
        {
            _timerRunning         = true;
            _timeInIntersection   = 0f;
            _penalty20Given       = false;
            _penalty30Given       = false;
            _blinkerUsed          = false;
            Debug.Log("RedLightDetector: зелёный сигнал — таймер запущен");
        }

        if (!_timerRunning) return;

        _timeInIntersection += Time.deltaTime;

        if (IsBlinkerOn()) _blinkerUsed = true;

        if (!_penalty20Given && _timeInIntersection > 20f)
        {
            _penalty20Given = true;
            ExamManager.Instance?.AddPenalty(
                "Затратил на проезд регулируемого перекрёстка более 20 секунд",
                ExamManager.P3_OVERTIME_20, 3);
        }

        if (!_penalty30Given && _timeInIntersection > 30f)
        {
            _penalty30Given = true;
            ExamManager.Instance?.AddPenalty(
                "Затратил на проезд регулируемого перекрёстка более 30 секунд",
                ExamManager.P3_OVERTIME_30, 3);
        }
    }

    bool IsActivated => linkedGreenLightTimer == null || linkedGreenLightTimer.IsActivated;

    void OnTriggerEnter(Collider other)
    {
        if (!IsActivated) return;
        if (other.GetComponentInParent<Car>() == null) return;

        _carInZone = true;

        if (!_exerciseStarted)
        {
            _exerciseStarted = true;
            ExamManager.Instance?.SetExerciseActive(3);
        }

        // Проверяем красный в момент въезда
        if (IsRedLight() && !_penalizedRedLight)
        {
            _penalizedRedLight = true;
            ExamManager.Instance?.AddPenalty(
                "Выехал на перекрёсток / пересёк стоп-линию на запрещающий сигнал светофора",
                ExamManager.P3_RED_LIGHT, 3);
            ExamManager.Instance?.MarkExerciseFailed(3);
            return;
        }

        // Если сразу на зелёном — сразу запускаем таймер
        if (IsGreenLight())
        {
            _timerRunning       = true;
            _timeInIntersection = 0f;
            _blinkerUsed        = false;
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
            // Проверка поворотника — только если в инспекторе указан нужный (не None)
            if (requiredBlinker != BlinkerCheck.None && _timeInIntersection > 1f && !_blinkerUsed)
                ExamManager.Instance?.AddPenalty(
                    $"Не включил указатель поворота ({BlinkerName()}) при выполнении поворота (Упр.3)",
                    ExamManager.P3_NO_BLINKER, 3);

            if (!_penalty30Given)
                ExamManager.Instance?.CompleteExercise(3);
        }

        // Активируем упражнение с пешеходом (только TF1_Check)
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
        BlinkerCheck.Left  => "левый",
        BlinkerCheck.Right => "правый",
        BlinkerCheck.Any   => "любой",
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
        // Yellow тоже считается — машина уже начала движение
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
