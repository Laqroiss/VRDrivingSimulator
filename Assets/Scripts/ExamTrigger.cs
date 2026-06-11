using UnityEngine;
using System.Collections;

/// <summary>
/// Start / finish line.
///
/// ExamStart: the car crosses the line -> checks the left turn signal -> StartExam().
///   Hands off to Exercise1_Start to check the 10-meter zone and seatbelt.
///
/// ExamFinish (Ex.10): before crossing - right turn signal (5 pts if missing).
///   After crossing - FinishExam().
/// </summary>
public class ExamTrigger : MonoBehaviour
{
    public enum TriggerType { ExamStart, ExamFinish }

    [Header("Trigger type")]
    public TriggerType triggerType = TriggerType.ExamStart;

    [Header("References (auto-found if empty)")]
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

            // Car has fully passed the line
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

        // Start the exam FIRST: StartExam() clears the penalty list and resets the total, so any
        // start-line penalty (seatbelt, left signal) MUST be added after it - otherwise it is wiped
        // out the instant it's recorded, and no start violation is ever counted.
        ExamManager.Instance.StartExam();

        // Notify Exercise1 of the crossing - it owns the seatbelt AND left-signal checks.
        // Only fall back to checking the signal here if no Exercise1 is wired, otherwise the
        // left-signal penalty would be added twice (here and in OnCrossStartLine).
        if (exercise1 != null)
            exercise1.OnCrossStartLine();
        else if (_indicators == null || !_indicators.LeftIndicatorOn)
            ExamManager.Instance.AddPenalty(
                "Crossed the \"Start\" line with the left turn signal off",
                ExamManager.P1_NO_LEFT_BLINKER, 1);

        GameLog.Info("ExamTrigger: Start line crossed");
    }

    void HandleFinish()
    {
        if (ExamManager.Instance == null) return;
        if (ExamManager.Instance.State != ExamManager.ExamState.InProgress) return;

        _triggered = true;

        // Exercise 10 - right turn signal before the finish
        ExamManager.Instance.SetExerciseActive(10);

        if (_indicators == null || !_indicators.RightIndicatorOn)
            ExamManager.Instance.AddPenalty(
                "Didn't turn on the right signal before crossing the finish line",
                ExamManager.P10_NO_BLINKER, 10);

        ExamManager.Instance.CompleteExercise(10);
        ExamManager.Instance.FinishExam();

        GameLog.Info("ExamTrigger: Finish!");
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
