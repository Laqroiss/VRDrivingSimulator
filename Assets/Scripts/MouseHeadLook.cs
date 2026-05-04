using UnityEngine;

public class MouseHeadLook : MonoBehaviour
{
    public Transform yawPivot;
    public Transform pitchPivot;

    public float sensitivity = 2f;
    public float minPitch = -70f;
    public float maxPitch = 70f;

    float yaw;
    float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = yawPivot.localEulerAngles.y;

        pitch = pitchPivot.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    void Update()
    {
        float mouseX = LegacyInput.GetAxis("Mouse X") * sensitivity;
        float mouseY = LegacyInput.GetAxis("Mouse Y") * sensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        yawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
