using UnityEngine;

/// <summary>
/// Определяет касание машины бордюра.
/// Прикрепи этот скрипт к КАЖДОМУ бордюру (или к родительскому объекту через GetComponentsInChildren).
/// 
/// Быстрая установка на все бордюры сразу:
/// Создай пустой объект BordureManager, прикрепи скрипт BordureManager (ниже).
/// </summary>
public class BordureContact : MonoBehaviour
{
    // Порог силы удара — лёгкое касание не считается ошибкой
    [Tooltip("Минимальная сила удара для фиксации касания")]
    public float minImpactForce = 0.5f;

    private float _lastErrorTime = -10f;
    private float _errorCooldown = 2f; // не дублировать ошибку чаще чем раз в 2 сек

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player") &&
            !collision.gameObject.CompareTag("Car")) return;

        float impulse = collision.impulse.magnitude;
        if (impulse < minImpactForce) return;

        if (Time.time - _lastErrorTime < _errorCooldown) return;
        _lastErrorTime = Time.time;

        ExamManager.Instance?.AddError($"Касание бордюра ({gameObject.name})");
        Debug.Log($"BordureContact: касание {gameObject.name}, сила: {impulse:F2}");
    }
}

// ——————————————————————————————————————————

/// <summary>
/// Менеджер бордюров — автоматически добавляет BordureContact на все бордюры в группе.
/// Прикрепи к корневому объекту группы бордюров (например Bordures_Serpantin).
/// </summary>
public class BordureManager : MonoBehaviour
{
    [Header("Автоматически добавить BordureContact на все дочерние объекты")]
    public bool autoSetup = true;
    public float minImpactForce = 0.5f;

    void Awake()
    {
        if (!autoSetup) return;

        int count = 0;
        // Ищем все объекты с именем Bordure_ в дочерних
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (!child.name.StartsWith("Bordure_")) continue;
            if (child.GetComponent<BordureContact>() != null) continue;

            var bc = child.gameObject.AddComponent<BordureContact>();
            bc.minImpactForce = minImpactForce;
            count++;
        }

        if (count > 0)
            Debug.Log($"BordureManager: добавлен BordureContact на {count} бордюров");
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
        Debug.Log($"Setup: добавлен BordureContact на {count} бордюров");
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
        ExamManager.Instance?.AddError($"Касание бордюра колесом ({gameObject.name})");
        Debug.Log($"WheelBordureDetector: колесо {gameObject.name} касание {collision.gameObject.name}, сила: {impulse:F2}");
    }
}
