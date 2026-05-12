using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главный скрипт поворотников. Управляет:
/// - Вводом (Z = лево, C = право, X = аварийка)
/// - 3D световыми объектами (фары)
/// - UI-иконками на Canvas
/// - Миганием
/// - Автоотменой поворотника при возврате руля к центру
/// </summary>
public class CarIndicators : MonoBehaviour
{
    [Header("Клавиши управления")]
    public KeyCode leftIndicatorKey = KeyCode.Z;
    public KeyCode rightIndicatorKey = KeyCode.C;
    public KeyCode hazardLightKey = KeyCode.X;

    [Header("3D световые объекты (необязательно)")]
    [Tooltip("Все объекты левого поворотника: спереди, сзади, зеркало")]
    public GameObject[] leftIndicatorLights;
    [Tooltip("Все объекты правого поворотника: спереди, сзади, зеркало")]
    public GameObject[] rightIndicatorLights;

    [Header("UI иконки на Canvas (необязательно)")]
    [Tooltip("Иконка левого поворотника. Если управляется из DashboardController — оставь пустым")]
    public Image leftUIIcon;
    [Tooltip("Иконка правого поворотника. Если управляется из DashboardController — оставь пустым")]
    public Image rightUIIcon;

    [Header("Цвета иконок")]
    public Color iconOnColor = new Color(1f, 0.6f, 0f, 1f);          // Оранжевый — активный
    public Color iconOffColor = new Color(1f, 0.6f, 0f, 0.2f);        // Тусклый оранжевый — выключен

    [Header("Мигание")]
    [Tooltip("Период одного мигания в секундах (0.5 = стандарт)")]
    public float blinkInterval = 0.5f;

    [Header("Звук реле")]
    public AudioSource relayAudioSource;
    public AudioClip   relayClick;
    [Tooltip("На какой секунде клипа находится сам щелчок (у вас 0.1)")]
    public float       relayClickOffset = 0.1f;

    [Header("Автоотмена по рулю")]
    [Tooltip("Насколько руль должен повернуться (0..1), чтобы взвести автоотмену")]
    [Range(0f, 1f)] public float armSteerThreshold = 0.30f;
    [Tooltip("Насколько близко к центру должен вернуться руль, чтобы поворотник выключился")]
    [Range(0f, 1f)] public float cancelSteerThreshold = 0.05f;

    // --- Публичное состояние (только чтение снаружи) ---
    public bool LeftIndicatorOn { get; private set; }
    public bool RightIndicatorOn { get; private set; }
    public bool HazardLightsOn { get; private set; }
    /// <summary>Текущая фаза мигания: true = огни горят прямо сейчас.</summary>
    public bool BlinkVisible => _blinkState;

    // --- Приватные поля ---
    private float _blinkTimer;
    private bool  _blinkState;
    private bool  _leftArmed;
    private bool  _rightArmed;
    private bool  _relayPlayed; // сыграли ли уже звук в этом цикле

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

    // ─── Ввод ────────────────────────────────────────────────────────────────

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

    // ─── Мигание ─────────────────────────────────────────────────────────────

    void UpdateBlink()
    {
        if (!LeftIndicatorOn && !RightIndicatorOn && !HazardLightsOn) return;

        _blinkTimer += Time.deltaTime;

        // Играем звук только в "выключенной" фазе — щелчок совпадает с ВКЛЮЧЕНИЕМ
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

    // ─── Автоотмена по рулю ──────────────────────────────────────────────────

    /// <summary>
    /// Двухфазная логика как в реальной машине:
    /// 1. Руль поворачивается в сторону поворотника → система «взводится»
    /// 2. Руль возвращается к центру → поворотник выключается
    /// Аварийка рулём не отменяется.
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
                // _leftArmed сбрасывается внутри TurnOffLeft
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

    // ─── Публичные методы включения/выключения ───────────────────────────────


    public void TurnOnLeft()
    {
        LeftIndicatorOn = true;
        _leftArmed = false;
        _blinkTimer = 0f;
        _blinkState = true;
        ApplyBlink();
        _relayPlayed = true; // первый клик — сразу при включении
        if (relayAudioSource != null && relayClick != null)
            relayAudioSource.PlayOneShot(relayClick);
        Debug.Log("CarIndicators: Левый поворотник ВКЛ");
    }

    public void TurnOffLeft()
    {
        LeftIndicatorOn = false;
        _leftArmed = false;
        SetLights(leftIndicatorLights, false);
        if (leftUIIcon != null) leftUIIcon.color = iconOffColor;
        Debug.Log("CarIndicators: Левый поворотник ВЫКЛ");
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
        Debug.Log("CarIndicators: Правый поворотник ВКЛ");
    }

    public void TurnOffRight()
    {
        RightIndicatorOn = false;
        _rightArmed = false;
        SetLights(rightIndicatorLights, false);
        if (rightUIIcon != null) rightUIIcon.color = iconOffColor;
        Debug.Log("CarIndicators: Правый поворотник ВЫКЛ");
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
        Debug.Log("CarIndicators: Аварийка ВКЛ");
    }

    public void TurnOffHazard()
    {
        HazardLightsOn = false;
        SetLights(leftIndicatorLights,  false);
        SetLights(rightIndicatorLights, false);
        ResetIconColors();
        Debug.Log("CarIndicators: Аварийка ВЫКЛ");
    }

    private void SetLights(GameObject[] lights, bool active)
    {
        if (lights == null) return;
        foreach (var go in lights)
            if (go != null) go.SetActive(active);
    }

    // ─── Вспомогательные ─────────────────────────────────────────────────────

    private void ResetIconColors()
    {
        if (leftUIIcon != null) leftUIIcon.color = iconOffColor;
        if (rightUIIcon != null) rightUIIcon.color = iconOffColor;
    }
}