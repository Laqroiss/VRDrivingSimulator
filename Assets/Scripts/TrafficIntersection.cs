using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a pair of traffic light groups (sideA/sideB) in alternating phases.
/// Cycle: Green → BlinkingGreen → Yellow → Red, then swap sides.
/// </summary>
public class TrafficIntersection : MonoBehaviour
{
    [Header("������ 1 (��������� ���� �������� �����)")]
    public List<TrafficLight> sideA;

    [Header("������ 2 (������������ �����)")]
    public List<TrafficLight> sideB;

    [Header("��������� ������� (� ��������)")]
    public float greenTime     = 25f; // 25 сек зелёного — достаточно для проезда но с запасом
    public float blinkTime     = 3f;
    public float yellowTime    = 2f;
    public float redYellowTime = 2f;

    // Публичное состояние для ExamResultSender
    public string PhaseNameA    { get; private set; } = "Red";
    public string PhaseNameB    { get; private set; } = "Green";
    public float  PhaseRemaining { get; private set; }
    public float  PhaseDuration  { get; private set; }

    void Start()
    {
        StartCoroutine(TrafficCycle());
    }

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
        // Зелёный
        SetLights(goSide, TrafficLight.LightState.Green);
        SetLights(stopSide, TrafficLight.LightState.Red);
        yield return StartCoroutine(TimedPhase(aGoes ? "Green" : "Red", aGoes ? "Red" : "Green", greenTime));

        // Мигающий зелёный
        float blinkInterval = 0.5f;
        int blinks = Mathf.RoundToInt(blinkTime / blinkInterval);
        for (int i = 0; i < blinks; i++)
        {
            SetLights(goSide, i % 2 == 0 ? TrafficLight.LightState.Off : TrafficLight.LightState.Green);
            yield return StartCoroutine(TimedPhase(
                aGoes ? "BlinkGreen" : "Red",
                aGoes ? "Red" : "BlinkGreen", blinkInterval));
        }

        // Жёлтый
        SetLights(goSide, TrafficLight.LightState.Yellow);
        yield return StartCoroutine(TimedPhase(aGoes ? "Yellow" : "Red", aGoes ? "Red" : "Yellow", yellowTime));

        // Красный + красно-жёлтый
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

    // ��������������� ������� ��� ������������ ������ ������ ����������
    private void SetLights(List<TrafficLight> lights, TrafficLight.LightState state)
    {
        foreach (var light in lights)
        {
            if (light != null)
                light.SetState(state);
        }
    }
}
