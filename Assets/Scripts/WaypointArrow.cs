using UnityEngine;

/// <summary>
/// Стрелка маршрута.
///
/// Checkpoint   — спиннинг-шайба, исчезает когда проезжаешь сквозь неё.
/// Straight     — стрелка вперёд, покачивается вдоль оси.
/// TurnLeft     — Г-образная стрелка влево, покачивается влево.
/// TurnRight    — Г-образная стрелка вправо, покачивается вправо.
/// UTurn        — стрелка назад.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class WaypointArrow : MonoBehaviour
{
    public enum DirectionType { Checkpoint, Straight, TurnLeft, TurnRight, UTurn }

    [Header("Тип")]
    public DirectionType direction = DirectionType.Checkpoint;

    [Header("Триггер")]
    public float triggerRadius = 4f;

    [Header("Анимация")]
    public float rotationSpeed = 90f;   // Checkpoint — градусов/сек спин
    public float bobAmplitude  = 20f;   // Direction — угол покачивания (градусы)
    public float bobSpeed      = 2f;    // Direction — скорость покачивания
    public float floatAmplitude = 0.2f; // подъём-опускание
    public float floatSpeed     = 1.5f;

    [Header("Цвет")]
    public Color arrowColor = new Color(1f, 0.8f, 0f, 1f);

    [HideInInspector] public RouteManager routeManager;
    [HideInInspector] public int           waypointIndex;

    private Transform _visual;
    private float     _baseY;
    private bool      _triggered;

    void Awake()
    {
        var col      = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = triggerRadius;

        BuildVisual();
        _baseY = transform.position.y;
    }

    void Update()
    {
        if (_visual == null) return;

        if (direction == DirectionType.Checkpoint)
        {
            _visual.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
        }
        else
        {
            // Покачивание вперёд-назад (наклон по X) — стрелка "кивает" в направлении
            float angle = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            _visual.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        // Плавное покачивание вверх-вниз
        var p = transform.position;
        p.y = _baseY + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = p;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (other.GetComponentInParent<Car>() == null) return;
        _triggered = true;
        routeManager?.OnWaypointReached(waypointIndex);
        gameObject.SetActive(false);
    }

    // ── Построение визуала ───────────────────────────────────────────────────

    void BuildVisual()
    {
        _visual = new GameObject("Visual").transform;
        _visual.SetParent(transform, false);
        // Визуал размещается над столбиком
        _visual.localPosition = new Vector3(0f, 0.5f, 0f);

        if (direction == DirectionType.Checkpoint)
            BuildCheckpoint();
        else
            BuildDirectionalArrow();

        BuildPole();
    }

    // Checkpoint: вертикальная монета-крест, которая вращается вокруг Y
    void BuildCheckpoint()
    {
        // Горизонтальная планка
        MakeCube(_visual, new Vector3(0.85f, 0.18f, 0.18f), Vector3.zero, arrowColor);
        // Вертикальная планка
        MakeCube(_visual, new Vector3(0.18f, 0.85f, 0.18f), Vector3.zero, arrowColor);
    }

    // Направляющая стрелка: вертикальная (в плоскости XY), наконечник вверх
    // Для поворотов поворачиваем по оси Z
    void BuildDirectionalArrow()
    {
        // Угол поворота по Z: 0=вверх(↑), -90=вправо(→), +90=влево(←), 180=вниз(↓)
        float rotZ = direction == DirectionType.TurnRight ?  -90f :
                     direction == DirectionType.TurnLeft  ?   90f :
                     direction == DirectionType.UTurn     ?  180f : 0f;

        var root = new GameObject("Root").transform;
        root.SetParent(_visual, false);
        root.localEulerAngles = new Vector3(0f, 0f, rotZ);

        // Стержень вдоль Y (вертикально)
        MakeCube(root, new Vector3(0.22f, 0.7f, 0.22f), new Vector3(0f, -0.18f, 0f), arrowColor);

        // Левая половина наконечника
        var hl = MakeCube(root, new Vector3(0.18f, 0.45f, 0.22f),
                          new Vector3(-0.2f, 0.2f, 0f), arrowColor);
        hl.localEulerAngles = new Vector3(0f, 0f, 40f);

        // Правая половина наконечника
        var hr = MakeCube(root, new Vector3(0.18f, 0.45f, 0.22f),
                          new Vector3( 0.2f, 0.2f, 0f), arrowColor);
        hr.localEulerAngles = new Vector3(0f, 0f, -40f);
    }

    void BuildPole()
    {
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(transform, false);
        pole.transform.localPosition = new Vector3(0f, -0.65f, 0f);
        pole.transform.localScale    = new Vector3(0.07f, 0.6f, 0.07f);
        SetMat(pole, new Color(0.55f, 0.55f, 0.55f));
        Destroy(pole.GetComponent<Collider>());
    }

    // ── Хелперы ──────────────────────────────────────────────────────────────

    static Transform MakeCube(Transform parent, Vector3 scale, Vector3 localPos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent, false);
        go.transform.localScale    = scale;
        go.transform.localPosition = localPos;
        Destroy(go.GetComponent<Collider>());
        // Красим существующий материал — не создаём новый, чтобы не сломать URP/HDRP
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        return go.transform;
    }

    static void SetMat(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }

    public void Reset()
    {
        _triggered = false;
        gameObject.SetActive(true);
    }

    void OnDrawGizmos()
    {
        Color gc = direction == DirectionType.Checkpoint ? new Color(1f, 0.8f, 0f, 0.3f) :
                   direction == DirectionType.TurnLeft   ? new Color(0f, 0.8f, 1f, 0.3f) :
                   direction == DirectionType.TurnRight  ? new Color(0f, 0.8f, 1f, 0.3f) :
                                                           new Color(0.5f, 1f, 0.5f, 0.3f);
        Gizmos.color = gc;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.8f, 0f, 1f);
        Gizmos.DrawSphere(transform.position, 0.2f);

        // Стрелка-подсказка в Scene View
#if UNITY_EDITOR
        Vector3 dir = direction == DirectionType.TurnLeft  ? -transform.right :
                      direction == DirectionType.TurnRight ?  transform.right :
                      direction == DirectionType.UTurn     ? -transform.forward :
                                                              transform.forward;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, dir * 2f);
#endif
    }
}
