using UnityEngine;
using System.Collections;

/// <summary>
///  №2 —    .
///         2 ,     .
///       ControlLineTrigger.cs.
/// </summary>
public class IntersectionExercise : MonoBehaviour
{
    [Header("  ")]
    public float timeLimit = 120f; // 2 

    private bool  _active     = false;
    private bool  _completed  = false;
    private float _timer      = 0f;
    private bool  _overtime15 = false;

    void OnTriggerEnter(Collider other)
    {
        if (_completed || _active) return;
        if (other.GetComponentInParent<Car>() == null) return;
        if (ExamManager.Instance != null && !ExamManager.Instance.IsExerciseUnlocked(2)) return;

        _active = true;
        _timer  = 0f;
        ExamManager.Instance?.SetExerciseActive(2);
        Debug.Log("IntersectionExercise:   №2");
    }

    void OnTriggerExit(Collider other)
    {
        if (!_active || _completed) return;
        if (other.GetComponentInParent<Car>() == null) return;

        _completed = true;
        _active    = false;

        if (!_overtime15)
            ExamManager.Instance?.CompleteExercise(2);
        else
            ExamManager.Instance?.MarkExerciseFailed(2);

        Debug.Log($"IntersectionExercise:   {_timer:F1} .");
    }

    void Update()
    {
        if (!_active || _completed) return;

        //      ExamManager —  Resume (  ).
        _timer = ExamManager.Instance != null
            ? ExamManager.Instance.GetTimeSinceActivation(2)
            : _timer + Time.deltaTime;

        if (!_overtime15 && _timer > timeLimit)
        {
            _overtime15 = true;
            ExamManager.Instance?.AddPenaltyOnce(
                "    №2  2 ",
                ExamManager.P2_OVERTIME, 2);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
