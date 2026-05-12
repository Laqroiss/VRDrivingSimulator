using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

/// <summary>
/// Отключает VR-трекинг головы если шлем не подключён.
/// Повесить на любой объект в сцене (например DontDestroyOnLoad).
/// </summary>
public class XRModeSetup : MonoBehaviour
{
    void Awake()
    {
        bool headsetConnected = XRSettings.isDeviceActive;

        if (!headsetConnected)
        {
            // Отключаем все TrackedPoseDriver в сцене
            foreach (var tpd in FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include))
                tpd.enabled = false;

            // Отключаем XR Device Simulator (он симулирует шлем мышкой)
            var simulator = GameObject.Find("XR Device Simulator");
            if (simulator != null) simulator.SetActive(false);

            Debug.Log("[XRModeSetup] Шлем не подключён — TrackedPoseDriver и XR Device Simulator отключены.");
        }
        else
        {
            Debug.Log("[XRModeSetup] VR шлем активен.");
        }
    }
}
