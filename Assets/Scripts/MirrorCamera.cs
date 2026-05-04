using UnityEngine;

/// <summary>
/// Окончательная версия. Работает корректно при наличии Depth Buffer в RenderTexture.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MirrorCamera : MonoBehaviour
{
    [Header("Render Texture Settings")]
    public RenderTexture mirrorTexture;
    public Renderer mirrorRenderer;
    public int materialIndex = 1;

    [Header("Camera Settings")]
    public float fieldOfView = 40f;
    public float nearClipPlane = 0.01f;
    public float farClipPlane = 1000f;
    public bool flipHorizontal = true;

    private Camera _cam;

    void Start()
    {
        _cam = GetComponent<Camera>();

        // Назначаем текстуру
        _cam.targetTexture = mirrorTexture;

        // Базовая настройка
        _cam.fieldOfView = fieldOfView;
        _cam.nearClipPlane = nearClipPlane;
        _cam.farClipPlane = farClipPlane;

        // Применяем инверсию матрицы для зеркальности
        // Теперь это сработает сразу, так как у камеры есть Depth Buffer
        if (flipHorizontal)
        {
            _cam.ResetProjectionMatrix();
            Matrix4x4 proj = _cam.projectionMatrix;
            proj.m00 = -proj.m00;
            _cam.projectionMatrix = proj;
        }

        // Применяем текстуру на материал
        if (mirrorRenderer != null && mirrorTexture != null)
        {
            Material[] mats = mirrorRenderer.materials;
            if (materialIndex < mats.Length)
            {
                mats[materialIndex].mainTexture = mirrorTexture;
                mirrorRenderer.materials = mats;
            }
        }

        Debug.Log("MirrorCamera: Инициализация завершена успешно.");
    }

    void OnDrawGizmos()
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawFrustum(Vector3.zero, fieldOfView, farClipPlane * 0.1f, nearClipPlane, 2f);
    }
}