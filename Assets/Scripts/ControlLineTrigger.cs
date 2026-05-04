using UnityEngine;

/// <summary>
/// Контрольная линия — штраф когда колесо машины оказывается над линией.
/// Проверяет реальные позиции всех 4 колёс через Car.GetWheelPosition(),
/// аналогично тому как CarBordureDetector проверяет бордюры.
/// </summary>
public class ControlLineTrigger : MonoBehaviour
{
    [Header("Номер упражнения (2, 5 или 6)")]
    public int exerciseNum = 5;

    [Header("Повторный штраф через N секунд (0 = разовый)")]
    public float cooldown = 5f;

    [Header("Допуск определения наезда (метры)")]
    [Tooltip("Насколько далеко колесо может быть от линии и всё равно считаться наездом")]
    public float wheelTolerance = 0.15f;

    private Car   _car;
    private float _lastPenaltyTime = -100f;
    private bool  _oneShotDone     = false;
    private BoxCollider _col;

    void Start()
    {
        _car = FindAnyObjectByType<Car>();
        _col = GetComponent<BoxCollider>();
    }

    void FixedUpdate()
    {
        if (_car == null || _col == null) return;
        if (cooldown <= 0f && _oneShotDone) return;
        if (Time.time - _lastPenaltyTime < cooldown) return;

        // Проверяем все 4 колеса
        for (int i = 0; i < 4; i++)
        {
            Vector3 wheelPos = _car.GetWheelPosition(i);
            if (IsWheelOnLine(wheelPos))
            {
                TriggerPenalty();
                break; // достаточно одного колеса
            }
        }
    }

    bool IsWheelOnLine(Vector3 worldPos)
    {
        // Переводим в локальное пространство коллайдера
        Vector3 local = _col.transform.InverseTransformPoint(worldPos) - _col.center;
        Vector3 half  = _col.size * 0.5f;

        return Mathf.Abs(local.x) <= half.x + wheelTolerance &&
               Mathf.Abs(local.z) <= half.z + wheelTolerance;
        // Y не проверяем — колёса всегда на высоте земли
    }

    void TriggerPenalty()
    {
        _lastPenaltyTime = Time.time;
        _oneShotDone     = true;

        int    points;
        string desc;

        switch (exerciseNum)
        {
            case 2:
                points = ExamManager.P2_WHEEL_ON_LINE;
                desc   = "Наехал колесом на контрольную линию (Упр.2)";
                break;
            case 5:
                points = ExamManager.P5_WHEEL_ON_LINE;
                desc   = "Наехал колесом на контрольную линию (Упр.5)";
                break;
            case 6:
                points = ExamManager.P6_WHEEL_ON_LINE;
                desc   = "Наехал колесом на контрольную линию (Упр.6)";
                break;
            default:
                return;
        }

        ExamManager.Instance?.AddPenalty(desc, points, exerciseNum);
    }

    void OnDrawGizmos()
    {
        if (_col == null) _col = GetComponent<BoxCollider>();
        if (_col == null) return;

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(_col.center, _col.size);
        Gizmos.color = new Color(1f, 0.85f, 0f, 1f);
        Gizmos.DrawWireCube(_col.center, _col.size);
    }
}
