using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class Engine
{
    public float idleRPM = 2400f;
    public float maxRPM = 7000f;
    public float IdleRPM => idleRPM;
    public float MaxRPM => maxRPM;
    public float[] gearRatios = { 3.50f, 2.80f, 2.30f, 1.90f, 1.60f, 1.30f, 1.00f, 0.85f };
    public float finalDriveRatio = 4.0f;
    private int currentGear = 0;
    public bool automaticTransmission = true;
    private bool switchingGears = false;
    private float gearChangeTime = 0.18f; //seconds to switch gears
    private float rpm = 0f;

    // Инерция двигателя: насколько быстро стрелка тахометра идёт вверх/вниз (RPM в секунду)
    public float revUpRate   = 9000f; // быстрый набор оборотов при газе
    public float revDownRate = 4000f; // плавный спад при отпускании/торможении

    public void SetRPM(float averageWheelAngularVelocity)
    {
        float averageWheelRPM = (averageWheelAngularVelocity * 60f) / (2f * Mathf.PI);
        float totalRatio = Math.Abs(gearRatios[currentGear] * finalDriveRatio);
        float transmissionRPM = averageWheelRPM * totalRatio;
        float targetRPM = Mathf.Clamp(Mathf.Max(idleRPM, transmissionRPM), idleRPM, maxRPM);

        // Сглаживание: разные скорости на подъём и на спад
        float rate = (targetRPM > rpm) ? revUpRate : revDownRate;
        rpm = Mathf.MoveTowards(rpm, targetRPM, rate * Time.fixedDeltaTime);
    }
    // Множитель умножения момента передачей (1.0 на 1-й, ~0.2 на топовой) — физика рычага КПП
    public float GetGearTorqueMultiplier()
    {
        float baseRatio    = Mathf.Abs(gearRatios[0] * finalDriveRatio);
        float currentRatio = Mathf.Abs(gearRatios[currentGear] * finalDriveRatio);
        return baseRatio > 0.001f ? currentRatio / baseRatio : 1f;
    }

    public float GetCurrentPower(MonoBehaviour context) // 0-1 в зависимости от RPM, кривая момента
    {
        if (switchingGears) return 0.3f; // Less power during gear switch

        // Колоколообразная кривая момента: 0.55 на idle, 1.0 на ~50% RPM, 0.7 на отсечке
        float t = Mathf.InverseLerp(idleRPM, maxRPM, rpm);
        float curve = 0.55f + Mathf.Sin(t * Mathf.PI) * 0.45f;

        // Умножаем на коэффициент текущей передачи (1-я тянет сильно, высшие — слабее)
        return Mathf.Clamp01(curve) * GetGearTorqueMultiplier();
    }
    public float AngularVelocityToRPM(float angularVelocity)
    {
        return angularVelocity * 60f / (2f * Mathf.PI);
    }

    // Причина последнего переключения (для debug-логов)
    [System.NonSerialized] public string lastShiftReason = "";

    public void UpGear(MonoBehaviour context)
    {
        if (currentGear < gearRatios.Length - 1 && !switchingGears)
        {
            int prev = currentGear + 1;
            currentGear++;
            switchingGears = true;
            Debug.Log($"<color=#7CFC00>[GEAR ↑]</color> {prev} → {currentGear + 1}  RPM={rpm:F0}  reason={lastShiftReason}");
            context.StartCoroutine(ResetSwitchingGearsCoroutine());
        }
    }

    public void DownGear(MonoBehaviour context)
    {
        if (currentGear > 0 && !switchingGears)
        {
            int prev = currentGear + 1;
            currentGear--;
            switchingGears = true;
            Debug.Log($"<color=#FFA500>[GEAR ↓]</color> {prev} → {currentGear + 1}  RPM={rpm:F0}  reason={lastShiftReason}");
            context.StartCoroutine(ResetSwitchingGearsCoroutine());
        }
    }

    private System.Collections.IEnumerator ResetSwitchingGearsCoroutine()
    {
        yield return new WaitForSeconds(gearChangeTime);
        switchingGears = false;
    }

    public int getCurrentGear()
    {
        return currentGear + 1; // Return 1-based gear number
    }

    // Адаптивные пороги переключения по нагрузке (как на реальном Polo 1.6 AT / Aisin AW)
    public float lightUpRPM     = 3300f;  // газ <30% — спокойная езда
    public float mediumUpRPM    = 4500f;  // газ 30–70%
    public float fullUpRPM      = 6000f;  // газ >70% (kickdown)
    public float lightDownRPM   = 1300f;
    public float mediumDownRPM  = 1800f;
    public float fullDownRPM    = 3200f;
    public float minGearHoldTime = 1.4f;  // минимум между переключениями (anti-hunting)
    private float lastShiftTime = -10f;

    [Header("Brake-induced downshift (торможение двигателем)")]
    public float brakeDownshiftRPM = 2800f;       // порог downshift при лёгком тормозе
    public float hardBrakeDownshiftRPM = 3800f;   // порог downshift при тормозе в пол
    public float hardBrakeHoldTime = 0.35f;       // ускоренный интервал между сбросами при тормозе в пол

    public void checkGearSwitching(MonoBehaviour context, float throttle01, float brake01 = 0f)
    {
        if (switchingGears) return;

        float t = Mathf.Clamp01(Mathf.Abs(throttle01));
        float b = Mathf.Clamp01(brake01);

        // При тормозе в пол — короче интервал между сбросами, чтобы быстро дойти с 6-й до 2-й
        float holdTime = (b > 0.7f) ? hardBrakeHoldTime : minGearHoldTime;
        if (Time.time - lastShiftTime < holdTime) return;

        // Точки переключения по педали газа
        float upTarget   = (t < 0.3f) ? Mathf.Lerp(lightUpRPM,  mediumUpRPM, t / 0.3f)
                         : (t < 0.7f) ? Mathf.Lerp(mediumUpRPM, fullUpRPM, (t - 0.3f) / 0.4f)
                                      : fullUpRPM;

        float downTarget = (t < 0.3f) ? Mathf.Lerp(lightDownRPM,  mediumDownRPM, t / 0.3f)
                         : (t < 0.7f) ? Mathf.Lerp(mediumDownRPM, fullDownRPM, (t - 0.3f) / 0.4f)
                                      : fullDownRPM;

        // Если водитель давит тормоз — поднимаем порог downshift (раньше сбрасываем для engine brake)
        if (b > 0.05f)
        {
            float brakeTarget = Mathf.Lerp(brakeDownshiftRPM, hardBrakeDownshiftRPM, b);
            downTarget = Mathf.Max(downTarget, brakeTarget);
        }

        bool braking = b > 0.1f;
        bool coasting = !braking && t < 0.05f;

        // При торможении upshift запрещён вообще — только downshift для engine braking
        bool canUpshift = !braking;

        // На коасте (нет ни газа, ни тормоза) — coast upshift только при действительно высоких оборотах
        // чтобы не было ползучего upshift на низкой скорости в стоп-энд-гоу
        if (coasting) upTarget = Mathf.Max(upTarget, 3800f);

        if (canUpshift && rpm > upTarget && currentGear < gearRatios.Length - 1)
        {
            lastShiftReason = coasting
                ? $"COAST upshift RPM>{upTarget:F0}"
                : $"RPM>{upTarget:F0} (throttle={t:F2})";
            UpGear(context);
            lastShiftTime = Time.time;
        }
        else if (rpm < downTarget && currentGear > 0)
        {
            lastShiftReason = braking
                ? $"BRAKE downshift (brake={b:F2}, RPM<{downTarget:F0})"
                : $"RPM<{downTarget:F0} (throttle={t:F2})";
            DownGear(context);
            lastShiftTime = Time.time;
        }
    }

    // Совместимость со старым вызовом
    public void checkGearSwitching(MonoBehaviour context) => checkGearSwitching(context, 0f, 0f);

    public float getRPM()
    {
        return rpm;
    }
    public bool isSwitchingGears()
    {
        return switchingGears;
    }
}

