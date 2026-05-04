using UnityEngine;

/// <summary>
/// Attach to a checkpoint GameObject (with BoxCollider Is Trigger = true).
/// When the car drives through it, it activates the assigned GreenLightTimers.
///
/// Usage:
///   CheckGreenLight3 -> OnCheckpointPass mode
///   Create a CheckPoint object on the route -> Add Component -> GreenLightActivator
///   Drag CheckGreenLight3 into the Targets field
/// </summary>
[RequireComponent(typeof(Collider))]
public class GreenLightActivator : MonoBehaviour
{
    [Tooltip("GreenLightTimers activated when the car passes through this trigger")]
    public GreenLightTimer[] targets;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"GreenLightActivator [{name}]: Collider auto-set to Is Trigger");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;

        foreach (var t in targets)
            if (t != null) t.Activate();

        Debug.Log($"GreenLightActivator [{name}]: car passed the checkpoint, activated {targets.Length} timer(s)");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
