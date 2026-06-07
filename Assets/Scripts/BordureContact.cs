using UnityEngine;

/// <summary>
/// Detects the car touching a curb.
/// Attach this script to EVERY curb (or to a parent object via GetComponentsInChildren).
///
/// Quick setup on all curbs at once:
/// Create an empty BordureManager object and attach the BordureManager script (below).
/// </summary>
public class BordureContact : MonoBehaviour
{
    // Impact force threshold - a light touch doesn't count as a mistake
    [Tooltip("Minimum impact force to register a touch")]
    public float minImpactForce = 0.5f;

    private float _lastErrorTime = -10f;
    private float _errorCooldown = 2f; // don't duplicate the error more than once every 2 sec

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player") &&
            !collision.gameObject.CompareTag("Car")) return;

        float impulse = collision.impulse.magnitude;
        if (impulse < minImpactForce) return;

        if (Time.time - _lastErrorTime < _errorCooldown) return;
        _lastErrorTime = Time.time;

        SpeedCameraShake.Instance?.TriggerKerbShake();
        ExamManager.Instance?.AddError($"Curb touch ({gameObject.name})");
        Debug.Log($"BordureContact: touch {gameObject.name}, force: {impulse:F2}");
    }
}

// ——————————————————————————————————————————

/// <summary>
/// Curb manager - automatically adds BordureContact to every curb in the group.
/// Attach to the root object of a curb group (e.g. Bordures_Serpantin).
/// </summary>
public class BordureManager : MonoBehaviour
{
    [Header("Automatically add BordureContact to all child objects")]
    public bool autoSetup = true;
    public float minImpactForce = 0.5f;

    void Awake()
    {
        if (!autoSetup) return;

        int count = 0;
        // Find all child objects named Bordure_
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (!child.name.StartsWith("Bordure_")) continue;
            if (child.GetComponent<BordureContact>() != null) continue;

            var bc = child.gameObject.AddComponent<BordureContact>();
            bc.minImpactForce = minImpactForce;
            count++;
        }

        if (count > 0)
            Debug.Log($"BordureManager: added BordureContact to {count} curbs");
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Setup Bordure Contacts")]
    static void SetupAll()
    {
        int count = 0;
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (!go.name.StartsWith("Bordure_")) continue;
            if (go.GetComponent<BordureContact>() != null) continue;
            var bc = go.AddComponent<BordureContact>();
            bc.minImpactForce = 0.5f;
            count++;
        }
        Debug.Log($"Setup: added BordureContact to {count} curbs");
    }
#endif

}
public class WheelBordureDetector : MonoBehaviour
{
    private float _lastErrorTime = -10f;
    private float _errorCooldown = 2f;

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.name.StartsWith("Bordure_")) return;
        if (Time.time - _lastErrorTime < _errorCooldown) return;

        float impulse = collision.impulse.magnitude;
        if (impulse < 0.3f) return;

        _lastErrorTime = Time.time;
        SpeedCameraShake.Instance?.TriggerKerbShake();
        ExamManager.Instance?.AddError($"Wheel touched a curb ({gameObject.name})");
        Debug.Log($"WheelBordureDetector: wheel {gameObject.name} touched {collision.gameObject.name}, force: {impulse:F2}");
    }
}
