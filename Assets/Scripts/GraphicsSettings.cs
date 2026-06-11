using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Self-building graphics settings panel (Quality / VSync / FPS cap).
/// Builds the UI in code (legacy uGUI). Instead of fiddly dropdowns it uses
/// "< value >" steppers, always visible and easy to click.
///
/// Setup:
///   1. Empty object -> GraphicsSettings component.
///   2. Ui Font -> menu font (Jupiter).
///   3. Settings button: OnClick -> GraphicsSettings.Toggle()  (or the Tab key).
/// Parent can stay empty - it builds its own overlay canvas (visible on screen).
/// Settings are saved to PlayerPrefs and applied at game startup.
/// </summary>
public class GraphicsSettings : MonoBehaviour
{
    [Header("Where to build (empty = own overlay canvas)")]
    public RectTransform parent;
    public Font          uiFont;

    [Header("Open")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Appearance")]
    public Color panelColor  = new Color(0.05f, 0.07f, 0.11f, 0.94f);
    public Color fieldColor  = new Color(0.11f, 0.14f, 0.20f, 0.96f);
    public Color textColor   = Color.white;
    public Color accentColor = new Color(0.15f, 0.65f, 1f, 1f);
    public int   titleFontSize = 34;
    public int   rowFontSize   = 26;
    public Vector2 panelSize   = new Vector2(620f, 360f);

    const string K_QUALITY = "gfx_quality";
    const string K_VSYNC   = "gfx_vsync";
    const string K_FPS     = "gfx_fpsCap";
    const string K_RSCALE  = "gfx_renderScale";
    static readonly int[]   FpsOptions         = { 0, 30, 60, 72, 90, 120, 144 };
    static readonly float[] RenderScaleOptions = { 0.6f, 0.7f, 0.8f, 0.85f, 0.9f, 1.0f };

    private GameObject _panel;

    // ── Apply saved settings at startup (before the scene) ────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedSettings()
    {
        if (PlayerPrefs.HasKey(K_QUALITY))
            QualitySettings.SetQualityLevel(
                Mathf.Clamp(PlayerPrefs.GetInt(K_QUALITY), 0, QualitySettings.names.Length - 1), true);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(K_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0);
        int fps = PlayerPrefs.GetInt(K_FPS, 0);
        Application.targetFrameRate = fps > 0 ? fps : -1;
        ApplyRenderScale(PlayerPrefs.GetFloat(K_RSCALE, 1f));
    }

    // Active URP asset for the current quality level (the per-quality PC/Mobile asset),
    // falling back to the project-wide default pipeline.
    static UniversalRenderPipelineAsset GetUrpAsset()
    {
        var asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (asset == null)
            asset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        return asset;
    }

    // Render scale renders the game at a fraction of the screen resolution and upscales.
    // The cheapest big GPU win on weak machines; softens the image slightly.
    static void ApplyRenderScale(float value)
    {
        var urp = GetUrpAsset();
        if (urp != null) urp.renderScale = Mathf.Clamp(value, 0.1f, 2f);
    }

    void Awake()
    {
        if (uiFont == null) uiFont = GetFallbackFont();
        Build();
        Hide();
    }

    void Update()
    {
        if (toggleKey != KeyCode.None && LegacyInput.GetKeyDown(toggleKey))
            Toggle();
    }

    public void Toggle()
    {
        if (_panel == null) { GameLog.Warn("[GraphicsSettings] Panel not built."); return; }
        bool on = !_panel.activeSelf;
        _panel.SetActive(on);
        if (on)
        {
            _panel.transform.SetAsLastSibling();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }
    public void Show() { if (_panel != null) { _panel.SetActive(true); _panel.transform.SetAsLastSibling(); } }
    public void Hide() { if (_panel != null) _panel.SetActive(false); }

    // ── Build ────────────────────────────────────────────────────────────────────
    void Build()
    {
        if (parent == null) parent = CreateOverlayCanvas();

        _panel = new GameObject("GraphicsPanel", typeof(RectTransform), typeof(Image),
                                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var rt = (RectTransform)_panel.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = panelSize;

        _panel.GetComponent<Image>().color = panelColor;
        var outline = _panel.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, 2f);

        var vlg = _panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(30, 30, 24, 26);
        vlg.spacing = 14;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        _panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddTitle("GRAPHICS");

        // Quality
        var qNames = QualitySettings.names.ToList();
        int qCur = Mathf.Clamp(PlayerPrefs.GetInt(K_QUALITY, QualitySettings.GetQualityLevel()), 0, qNames.Count - 1);
        QualitySettings.SetQualityLevel(qCur, true);
        AddSelector("Quality", qNames, qCur, i =>
        { QualitySettings.SetQualityLevel(i, true); SaveInt(K_QUALITY, i); });

        // VSync
        int vCur = PlayerPrefs.GetInt(K_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0);
        QualitySettings.vSyncCount = vCur;
        AddSelector("VSync", new List<string> { "Off", "On" }, vCur, i =>
        { QualitySettings.vSyncCount = i; SaveInt(K_VSYNC, i); });

        // FPS
        int savedFps = PlayerPrefs.GetInt(K_FPS, 0);
        int fIdx = Array.IndexOf(FpsOptions, savedFps); if (fIdx < 0) fIdx = 0;
        Application.targetFrameRate = savedFps > 0 ? savedFps : -1;
        var fpsNames = FpsOptions.Select(f => f == 0 ? "Unlimited" : f.ToString()).ToList();
        AddSelector("Frame limit (FPS)", fpsNames, fIdx, i =>
        { int fps = FpsOptions[i]; Application.targetFrameRate = fps > 0 ? fps : -1; SaveInt(K_FPS, fps); });

        // Render scale
        float savedRs = PlayerPrefs.GetFloat(K_RSCALE, 1f);
        int rIdx = Array.IndexOf(RenderScaleOptions, savedRs);
        if (rIdx < 0) rIdx = RenderScaleOptions.Length - 1; // default 100%
        ApplyRenderScale(RenderScaleOptions[rIdx]);
        var rsNames = RenderScaleOptions.Select(v => Mathf.RoundToInt(v * 100f) + "%").ToList();
        AddSelector("Render scale", rsNames, rIdx, i =>
        { float v = RenderScaleOptions[i]; ApplyRenderScale(v); SaveFloat(K_RSCALE, v); });

        GameLog.Info($"[GraphicsSettings] Panel built. Open with: {toggleKey} or Settings.Toggle()");
    }

    // ── Elements ──────────────────────────────────────────────────────────────────
    void AddTitle(string text)
    {
        var t = MakeText(_panel.transform, text, accentColor, titleFontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.GetComponent<LayoutElement>().minHeight = titleFontSize + 14;
    }

    // Row: [label] [ < value > ]
    void AddSelector(string label, List<string> options, int current, Action<int> onChange)
    {
        var row = NewRow("Row_" + label, 50);

        var lbl = MakeText(row.transform, label, textColor, rowFontSize, TextAnchor.MiddleLeft, FontStyle.Normal);
        lbl.GetComponent<LayoutElement>().minWidth = 270;

        // field with arrows
        var field = new GameObject("Field", typeof(RectTransform), typeof(Image),
                                   typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        field.transform.SetParent(row.transform, false);
        field.GetComponent<Image>().color = fieldColor;
        var fle = field.GetComponent<LayoutElement>(); fle.minWidth = 280; fle.flexibleWidth = 1;
        var fhlg = field.GetComponent<HorizontalLayoutGroup>();
        fhlg.childControlWidth = fhlg.childControlHeight = true;
        fhlg.childForceExpandWidth = false; fhlg.childForceExpandHeight = true;
        fhlg.childAlignment = TextAnchor.MiddleCenter;
        fhlg.padding = new RectOffset(6, 6, 4, 4); fhlg.spacing = 4;

        int idx = Mathf.Clamp(current, 0, options.Count - 1);
        int n = options.Count;

        var left  = MakeArrowButton(field.transform, "<");
        var valGO = MakeText(field.transform, options[idx], textColor, rowFontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
        valGO.GetComponent<LayoutElement>().flexibleWidth = 1;
        var right = MakeArrowButton(field.transform, ">");

        left.onClick.AddListener(() => { idx = (idx - 1 + n) % n; valGO.text = options[idx]; onChange(idx); });
        right.onClick.AddListener(() => { idx = (idx + 1) % n; valGO.text = options[idx]; onChange(idx); });
    }

    GameObject NewRow(string name, float minHeight)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_panel.transform, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().minHeight = minHeight;
        return row;
    }

    Text MakeText(Transform parent, string text, Color col, int size, TextAnchor anchor, FontStyle style)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = uiFont; t.text = text; t.color = col; t.fontSize = size;
        t.alignment = anchor; t.fontStyle = style; t.horizontalOverflow = HorizontalWrapMode.Overflow;
        go.GetComponent<LayoutElement>().minHeight = size + 6;
        return t;
    }

    Button MakeArrowButton(Transform parent, string label)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.30f);
        var le = go.GetComponent<LayoutElement>(); le.minWidth = 46; le.minHeight = 40;

        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.highlightedColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f);
        cb.pressedColor     = accentColor;
        btn.colors = cb;

        var tgo = new GameObject("T", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)tgo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = tgo.GetComponent<Text>();
        t.font = uiFont; t.text = label; t.color = textColor; t.fontSize = rowFontSize;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
        return btn;
    }

    // ── Misc ──────────────────────────────────────────────────────────────────────
    RectTransform CreateOverlayCanvas()
    {
        var go = new GameObject("GraphicsSettingsCanvas",
                                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        return go.transform as RectTransform;
    }

    static void SaveInt(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }

    static void SaveFloat(string key, float value) { PlayerPrefs.SetFloat(key, value); PlayerPrefs.Save(); }

    static Font GetFallbackFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }
}