[Serializable]
public class WheelProperties
{
    [HideInInspector] public TrailRenderer skidTrail;
    [HideInInspector] public GameObject skidTrailGameObject;

    public Vector3 localPosition;
    public float turnAngle = 30f;
    public float suspensionLength = 0.5f;

    [HideInInspector] public float lastSuspensionLength = 0.0f;
    public float mass = 16f;
    public float size = 0.5f;
    public float engineTorque = 40f;
    public float brakeStrength = 0.5f;
    public bool slidding = false;
    [HideInInspector] public Vector3 worldSlipDirection;
    [HideInInspector] public Vector3 suspensionForceDirection;
    [HideInInspector] public Vector3 wheelWorldPosition;
    [HideInInspector] public float wheelCircumference;
    [HideInInspector] public float torque = 0.0f;
    [HideInInspector] public GameObject wheelObject;
    [HideInInspector] public Vector3 localVelocity;
    [HideInInspector] public float normalForce;
    [HideInInspector] public float angularVelocity;
    [HideInInspector] public float slip;
    [HideInInspector] public Vector2 input = Vector2.zero;
    [HideInInspector] public float brake = 0;
    [HideInInspector] public float slipHistory = 0f;
    [HideInInspector] public float tcsReduction = 0f; // Traction control reduction factor
}

