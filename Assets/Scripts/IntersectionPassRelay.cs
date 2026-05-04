using UnityEngine;

/// <summary>
/// Вешается на большой триггер IntersectionPass (общая зона всего перекрёстка).
/// При выезде машины — останавливает все GreenLightTimer-ы в Intersection_Manager.
/// </summary>
public class IntersectionPassRelay : MonoBehaviour
{
    [Tooltip("Если пусто — автоматически найдёт все GreenLightTimer-ы в родителе")]
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
