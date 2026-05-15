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
            // Покачивание в направлении стрелки
            float angle = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            Vector3 axis = direction == DirectionType.TurnLeft  ? Vector3.up :
                           direction == DirectionType.TurnRight ? Vector3.up :
                           Vector3.right;
            float sign   = direction == DirectionType.TurnLeft ? -1f : 1f;
            _visual.localRotation = Quaternion.AngleAxis(angle * sign, axis);
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
        _visual.localPosition = Vector3.zero;

        switch (direction)
        {
            case DirectionType.Checkpoint: BuildCheckpoint(); break;
            case DirectionType.Straight:   BuildStraightArrow(0f);    break;
            case DirectionType.TurnLeft:   BuildTurnArrow(-90f);      break;
            case DirectionType.TurnRight:  BuildTurnArrow(90f);       break;
            case DirectionType.UTurn:      BuildStraightArrow(180f);  break;
        }

        BuildPole();
    }

    // Вращающийся диск-шайба (контрольная точка)
    void BuildCheckpoint()
    {
        var ring = MakeCube(_visual, new Vector3(0.8f, 0.1f, 0.8f), Vector3.zero, arrowColor);

        // Крест внутри — намекает на точку
        MakeCube(_visual, new Vector3(0.15f, 0.15f, 0.6f), Vector3.zero, arrowColor * 1.3f);
        MakeCube(_visual, new Vector3(0.6f,  0.15f, 0.15f), Vector3.zero, arrowColor * 1.3f);
    }

    // Прямая стрелка (прямо или назад)
    void BuildStraightArrow(float yawOffset)
    {
        var root = new GameObject("ArrowRoot").transform;
        root.SetParent(_visual, false);
        root.localEulerAngles = new Vector3(0f, yawOffset, 0f);

        // Хвост
        MakeCube(root, new Vector3(0.2f, 0.2f, 0.7f), new Vector3(0f, 0f, -0.15f), arrowColor);

        // Наконечник (два наклонных куба образуют >)
        MakeCube(root, new Vector3(0.55f, 0.2f, 0.2f),
                 new Vector3(0.2f, 0f, 0.25f), arrowColor)
            .localEulerAngles = new Vector3(0f, 45f, 0f);
        MakeCube(root, new Vector3(0.55f, 0.2f, 0.2f),
                 new Vector3(-0.2f, 0f, 0.25f), arrowColor)
            .localEulerAngles = new Vector3(0f, -45f, 0f);
    }

    // Г-образная стрелка поворота
    void BuildTurnArrow(float yawDeg)
    {
        var root = new GameObject("TurnRoot").transform;
        root.SetParent(_visual, false);
        root.localEulerAngles = new Vector3(0f, yawDeg, 0f);

        // Горизонтальный сегмент (боковое плечо)
        MakeCube(root, new Vector3(0.6f, 0.2f, 0.2f), new Vector3(0.3f, 0f, 0.2f), arrowColor);

        // Вертикальный сегмент (нога)
        MakeCube(root, new Vector3(0.2f, 0.2f, 0.55f), new Vector3(0f, 0f, -0.075f), arrowColor);

        // Наконечник — два куба образуют угол >
        var tip1 = MakeCube(root, new Vector3(0.4f, 0.2f, 0.18f),
                            new Vector3(0.45f, 0f, -0.1f), arrowColor);
        tip1.localEulerAngles = new Vector3(0f, 45f, 0f);

        var tip2 = MakeCube(root, new Vector3(0.4f, 0.2f, 0.18f),
                            new Vector3(0.45f, 0f, 0.5f), arrowColor);
        tip2.localEulerAngles = new Vector3(0f, -45f, 0f);
    }

    void BuildPole()
    {
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(transform, false);
        pole.transform.localPosition = new Vector3(0f, -1.0f, 0f);
        pole.transform.localScale    = new Vector3(0.07f, 0.9f, 0.07f);
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
        SetMat(go, color);
        Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    static void SetMat(GameObject go, Color c)
    {
        var r   = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(Shader.Find("Standard")) { color = c };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 0.25f);
        r.material = mat;
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
