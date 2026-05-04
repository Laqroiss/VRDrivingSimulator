using UnityEngine;

/// <summary>
/// Детектор касания бордюра колесом.
/// Проверяет позиции всех 4 колёс через Car.GetWheelPosition() —
/// аналогично ControlLineTrigger. Если колесо оказалось рядом с бордюром → штраф.
/// </summary>
public class CarBordureDetector : MonoBehaviour
{
    [Header("Радиус проверки вокруг каждого колеса")]
    public float wheelRadius  = 0.45f;

    [Header("Задержка между повторными штрафами (сек)")]
    public float cooldown     = 2f;

    // Поля нужны ExamTrigger для CheckCarOverlap() — не удаляем
    [HideInInspector] public float capsuleRadius = 0.85f;
    [HideInInspector] public float centerOffsetY = -0.7f;
    [HideInInspector] public float halfLength    = 1.8f;

    private Car   _car;
    private float _lastPenaltyTime = -100f;

    void Start()
    {
        _car = GetComponentInParent<Car>();
        if (_car == null) _car = FindAnyObjectByType<Car>();
    }

    void FixedUpdate()
    {
        if (_car == null) return;
        if (Time.time - _lastPenaltyTime < cooldown) return;

        for (int i = 0; i < 4; i++)
        {
            Vector3 wheelPos = _car.GetWheelPosition(i);

            // Ищем бордюры рядом с колесом
            Collider[] hits = Physics.OverlapSphere(wheelPos, wheelRadius);
            foreach (var hit in hits)
            {
                if (!hit.gameObject.name.StartsWith("Bordure_")) continue;

                _lastPenaltyTime = Time.time;
                ExamManager.Instance?.AddCollision();
                return; // один штраф за кадр
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (_car == null) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 wp = _car.GetWheelPosition(i);
            Gizmos.DrawWireSphere(wp, wheelRadius);
        }
    }
}
