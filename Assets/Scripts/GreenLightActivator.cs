using UnityEngine;

/// <summary>
/// Вешается на GameObject-чекпоинт (с BoxCollider Is Trigger = true).
/// Когда машина проезжает через него — активирует назначенные GreenLightTimer-ы.
///
/// Использование:
///   CheckGreenLight3 → режим OnCheckpointPass
///   Создать CheckPoint-объект на маршруте → Add Component → GreenLightActivator
///   Перетащить CheckGreenLight3 в поле Targets
/// </summary>
[RequireComponent(typeof(Collider))]
public class GreenLightActivator : MonoBehaviour
{
    [Tooltip("GreenLightTimer-ы которые активируются при проезде через этот триггер")]
    public GreenLightTimer[] targets;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"GreenLightActivator [{name}]: Collider автоматически переведён в Is Trigger");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Car>() == null) return;

        foreach (var t in targets)
            if (t != null) t.Activate();

        Debug.Log($"GreenLightActivator [{name}]: машина проехала чекпоинт, активировано {targets.Length} таймер(ов)");
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
