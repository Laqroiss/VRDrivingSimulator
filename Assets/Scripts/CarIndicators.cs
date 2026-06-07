using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main turn-signal script. Handles:
/// - Input (Z = left, C = right, X = hazards)
/// - 3D light objects (lamps)
/// - UI icons on Canvas
/// - Blinking
/// - Auto-cancel when the wheel returns to center
/// </summary>
public class CarIndicators : MonoBehaviour
{
    [Header("Control keys")]
    public KeyCode leftIndicatorKey = KeyCode.Z;
    public KeyCode rightIndicatorKey = KeyCode.C;
    public KeyCode hazardLightKey = KeyCode.X;

    [Header("3D light objects (optional)")]
    [Tooltip("All left turn-signal objects: front, rear, mirror")]
    public GameObject[] leftIndicatorLights;
    [Tooltip("All right turn-signal objects: front, rear, mirror")]
    public GameObject[] rightIndicatorLights;

    [Header("UI icons on Canvas (optional)")]
    [Tooltip("Left turn-signal icon. Leave empty if driven by DashboardController")]
    public Image leftUIIcon;
    [Tooltip("Right turn-signal icon. Leave empty if driven by DashboardController")]
    public Image rightUIIcon;

    [Header("Icon colors")]
    public Color iconOnColor = new Color(1f, 0.6f, 0f, 1f);          // Orange - active
    public Color iconOffColor = new Color(1f, 0.6f, 0f, 0.2f);        // Dim orange - off

    [Header("Blink")]
    [Tooltip("One blink period in seconds (0.5 = standard)")]
    public float blinkInterval = 0.5f;

    [Header("Relay sound")]
    public AudioSource relayAudioSource;
    public AudioClip   relayClick;
    [Tooltip("Where the click sits within the clip, in seconds (yours is 0.1)")]
    public float       relayClickOffset = 0.1f;

    [Header("Auto-cancel by steering")]
    [Tooltip("How far the wheel must turn (0..1) to arm auto-cancel")]
    [Range(0f, 1f)] public float armSteerThreshold = 0.30f;
    [Tooltip("How close to center the wheel must return for the signal to switch off")]
    [Range(0f, 1f)] public float cancelSteerThreshold = 0.05f;

    // --- Public state (read-only from outside) ---
    public bool LeftIndicatorOn { get; private set; }
    public bool RightIndicatorOn { get; private set; }
    public bool HazardLightsOn { get; private set; }
    /// <summary>Current blink phase: true = lights are on right now.</summary>
    public bool BlinkVisible => _blinkState;

    // --- Private fields ---
    private float _blinkTimer;
    private bool  _blinkState;
    private bool  _leftArmed;
    private bool  _rightArmed;
    private bool  _relayPlayed; // whether the sound already played this cycle

    private Car _car;

    void Awake()
    {
        _car = GetComponent<Car>();
        if (_car == null) _car = GetComponentInParent<Car>();

        ResetIconColors();
    }

    void Update()
    {
        HandleInput();
        UpdateBlink();
        UpdateAutoCancel();
    }

    // ─── Input ───────────────────────────────────────────────────────────────

    void HandleInput()
    {
        if (LegacyInput.GetKeyDown(leftIndicatorKey))
        {
            if (LeftIndicatorOn) TurnOffLeft();
            else { TurnOffRight(); TurnOnLeft(); }
        }

        if (LegacyInput.GetKeyDown(rightIndicatorKey))
        {
            if (RightIndicatorOn) TurnOffRight();
            else { TurnOffLeft(); TurnOnRight(); }
        }

        if (LegacyInput.GetKeyDown(hazardLightKey))
        {
            if (HazardLightsOn) TurnOffHazard();
            else TurnOnHazard();
        }
    }

    // ─── Blinking ──────────────────────────────────────────────────────────────

    void UpdateBlink()
    {
        if (!LeftIndicatorOn && !RightIndicatorOn && !HazardLightsOn) return;

        _blinkTimer += Time.deltaTime;

        // Play the sound only in the "off" phase - the click lands on the lights turning ON
        float triggerTime = blinkInterval - relayClickOffset;
        if (!_relayPlayed && !_blinkState && _blinkTimer >= triggerTime)
        {
            if (relayAudioSource != null && relayClick != null)
                relayAudioSource.PlayOneShot(relayClick);
            _relayPlayed = true;
        }

        if (_blinkTimer < blinkInterval) return;

        _blinkTimer  = 0f;
        _relayPlayed = false;
        _blinkState  = !_blinkState;
        ApplyBlink();
    }

