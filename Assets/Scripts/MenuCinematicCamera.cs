using UnityEngine;

/// <summary>
/// Кинематическая камера главного меню.
/// Плавно летит между заданными точками, показывая трассу.
/// </summary>
public class MenuCinematicCamera : MonoBehaviour
{
    [System.Serializable]
    public class CameraPoint
    {
        public Transform point;
        [Tooltip("Сколько секунд камера держится на этой точке")]
        public float holdTime = 2f;
        [Tooltip("Время перелёта ДО этой точки")]
        public float travelTime = 3f;
    }

    [Header("Точки облёта")]
    public CameraPoint[] points;

    [Header("Настройки")]
    [Tooltip("Плавность поворота камеры")]
    public float rotationSmoothness = 2f;
    public bool loop = true;

    private int   _currentIndex = 0;
    private float _timer        = 0f;
    private bool  _travelling   = false; // true = летим, false = держимся
    private Vector3    _startPos;
    private Quaternion _startRot;

    void Start()
    {
        if (points == null || points.Length == 0) return;

        // Ставим камеру на первую точку сразу
        var first = points[0];
        if (first.point != null)
        {
            transform.position = first.point.position;
            transform.rotation = first.point.rotation;
        }

        _timer     = 0f;
        _travelling = false;
    }

    void Update()
    {
        if (points == null || points.Length < 2) return;

        var current = points[_currentIndex];
        _timer += Time.deltaTime;

        if (!_travelling)
        {
            // Держимся на точке
            if (_timer >= current.holdTime)
            {
                // Начинаем перелёт к следующей
                _timer      = 0f;
                _travelling = true;
                _startPos   = transform.position;
                _startRot   = transform.rotation;
                _currentIndex = (_currentIndex + 1) % points.Length;

                if (_currentIndex == 0 && !loop)
                {
                    enabled = false;
                    return;
                }
            }
        }
        else
        {
            // Летим к следующей точке
            var target      = points[_currentIndex];
            float travelTime = Mathf.Max(0.1f, target.travelTime);
            float t          = Mathf.Clamp01(_timer / travelTime);
            float smooth     = Mathf.SmoothStep(0f, 1f, t); // плавный старт и финиш

            if (target.point != null)
            {
                transform.position = Vector3.Lerp(_startPos, target.point.position, smooth);
                transform.rotation = Quaternion.Slerp(_startRot, target.point.rotation, smooth);
            }

            if (t >= 1f)
            {
                _timer      = 0f;
                _travelling = false;
            }
        }
    }

    /// Рисуем путь камеры в редакторе
    void OnDrawGizmos()
    {
        if (points == null || points.Length < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            if (a.point != null && b.point != null)
                Gizmos.DrawLine(a.point.position, b.point.position);
            if (a.point != null)
                Gizmos.DrawSphere(a.point.position, 0.5f);
        }
    }
}
