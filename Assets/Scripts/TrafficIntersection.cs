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

    void Start()
    {
        // ��������� ����������� ���� ��� ������ �����
        StartCoroutine(TrafficCycle());
    }

    IEnumerator TrafficCycle()
    {
        while (true)
        {
            // ���� 1: ������� � ����, ������� � �����
            yield return StartCoroutine(RunPhase(sideA, sideB));

            // ���� 2: ������� � ����, ������� � �����
            yield return StartCoroutine(RunPhase(sideB, sideA));
        }
    }

    IEnumerator RunPhase(List<TrafficLight> goSide, List<TrafficLight> stopSide)
    {
        // 1. �������� ������� ��� ������, ������� ��� �������
        SetLights(goSide, TrafficLight.LightState.Green);
        SetLights(stopSide, TrafficLight.LightState.Red);
        yield return new WaitForSeconds(greenTime);

        // 2. �������� ������� (������ ������ 0.5 ������)
        float blinkInterval = 0.5f;
        int blinks = Mathf.RoundToInt(blinkTime / blinkInterval);
        for (int i = 0; i < blinks; i++)
        {
            // ��������: �������� / �������
            if (i % 2 == 0)
                SetLights(goSide, TrafficLight.LightState.Off);
            else
                SetLights(goSide, TrafficLight.LightState.Green);

            yield return new WaitForSeconds(blinkInterval);
        }

        // 3. ������ ���� (��������, ����� �������)
        SetLights(goSide, TrafficLight.LightState.Yellow);
        yield return new WaitForSeconds(yellowTime);

        // 4. ������� ��� ��� ��� ����, � �������+������ ��� ���, ��� ���������
        SetLights(goSide, TrafficLight.LightState.Red);
        SetLights(stopSide, TrafficLight.LightState.RedYellow);
        yield return new WaitForSeconds(redYellowTime);
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
