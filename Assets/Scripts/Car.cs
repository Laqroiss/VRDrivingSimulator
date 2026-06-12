using System;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
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

    // Engine inertia: how fast RPM climbs/falls (RPM per second)
    public float revUpRate   = 9000f; // quick rev-up on throttle
    public float revDownRate = 4000f; // gentle fall when off-throttle/braking

    public void SetRPM(float averageWheelAngularVelocity)
    {
        float averageWheelRPM = (averageWheelAngularVelocity * 60f) / (2f * Mathf.PI);
        float totalRatio = Math.Abs(gearRatios[currentGear] * finalDriveRatio);
        float transmissionRPM = averageWheelRPM * totalRatio;
        float targetRPM = Mathf.Clamp(Mathf.Max(idleRPM, transmissionRPM), idleRPM, maxRPM);

        // Smooth toward target, different rates up vs down
        float rate = (targetRPM > rpm) ? revUpRate : revDownRate;
        rpm = Mathf.MoveTowards(rpm, targetRPM, rate * Time.fixedDeltaTime);
    }
    // Gear torque multiplier (1.0 in 1st, ~0.2 in top gear)
    public float GetGearTorqueMultiplier()
    {
        float baseRatio    = Mathf.Abs(gearRatios[0] * finalDriveRatio);
        float currentRatio = Mathf.Abs(gearRatios[currentGear] * finalDriveRatio);
        return baseRatio > 0.001f ? currentRatio / baseRatio : 1f;
    }

    public float GetCurrentPower(MonoBehaviour context) // 0-1 torque curve based on RPM
    {
        if (switchingGears) return 0.3f; // Less power during gear switch

        // Bell-shaped torque curve: 0.55 at idle, 1.0 at ~50% RPM, 0.7 at redline
        float t = Mathf.InverseLerp(idleRPM, maxRPM, rpm);
        float curve = 0.55f + Mathf.Sin(t * Mathf.PI) * 0.45f;

        // Scale by current gear (1st pulls hard, higher gears weaker)
        return Mathf.Clamp01(curve) * GetGearTorqueMultiplier();
    }
    public float AngularVelocityToRPM(float angularVelocity)
    {
        return angularVelocity * 60f / (2f * Mathf.PI);
    }

    // Reason for the last shift (for debug logs)
    [System.NonSerialized] public string lastShiftReason = "";

    public void UpGear(MonoBehaviour context)
    {
        if (currentGear < gearRatios.Length - 1 && !switchingGears)
        {
            int prev = currentGear + 1;
            currentGear++;
            switchingGears = true;
            GameLog.Info($"<color=#7CFC00>[GEAR ↑]</color> {prev} → {currentGear + 1}  RPM={rpm:F0}  reason={lastShiftReason}");
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
            GameLog.Info($"<color=#FFA500>[GEAR ↓]</color> {prev} → {currentGear + 1}  RPM={rpm:F0}  reason={lastShiftReason}");
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

    // Load-adaptive shift thresholds (like a real Polo 1.6 AT / Aisin AW)
    public float lightUpRPM     = 3300f;  // throttle <30% - calm driving
    public float mediumUpRPM    = 4500f;  // throttle 30-70%
    public float fullUpRPM      = 6000f;  // throttle >70% (kickdown)
    public float lightDownRPM   = 1300f;
    public float mediumDownRPM  = 1800f;
    public float fullDownRPM    = 3200f;
    public float minGearHoldTime = 1.4f;  // min time between shifts (anti-hunting)
    private float lastShiftTime = -10f;

    [Header("Brake-induced downshift (engine braking)")]
    public float brakeDownshiftRPM = 2800f;       // downshift threshold on light braking
    public float hardBrakeDownshiftRPM = 3800f;   // downshift threshold on full braking
    public float hardBrakeHoldTime = 0.35f;       // faster shift interval under full braking

    public void checkGearSwitching(MonoBehaviour context, float throttle01, float brake01 = 0f)
    {
        if (switchingGears) return;

        float t = Mathf.Clamp01(Mathf.Abs(throttle01));
        float b = Mathf.Clamp01(brake01);

        // Full braking shortens the interval so it drops from 6th to 2nd quickly
        float holdTime = (b > 0.7f) ? hardBrakeHoldTime : minGearHoldTime;
        if (Time.time - lastShiftTime < holdTime) return;

        // Shift points by throttle pedal
        float upTarget   = (t < 0.3f) ? Mathf.Lerp(lightUpRPM,  mediumUpRPM, t / 0.3f)
                         : (t < 0.7f) ? Mathf.Lerp(mediumUpRPM, fullUpRPM, (t - 0.3f) / 0.4f)
                                      : fullUpRPM;

        float downTarget = (t < 0.3f) ? Mathf.Lerp(lightDownRPM,  mediumDownRPM, t / 0.3f)
                         : (t < 0.7f) ? Mathf.Lerp(mediumDownRPM, fullDownRPM, (t - 0.3f) / 0.4f)
                                      : fullDownRPM;

        // Braking raises the downshift threshold (shift down sooner for engine braking)
        if (b > 0.05f)
        {
            float brakeTarget = Mathf.Lerp(brakeDownshiftRPM, hardBrakeDownshiftRPM, b);
            downTarget = Mathf.Max(downTarget, brakeTarget);
        }

        bool braking = b > 0.1f;
        bool coasting = !braking && t < 0.05f;

        // No upshifts while braking - only downshifts for engine braking
        bool canUpshift = !braking;

        // Coasting (no throttle, no brake): upshift only at really high RPM,
        // to avoid creeping upshifts at low speed in stop-and-go
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

    // Compatibility with the old call
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
    [HideInInspector] public float visualBottomOffset = 0f; // distance from wheel pivot to tire bottom (measured from mesh)

    // ── Physics diagnostics (for detailed logging, no effect on behavior) ──
    [HideInInspector] public bool  dbgGrounded;
    [HideInInspector] public float dbgGroundDist;
    [HideInInspector] public float dbgCompression;
    [HideInInspector] public float dbgCompSpeed;
    [HideInInspector] public float dbgVelDamp;
    [HideInInspector] public float dbgUpAlign;
    [HideInInspector] public float dbgDrop;
}

public class Car : MonoBehaviour
{
    public Engine e;
    public GameObject skidMarkPrefab;
    public float smoothTurn = 0.03f;
    [Header("Road grip")]
    [Tooltip("Static friction coefficient (rubber on dry asphalt ~ 1.0..2.0)")]
    public float coefStaticFriction = 1.95f;
    [Tooltip("Kinetic friction coefficient. Closer to static = softer breakaway (don't go far below 1.5)")]
    public float coefKineticFriction = 1.55f;
    [Tooltip("Width of the static->kinetic blend zone (slip from 1.0 to 1.0+window). Larger = softer breakaway")]
    [Range(0.05f, 1f)] public float slipBlendWindow = 0.4f;
    [Tooltip("Yaw damping: damps rotation around the vertical axis to prevent spinning out. 0 = off")]
    [Range(0f, 50f)] public float yawDamping = 12f;
    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    [Tooltip("Lateral grip. Too low = car slides, too high = unnaturally sticky")]
    public float wheelGripX = 22f;
    [Tooltip("Longitudinal grip (accel/braking)")]
    public float wheelGripZ = 42f;
    public float suspensionForce = 90f;
    public float dampAmount = 2.5f;
    public float suspensionForceClamp = 200f;
    [Tooltip("Max suspension travel change per frame for the damper (m). Smaller = curbs feel softer, larger = stiffer/sharper jolt. Caps the geometric jump when hitting a curb so the car doesn't rear up")]
    public float maxSuspensionStep = 0.025f;
    [Tooltip("Suspension velocity damper (fraction of suspensionForce). Damps compression/rebound speed so the car doesn't bounce. Too high = the damper itself oscillates. 0.05..0.08 is fine. 0 = off.")]
    public float suspensionDampRatio = 0.06f;
    [Tooltip("Layers the wheels rest on (road, ramp, curbs). Anything not in the mask (barriers, walls, posts, decor) is ignored by suspension - wheels won't climb it. Default = everything; to keep wheels off obstacles, put them on a separate layer and remove it from the mask.")]
    public LayerMask groundMask = ~0;
    // Reused buffer for the suspension SphereCast (no per-frame allocations).
    static readonly RaycastHit[] _suspCastBuf = new RaycastHit[16];
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public bool forwards = true;

    public enum TransmissionMode { Park, Drive, Reverse, Neutral }
    public TransmissionMode transmissionMode = TransmissionMode.Park;
    public TransmissionMode CurrentMode => transmissionMode;

    // When false, the car ignores ALL driver input (steering, throttle, brake, gear keys).
    // MenuManager clears this while the menu/pause owns the car (cinematic fly-over, paused),
    // so the car can't be driven away from under the menu, and restores it on cockpit entry.
    [HideInInspector] public bool inputEnabled = true;

    // Enabled externally (HillStartExercise) - holds the car on a slope with the brake
    [HideInInspector] public bool hillHoldAllowed = false;
    [Range(0f, 5f)] public float hillHoldSpeedThreshold = 0.5f;

    // Hard lock: car can't roll back at all (hill-start exercise)
    [HideInInspector] public bool fullStopHold = false;
    private bool _isHardLocked = false;
    private Vector3 _lockPosition;
    private Quaternion _lockRotation;

    [Header("Automatic transmission: shift lock and creep")]
    [Tooltip("Max speed (km/h) at which you can shift D<->R")]
    public float shiftLockSpeed = 5f;
    [Tooltip("Enable creep in D/R when off-throttle and not braking (torque converter feel)")]
    public bool creepEnabled = true;
    [Tooltip("Creep speed in km/h (a real automatic creeps ~7 km/h)")]
    public float creepSpeedKmh = 7f;
    [Tooltip("Creep strength (0..0.3 of full throttle)")]
    [Range(0f, 0.5f)] public float creepThrottle = 0.18f;
    [Tooltip("Engine braking strength when off-throttle")]
    [Range(0f, 0.5f)] public float engineBrakeFactor = 0.22f;
    [Tooltip("Aerodynamic drag (speed squared). 0.4 is typical for a sedan")]
    [Range(0f, 2f)] public float airDragCoeff = 0.4f;

    /// <summary>
    /// World position of the i-th wheel.
    /// Used by external scripts (CarBordureDetector, ParkingZone, ControlLineTrigger).
    /// </summary>
    public Vector3 GetWheelPosition(int index)
    {
        if (wheels == null || index < 0 || index >= wheels.Length) return transform.position;
        var w = wheels[index];
        if (w.wheelObject != null) return w.wheelObject.transform.position;
        return transform.TransformPoint(w.localPosition);
    }

    public int WheelCount => wheels != null ? wheels.Length : 0;

    [Header("Brake lights")]
    public GameObject[] brakeLights;

    [Header("Reverse lights")]
    public GameObject[] reverseLights;

    [Header("Debug")]
    public bool debugLog = true;
    [Tooltip("Interval between periodic state logs, sec")]
    public float debugLogInterval = 0.5f;
    private float _lastDebugTime = 0f;

    [Header("Physics Debug (wheel/body diagnostics)")]
    [Tooltip("Enable detailed [PHYS] physics log. Logs per-frame on anomalies (launch/spin/tip-over/force spike) + a rare heartbeat. Nearly silent during normal driving.")]
    public bool physicsDebug = false;
    [Tooltip("Physics-log heartbeat interval during calm driving, sec")]
    public float physicsDebugHeartbeat = 0.5f;
    private float _lastPhysHeartbeat = 0f;
    private float _physElapsed = 0f;
    private Vector3 _prevVel = Vector3.zero;


    // Assists
    public bool steeringAssist = true;
    [Range(0f, 1f)] public float steeringAssistStrength = 0.2f; // Strength of steering assist
    public bool throttleAssist = true;
    public bool brakeAssist = true;
    [HideInInspector] public Vector2 userInput = Vector2.zero;
    public enum InputMode { Keyboard, Wheel }
    [Header("Input mode")]
    public InputMode inputMode = InputMode.Keyboard;

    [HideInInspector] public bool  externalInput    = false;
    [HideInInspector] public float externalThrottle = 0f;
    [HideInInspector] public float externalBrake    = 0f;
    [HideInInspector] public float externalSteer    = 0f;
    [Tooltip("Steering smoothing time constant (sec) for a physical wheel/pedals. A wheel is already analog - use a small value (0.01..0.03) for instant response. 0 = direct, no filter.")]
    public float wheelSteerSmoothing = 0.018f;
    [Tooltip("Quadratic downforce: F = v² * downforce. ~0.1..0.3 for a sedan. Increases grip with speed")]
    public float downforce = 0.16f;
    [HideInInspector] public float isBraking = 0f;
    public Vector3 COMOffset = new Vector3(0, -0.2f, 0);
    public float Inertia = 1.2f; // Multiplier for inertia tensor

    void Awake()
    {
        // Assign rb in Awake (not Start): several exercise scripts read car.rb in their own Start(),
        // and the Start() order between scripts is non-deterministic - it differs between the editor
        // and a build, which made e.g. the pedestrian exercise work in the editor but not the build
        // (its _carRb stayed null, so its Update bailed out every frame). Awake always runs first.
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
    }

    void Start()
    {
        foreach (var w in wheels)
        {
            w.wheelObject = Instantiate(wheelPrefab, transform);
            w.wheelObject.transform.localPosition = w.localPosition;
            w.wheelObject.transform.eulerAngles = transform.eulerAngles;
            w.wheelObject.transform.localScale = 2f * new Vector3(w.size, w.size, w.size);
            w.wheelCircumference = 2f * Mathf.PI * w.size;

            // Measure the actual tire bottom from the mesh so the wheel touches the road
            // with no gap regardless of model geometry (size stays the physical radius).
            w.visualBottomOffset = w.size;
            var rends = w.wheelObject.GetComponentsInChildren<MeshRenderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                w.visualBottomOffset = w.wheelObject.transform.position.y - b.min.y;
            }

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

        // Guard against launching skyward on a hard landing/flip:
        // cap depenetration speed out of deep collider overlap and max angular
        // velocity so the car doesn't get flung or spun.
        rb.maxDepenetrationVelocity = 1.5f;
        rb.maxAngularVelocity       = 7f;
        // More solver iterations so a resting/jammed collider doesn't jitter.
        rb.solverIterations         = 12;
        rb.solverVelocityIterations = 4;
    }

    void Update()
    {
        externalInput = (inputMode == InputMode.Wheel);

        // Menu/pause owns the car: ignore every driver input and keep it parked in place.
        if (!inputEnabled)
        {
            userInput = Vector2.zero;
            return;
        }

        if (LegacyInput.GetKeyDown(KeyCode.R))
        {
            transform.rotation = Quaternion.identity;
            transform.position += Vector3.up * 2f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // In Park only leaving Park is allowed (F or S), everything else is blocked
        if (transmissionMode == TransmissionMode.Park)
        {
            if (LegacyInput.GetKeyDown(KeyCode.F))
                transmissionMode = TransmissionMode.Drive;
            else if (LegacyInput.GetKeyDown(KeyCode.S))
                transmissionMode = TransmissionMode.Reverse;
            return;
        }

        // Transmission mode switching (guard: can't shift D<->R while moving)
        float currentSpeedKmh = rb.linearVelocity.magnitude * 3.6f;
        if (LegacyInput.GetKeyDown(KeyCode.P))
        {
            transmissionMode = TransmissionMode.Park; // Park is always allowed
        }
        else if (LegacyInput.GetKeyDown(KeyCode.S))
        {
            if (currentSpeedKmh < shiftLockSpeed)
                transmissionMode = TransmissionMode.Reverse;
            else
                GameLog.Warn($"<color=#FF8C00>[GEAR LOCK]</color> Can't engage Reverse at {currentSpeedKmh:F1} km/h. Stop first.");
        }
        else if (LegacyInput.GetKeyDown(KeyCode.F))
        {
            if (currentSpeedKmh < shiftLockSpeed)
                transmissionMode = TransmissionMode.Drive;
            else
                GameLog.Warn($"<color=#FF8C00>[GEAR LOCK]</color> Can't engage Drive at {currentSpeedKmh:F1} km/h. Stop first.");
        }

        // ── Input ───────────────────────────────────────────────────────────────
        float rawThrottle = 0f;
        bool  brakePedal  = false;

        // Steering slows down with speed: the faster you go, the heavier the wheel.
        // Divisor 18 (was 28) - noticeably less twitchy at 30-60 km/h.
        float speedSteerScale = 1f / (1f + rb.linearVelocity.magnitude / 18f);

        if (externalInput)
        {
            // Wheel/pedals are analog - near-instant response (no keyboard filter needed).
            float steerTarget = externalSteer * speedSteerScale;
            userInput.x = wheelSteerSmoothing <= 0f
                ? steerTarget
                : Mathf.Lerp(userInput.x, steerTarget, 1f - Mathf.Exp(-Time.deltaTime / wheelSteerSmoothing));
            rawThrottle = externalThrottle;
            brakePedal  = externalBrake > 0.02f;
        }
        else
        {
            // Keyboard gives an instant ±1: smooth more so the car doesn't jerk.
            float steerTarget = LegacyInput.GetAxisRaw("Horizontal") * speedSteerScale;
            userInput.x = Mathf.Lerp(userInput.x, steerTarget, 1f - Mathf.Exp(-Time.deltaTime / 0.18f));
            rawThrottle = Mathf.Max(0f, LegacyInput.GetAxisRaw("Vertical"));
            brakePedal  = LegacyInput.GetKey(KeyCode.Space);
        }

        // ── Creep + engine brake (always, regardless of input source) ────
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

            // Traction control: triggers earlier for a training car - cut power when a wheel
            // is already at 70%+ of its grip limit, to prevent a full breakaway.
            if (throttleAssist)
            {
                float targetSlip = 0.70f;     // Training car: keep grip in reserve
                float slipTolerance = 0.08f;  // Wider stability band - fewer oscillations
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

            // Steering assist: only steps in when a wheel is really close to breaking away (slip > 0.6),
            // not on every turn during normal driving.
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

        // Brake strength 0..1 for downshift logic:
        // brake pedal = 1.0, opposing throttle = its magnitude, hillHold = 0.3
        float brakeStrength01 = 0f;
        if (brakePedal) brakeStrength01 = 1f;
        else if (wrongDirection) brakeStrength01 = Mathf.Abs(rawThrottle);
        else if (hillHold) brakeStrength01 = 0.3f;

        e.checkGearSwitching(this, Mathf.Abs(rawThrottle), brakeStrength01);

        // Periodic state debug log
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
            GameLog.Info(
                $"[CAR] mode=<b>{mode}</b> gear=<b>{e.getCurrentGear()}</b> " +
                $"RPM=<b>{e.getRPM():F0}</b> speed=<b>{speedKmh:F1} km/h</b> " +
                $"gas={gas} brake={brake}"
            );
        }
    }

    // Detailed physics diagnostics. Logs per-frame ONLY on an anomaly
    // (launch/sharp impulse/spin/tip-over/suspension force spike) plus a rare
    // heartbeat during calm driving, so the log shows the exact glitch without noise.
    static readonly string[] _wheelNames4 = { "RR", "FR", "FL", "RL" };
    void LogPhysics()
    {
        _physElapsed += Time.fixedDeltaTime;

        Vector3 vel   = rb.linearVelocity;
        Vector3 accel = (vel - _prevVel) / Mathf.Max(1e-5f, Time.fixedDeltaTime);
        _prevVel      = vel;

        float spd  = vel.magnitude * 3.6f;
        float velY = vel.y;
        float angV = rb.angularVelocity.magnitude;
        float tilt = Vector3.Angle(transform.up, Vector3.up);

        int   groundedCount = 0;
        float maxN = 0f;
        foreach (var w in wheels)
        {
            if (w.dbgGrounded) groundedCount++;
            if (w.normalForce > maxN) maxN = w.normalForce;
        }
        bool forceSpike = maxN >= suspensionForceClamp * 0.95f;

        // Anomaly condition: something is happening to the car that isn't normal.
        bool anomaly =
            Mathf.Abs(velY) > 4f ||   // launch/fall
            accel.magnitude  > 30f || // sharp impulse (impact/launch)
            angV             > 4f  || // fast spin
            tilt             > 20f || // tip-over
            forceSpike;               // suspension hit its force ceiling

        bool heartbeat = (_physElapsed - _lastPhysHeartbeat) >= physicsDebugHeartbeat;
        if (!anomaly && !heartbeat) return;
        if (heartbeat) _lastPhysHeartbeat = _physElapsed;

        var sb = new StringBuilder(256);
        sb.Append(anomaly ? "[PHYS ⚠ANOMALY]" : "[PHYS beat]");
        sb.Append($" t={_physElapsed:F2} spd={spd:F1}km/h posY={transform.position.y:F2}");
        sb.Append($" velY={velY:+0.0;-0.0} |v|={vel.magnitude:F1} acc={accel.magnitude:F0}");
        sb.Append($" angV={angV:F2} tilt={tilt:F0}° gnd={groundedCount}/{wheels.Length}");
        for (int i = 0; i < wheels.Length; i++)
        {
            var w = wheels[i];
            string nm = (wheels.Length == 4) ? _wheelNames4[i] : $"W{i}";
            sb.Append($"\n   {nm} g={(w.dbgGrounded ? 1 : 0)} gd={w.dbgGroundDist:F3}");
            sb.Append($" comp={w.dbgCompression:F3} N={w.normalForce:F0} cSpd={w.dbgCompSpeed:+0.0;-0.0}");
            sb.Append($" vDmp={w.dbgVelDamp:F0} up={w.dbgUpAlign:F2} drop={w.dbgDrop:F3} slip={w.slip:F2}");
        }
        if (anomaly) GameLog.Warn(sb.ToString());
        else         GameLog.Info(sb.ToString());
    }

    // Body collider impact log (barrier/wall/obstacle) - only with physicsDebug on.
    // Shows what was hit, with what impulse and speed, to diagnose a hard bounce
    // off a barrier.
    void OnCollisionEnter(Collision c)
    {
        if (!physicsDebug) return;
        float spdKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
        Vector3 n = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.zero;
        GameLog.Warn(
            $"[PHYS HIT] '{c.gameObject.name}' impulse={c.impulse.magnitude:F0} " +
            $"relVel={c.relativeVelocity.magnitude:F1} spd={spdKmh:F1}km/h normal=({n.x:F2},{n.y:F2},{n.z:F2})");
    }

    void FixedUpdate()
    {
        // Quadratic downforce: grows with v², like real aerodynamics.
        // Greatly increases normalForce at high speed - and through it, max grip.
        float vSpeed = rb.linearVelocity.magnitude;
        rb.AddForce(-transform.up * vSpeed * vSpeed * downforce);

        // Aerodynamic drag: F = -v * |v| * coeff (speed squared)
        Vector3 horizVel = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        float speed = horizVel.magnitude;
        if (speed > 0.3f)
            rb.AddForce(-horizVel.normalized * speed * speed * airDragCoeff);

        // Yaw damping: damps rotation around the vertical axis.
        // Suppresses spin/wobble without blocking normal turning - torque is proportional to yaw rate.
        if (yawDamping > 0.0001f)
        {
            float yawVel = Vector3.Dot(rb.angularVelocity, transform.up);
            rb.AddTorque(-transform.up * yawVel * yawDamping, ForceMode.Force);
        }
        float averageWheelAngularVelocity = 0f;
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

            // SphereCast with the tire radius: a wheel is a circle, not a point. The sphere
            // rolls smoothly over a curb edge without a distance jump (which used to spike
            // the force and launch the wheel into the arch). The tilted normal at the edge
            // gives a real, proportional jolt - the curb is felt, but the car isn't blown up.
            float castDist = w.suspensionLength + w.size; // downward travel of the wheel center
            bool grounded = false;
            hit = default;
            float nearest = float.MaxValue;
            int hitCount = Physics.SphereCastNonAlloc(
                w.wheelWorldPosition, w.size, -transform.up,
                _suspCastBuf, castDist, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                // Skip the car's own colliders (body/arches/wheels) so the sphere
                // doesn't catch on itself - no layer juggling needed.
                if (_suspCastBuf[i].collider.attachedRigidbody == rb) continue;
                // Barriers (Fence) are NOT support: their top is horizontal, so the wheel
                // would climb onto them, max out the suspension force and flip the car.
                // Only the body box collider should hit a barrier.
                if (_suspCastBuf[i].collider.name.IndexOf("Fence", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                // Ignore walls/barriers: suspension should rest only on surfaces whose
                // normal points up. Otherwise the wheel-radius sphere catches a vertical
                // wall sideways -> groundDist collapses -> the wheel visually jumps into
                // the arch. Wall collisions are still handled by the body box collider.
                // Threshold ~70° (dot<0.34 = wall).
                if (Vector3.Dot(_suspCastBuf[i].normal, transform.up) < 0.34f) continue;
                if (_suspCastBuf[i].distance < nearest)
                {
                    nearest = _suspCastBuf[i].distance;
                    hit = _suspCastBuf[i];
                    grounded = true;
                }
            }
            // Distance to the surface along the ray (like the old Raycast): the sphere
            // center contacts (size) above the surface, hence +size.
            // On flat road this equals the old Raycast value exactly.
            float groundDist = grounded ? hit.distance + w.size : rayLen;
            Vector3 worldVelAtHit = rb.GetPointVelocity(hit.point);
            float lateralHitVel = wheelObj.InverseTransformDirection(worldVelAtHit).x;

            float lateralFriction = -wheelGripX * lateralVel - 2f * lateralHitVel;
            float longitudinalFriction = -wheelGripZ * (w.localVelocity.z - w.angularVelocity * w.size);

            w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;
            w.angularVelocity *= 1 - w.brake * w.brakeStrength * Time.fixedDeltaTime;
            if (LegacyInput.GetKey(KeyCode.LeftShift)) // Handbrake
            {
                w.angularVelocity = 0;
            }

            Vector3 totalLocalForce = new Vector3(lateralFriction, 0f, longitudinalFriction)
                * w.normalForce * coefStaticFriction * Time.fixedDeltaTime;
            float currentMaxFrictionForce = w.normalForce * coefStaticFriction;

            w.slip = currentMaxFrictionForce > 0.0001f
                ? totalLocalForce.magnitude / currentMaxFrictionForce
                : 0f;
            // Smooth static->kinetic transition: full grip while slip<=1.0, then
            // degrade linearly to kinetic over slipBlendWindow.
            // Removes the grip "cliff" that turned a breakaway into an uncontrollable slide.
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
                // Suspension compression from ray geometry. Clamped to rayLen so a tall
                // curb can't inject an enormous force in a single frame.
                float compression = Mathf.Clamp(rayLen - groundDist, 0f, rayLen);

                // Damper on suspension compression speed (travel change per frame). This is
                // what gives the noticeable curb "jolt". The per-frame step is capped
                // (maxSuspensionStep): flat road behaves as before, but a sharp geometric
                // jump on a curb no longer produces a force of hundreds of kN -
                // the jolt stays felt, but the car doesn't rear up.
                float suspStep = Mathf.Clamp(w.lastSuspensionLength - groundDist,
                                             -maxSuspensionStep, maxSuspensionStep);
                float damping = suspStep * dampAmount;

                // Velocity damper: damps compression speed ALONG the suspension axis
                // (transform.up), not along the contact normal. This matters: at a curb/line
                // edge the normal tilts, and using hit.normal let the car's forward speed
                // (tens of km/h) leak into the damper -> a false impulse of tens of kN and a
                // launch/convulsion. Along the suspension axis only real vertical body
                // motion counts. Clamp ±4 m/s so a single contact glitch can't spike the force.
                float compressionSpeed = Mathf.Clamp(-Vector3.Dot(velocityAtWheel, transform.up), -4f, 4f);
                float velDamp = compressionSpeed * suspensionForce * suspensionDampRatio;

                w.normalForce = Mathf.Clamp(
                    (compression + damping) * suspensionForce + velDamp,
                    0f, suspensionForceClamp);

                // Tame chaos on flips/wall jams. upDot = how well the car's up aligns with
                // the contact normal. On flat road and normal ramp climbs the car is aligned
                // with the surface -> upDot≈1 -> full force. When tipped on its side ->
                // upDot drops -> suspension cuts out, so a downed car isn't bounced around.
                // 1.0 when tilt <25°, 0.0 when tilt >60°.
                float upDot   = Vector3.Dot(hit.normal, transform.up);
                float upAlign = Mathf.Clamp01((upDot - 0.5f) / 0.4f);
                w.normalForce *= upAlign;

                // snapshot for the physics log
                w.dbgGrounded = true; w.dbgGroundDist = groundDist; w.dbgCompression = compression;
                w.dbgCompSpeed = compressionSpeed; w.dbgVelDamp = velDamp; w.dbgUpAlign = upAlign;

                // Grip force (friction) is damped by upAlign too - otherwise on its side/roof
                // the friction (from last frame's normalForce) would keep pushing the body
                // off the surface.
                // flip: if the car is upside down (its up points down) the suspension fully
                // cuts out, so the wheels don't push a downed car.
                float flip = Vector3.Dot(transform.up, Vector3.up) < 0.2f ? 0f : 1f;
                w.normalForce *= flip;

                Vector3 springDir = hit.normal * w.normalForce;
                w.suspensionForceDirection = springDir;

                rb.AddForceAtPosition((springDir + totalWorldForce * upAlign) * flip, hit.point);
                w.lastSuspensionLength = groundDist;

                // Visual wheel: seat the tire bottom right on the contact point (via the
                // measured visualBottomOffset - closes the "floating" gap above the road).
                // The lower clamp of 0 stops the wheel rising above its mount on a curb.
                // groundDist changes smoothly (sphere-cast), so the wheel no longer jumps
                // into the arch on a curb (hit.distance used to collapse to 0 and teleport
                // the wheel to its mount).
                float wheelDrop = Mathf.Clamp(groundDist - w.visualBottomOffset, 0f, rayLen);
                wheelObj.position = w.wheelWorldPosition - transform.up * wheelDrop;
                w.dbgDrop = wheelDrop;

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
                w.normalForce = 0f; // wheel in the air - no grip force
                w.lastSuspensionLength = rayLen; // reset travel: no false damper spike on next touchdown
                wheelObj.position = w.wheelWorldPosition - transform.up * (w.suspensionLength + w.size);
                // snapshot for the physics log: wheel in the air
                w.dbgGrounded = false; w.dbgGroundDist = rayLen; w.dbgCompression = 0f;
                w.dbgCompSpeed = 0f; w.dbgVelDamp = 0f; w.dbgUpAlign = 0f;
                w.dbgDrop = w.suspensionLength + w.size;
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

        if (physicsDebug) LogPhysics();

        // Park - brake, and lock only once stopped
        if (transmissionMode == TransmissionMode.Park)
        {
            float spd = rb.linearVelocity.magnitude;
            if (spd < 0.3f)
            {
                // Slow enough - lock it
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                foreach (var w in wheels) w.angularVelocity = 0f;
            }
            else
            {
                // Still moving - apply emergency braking through the wheels
                foreach (var w in wheels) w.brake = 1f;
            }
        }

        // Full stop-hold: in a HillStopZone + brake held = car pinned in place.
        // Brake comes from the active input source: pedal (externalBrake) on a wheel,
        // Space on keyboard. Otherwise the hold didn't trigger on a wheel and the car slid off the slope.
        bool brakeHeld      = externalInput ? (externalBrake > 0.02f) : LegacyInput.GetKey(KeyCode.Space);
        bool shouldHardLock = fullStopHold && brakeHeld;
        if (shouldHardLock)
        {
            if (!_isHardLocked)
            {
                // First locked frame - save the lock pose
                _lockPosition = rb.position;
                _lockRotation = rb.rotation;
                _isHardLocked = true;
            }
            // Pin in place: kill velocities and restore the locked position/rotation
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