public class Car : MonoBehaviour
{
    public Engine e;
    public GameObject skidMarkPrefab;
    public float smoothTurn = 0.03f;
    [Header("Сцепление с дорогой")]
    [Tooltip("Коэффициент трения покоя (рубер по сухому асфальту ≈ 1.0..2.0)")]
    public float coefStaticFriction = 1.95f;
    [Tooltip("Коэффициент трения скольжения. Чем ближе к статическому — тем мягче срыв (НЕ ставьте сильно ниже 1.5)")]
    public float coefKineticFriction = 1.55f;
    [Tooltip("Ширина зоны плавного перехода static→kinetic (slip от 1.0 до 1.0+window)). Чем больше — тем мягче срыв")]
    [Range(0.05f, 1f)] public float slipBlendWindow = 0.4f;
    [Tooltip("Демпфер рысканья: гасит вращение вокруг вертикальной оси, предотвращая занос. 0 = выкл")]
    [Range(0f, 50f)] public float yawDamping = 12f;
    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    [Tooltip("Боковое сцепление. Слишком низкое = машину сносит, слишком высокое = неестественно цепкая")]
    public float wheelGripX = 22f;
    [Tooltip("Продольное сцепление (разгон/торможение)")]
    public float wheelGripZ = 42f;
    public float suspensionForce = 90f;
    public float dampAmount = 2.5f;
    public float suspensionForceClamp = 200f;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public bool forwards = true;

    public enum TransmissionMode { Park, Drive, Reverse, Neutral }
    public TransmissionMode transmissionMode = TransmissionMode.Park;
    public TransmissionMode CurrentMode => transmissionMode;

    // Включается извне (HillStartExercise) — удерживает машину тормозом на склоне
    [HideInInspector] public bool hillHoldAllowed = false;
    [Range(0f, 5f)] public float hillHoldSpeedThreshold = 0.5f;

    // Жёсткая фиксация: машина не может скатываться назад вообще (упражнение «подъём»)
    [HideInInspector] public bool fullStopHold = false;
    private bool _isHardLocked = false;
    private Vector3 _lockPosition;
    private Quaternion _lockRotation;

    [Header("АКПП: защита и creep")]
    [Tooltip("Максимальная скорость (км/ч), на которой можно переключиться D↔R")]
    public float shiftLockSpeed = 5f;
    [Tooltip("Включить ползущий ход на D/R при отпущенном газе и без тормоза (имитация гидротрансформатора)")]
    public bool creepEnabled = true;
    [Tooltip("Скорость крипа в км/ч (реальная АКПП ползёт ~7 км/ч)")]
    public float creepSpeedKmh = 7f;
    [Tooltip("Сила крипа (0..0.3 от полного газа)")]
    [Range(0f, 0.5f)] public float creepThrottle = 0.18f;
    [Tooltip("Сила торможения двигателем при отпущенном газе (engine brake)")]
    [Range(0f, 0.5f)] public float engineBrakeFactor = 0.22f;
    [Tooltip("Аэродинамическое сопротивление (квадрат скорости). 0.4 — типично для седана")]
    [Range(0f, 2f)] public float airDragCoeff = 0.4f;

    /// <summary>
    /// Возвращает мировую позицию i-го колеса (контактная/локальная привязка).
    /// Используется внешними скриптами (CarBordureDetector, ParkingZone, ControlLineTrigger).
    /// </summary>
    public Vector3 GetWheelPosition(int index)
    {
        if (wheels == null || index < 0 || index >= wheels.Length) return transform.position;
        var w = wheels[index];
        if (w.wheelObject != null) return w.wheelObject.transform.position;
        return transform.TransformPoint(w.localPosition);
    }

