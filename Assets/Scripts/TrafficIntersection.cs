ïusing System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a pair of traffic light groups (sideA/sideB) in alternating phases.
/// Cycle: Green â†’ BlinkingGreen â†’ Yellow â†’ Red, then swap sides.
/// </summary>
public class TrafficIntersection : MonoBehaviour
{
    [Header("ïïïïïï 1 (ïïïïïïïïï ïïïï ïïïïïïïï ïïïïï)")]
    public List<TrafficLight> sideA;

    [Header("ïïïïïï 2 (ïïïïïïïïïïïï ïïïïï)")]
    public List<TrafficLight> sideB;

    [Header("ïïïïïïïïï ïïïïïïï (ï ïïïïïïïï)")]
    public float greenTime     = 25f; // 25s of green - enough to cross, with margin
    public float blinkTime     = 3f;
    public float yellowTime    = 2f;
    public float redYellowTime = 2f;

    void Start()
    {
        // ïïïïïïïïï ïïïïïïïïïïï ïïïï ïïï ïïïïïï ïïïïï
        StartCoroutine(TrafficCycle());
    }

    IEnumerator TrafficCycle()
    {
        while (true)
        {
            // ïïïï 1: ïïïïïïï ï ïïïï, ïïïïïïï ï ïïïïï
            yield return StartCoroutine(RunPhase(sideA, sideB));

            // ïïïï 2: ïïïïïïï ï ïïïï, ïïïïïïï ï ïïïïï
            yield return StartCoroutine(RunPhase(sideB, sideA));
        }
    }

    IEnumerator RunPhase(List<TrafficLight> goSide, List<TrafficLight> stopSide)
    {
        // 1. ïïïïïïïï ïïïïïïï ïïï ïïïïïï, ïïïïïïï ïïï ïïïïïïï
        SetLights(goSide, TrafficLight.LightState.Green);
        SetLights(stopSide, TrafficLight.LightState.Red);
        yield return new WaitForSeconds(greenTime);

        // 2. ïïïïïïïï ïïïïïïï (ïïïïïï ïïïïïï 0.5 ïïïïïï)
        float blinkInterval = 0.5f;
        int blinks = Mathf.RoundToInt(blinkTime / blinkInterval);
        for (int i = 0; i < blinks; i++)
        {
            // ïïïïïïïï: ïïïïïïïï / ïïïïïïï
            if (i % 2 == 0)
                SetLights(goSide, TrafficLight.LightState.Off);
            else
                SetLights(goSide, TrafficLight.LightState.Green);

            yield return new WaitForSeconds(blinkInterval);
        }

        // 3. ïïïïïï ïïïï (ïïïïïïïï, ïïïïï ïïïïïïï)
        SetLights(goSide, TrafficLight.LightState.Yellow);
        yield return new WaitForSeconds(yellowTime);

        // 4. ïïïïïïï ïïï ïïï ïïï ïïïï, ï ïïïïïïï+ïïïïïï ïïï ïïï, ïïï ïïïïïïïïï
        SetLights(goSide, TrafficLight.LightState.Red);
        SetLights(stopSide, TrafficLight.LightState.RedYellow);
        yield return new WaitForSeconds(redYellowTime);
    }

    // ïïïïïïïïïïïïïïï ïïïïïïï ïïï ïïïïïïïïïïïï ïïïïïï ïïïïïï ïïïïïïïïïï
    private void SetLights(List<TrafficLight> lights, TrafficLight.LightState state)
    {
        foreach (var light in lights)
        {
            if (light != null)
                light.SetState(state);
        }
    }
}
