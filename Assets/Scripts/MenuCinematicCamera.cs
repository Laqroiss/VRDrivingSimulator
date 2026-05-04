using UnityEngine;

/// <summary>
///    .
///     ,  .
/// </summary>
public class MenuCinematicCamera : MonoBehaviour
{
    [System.Serializable]
    public class CameraPoint
    {
        public Transform point;
        [Tooltip("      ")]
        public float holdTime = 2f;
        [Tooltip("    ")]
        public float travelTime = 3f;
    }

    [Header(" ")]
    public CameraPoint[] points;

    [Header("Settings")]
    [Tooltip("  ")]
    public float rotationSmoothness = 2f;
    public bool loop = true;

    private int   _currentIndex = 0;
    private float _timer        = 0f;
    private bool  _travelling   = false; // true = , false = 
    private Vector3    _startPos;
    private Quaternion _startRot;

    void Start()
    {
        if (points == null || points.Length == 0) return;

        // Place the camera at the first point 
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
            //   
            if (_timer >= current.holdTime)
            {
                //    
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
            //    
            var target      = points[_currentIndex];
            float travelTime = Mathf.Max(0.1f, target.travelTime);
            float t          = Mathf.Clamp01(_timer / travelTime);
            float smooth     = Mathf.SmoothStep(0f, 1f, t); //    

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

    ///     
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