    public int WheelCount => wheels != null ? wheels.Length : 0;

    [Header("Стоп-сигналы")]
    public GameObject[] brakeLights;

    [Header("Фонари заднего хода")]
    public GameObject[] reverseLights;

    [Header("Debug")]
    public bool debugLog = true;
    [Tooltip("Интервал между периодическими логами состояния, сек")]
    public float debugLogInterval = 0.5f;
    private float _lastDebugTime = 0f;


    // Assists
    public bool steeringAssist = true;
    [Range(0f, 1f)] public float steeringAssistStrength = 0.2f; // Strength of steering assist
    public bool throttleAssist = true;
    public bool brakeAssist = true;
    [HideInInspector] public Vector2 userInput = Vector2.zero;
    public enum InputMode { Keyboard, Wheel }
    [Header("Режим управления")]
    public InputMode inputMode = InputMode.Keyboard;

    [HideInInspector] public bool  externalInput    = false;
    [HideInInspector] public float externalThrottle = 0f;
    [HideInInspector] public float externalBrake    = 0f;
    [HideInInspector] public float externalSteer    = 0f;
    [Tooltip("Квадратичный прижим: F = v² * downforce. Полезно ~0.1..0.3 для седана. Растёт сцепление на скорости")]
    public float downforce = 0.16f;
    [HideInInspector] public float isBraking = 0f;
    public Vector3 COMOffset = new Vector3(0, -0.2f, 0);
    public float Inertia = 1.2f; // Multiplier for inertia tensor

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        foreach (var w in wheels)
        {
            w.wheelObject = Instantiate(wheelPrefab, transform);
            w.wheelObject.transform.localPosition = w.localPosition;
            w.wheelObject.transform.eulerAngles = transform.eulerAngles;
            w.wheelObject.transform.localScale = 2f * new Vector3(w.size, w.size, w.size);
            w.wheelCircumference = 2f * Mathf.PI * w.size;

            if (skidMarkPrefab != null)
            {
                w.skidTrailGameObject = Instantiate(skidMarkPrefab, w.wheelObject.transform);
                w.skidTrailGameObject.transform.localPosition = Vector3.zero;
                w.skidTrailGameObject.transform.localRotation = Quaternion.identity;
                w.skidTrailGameObject.transform.parent = null;

                w.skidTrail = w.skidTrailGameObject.GetComponent<TrailRenderer>();
                if (w.skidTrail != null)
                    w.skidTrail.emitting = false;
            }
        }

        foreach (var w in wheels)
        {
            w.tcsReduction = 0f;
            w.slipHistory = 0f;
        }

