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

            // Without a headset the XR UI ray interactors still register with the XRUIInputModule
            // and feed it an invalid (NaN) controller pose, spamming
            // "Screen position out of view frustum (NaN)" every frame. Disable any XR UI interactor
            // (matched by the IUIInteractor interface via reflection, so we don't hard-depend on a
            // specific XRI version) - the mouse keeps driving the UI.
            int uiInteractorsOff = 0;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null) continue;
                foreach (var itf in mb.GetType().GetInterfaces())
                {
                    if (itf.Name != "IUIInteractor") continue;
                    mb.enabled = false;
                    uiInteractorsOff++;
                    break;
                }
            }

            GameLog.Info($"[XRModeSetup] No headset connected - TrackedPoseDriver, XR Device Simulator " +
                         $"and {uiInteractorsOff} XR UI interactor(s) disabled.");
        }
        else
        {
            GameLog.Info("[XRModeSetup] VR headset active.");
        }
    }
}
