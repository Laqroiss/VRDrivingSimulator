#if USE_SPLINES
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Switches the RouteRibbon between three route spline segments as the exam progresses:
///   • Segment 1 - active from the very start.
///   • Segment 2 - after "Reverse parking" (Ex.5) is done.
///   • Segment 3 - after "Parallel parking" (Ex.6) is done.
/// Exactly one segment is shown at any time.
/// </summary>
public class RouteSegmentController : MonoBehaviour
{
    [Header("Route ribbon")]
    public RouteRibbon ribbon;

    [Header("Spline segments")]
    [Tooltip("Active from the start")]
    public SplineContainer segment1;
    [Tooltip("Enabled after reverse parking (Ex.5)")]
    public SplineContainer segment2;
    [Tooltip("Enabled after parallel parking (Ex.6)")]
    public SplineContainer segment3;

    private int _stage = -1;

    void Start()
    {
        if (ribbon == null) ribbon = FindAnyObjectByType<RouteRibbon>();
        Apply(1); // at the start - only the first segment
    }

    void Update()
    {
        var em = ExamManager.Instance;
        bool rearDone     = em != null && em.RearParkingDone;     // Ex.5
        bool parallelDone = em != null && em.ParallelParkingDone; // Ex.6

        int stage = parallelDone ? 3 : rearDone ? 2 : 1;
        if (stage != _stage) Apply(stage);
    }

    void Apply(int stage)
    {
        _stage = stage;

        // Only the current segment's GameObject stays active (2 and 3 are off until their stage)
        if (segment1 != null) segment1.gameObject.SetActive(stage == 1);
        if (segment2 != null) segment2.gameObject.SetActive(stage == 2);
        if (segment3 != null) segment3.gameObject.SetActive(stage == 3);

        if (ribbon == null) return;

        ribbon.splineContainer = stage == 3 ? segment3
                               : stage == 2 ? segment2
                                            : segment1;
        ribbon.BuildSpline(); // rebuild the ribbon for the new spline

        GameLog.Info($"[RouteSegmentController] Segment {stage} active");
    }
}
#endif