        rb.centerOfMass += COMOffset;
        rb.inertiaTensor *= Inertia;
    }

    void Update()
    {
        externalInput = (inputMode == InputMode.Wheel);

        if (LegacyInput.GetKeyDown(KeyCode.R))
        {
            transform.rotation = Quaternion.identity;
            transform.position += Vector3.up * 2f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // В Park — только выход из Park разрешён (F или S), всё остальное заблокировано
        if (transmissionMode == TransmissionMode.Park)
        {
            if (LegacyInput.GetKeyDown(KeyCode.F))
                transmissionMode = TransmissionMode.Drive;
            else if (LegacyInput.GetKeyDown(KeyCode.S))
                transmissionMode = TransmissionMode.Reverse;
            return;
        }

        // Переключение режима трансмиссии (защита: нельзя на ходу переключать D↔R)
        float currentSpeedKmh = rb.linearVelocity.magnitude * 3.6f;
        if (LegacyInput.GetKeyDown(KeyCode.P))
        {
            transmissionMode = TransmissionMode.Park; // Park всегда разрешён
        }
        else if (LegacyInput.GetKeyDown(KeyCode.S))
        {
            if (currentSpeedKmh < shiftLockSpeed)
                transmissionMode = TransmissionMode.Reverse;
            else
                Debug.LogWarning($"<color=#FF8C00>[GEAR LOCK]</color> Нельзя включить Reverse на скорости {currentSpeedKmh:F1} км/ч. Остановитесь.");
        }
        else if (LegacyInput.GetKeyDown(KeyCode.F))
        {
            if (currentSpeedKmh < shiftLockSpeed)
                transmissionMode = TransmissionMode.Drive;
            else
                Debug.LogWarning($"<color=#FF8C00>[GEAR LOCK]</color> Нельзя включить Drive на скорости {currentSpeedKmh:F1} км/ч. Остановитесь.");
        }

        // ── Ввод ──────────────────────────────────────────────────────────────
        float rawThrottle = 0f;
        bool  brakePedal  = false;

        // Замедление руля по скорости: чем быстрее, тем тяжелее руль.
        // Делитель 18 (раньше 28) — заметно мягче «дёргается» на 30-60 км/ч.
        float speedSteerScale = 1f / (1f + rb.linearVelocity.magnitude / 18f);

        if (externalInput)
        {
            // Руль/педали — аналог, можно быстрее реагировать.
            float steerTarget = externalSteer * speedSteerScale;
            userInput.x = Mathf.Lerp(userInput.x, steerTarget, 1f - Mathf.Exp(-Time.deltaTime / 0.07f));
            rawThrottle = externalThrottle;
            brakePedal  = externalBrake > 0.02f;
        }
        else
        {
            // Клавиатура даёт мгновенный ±1: сглаживаем сильнее, чтобы машину не дёргало.
            float steerTarget = LegacyInput.GetAxisRaw("Horizontal") * speedSteerScale;
            userInput.x = Mathf.Lerp(userInput.x, steerTarget, 1f - Mathf.Exp(-Time.deltaTime / 0.18f));
            rawThrottle = Mathf.Max(0f, LegacyInput.GetAxisRaw("Vertical"));
            brakePedal  = LegacyInput.GetKey(KeyCode.Space);
        }

        // ── Creep + engine brake (всегда, независимо от источника ввода) ────
        float signedThrottle = transmissionMode == TransmissionMode.Reverse ? -rawThrottle
                             : transmissionMode == TransmissionMode.Neutral  ? 0f
                             : rawThrottle;

        if (rawThrottle < 0.05f && !brakePedal && transmissionMode != TransmissionMode.Neutral)
        {
            float fwdSpeedKmh = Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
            if (transmissionMode == TransmissionMode.Drive)
            {
                if (creepEnabled && fwdSpeedKmh < creepSpeedKmh)
                    signedThrottle = creepThrottle;
                else if (fwdSpeedKmh > creepSpeedKmh)
                    signedThrottle = -engineBrakeFactor;
            }
            else
            {
                if (creepEnabled && fwdSpeedKmh > -creepSpeedKmh)
                    signedThrottle = -creepThrottle;
                else if (fwdSpeedKmh < -creepSpeedKmh)
                    signedThrottle = engineBrakeFactor;
            }
        }

        userInput.y = Mathf.Lerp(userInput.y, signedThrottle, 0.2f);

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        bool wrongDirection =
            (transmissionMode == TransmissionMode.Drive   && forwardSpeed < -0.5f) ||
            (transmissionMode == TransmissionMode.Reverse && forwardSpeed >  0.5f);
        bool hillHold = hillHoldAllowed
                        && Mathf.Abs(rawThrottle) < 0.05f
                        && rb.linearVelocity.magnitude < hillHoldSpeedThreshold;
        bool isBraking = brakePedal || (wrongDirection && Mathf.Abs(rawThrottle) > 0.05f) || hillHold;
        if (isBraking) userInput.y = 0;
        SetBrakeLights(isBraking);
        SetReverseLights(transmissionMode == TransmissionMode.Reverse);

        for (int i = 0; i < wheels.Length; i++)
        {
            var w = wheels[i];

            // Ensure no NaN values from previous frames
            if (float.IsNaN(w.slip) || float.IsInfinity(w.slip))
                w.slip = 0f;

            // Traction control: для учебной машины срабатывает раньше — режем тягу, когда колесо
            // уже работает на 70%+ от предела сцепления, чтобы не допустить полного срыва.
            if (throttleAssist)
            {
                float targetSlip = 0.70f;     // Учебный авто: бережём запас сцепления
                float slipTolerance = 0.08f;  // Шире зона стабильности — меньше осцилляций
                if (w.slip > targetSlip + slipTolerance)
                {
                    float overshoot = w.slip - targetSlip;
                    float reduction = Mathf.Clamp01(overshoot * 2.0f);
                    w.tcsReduction = Mathf.Lerp(w.tcsReduction, 1, reduction / 5f);
                }
                else if (w.slip < targetSlip - slipTolerance)
                {
                    w.tcsReduction = Mathf.Lerp(w.tcsReduction, 0f, 0.6f * Time.deltaTime);
                }
                w.tcsReduction = Mathf.Clamp01(w.tcsReduction);
            }
            w.brake = (isBraking == true ? 1 : 0) * (1 - w.tcsReduction);

            // Steering assist: вмешивается только когда колесо реально близко к срыву (slip > 0.6),
            // а не на каждом повороте при штатной езде.
            float s = Mathf.Clamp01(w.slip);
            w.input.x = Mathf.Lerp(w.input.x, userInput.x, Time.deltaTime * 60f);
            if (s > 0.6f && s < 1.5f && steeringAssist) w.input.x = Mathf.Lerp(w.input.x, 0, s * Time.deltaTime * steeringAssistStrength);

            // Apply throttle with TCS - more responsive for F1
            float inputY = transmissionMode == TransmissionMode.Neutral ? 0f : userInput.y;
            float finalThrottle = inputY * (1f - w.tcsReduction);
            if (float.IsNaN(finalThrottle) || float.IsInfinity(finalThrottle))
                finalThrottle = 0f;
            w.input.y = Mathf.Lerp(w.input.y, finalThrottle, 0.95f * Time.deltaTime * 60f);
            if (float.IsNaN(w.input.y) || float.IsInfinity(w.input.y))
                w.input.y = 0f;
        }

        if (LegacyInput.GetKeyDown(KeyCode.E)) e.UpGear(this);
        else if (LegacyInput.GetKeyDown(KeyCode.Q)) e.DownGear(this);

        // Сила тормоза 0..1 для логики downshift:
        // педаль тормоза = 1.0, встречный газ = по величине, hillHold = 0.3
        float brakeStrength01 = 0f;
        if (brakePedal) brakeStrength01 = 1f;
        else if (wrongDirection) brakeStrength01 = Mathf.Abs(rawThrottle);
        else if (hillHold) brakeStrength01 = 0.3f;

        e.checkGearSwitching(this, Mathf.Abs(rawThrottle), brakeStrength01);

        // Периодический debug-лог состояния
        if (debugLog && Time.time - _lastDebugTime >= debugLogInterval)
        {
            _lastDebugTime = Time.time;
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            float gasPedal = Mathf.Abs(rawThrottle);
            string mode = transmissionMode.ToString();
            string gas   = gasPedal > 0.05f ? $"<color=#7CFC00>ON {gasPedal:F2}</color>" : "off";
            string brake = brakePedal     ? "<color=#FF5050>ON</color>"
                         : wrongDirection ? "<color=#FFA500>opposing</color>"
                         : hillHold       ? "<color=#FFD700>hillHold</color>"
                                          : "off";
            Debug.Log(
                $"[CAR] mode=<b>{mode}</b> gear=<b>{e.getCurrentGear()}</b> " +
                $"RPM=<b>{e.getRPM():F0}</b> speed=<b>{speedKmh:F1} км/ч</b> " +
                $"gas={gas} brake={brake}"
            );
        }
    }

    void FixedUpdate()
    {
        // Квадратичный downforce: прижим растёт по v², как у реальной аэродинамики.
        // Это сильно увеличивает normalForce на высокой скорости — а через него и максимум сцепления.
        float vSpeed = rb.linearVelocity.magnitude;
        rb.AddForce(-transform.up * vSpeed * vSpeed * downforce);

        // Аэродинамическое сопротивление: F = -v * |v| * coeff (квадрат скорости)
        Vector3 horizVel = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        float speed = horizVel.magnitude;
        if (speed > 0.3f)
            rb.AddForce(-horizVel.normalized * speed * speed * airDragCoeff);

        // Yaw damping: гасит вращение вокруг вертикальной оси.
        // Подавляет занос/раскачку без блокировки нормального поворота — момент пропорционален yaw-скорости.
        if (yawDamping > 0.0001f)
        {
            float yawVel = Vector3.Dot(rb.angularVelocity, transform.up);
            rb.AddTorque(-transform.up * yawVel * yawDamping, ForceMode.Force);
        }
        float averageWheelAngularVelocity = 0f;
        // Debug.Log(rb.velocity.magnitude);
        foreach (var w in wheels)
        {
            RaycastHit hit;
            float rayLen = w.size * 2f + w.suspensionLength;
            Transform wheelObj = w.wheelObject.transform;
            Transform wheelVisual = wheelObj.GetChild(0);

            wheelObj.localRotation = Quaternion.Euler(0, w.turnAngle * w.input.x, 0);
            w.wheelWorldPosition = transform.TransformPoint(w.localPosition);
            Vector3 velocityAtWheel = rb.GetPointVelocity(w.wheelWorldPosition);
            w.localVelocity = wheelObj.InverseTransformDirection(velocityAtWheel);
            forwards = w.localVelocity.z > 0.1f;
            w.torque = w.engineTorque * w.input.y * e.GetCurrentPower(this);

            float inertia = w.mass * w.size * w.size / 2f;
            float lateralVel = w.localVelocity.x;

            bool grounded = Physics.Raycast(w.wheelWorldPosition, -transform.up, out hit, rayLen);
            Vector3 worldVelAtHit = rb.GetPointVelocity(hit.point);
            float lateralHitVel = wheelObj.InverseTransformDirection(worldVelAtHit).x;

            float lateralFriction = -wheelGripX * lateralVel - 2f * lateralHitVel;
            float longitudinalFriction = -wheelGripZ * (w.localVelocity.z - w.angularVelocity * w.size);

            w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;
            w.angularVelocity *= 1 - w.brake * w.brakeStrength * Time.fixedDeltaTime;
            if (LegacyInput.GetKey(KeyCode.LeftShift)) // Ручник (handbrake)
            {
                w.angularVelocity = 0;
            }

            Vector3 totalLocalForce = new Vector3(lateralFriction, 0f, longitudinalFriction)
                * w.normalForce * coefStaticFriction * Time.fixedDeltaTime;
            float currentMaxFrictionForce = w.normalForce * coefStaticFriction;

            w.slip = currentMaxFrictionForce > 0.0001f
                ? totalLocalForce.magnitude / currentMaxFrictionForce
                : 0f;
            // Плавный переход static→kinetic: пока slip<=1.0 — полное сцепление,
            // далее линейно деградируем до kinetic за окно slipBlendWindow.
            // Это убирает «обрыв» сцепления, когда срыв превращался в неконтролируемый снос.
            float slipExcess = Mathf.Max(0f, w.slip - 1f);
            float kineticRatio = coefStaticFriction > 0.0001f ? coefKineticFriction / coefStaticFriction : 1f;
            float gripFactor = Mathf.Lerp(1f, kineticRatio,
                Mathf.Clamp01(slipExcess / Mathf.Max(0.01f, slipBlendWindow)));
            w.slidding = w.slip > 1f + slipBlendWindow * 0.5f;
            totalLocalForce = Vector3.ClampMagnitude(totalLocalForce, currentMaxFrictionForce);
            totalLocalForce *= gripFactor;

            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            w.worldSlipDirection = totalWorldForce;

            if (grounded)
            {
                float compression = rayLen - hit.distance;
                float damping = (w.lastSuspensionLength - hit.distance) * dampAmount;
                w.normalForce = (compression + damping) * suspensionForce;
                w.normalForce = Mathf.Clamp(w.normalForce, 0f, suspensionForceClamp);

                Vector3 springDir = hit.normal * w.normalForce;
                w.suspensionForceDirection = springDir;

                rb.AddForceAtPosition(springDir + totalWorldForce, hit.point);
                w.lastSuspensionLength = hit.distance;
                wheelObj.position = hit.point + transform.up * w.size;

                if (w.slidding)
                {
                    // If no skid trail exists or if it was detached previously, instantiate a new one.
                    if (w.skidTrail == null && skidMarkPrefab != null)
                    {
                        GameObject skidTrailObj = Instantiate(skidMarkPrefab, transform);
                        skidTrailObj.transform.SetParent(w.wheelObject.transform);
                        skidTrailObj.transform.localPosition = Vector3.zero;
                        w.skidTrail = skidTrailObj.GetComponent<TrailRenderer>();
                        w.skidTrail.time = 3f; // Trail lasts for 10 seconds
                        w.skidTrail.autodestruct = true;
                        w.skidTrail.emitting = false;
                        w.skidTrail.transform.position = hit.point;
                        if (w.skidTrail != null)
                        {
                            w.skidTrail.emitting = true;
                        }
                    }
                    else if (w.skidTrail != null)
                    {
                        // Continue emitting and update its position to the contact point.
                        w.skidTrail.emitting = true;
                        w.skidTrail.transform.position = hit.point + transform.up * 0.2f;
                        // Align the skid trail so its up vector is the road normal.
                        // This projects the wheel's forward direction onto the road plane to preserve skid direction.
                        // Now update to real position/rotation
                        w.skidTrail.transform.position = hit.point;

                        Vector3 skidDir = Vector3.ProjectOnPlane(w.worldSlipDirection.normalized, hit.normal);
                        if (skidDir.sqrMagnitude < 0.001f)
                            skidDir = Vector3.ProjectOnPlane(wheelObj.forward, hit.normal).normalized;

                        Quaternion flatRot = Quaternion.LookRotation(skidDir, hit.normal)
                                            * Quaternion.Euler(90f, 0f, 0f);
                        w.skidTrail.transform.rotation = flatRot;
                    }
                }
                else if (w.skidTrail != null && w.skidTrail.emitting)
                {
                    // Stop emitting and detach the skid trail so it remains in the scene to fade out.
                    w.skidTrail.emitting = false;
                    w.skidTrail.transform.parent = null;
                    // Optionally, destroy the skid trail after its lifetime has elapsed.
                    Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    w.skidTrail = null;
                }
            }
            else
            {
                wheelObj.position = w.wheelWorldPosition + transform.up * (w.size - rayLen);
                if (w.skidTrail != null && w.skidTrail.emitting)
                {
                    w.skidTrail.emitting = false;
                    w.skidTrail.transform.parent = null;
                    Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    w.skidTrail = null;
                }
            }

            averageWheelAngularVelocity += w.angularVelocity;

            wheelVisual.Rotate(
                Vector3.right,
                w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime,
                Space.Self
            );
        }

        averageWheelAngularVelocity /= wheels.Length;
        e.SetRPM(averageWheelAngularVelocity);

        // Park — тормозим и блокируем только после остановки
        if (transmissionMode == TransmissionMode.Park)
        {
            float spd = rb.linearVelocity.magnitude;
            if (spd < 0.3f)
            {
                // Скорость достаточно мала — фиксируем
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                foreach (var w in wheels) w.angularVelocity = 0f;
            }
            else
            {
                // Ещё едем — применяем экстренное торможение через колёса
                foreach (var w in wheels) w.brake = 1f;
            }
        }

        // Полный стоп-холд: в зоне HillStopZone + зажат тормоз = машина прибита к точке
        bool shouldHardLock = fullStopHold && LegacyInput.GetKey(KeyCode.Space);
        if (shouldHardLock)
        {
            if (!_isHardLocked)
            {
                // Первый кадр блокировки — сохраняем точку фиксации
                _lockPosition = rb.position;
                _lockRotation = rb.rotation;
                _isHardLocked = true;
            }
            // Прибиваем к точке: гасим скорости и возвращаем позицию/поворот к зафиксированным
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = _lockPosition;
            rb.rotation = _lockRotation;
            foreach (var w in wheels) w.angularVelocity = 0f;
        }
        else
        {
            _isHardLocked = false;
        }
    }

    private bool _brakeLightsOn   = false;
    private bool _reverseLightsOn = false;
    public  bool BrakeLightsOn   => _brakeLightsOn;
    public  bool ReverseLightsOn => _reverseLightsOn;

    void SetBrakeLights(bool on)
    {
        if (on == _brakeLightsOn) return;
        _brakeLightsOn = on;
        if (brakeLights == null) return;
        foreach (var go in brakeLights)
            if (go != null) go.SetActive(on);
    }

    void SetReverseLights(bool on)
    {
        if (on == _reverseLightsOn) return;
        _reverseLightsOn = on;
        if (reverseLights == null) return;
        foreach (var go in reverseLights)
            if (go != null) go.SetActive(on);
    }
}