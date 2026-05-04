using UnityEngine;

/// <summary>
/// Attach to the large IntersectionPass trigger (the whole intersection zone).
/// When the car exits, it stops all GreenLightTimers in Intersection_Manager.
/// </summary>
public class IntersectionPassRelay : MonoBehaviour
{
    [Tooltip("If empty - auto-finds all GreenLightTimers in the parent")]
    public GreenLightTimer[] timers;

    void Start()
    {
        if (timers == null || timers.Length == 0)
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            timers = root.GetComponentsInChildren<GreenLightTimer>(includeInactive: true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;

        foreach (var t in timers)
            if (t != null) t.OnExitIntersectionPass();
    }
}
