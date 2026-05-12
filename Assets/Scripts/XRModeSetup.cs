using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

/// <summary>
/// Disables VR head tracking when no headset is connected.
/// Attach to any object in the scene (e.g. DontDestroyOnLoad).
/// </summary>
public class XRModeSetup : MonoBehaviour
{
    void Awake()
    {
        bool headsetConnected = XRSettings.isDeviceActive;

        if (!headsetConnected)
        {
            // Disable every TrackedPoseDriver in the scene
            foreach (var tpd in FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include))
                tpd.enabled = false;

            // Disable the XR Device Simulator (it simulates a headset with the mouse)
            var simulator = GameObject.Find("XR Device Simulator");
            if (simulator != null) simulator.SetActive(false);

            Debug.Log("[XRModeSetup] No headset connected - TrackedPoseDriver and XR Device Simulator disabled.");
        }
        else
        {
            Debug.Log("[XRModeSetup] VR headset active.");
        }
    }
}
