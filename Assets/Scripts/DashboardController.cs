using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Отвечает только за визуал приборной панели:
/// 1. Спидометр, тахометр, RPM
/// 2. Анимацию рычага поворотников (signalstalk) — читает состояние из CarIndicators
/// 3. Дублирующие UI-иконки поворотников (если нужны вторые иконки помимо тех что в CarIndicators)
///
/// Управление поворотниками и автоотмена по рулю — в CarIndicators, не здесь.
/// </summary>
public class DashboardController : MonoBehaviour
{
    [Header("Тахометр / спидометр")]
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI rpmText;
    public Image tachometerFill;
    public Color normalColor = new Color(0f, 0.5f, 1f);
    public Color warningColor = Color.red;
    public float redLine = 5500f;

    [Header("Передача (P R N D + номер передачи)")]
    public TextMeshProUGUI gearText;
    [Tooltip("Показывать номер передачи рядом с D (D1, D2, ...)")]
    public bool showGearNumberInDrive = true;
    public Color driveColor   = Color.white;
    public Color reverseColor = new Color(1f, 0.4f, 0.2f);
    public Color neutralColor = new Color(0.7f, 0.7f, 0.7f);

    [Header("Дополнительные UI-иконки поворотников (необязательно)")]
    [Tooltip("Оставь пустым если иконки уже настроены в CarIndicators")]
    public Image leftBlinkerIcon;
    public Image rightBlinkerIcon;
    public Color blinkerActiveColor = new Color(1f, 0.6f, 0f, 1f);
    public Color blinkerOffColor = new Color(1f, 0.6f, 0f, 0.2f);

    [Header("Анимация рычага поворотников")]
    public Transform signalStalk;
    public float stalkRotationAngle = 12f;
    public Vector3 stalkRotationAxis = Vector3.forward;
    public float stalkAnimSpeed = 12f;

    [Header("Ссылки")]
    public Car carScript;
    public CarIndicators carIndicators;

    private Quaternion _stalkNeutral;
    private bool _stalkCaptured;

    void Start()
    {
        if (carIndicators == null) carIndicators = GetComponent<CarIndicators>();
        if (carIndicators == null) carIndicators = GetComponentInParent<CarIndicators>();

        if (signalStalk != null)
        {
            _stalkNeutral = signalStalk.localRotation;
            _stalkCaptured = true;
        }
    }

    void Update()
    {
        if (carScript == null) return;
        if (carScript.rb == null) return; // машина ещё не инициализирована (меню)

        UpdateEngineUI();

        if (carIndicators != null)
        {
            UpdateExtraIcons();
            UpdateStalkAnimation();
        }
    }

    void UpdateEngineUI()
    {
        float rpm = carScript.e.getRPM();
        bool warn = rpm >= redLine;

        if (speedText != null)
            speedText.text = Mathf.RoundToInt(carScript.rb.linearVelocity.magnitude * 3.6f).ToString();

        if (rpmText != null)
        {
            rpmText.text = (Mathf.RoundToInt(rpm / 100f) * 100).ToString();
            rpmText.color = warn ? warningColor : normalColor;
        }

        if (tachometerFill != null)
        {
            tachometerFill.fillAmount = Mathf.InverseLerp(carScript.e.IdleRPM, carScript.e.MaxRPM, rpm);
            tachometerFill.color = warn ? warningColor : normalColor;
        }

        UpdateGearText();
    }

    void UpdateGearText()
    {
        if (gearText == null) return;

        string label;
        Color color;

        switch (carScript.transmissionMode)
        {
            case Car.TransmissionMode.Reverse:
                label = "R";
                color = reverseColor;
                break;
            case Car.TransmissionMode.Neutral:
                label = "N";
                color = neutralColor;
                break;
            default: // Drive
                label = showGearNumberInDrive
                    ? "D" + carScript.e.getCurrentGear()
                    : "D";
                color = driveColor;
                break;
        }

        gearText.text  = label;
        gearText.color = color;
    }

    // Синхронизирует дополнительные иконки (если они есть) с CarIndicators
    void UpdateExtraIcons()
    {
        if (leftBlinkerIcon != null)
        {
            bool lit = (carIndicators.HazardLightsOn || carIndicators.LeftIndicatorOn)
                       && carIndicators.BlinkVisible;
            leftBlinkerIcon.color = lit ? blinkerActiveColor : blinkerOffColor;
        }

        if (rightBlinkerIcon != null)
        {
            bool lit = (carIndicators.HazardLightsOn || carIndicators.RightIndicatorOn)
                       && carIndicators.BlinkVisible;
            rightBlinkerIcon.color = lit ? blinkerActiveColor : blinkerOffColor;
        }
    }

    void UpdateStalkAnimation()
    {
        if (signalStalk == null) return;

        if (!_stalkCaptured)
        {
            _stalkNeutral = signalStalk.localRotation;
            _stalkCaptured = true;
        }

        float angle = 0f;
        if (!carIndicators.HazardLightsOn)
        {
            if (carIndicators.LeftIndicatorOn) angle = -stalkRotationAngle;
            else if (carIndicators.RightIndicatorOn) angle = +stalkRotationAngle;
        }

        signalStalk.localRotation = Quaternion.Slerp(
            signalStalk.localRotation,
            _stalkNeutral * Quaternion.AngleAxis(angle, stalkRotationAxis),
            Mathf.Clamp01(Time.deltaTime * stalkAnimSpeed)
        );
    }
}