using UnityEngine;

/// <summary>
/// Control line - penalty when a wheel ends up over the line.
/// Checks the actual positions of all 4 wheels via Car.GetWheelPosition(),
/// just like CarBordureDetector checks curbs.
/// </summary>
public class ControlLineTrigger : MonoBehaviour
{
    [Header("Exercise number (2, 5 or 6)")]
    public int exerciseNum = 5;

    [Header("Repeat penalty after N seconds (0 = one-shot)")]
    public float cooldown = 5f;

    [Header("Detection tolerance (meters)")]
    [Tooltip("How far a wheel can be from the line and still count as on it")]
    public float wheelTolerance = 0.15f;

    private Car   _car;
    private float _lastPenaltyTime = -100f;
    private bool  _oneShotDone     = false;
    private BoxCollider _col;
    private Transform   _colTransform;
    private float       _maxCheckDist;

    void Start()
    {
        _car = FindAnyObjectByType<Car>();
        _col = GetComponent<BoxCollider>();
        if (_col != null)
        {
            _colTransform = _col.transform;
            Vector3 half = _col.size * 0.5f;
            _maxCheckDist = Mathf.Max(half.x, half.z) + wheelTolerance + 3f;
        }
    }

    void FixedUpdate()
    {
        if (_car == null || _col == null) return;
        if (cooldown <= 0f && _oneShotDone) return;
        if (Time.time - _lastPenaltyTime < cooldown) return;

        // Cheap distance check - skip if the car is far away
        if (Vector3.SqrMagnitude(_car.transform.position - _colTransform.position) > _maxCheckDist * _maxCheckDist)
            return;

        for (int i = 0; i < 4; i++)
        {
            Vector3 wheelPos = _car.GetWheelPosition(i);
            if (IsWheelOnLine(wheelPos))
            {
                TriggerPenalty();
                break;
            }
        }
    }

    bool IsWheelOnLine(Vector3 worldPos)
    {
        Vector3 local = _colTransform.InverseTransformPoint(worldPos) - _col.center;
        Vector3 half  = _col.size * 0.5f;

        return Mathf.Abs(local.x) <= half.x + wheelTolerance &&
               Mathf.Abs(local.z) <= half.z + wheelTolerance;
        // No Y check - wheels are always at ground height
    }

    void TriggerPenalty()
    {
        _lastPenaltyTime = Time.time;
        _oneShotDone     = true;

        int    points;
        string desc;

        switch (exerciseNum)
        {
            case 2:
                points = ExamManager.P2_WHEEL_ON_LINE;
                desc   = "Drove a wheel onto the control line (Ex.2)";
                break;
            case 5:
                points = ExamManager.P5_WHEEL_ON_LINE;
                desc   = "Drove a wheel onto the control line (Ex.5)";
                break;
            case 6:
                points = ExamManager.P6_WHEEL_ON_LINE;
                desc   = "Drove a wheel onto the control line (Ex.6)";
                break;
            default:
                return;
        }

        ExamManager.Instance?.AddPenalty(desc, points, exerciseNum);
    }

    void OnDrawGizmos()
    {
        if (_col == null) _col = GetComponent<BoxCollider>();
        if (_col == null) return;

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(_col.center, _col.size);
        Gizmos.color = new Color(1f, 0.85f, 0f, 1f);
        Gizmos.DrawWireCube(_col.center, _col.size);
    }
}
