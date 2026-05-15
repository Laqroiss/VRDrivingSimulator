using UnityEngine;

/// <summary>
///  :   Y,      .
///      вЂ”   .
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class WaypointArrow : MonoBehaviour
{
    [Header("Animation")]
    public float rotationSpeed = 90f;   //   
    public float bobAmplitude  = 0.3f;  //  - (0 = )
    public float bobSpeed      = 1.5f;

    [Header("")]
    public float triggerRadius = 4f;

    [Header("")]
    public Color arrowColor = new Color(1f, 0.75f, 0f, 1f);   // -

    //    вЂ”    RouteManager
    [HideInInspector] public RouteManager routeManager;
    [HideInInspector] public int           waypointIndex;

    private Transform  _visual;
    private float      _baseY;
    private bool       _triggered;

    void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = triggerRadius;

        BuildVisual();
        _baseY = transform.position.y;
    }

    void Update()
    {
        if (_visual == null) return;
        _visual.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);

        if (bobAmplitude > 0f)
        {
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = p;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (other.GetComponentInParent<Car>() == null) return;

        _triggered = true;
        routeManager?.OnWaypointReached(waypointIndex);
        gameObject.SetActive(false);
    }

    // вв     вввввввввввввввввввввввввввввввввввввввв

    void BuildVisual()
    {
        _visual = new GameObject("ArrowVisual").transform;
        _visual.SetParent(transform, false);

        //  ( )
        var shaft     = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name    = "Shaft";
        shaft.transform.SetParent(_visual, false);
        shaft.transform.localPosition = new Vector3(0f, 0f, -0.2f);
        shaft.transform.localScale    = new Vector3(0.25f, 0.25f, 1.0f);
        SetColor(shaft, arrowColor);
        Destroy(shaft.GetComponent<Collider>());

        //  (   45 вЂ” )
        var head      = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name     = "Head";
        head.transform.SetParent(_visual, false);
        head.transform.localPosition    = new Vector3(0f, 0f, 0.45f);
        head.transform.localEulerAngles = new Vector3(0f, 45f, 0f);
        head.transform.localScale       = new Vector3(0.5f, 0.5f, 0.5f);
        SetColor(head, arrowColor);
        Destroy(head.GetComponent<Collider>());

        //   ()
        var pole      = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name     = "Pole";
        pole.transform.SetParent(transform, false);   //  
        pole.transform.localPosition = new Vector3(0f, -1.1f, 0f);
        pole.transform.localScale    = new Vector3(0.08f, 1.0f, 0.08f);
        SetColor(pole, new Color(0.6f, 0.6f, 0.6f));
        Destroy(pole.GetComponent<Collider>());
    }

    static void SetColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = c;
        //   вЂ”  emission   
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 0.3f);
        r.material = mat;
    }

    //   (  )
    public void Reset()
    {
        _triggered = false;
        gameObject.SetActive(true);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.75f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.75f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
