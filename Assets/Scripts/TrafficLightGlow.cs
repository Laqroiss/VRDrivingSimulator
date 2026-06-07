using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Billboard glow sprite for a traffic-light lens.
/// Keeps a minimum size on screen so the signal stays readable from far away,
/// always faces the main camera, and sits just in front of the lens to avoid
/// z-fighting. Toggled in sync with the lens emission via <see cref="SetOn"/>.
///
/// Everything (mesh, material, soft radial texture) is built procedurally, so
/// no manual setup in the Unity Editor is required.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class TrafficLightGlow : MonoBehaviour
{
    [Header("Anchor")]
    [Tooltip("Lens renderer this glow belongs to. Its bounds centre is used as the glow position.")]
    public Renderer anchorRenderer;

    [Header("Look")]
    [ColorUsage(true, true)] public Color glowColor = Color.white;

    [Tooltip("Smallest fraction of the screen height the glow is allowed to shrink to. " +
             "0.02 means it never gets smaller than ~2% of screen height, so it stays visible at distance.")]
    [Range(0.001f, 0.2f)] public float minScreenFraction = 0.02f;

    [Tooltip("World-space size (metres) when close up.")]
    public float baseWorldSize = 0.22f;

    [Tooltip("How far in front of the lens (toward the camera) the glow sits, to avoid z-fighting.")]
    public float frontOffset = 0.06f;

    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _mpb;

    // Shared resources, built once for every glow in the scene.
    private static Mesh s_quad;
    private static Material s_material;
    private static Texture2D s_texture;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        _mpb = new MaterialPropertyBlock();

        var mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = GetSharedQuad();

        _meshRenderer.sharedMaterial = GetSharedMaterial();
        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        ApplyColor();
    }

    /// <summary>Tint the glow per-instance without breaking material batching.</summary>
    public void ApplyColor()
    {
        if (_meshRenderer == null) return;
        _meshRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, glowColor);
        _meshRenderer.SetPropertyBlock(_mpb);
    }

    /// <summary>Show/hide the glow in sync with the lens emission.</summary>
    public void SetOn(bool on)
    {
        if (_meshRenderer != null) _meshRenderer.enabled = on;
        enabled = on; // also (un)subscribes the per-camera billboard via OnEnable/OnDisable
    }

    void OnEnable()
    {
        // Orient per rendering camera so the billboard is correct in Game, Scene
        // and mirror views — not just for Camera.main.
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        FaceCamera(cam);
    }

    private void FaceCamera(Camera cam)
    {
        if (cam == null) return;

        Vector3 anchor = anchorRenderer != null ? anchorRenderer.bounds.center : transform.position;
        Vector3 toCam = cam.transform.position - anchor;
        float dist = toCam.magnitude;
        if (dist < 1e-4f) return;
        Vector3 dir = toCam / dist;

        // Sit just in front of the lens and face this camera.
        transform.position = anchor + dir * frontOffset;
        transform.rotation = Quaternion.LookRotation(-dir, cam.transform.up);

        // Enforce a minimum on-screen size for this camera.
        float worldForMinFraction = cam.orthographic
            ? 2f * cam.orthographicSize * minScreenFraction
            : 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * minScreenFraction;

        float size = Mathf.Max(baseWorldSize, worldForMinFraction);

        // Build the world-space scale directly (no parent shear from non-uniform FBX scale).
        transform.localScale = Vector3.one;
        Vector3 ls = transform.lossyScale;
        transform.localScale = new Vector3(
            size / Mathf.Max(Mathf.Abs(ls.x), 1e-4f),
            size / Mathf.Max(Mathf.Abs(ls.y), 1e-4f),
            size / Mathf.Max(Mathf.Abs(ls.z), 1e-4f));
    }

    // --- Shared procedural resources -------------------------------------------------

    private static Mesh GetSharedQuad()
    {
        if (s_quad != null) return s_quad;
        s_quad = new Mesh { name = "TrafficLightGlowQuad" };
        s_quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
        };
        s_quad.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
        };
        s_quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        s_quad.RecalculateBounds();
        return s_quad;
    }

    private static Material GetSharedMaterial()
    {
        if (s_material != null) return s_material;

        // URP Unlit, transparent + additive, double-sided, no depth write.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        s_material = new Material(shader) { name = "TrafficLightGlow" };

        s_material.SetTexture("_BaseMap", GetSharedTexture());
        s_material.SetColor("_BaseColor", Color.white);

        // Additive transparent blend.
        s_material.SetFloat("_Surface", 1f);                       // Transparent
        s_material.SetFloat("_Blend", 2f);                         // Additive
        s_material.SetFloat("_SrcBlend", (float)BlendMode.One);
        s_material.SetFloat("_DstBlend", (float)BlendMode.One);
        s_material.SetFloat("_ZWrite", 0f);
        s_material.SetFloat("_Cull", (float)CullMode.Off);
        s_material.SetFloat("_AlphaClip", 0f);

        s_material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        s_material.DisableKeyword("_ALPHATEST_ON");
        s_material.renderQueue = (int)RenderQueue.Transparent;

        return s_material;
    }

    private static Texture2D GetSharedTexture()
    {
        if (s_texture != null) return s_texture;

        const int size = 128;
        s_texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "TrafficLightGlowTex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        float half = size * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);     // 0 centre -> 1 edge
                // Soft radial falloff (bright core, smooth fade to black at the rim).
                float v = Mathf.Clamp01(1f - r);
                v = v * v;                                   // tighten the core a little
                byte b = (byte)(v * 255f);
                pixels[y * size + x] = new Color32(b, b, b, b);
            }
        }
        s_texture.SetPixels32(pixels);
        s_texture.Apply(false, false);
        return s_texture;
    }
}
