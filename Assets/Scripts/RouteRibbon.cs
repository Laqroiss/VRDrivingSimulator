using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if USE_SPLINES
using UnityEngine.Splines;
#endif

/// <summary>
/// Forza Horizon-style route ribbon: a glowing strip along the road with running
/// dots that starts ahead of the car and shortens as you drive.
/// Built from an assigned SplineContainer (Unity Splines package).
///
/// Setup: attach to an object, assign a Spline Container (or RouteSegmentController
/// sets it). The mesh/material are created automatically.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RouteRibbon : MonoBehaviour
{
    [Header("Route source")]
#if USE_SPLINES
    [Tooltip("Spline route. Drawn with Unity's native Spline tool. Assigned by RouteSegmentController or by hand")]
    public SplineContainer splineContainer;
#endif

    [Header("Ribbon geometry")]
    [Tooltip("Ribbon width, m")]
    public float width = 2.6f;
    [Tooltip("Height above the road, m (to avoid z-fighting)")]
    public float heightOffset = 0.06f;
    [Tooltip("Samples per spline segment - more = smoother turns")]
    public int samplesPerSegment = 12;
    [Tooltip("Layers to search for the ground (downward raycast)")]
    public LayerMask groundMask = ~0;
    [Tooltip("Height to cast the downward ray from to land on the road")]
    public float groundRayUp = 5f;

    [Header("Appearance")]
    [Tooltip("Color on straight sections")]
    public Color color = new Color(0.15f, 0.65f, 1f, 1f);
    [Tooltip("Color on turns (high curvature / clustered knots)")]
    public Color turnColor = new Color(1f, 0.25f, 0.15f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Overall ribbon opacity")]
    public float ribbonAlpha = 0.55f;
    [Tooltip("How many meters before a turn to start turning red")]
    public float turnLookAheadDistance = 6f;
    [Tooltip("Total turn (deg) over that distance for fully red")]
    public float turnAngleForFullRed = 45f;
    [Tooltip("Distance between running dots along the ribbon, m")]
    public float tileLength = 3f;
    [Tooltip("Dot running speed (tiles/sec). Negative - the other way")]
    public float scrollSpeed = 1.6f;
    [Tooltip("Custom ribbon material (optional). If empty - created automatically")]
    public Material routeMaterial;

    [Header("Behavior")]
    [Tooltip("The ribbon shortens as the car moves")]
    public bool followCar = true;
    [Tooltip("How many meters AHEAD of the car to start the ribbon (so the tail doesn't show in the mirror)")]
    public float trimAheadDistance = 2f;
    [Tooltip("Visible ribbon length ahead (m). The ribbon isn't drawn all at once - only this far ahead, the rest fills in as you drive. 0 = to the end of the spline")]
    public float lookAheadDistance = 18f;
    [Tooltip("DEBUG: always show the ribbon, without waiting for the exam to start")]
    public bool alwaysVisible = false;

    private Transform   _car;
    private Mesh        _mesh;
    private Material    _mat;
    private MeshRenderer _mr;

    // Cache of the (ground-projected) spline, cumulative length, and turn "redness"
    private readonly List<Vector3> _pts        = new List<Vector3>();
    private readonly List<float>   _cumDist    = new List<float>();
    private readonly List<float>   _turnFactor = new List<float>(); // 0 straight … 1 sharp turn
    private int  _headIndex;      // ribbon render start (trimAheadDistance ahead of the car)
    private int  _carIndex;       // spline point at the car (grows monotonically while driving)
    private bool _prevActive;

    void Awake()
    {
        _mesh = new Mesh { name = "RouteRibbon" };
        _mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        _mr = GetComponent<MeshRenderer>();
        _mr.shadowCastingMode = ShadowCastingMode.Off;
        _mr.receiveShadows    = false;
    }

    void Start()
    {
        var car = FindAnyObjectByType<Car>();
        if (car != null) _car = car.transform;

        SetupMaterial();
        BuildSpline();

        _mr.enabled = false;
    }

    void LateUpdate()
    {
        // Navigation help (setting). Read the pref every frame - toggles on the fly.
        bool navOn = PlayerPrefs.GetInt("nav_help", 1) == 1;

        // Route activity by exam state (for restart logic; independent of nav_help).
        bool routeActive;
        if (alwaysVisible)                     routeActive = true;
        else if (ExamManager.Instance != null) routeActive = ExamManager.Instance.State == ExamManager.ExamState.InProgress;
        else                                   routeActive = true;

        // Route restart (new exam) -> ribbon back to the start.
        // Tied to routeActive, NOT nav_help - so toggling help doesn't reset progress.
        if (routeActive && !_prevActive) { _headIndex = 0; _carIndex = 0; }
        _prevActive = routeActive;

        // Draw only if the route is active AND help is on.
        // When off - just hide the mesh (don't move AdvanceHead); when on,
        // AdvanceHead catches up to the car along the current segment in one frame - no reset.
        if (!routeActive || !navOn || _pts.Count < 2)
        {
            _mr.enabled = false;
            return;
        }

        AdvanceHead();
        RebuildMesh(); // each frame: the window follows the car, chevron scroll is baked into UV
    }

    // ── Build the spline and project onto the road ───────────────────────────

    public void BuildSpline()
    {
        _pts.Clear();
        _cumDist.Clear();

#if USE_SPLINES
        // Top priority - the Spline. Sample it directly (already smooth) and project onto the road.
        if (splineContainer != null && splineContainer.Spline != null && splineContainer.Spline.Count >= 2)
        {
            int knots = splineContainer.Spline.Count;
            int total = Mathf.Max(2, (knots - 1) * Mathf.Max(1, samplesPerSegment));
            for (int i = 0; i <= total; i++)
            {
                float t = (float)i / total;
                Vector3 p = splineContainer.EvaluatePosition(t);
                _pts.Add(ProjectToGround(p));
            }
            _cumDist.Add(0f);
            for (int i = 1; i < _pts.Count; i++)
                _cumDist.Add(_cumDist[i - 1] + Vector3.Distance(_pts[i], _pts[i - 1]));
            ComputeTurnFactors();
            _headIndex = 0;
            _carIndex = 0;
            return;
        }
        Debug.LogWarning("[RouteRibbon] No assigned spline with >=2 knots - ribbon not built.");
#else
        Debug.LogWarning("[RouteRibbon] Needs the Splines package and the USE_SPLINES define.");
#endif
    }

    /// <summary>
    /// For each point, computes turn "redness": total heading change over the next
    /// turnLookAheadDistance meters AHEAD, normalized to 0..1.
    /// This way the ribbon turns red early, as you approach a turn.
    /// </summary>
    void ComputeTurnFactors()
    {
        _turnFactor.Clear();
        int n = _pts.Count;
        if (n == 0) return;

        // Heading change (degrees) at each sample
        var head = new float[n];
        for (int i = 1; i < n - 1; i++)
        {
            Vector3 a = _pts[i] - _pts[i - 1]; a.y = 0f;
            Vector3 b = _pts[i + 1] - _pts[i]; b.y = 0f;
            if (a.sqrMagnitude > 1e-6f && b.sqrMagnitude > 1e-6f)
                head[i] = Vector3.Angle(a, b);
        }

        float full = Mathf.Max(1f, turnAngleForFullRed);
        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            for (int j = i; j < n - 1 && _cumDist[j] - _cumDist[i] < turnLookAheadDistance; j++)
                sum += head[j];
            _turnFactor.Add(Mathf.Clamp01(sum / full));
        }
    }

    Vector3 ProjectToGround(Vector3 p)
    {
        Vector3 origin = p + Vector3.up * groundRayUp;
        if (Physics.Raycast(origin, Vector3.down, out var hit, groundRayUp * 2f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * heightOffset;
        return new Vector3(p.x, p.y + heightOffset, p.z);
    }

    // ── Ribbon head follows the car ──────────────────────────────────────────

    void AdvanceHead()
    {
        if (!followCar || _car == null) return;

        Vector3 c = _car.position;

        // Advance the car point along the spline while the car has "passed" the current point
        // (the point->car vector points along the path). This tracks real progress along the
        // route, doesn't rush to the end, and doesn't jump to nearby loop sections.
        while (_carIndex < _pts.Count - 1)
        {
            Vector3 dir = _pts[_carIndex + 1] - _pts[_carIndex]; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) { _carIndex++; continue; }
            Vector3 toCar = c - _pts[_carIndex]; toCar.y = 0f;
            if (Vector3.Dot(toCar, dir) > 0f) _carIndex++;   // car is past this point -> it passed it
            else break;
        }

        // Render head - trimAheadDistance meters ahead of the car (the ribbon starts
        // ahead of the hood and doesn't show in the mirror).
        float targetDist = _cumDist[_carIndex] + Mathf.Max(0f, trimAheadDistance);
        int head = _carIndex;
        while (head < _pts.Count - 1 && _cumDist[head] < targetDist) head++;
        _headIndex = head;
    }

    // ── Mesh generation ──────────────────────────────────────────────────────

    void RebuildMesh()
    {
        int start = Mathf.Clamp(_headIndex, 0, _pts.Count - 1);

        // Cap the visible length: draw only lookAheadDistance meters ahead,
        // the rest fills in as you drive (the window slides along with the head).
        int last;
        if (lookAheadDistance > 0.01f)
        {
            float maxDist = _cumDist[start] + lookAheadDistance;
            last = start;
            while (last < _pts.Count - 1 && _cumDist[last] < maxDist) last++;
        }
        else last = _pts.Count - 1; // 0 = to the end of the spline

        int n = last - start + 1;
        if (n < 2) { _mr.enabled = false; return; }

        // Local copy of points starting at the head (already trimAheadDistance ahead of the car).
        // Don't mix in the car - otherwise a line would stretch from the car to the spline start.
        var pts = new Vector3[n];
        for (int k = 0; k < n; k++) pts[k] = _pts[start + k];

        var verts = new Vector3[n * 2];
        var uvs   = new Vector2[n * 2];
        var cols  = new Color[n * 2];
        var tris  = new int[(n - 1) * 6];

        for (int k = 0; k < n; k++)
        {
            int gi = start + k;
            // Color: blue on straights -> red on turns; overall opacity ribbonAlpha
            float tf = (gi < _turnFactor.Count) ? _turnFactor[gi] : 0f;
            Color vc = Color.Lerp(color, turnColor, tf);
            vc.a = ribbonAlpha;

            Vector3 tangent =
                k == 0     ? (pts[1] - pts[0]) :
                k == n - 1 ? (pts[n - 1] - pts[n - 2]) :
                             (pts[k + 1] - pts[k - 1]);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.forward;
            tangent.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 p = pts[k];

            // V is tied to the GLOBAL distance along the spline (not the window) - otherwise the
            // pattern would jump when the head moves. Dot scroll - via subtracting time.
            float v = _cumDist[gi] / Mathf.Max(0.01f, tileLength) - Time.time * scrollSpeed;

            verts[2 * k]     = transform.InverseTransformPoint(p - right * width * 0.5f);
            verts[2 * k + 1] = transform.InverseTransformPoint(p + right * width * 0.5f);
            uvs[2 * k]       = new Vector2(0f, v);
            uvs[2 * k + 1]   = new Vector2(1f, v);
            cols[2 * k]      = vc;
            cols[2 * k + 1]  = vc;
        }

        for (int k = 0; k < n - 1; k++)
        {
            int i0 = 2 * k, i1 = 2 * k + 1, i2 = 2 * k + 2, i3 = 2 * k + 3;
            int t = k * 6;
            tris[t]     = i0; tris[t + 1] = i2; tris[t + 2] = i1;
            tris[t + 3] = i1; tris[t + 4] = i2; tris[t + 5] = i3;
        }

        _mesh.Clear();
        _mesh.vertices  = verts;
        _mesh.uv        = uvs;
        _mesh.colors    = cols;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
        _mr.enabled = true;
    }

    // ── Material and chevron texture ─────────────────────────────────────────

    void SetupMaterial()
    {
        if (routeMaterial != null)
        {
            _mat = routeMaterial;
        }
        else
        {
            // Sprites/Default multiplies the texture by the VERTEX color and is transparent/double-sided -
            // needed to color the ribbon by curvature (blue->red) via mesh.colors.
            bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = urp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Transparent");

            _mat = new Material(sh) { name = "RouteRibbon_Mat" };
            var tex = GenerateRibbonTexture(64, 128);
            _mat.mainTexture = tex;                          // _MainTex (dot scroll - via UV)
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", Color.white);
            _mat.renderQueue = (int)RenderQueue.Transparent;
        }

        GetComponent<MeshRenderer>().sharedMaterial = _mat;
    }

    // Ribbon texture: a soft glowing strip (smooth edges) + one running comet dot
    // per tile down the center. Color comes from the vertex color; here only brightness+alpha.
    // V repeats and scrolls (see UV in RebuildMesh) -> the dots "run" forward.
    static Texture2D GenerateRibbonTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapModeU  = TextureWrapMode.Clamp,
            wrapModeV  = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w;
            float v = (y + 0.5f) / h;

            // Across (U): soft edges - dense in the center, fading toward the sides
            float edge = Mathf.Abs(u - 0.5f) * 2f;            // 0 center … 1 side
            float band = 1f - Mathf.SmoothStep(0.45f, 1f, edge);
            // a thin bright "vein" right down the center for structure
            float core = 1f - Mathf.SmoothStep(0.0f, 0.12f, edge);

            // Running comet dot (one per tile): bright head + short tail behind
            float du   = (u - 0.5f) / 0.16f;
            float head = Mathf.Exp(-(du * du) - Mathf.Pow((v - 0.62f) / 0.10f, 2f));
            float tail = Mathf.Exp(-(du * du) - Mathf.Pow((v - 0.40f) / 0.22f, 2f)) * 0.45f;
            float dot  = Mathf.Clamp01(head + tail);

            float bandAlpha = band * 0.30f;
            float level = Mathf.Clamp01(Mathf.Max(0.5f * band + 0.5f * core, dot));
            float alpha = Mathf.Clamp01(Mathf.Max(bandAlpha, Mathf.Max(core * 0.6f, dot)));

            tex.SetPixel(x, y, new Color(level, level, level, alpha));
        }
        tex.Apply();
        return tex;
    }
}
