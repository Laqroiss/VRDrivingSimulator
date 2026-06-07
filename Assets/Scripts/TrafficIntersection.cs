using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a pair of traffic light groups (sideA/sideB) in alternating phases.
/// Cycle: Green → BlinkingGreen → Yellow → Red, then swap sides.
/// </summary>
public class TrafficIntersection : MonoBehaviour
{
    [Header("Side 1 (lights for the first direction)")]
    public List<TrafficLight> sideA;

    [Header("Side 2 (opposite lights)")]
    public List<TrafficLight> sideB;

    [Header("Phase durations (seconds)")]
    public float greenTime     = 25f; // 25s of green - enough to cross, with margin
    public float blinkTime     = 3f;
    public float yellowTime    = 2f;
    public float redYellowTime = 2f;

    // Public state for ExamResultSender
    public string PhaseNameA    { get; private set; } = "Red";
    public string PhaseNameB    { get; private set; } = "Green";
    public float  PhaseRemaining { get; private set; }
    public float  PhaseDuration  { get; private set; }

    private Coroutine _cycleCoroutine;

    void Start()
    {
        _cycleCoroutine = StartCoroutine(TrafficCycle());
    }

    public void StopCycle()
    {
        if (_cycleCoroutine != null) { StopCoroutine(_cycleCoroutine); _cycleCoroutine = null; }
    }

    public void ResumeCycle()
    {
        if (_cycleCoroutine == null) _cycleCoroutine = StartCoroutine(TrafficCycle());
    }

    public void ForcePhase(string phaseNameA, string phaseNameB)
    {
        PhaseNameA = phaseNameA;
        PhaseNameB = phaseNameB;
        SetLights(sideA, ParseState(phaseNameA));
        SetLights(sideB, ParseState(phaseNameB));
    }

    static TrafficLight.LightState ParseState(string name) => name switch
    {
        "Green"      => TrafficLight.LightState.Green,
        "BlinkGreen" => TrafficLight.LightState.BlinkingGreen,
        "Yellow"     => TrafficLight.LightState.Yellow,
        "Red"        => TrafficLight.LightState.Red,
        "RedYellow"  => TrafficLight.LightState.RedYellow,
        _            => TrafficLight.LightState.Off,
    };

    IEnumerator TrafficCycle()
    {
        while (true)
        {
            yield return StartCoroutine(RunPhase(sideA, sideB, true));
            yield return StartCoroutine(RunPhase(sideB, sideA, false));
        }
    }

    IEnumerator RunPhase(List<TrafficLight> goSide, List<TrafficLight> stopSide, bool aGoes)
    {
        // Green
        SetLights(goSide, TrafficLight.LightState.Green);
        SetLights(stopSide, TrafficLight.LightState.Red);
        yield return StartCoroutine(TimedPhase(aGoes ? "Green" : "Red", aGoes ? "Red" : "Green", greenTime));

        // Blinking green
        float blinkInterval = 0.5f;
        int blinks = Mathf.RoundToInt(blinkTime / blinkInterval);
        for (int i = 0; i < blinks; i++)
        {
            SetLights(goSide, i % 2 == 0 ? TrafficLight.LightState.Off : TrafficLight.LightState.Green);
            yield return StartCoroutine(TimedPhase(
                aGoes ? "BlinkGreen" : "Red",
                aGoes ? "Red" : "BlinkGreen", blinkInterval));
        }

        // Yellow
        SetLights(goSide, TrafficLight.LightState.Yellow);
        yield return StartCoroutine(TimedPhase(aGoes ? "Yellow" : "Red", aGoes ? "Red" : "Yellow", yellowTime));

        // Red + red-yellow
        SetLights(goSide, TrafficLight.LightState.Red);
        SetLights(stopSide, TrafficLight.LightState.RedYellow);
        yield return StartCoroutine(TimedPhase(aGoes ? "Red" : "RedYellow", aGoes ? "RedYellow" : "Red", redYellowTime));
    }

    IEnumerator TimedPhase(string nameA, string nameB, float duration)
    {
        PhaseNameA    = nameA;
        PhaseNameB    = nameB;
        PhaseDuration = duration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            PhaseRemaining = duration - elapsed;
            elapsed += Time.deltaTime;
            yield return null;
        }
        PhaseRemaining = 0f;
    }

    // Helper to apply a state to a whole group of lights
    private void SetLights(List<TrafficLight> lights, TrafficLight.LightState state)
    {
        foreach (var light in lights)
        {
            if (light != null)
                light.SetState(state);
        }
    }
}
