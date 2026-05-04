using UnityEngine;
using System.Collections;

/// <summary>
/// Упражнение №2 — Повороты на нерегулируемых перекрёстках.
/// Машина должна проехать зону не более чем за 2 минуты, не наезжая на контрольные линии.
/// Контрольные линии добавляют штраф сами через ControlLineTrigger.cs.
/// </summary>
public class IntersectionExercise : MonoBehaviour
{
    [Header("Время на упражнение")]
    public float timeLimit = 120f; // 2 минуты

    private bool  _active     = false;
    private bool  _completed  = false;
    private float _timer      = 0f;
    private bool  _overtime15 = false;

    void OnTriggerEnter(Collider other)
    {
        if (_completed || _active) return;
        if (other.GetComponentInParent<Car>() == null) return;

        _active = true;
        _timer  = 0f;
        ExamManager.Instance?.SetExerciseActive(2);
        Debug.Log("IntersectionExercise: Начало упражнения №2");
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

        Debug.Log($"IntersectionExercise: Завершено за {_timer:F1} сек.");
    }

    void Update()
    {
        if (!_active || _completed) return;

        _timer += Time.deltaTime;

        if (!_overtime15 && _timer > timeLimit)
        {
            _overtime15 = true;
            ExamManager.Instance?.AddPenalty(
                "Затратил на выполнение упражнения №2 более 2 минут",
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
