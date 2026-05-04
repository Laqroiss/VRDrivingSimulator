using UnityEngine;

public class TrafficLight : MonoBehaviour
{
    // Все возможные состояния нашего светофора
    public enum LightState { Red, RedYellow, Green, BlinkingGreen, Yellow, Off }
    public LightState currentState = LightState.Off;

    [Header("Лампочки (Point Lights)")]
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;

    [Header("Сами стекляшки (Mesh Renderers)")]
    public MeshRenderer redLens;
    public MeshRenderer yellowLens;
    public MeshRenderer greenLens;

    [Header("Цвета свечения (HDR)")]
    [ColorUsage(true, true)] public Color redEmission = Color.red * 3f;
    [ColorUsage(true, true)] public Color yellowEmission = Color.yellow * 3f;
    [ColorUsage(true, true)] public Color greenEmission = Color.green * 3f;

    public void SetState(LightState state)
    {
        currentState = state;

        // 1. Сначала жестко выключаем всё (и свет, и свечение линз)
        if (redLight != null) redLight.SetActive(false);
        if (yellowLight != null) yellowLight.SetActive(false);
        if (greenLight != null) greenLight.SetActive(false);

        TurnOffLens(redLens);
        TurnOffLens(yellowLens);
        TurnOffLens(greenLens);

        // 2. Включаем только то, что нужно сейчас
        switch (state)
        {
            case LightState.Red:
                if (redLight != null) redLight.SetActive(true);
                TurnOnLens(redLens, redEmission);
                break;

            case LightState.RedYellow:
                if (redLight != null) redLight.SetActive(true);
                if (yellowLight != null) yellowLight.SetActive(true);
                TurnOnLens(redLens, redEmission);
                TurnOnLens(yellowLens, yellowEmission);
                break;

            case LightState.Green:
            case LightState.BlinkingGreen: // Само мигание сделаем в другом скрипте
                if (greenLight != null) greenLight.SetActive(true);
                TurnOnLens(greenLens, greenEmission);
                break;

            case LightState.Yellow:
                if (yellowLight != null) yellowLight.SetActive(true);
                TurnOnLens(yellowLens, yellowEmission);
                break;

            case LightState.Off:
                break;
        }
    }

    // --- Вспомогательные функции для работы с материалами ---

    private void TurnOnLens(MeshRenderer lens, Color glowColor)
    {
        if (lens != null)
        {
            lens.material.EnableKeyword("_EMISSION");
            lens.material.SetColor("_EmissionColor", glowColor);
        }
    }

    private void TurnOffLens(MeshRenderer lens)
    {
        if (lens != null)
        {
            // Ставим черный цвет свечения, чтобы линза "потухла"
            lens.material.SetColor("_EmissionColor", Color.black);
        }
    }
}