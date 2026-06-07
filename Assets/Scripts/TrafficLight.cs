using UnityEngine;

public class TrafficLight : MonoBehaviour
{
    // Possible states of one traffic light
    public enum LightState { Red, RedYellow, Green, BlinkingGreen, Yellow, Off }
    public LightState currentState = LightState.Off;

    // Lamps (Point/Spot Lights)
    [Header("Lamps (Point Lights)")]
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;

    // Lenses (Mesh Renderers)
    [Header("Lenses (Mesh Renderers)")]
    public MeshRenderer redLens;
    public MeshRenderer yellowLens;
    public MeshRenderer greenLens;

    // Emission color (HDR)
    [Header("Emission color (HDR)")]
    [ColorUsage(true, true)] public Color redEmission = Color.red * 3f;
    [ColorUsage(true, true)] public Color yellowEmission = Color.yellow * 3f;
    [ColorUsage(true, true)] public Color greenEmission = Color.green * 3f;

    [Header("Glow boost")]
    [Tooltip("Emission multiplier on the active lens over its set color (bright glow + Bloom). ~×4 over color ×3 ≈ red ×12.")]
    public float emissionBoost = 4f;

    [Header("Billboard glow (long-distance visibility)")]
    public bool enableGlow = true;
    [Tooltip("Minimum fraction of screen height the glow can shrink to. 0.02 ≈ 2% of the screen.")]
    [Range(0.001f, 0.2f)] public float glowMinScreenFraction = 0.02f;
    [Tooltip("Base glow size up close, meters.")]
    public float glowBaseSize = 0.22f;
    [Tooltip("Billboard glow brightness (HDR multiplier). >1 - blooms more.")]
    public float glowIntensity = 2.2f;

    private TrafficLightGlow _redGlow;
    private TrafficLightGlow _yellowGlow;
    private TrafficLightGlow _greenGlow;

    void Awake()
    {
        // Each lens gets its own material instance (renderer.material),
        // so the lamps switch independently and don't share one material.
        EnsureInstancedMaterial(redLens);
        EnsureInstancedMaterial(yellowLens);
        EnsureInstancedMaterial(greenLens);

        if (enableGlow)
        {
            _redGlow    = CreateGlow(redLens,    redEmission,    "RedGlow");
            _yellowGlow = CreateGlow(yellowLens, yellowEmission, "YellowGlow");
            _greenGlow  = CreateGlow(greenLens,  greenEmission,  "GreenGlow");
        }

        // Match the visuals to the initial state.
        SetState(currentState);
    }

    // Set the state
    public void SetState(LightState state)
    {
        currentState = state;

        // 1. First turn everything off (lamp light, lens emission, and glow)
        if (redLight != null) redLight.SetActive(false);
        if (yellowLight != null) yellowLight.SetActive(false);
        if (greenLight != null) greenLight.SetActive(false);

        TurnOffLens(redLens);    SetGlow(_redGlow, false);
        TurnOffLens(yellowLens); SetGlow(_yellowGlow, false);
        TurnOffLens(greenLens);  SetGlow(_greenGlow, false);

        // 2. Turn on only what should be lit
        switch (state)
        {
            case LightState.Red:
                if (redLight != null) redLight.SetActive(true);
                TurnOnLens(redLens, redEmission); SetGlow(_redGlow, true);
                break;

            case LightState.RedYellow:
                if (redLight != null) redLight.SetActive(true);
                if (yellowLight != null) yellowLight.SetActive(true);
                TurnOnLens(redLens, redEmission);       SetGlow(_redGlow, true);
                TurnOnLens(yellowLens, yellowEmission); SetGlow(_yellowGlow, true);
                break;

            case LightState.Green:
            case LightState.BlinkingGreen: // blinking green = solid green here
                if (greenLight != null) greenLight.SetActive(true);
                TurnOnLens(greenLens, greenEmission); SetGlow(_greenGlow, true);
                break;

            case LightState.Yellow:
                if (yellowLight != null) yellowLight.SetActive(true);
                TurnOnLens(yellowLens, yellowEmission); SetGlow(_yellowGlow, true);
                break;

            case LightState.Off:
                break;
        }
    }

    // --- Helper methods for lenses and glow ---

    private void EnsureInstancedMaterial(MeshRenderer lens)
    {
        // Accessing .material creates a unique material instance for this renderer.
        if (lens != null) _ = lens.material;
    }

    private void TurnOnLens(MeshRenderer lens, Color glowColor)
    {
        if (lens != null)
        {
            lens.material.EnableKeyword("_EMISSION");
            lens.material.SetColor("_EmissionColor", glowColor * Mathf.Max(1f, emissionBoost));
        }
    }

    private void TurnOffLens(MeshRenderer lens)
    {
        if (lens != null)
        {
            // Black emission - the lens goes fully dark.
            lens.material.SetColor("_EmissionColor", Color.black);
        }
    }

    private void SetGlow(TrafficLightGlow glow, bool on)
    {
        if (glow != null) glow.SetOn(on);
    }

    private TrafficLightGlow CreateGlow(MeshRenderer lens, Color emission, string name)
    {
        if (lens == null) return null;

        var go = new GameObject(name);
        go.transform.SetParent(lens.transform, false);
        go.transform.localPosition = Vector3.zero;

        var glow = go.AddComponent<TrafficLightGlow>();
        glow.anchorRenderer = lens;
        glow.glowColor = NormalizedGlowColor(emission, glowIntensity);
        glow.minScreenFraction = glowMinScreenFraction;
        glow.baseWorldSize = glowBaseSize;
        glow.ApplyColor(); // Awake already ran with the default colour — push the real one.
        glow.SetOn(false);
        return glow;
    }

    // Normalizes the HDR emission color to a saturated hue and scales by the glow brightness.
    private static Color NormalizedGlowColor(Color c, float intensity)
    {
        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (m < 1e-4f) m = 1f;
        return new Color(c.r / m, c.g / m, c.b / m, 1f) * intensity;
    }
}