    void ApplyBlink()
    {

        Color uiColor = _blinkState ? iconOnColor : iconOffColor;

        bool leftShouldBlink = LeftIndicatorOn || HazardLightsOn;
        bool rightShouldBlink = RightIndicatorOn || HazardLightsOn;

        if (leftShouldBlink)
        {
            SetLights(leftIndicatorLights, _blinkState);
            if (leftUIIcon != null) leftUIIcon.color = uiColor;
        }

        if (rightShouldBlink)
        {
            SetLights(rightIndicatorLights, _blinkState);
            if (rightUIIcon != null) rightUIIcon.color = uiColor;
        }
    }

    // ─── Auto-cancel by steering ───────────────────────────────────────────────

    /// <summary>
    /// Two-phase logic like a real car:
    /// 1. Wheel turns toward the signal -> the system "arms"
    /// 2. Wheel returns to center -> the signal switches off
    /// Hazards are not cancelled by steering.
    /// </summary>
    void UpdateAutoCancel()
    {
        if (HazardLightsOn || _car == null) return;

        float steer = _car.userInput.x; // -1..1

        if (LeftIndicatorOn)
        {
            if (steer <= -armSteerThreshold) _leftArmed = true;
            if (_leftArmed && steer >= -cancelSteerThreshold)
            {
                TurnOffLeft();
                // _leftArmed is reset inside TurnOffLeft
            }
        }

        if (RightIndicatorOn)
        {
            if (steer >= armSteerThreshold) _rightArmed = true;
            if (_rightArmed && steer <= cancelSteerThreshold)
            {
                TurnOffRight();
            }
        }
    }

    // ─── Public on/off methods ─────────────────────────────────────────────────


    public void TurnOnLeft()
    {
        LeftIndicatorOn = true;
        _leftArmed = false;
        _blinkTimer = 0f;
        _blinkState = true;
        ApplyBlink();
        _relayPlayed = true; // first click - right when turned on
        if (relayAudioSource != null && relayClick != null)
            relayAudioSource.PlayOneShot(relayClick);
        Debug.Log("CarIndicators: Left turn signal ON");
    }

    public void TurnOffLeft()
    {
        LeftIndicatorOn = false;
        _leftArmed = false;
        SetLights(leftIndicatorLights, false);
        if (leftUIIcon != null) leftUIIcon.color = iconOffColor;
        Debug.Log("CarIndicators: Left turn signal OFF");
    }

    public void TurnOnRight()
    {
        RightIndicatorOn = true;
        _rightArmed = false;
        _blinkTimer = 0f;
        _blinkState = true;
        ApplyBlink();
        _relayPlayed = true;
        if (relayAudioSource != null && relayClick != null)
            relayAudioSource.PlayOneShot(relayClick);
        Debug.Log("CarIndicators: Right turn signal ON");
    }

    public void TurnOffRight()
    {
        RightIndicatorOn = false;
        _rightArmed = false;
        SetLights(rightIndicatorLights, false);
        if (rightUIIcon != null) rightUIIcon.color = iconOffColor;
        Debug.Log("CarIndicators: Right turn signal OFF");
    }

    public void TurnOnHazard()
    {
        HazardLightsOn = true;
        LeftIndicatorOn = false;
        RightIndicatorOn = false;
        _leftArmed = _rightArmed = false;
        _blinkTimer = 0f;
        _blinkState = true;
        ApplyBlink();
        _relayPlayed = true;
        if (relayAudioSource != null && relayClick != null)
            relayAudioSource.PlayOneShot(relayClick);
        Debug.Log("CarIndicators: Hazards ON");
    }

    public void TurnOffHazard()
    {
        HazardLightsOn = false;
        SetLights(leftIndicatorLights,  false);
        SetLights(rightIndicatorLights, false);
        ResetIconColors();
        Debug.Log("CarIndicators: Hazards OFF");
    }

    private void SetLights(GameObject[] lights, bool active)
    {
        if (lights == null) return;
        foreach (var go in lights)
            if (go != null) go.SetActive(active);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private void ResetIconColors()
    {
        if (leftUIIcon != null) leftUIIcon.color = iconOffColor;
        if (rightUIIcon != null) rightUIIcon.color = iconOffColor;
    }
}