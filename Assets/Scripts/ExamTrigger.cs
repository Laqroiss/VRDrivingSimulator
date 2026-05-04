using UnityEngine;
using System.Collections;

/// <summary>
/// Стартовая / финишная линия.
///
/// ExamStart: машина пересекает линию → проверяет левый поворотник → StartExam().
///   Передаёт управление Exercise1_Start для проверки 10-метровой зоны и ремня.
///
/// ExamFinish (Упр.10): до пересечения — правый поворотник (5 б. если нет).
///   После пересечения — FinishExam().
/// </summary>
public class ExamTrigger : MonoBehaviour
{
    public enum TriggerType { ExamStart, ExamFinish }

    [Header("Тип триггера")]
    public TriggerType triggerType = TriggerType.ExamStart;

    [Header("Ссылки (найдутся автоматически если пусто)")]
    public Exercise1_Start exercise1;

    private bool _triggered  = false;
    private bool _carInside  = false;

    private CarBordureDetector _detector;
    private CarIndicators      _indicators;

    void Start()
    {
        _detector   = FindAnyObjectByType<CarBordureDetector>();
        _indicators = FindAnyObjectByType<CarIndicators>();

        if (exercise1 == null)
            exercise1 = FindAnyObjectByType<Exercise1_Start>();
    }

    void Update()
    {
        if (_triggered || _detector == null) return;

        bool carOverlaps = CheckCarOverlap();

        if (triggerType == TriggerType.ExamStart)
        {
            if (carOverlaps && !_carInside)
                _carInside = true;

            // Машина полностью прошла линию
            if (!carOverlaps && _carInside)
            {
                _carInside = false;
                HandleStart();
            }
        }
        else // ExamFinish
        {
            if (carOverlaps && !_carInside)
            {
                _carInside = true;
                HandleFinish();
            }
        }
    }

    bool CheckCarOverlap()
    {
        if (_detector == null) return false;

        Vector3 center = _detector.transform.position +
                         _detector.transform.up * _detector.centerOffsetY;
        Vector3 pointA = center + _detector.transform.forward * _detector.halfLength;
        Vector3 pointB = center - _detector.transform.forward * _detector.halfLength;

        Collider[] hits = Physics.OverlapCapsule(pointA, pointB, _detector.capsuleRadius);
        foreach (var hit in hits)
            if (hit.gameObject == gameObject) return true;

        return false;
    }

    void HandleStart()
    {
        if (ExamManager.Instance == null) return;
        if (ExamManager.Instance.State != ExamManager.ExamState.WaitingStart) return;

        _triggered = true;

        // Проверка левого поворотника
        if (_indicators == null || !_indicators.LeftIndicatorOn)
            ExamManager.Instance.AddPenalty(
                "Пересёк линию «Старт» с выключенным левым указателем поворота",
                ExamManager.P1_NO_LEFT_BLINKER, 1);

        // Уведомляем Exercise1 о пересечении (проверка ремня и т.д.)
        exercise1?.OnCrossStartLine();

        // Запускаем экзамен
        ExamManager.Instance.StartExam();

        Debug.Log("ExamTrigger: Стартовая линия пересечена");
    }

    void HandleFinish()
    {
        if (ExamManager.Instance == null) return;
        if (ExamManager.Instance.State != ExamManager.ExamState.InProgress) return;

        _triggered = true;

        // Упражнение 10 — правый поворотник до финиша
        ExamManager.Instance.SetExerciseActive(10);

        if (_indicators == null || !_indicators.RightIndicatorOn)
            ExamManager.Instance.AddPenalty(
                "Не включил правый указатель поворота до пересечения финишной линии",
                ExamManager.P10_NO_BLINKER, 10);

        ExamManager.Instance.CompleteExercise(10);
        ExamManager.Instance.FinishExam();

        Debug.Log("ExamTrigger: Финиш!");
    }

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = triggerType == TriggerType.ExamStart
            ? new Color(0f, 1f, 0f, 0.3f)
            : new Color(1f, 0f, 0f, 0.3f);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
