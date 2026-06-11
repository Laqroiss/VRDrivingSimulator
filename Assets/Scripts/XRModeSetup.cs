using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Disables VR head tracking when no headset is connected.
/// Attach to any object in the scene (e.g. DontDestroyOnLoad).
/// </summary>
public class XRModeSetup : MonoBehaviour
{
    void Awake()
    {
        bool headsetConnected = XRSettings.isDeviceActive;

        // VR renders two high-resolution eye textures, so MSAA costs far more than on a flat screen.
        // Use a lighter 2x in VR (keeps the thin track lines clean without tanking the GPU) and the
        // full 4x on desktop, where there is plenty of headroom.
        ApplyMsaaForDisplay(headsetConnected);

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

    // Sets MSAA on the active URP asset: 2x in VR (cheap, still smooths edges), 4x on desktop.
    static void ApplyMsaaForDisplay(bool vr)
    {
        var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
               ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
            urp.msaaSampleCount = vr ? 2 : 4;
    }
}
