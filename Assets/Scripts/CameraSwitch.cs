using UnityEngine;

public class CameraSwitch : MonoBehaviour
{
    [Header("Assign your cameras here")]
    public Camera firstPersonCamera;
    public Camera thirdPersonCamera;

    [Header("Settings")]
    public KeyCode switchKey = KeyCode.V;
    public bool startWithFirstPerson = true;

    private bool isFirstPerson;

    void Start()
    {
        // ïïïïïïïïïïïïï ïïïïïïïïï ïïïïïï
        isFirstPerson = startWithFirstPerson;
        UpdateCameraState();
    }

    void Update()
    {
        // ïïïïïïï ïïïïïï ïïï ïïïïïïïïïïïï ïïïïïï
        if (LegacyInput.GetKeyDown(switchKey))
        {
            isFirstPerson = !isFirstPerson;
            UpdateCameraState();
        }
    }

    void UpdateCameraState()
    {
        if (firstPersonCamera != null) firstPersonCamera.enabled = isFirstPerson;
        if (thirdPersonCamera != null) thirdPersonCamera.enabled = !isFirstPerson;
    }
}
