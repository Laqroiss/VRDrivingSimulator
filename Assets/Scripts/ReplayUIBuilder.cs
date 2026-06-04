using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the Screen Space - Overlay UI for ReplaySystem in code.
/// Add to any GameObject in the scene - the Canvas is created automatically.
/// After Awake, all references in ReplaySystem are filled in automatically.
/// </summary>
public class ReplayUIBuilder : MonoBehaviour
{
    [Header("ReplaySystem reference")]
    public ReplaySystem replaySystem;

    [Header("Font (TMP)")]
    public TMP_FontAsset font;

    [Header("Colors")]
    public Color colorBg         = new Color(0f,   0f,   0f,   0.72f);
    public Color colorBtn        = new Color(0.15f, 0.15f, 0.15f, 1f);
    public Color colorBtnPlay    = new Color(0.18f, 0.72f, 0.36f, 1f);
    public Color colorBtnStop    = new Color(0.75f, 0.18f, 0.18f, 1f);
    public Color colorBtnRecord  = new Color(0.18f, 0.72f, 0.36f, 1f);
    public Color colorText       = Color.white;
    public Color colorSliderBg   = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color colorSliderFill = new Color(0.24f, 0.71f, 1f,   1f);
    public Color colorSliderKnob = Color.white;

    [Header("Sizes (px at 1080p)")]
    public float playerPanelW  = 700f;
    public float playerPanelH  = 180f;
    public float listPanelW    = 500f;
    public float listPanelH    = 420f;
    public float btnH          =  52f;
    public float fontSize      =  28f;
    public float smallFontSize =  22f;
    public float padding       =  16f;
    public float spacing       =  10f;
    public float sliderH       =  18f;

    private Transform _root; // root inside the Canvas

    // тт Unity ттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт

    void Awake()
    {
        if (replaySystem == null)
            replaySystem = FindAnyObjectByType<ReplaySystem>();

        if (replaySystem == null)
            replaySystem = gameObject.AddComponent<ReplaySystem>();

        _root = BuildCanvas();
        BuildPlayerPanel();
    }

    // тттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт
    // CANVAS
    // тттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт

    Transform BuildCanvas()
    {
        var go = new GameObject("ReplayCanvas");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above the rest of the UI

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    // тттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт
    // PLAYER PANEL  (bottom-center, appears during playback)
    // тттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт

    void BuildPlayerPanel()
    {
        var panel = MakePanel("ReplayPlayerPanel", playerPanelW, playerPanelH);
        // Bottom-center
        panel.anchorMin        = new Vector2(0.5f, 0f);
        panel.anchorMax        = new Vector2(0.5f, 0f);
        panel.pivot            = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 24f);

        var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        vlg.spacing                = spacing;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Slider
        var sliderObj = new GameObject("ReplaySlider");
        sliderObj.transform.SetParent(panel, false);
        var sliderLE = sliderObj.AddComponent<LayoutElement>();
        sliderLE.preferredHeight = sliderH + 16f;
        sliderLE.minHeight       = sliderH + 16f;
        replaySystem.replaySlider = BuildSlider(sliderObj);

        // Time label
        var timeLbl = MakeLabel(panel.gameObject, "TimeLabel", "0.0s / 0.0s", smallFontSize);
        timeLbl.alignment = TextAlignmentOptions.Center;
        var timeLblLE = timeLbl.gameObject.AddComponent<LayoutElement>();
        timeLblLE.preferredHeight = smallFontSize + 8f;
        replaySystem.replayTimeLabel = timeLbl;

        // Play / Stop buttons
        var btnRow = MakeHRow(panel.gameObject, "BtnRow", btnH);
        replaySystem.btnReplayPlay = MakeButton(btnRow, "BtnPlay", "т  Play", colorBtnPlay);
        replaySystem.btnReplayStop = MakeButton(btnRow, "BtnStop", "т  Stop", colorBtnStop);

        replaySystem.replayPlayerPanel = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    // тттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт
    // HELPERS
    // тттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт

    RectTransform MakePanel(string name, float w, float h)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(_root, false);

        var img = go.AddComponent<Image>();
        img.color = colorBg;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    GameObject MakeHRow(GameObject parent, string name, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight       = height;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = spacing;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        return go;
    }

    Slider BuildSlider(GameObject parent)
    {
        var bg = new GameObject("Background");
        bg.transform.SetParent(parent.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.5f);
        bgRT.anchorMax = new Vector2(1f, 0.5f);
        bgRT.sizeDelta = new Vector2(0f, sliderH);
        bg.AddComponent<Image>().color = colorSliderBg;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(parent.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.5f);
        faRT.anchorMax = new Vector2(1f, 0.5f);
        faRT.offsetMin = new Vector2(sliderH * 0.5f, -sliderH * 0.5f);
        faRT.offsetMax = new Vector2(-sliderH * 0.5f, sliderH * 0.5f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = colorSliderFill;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(parent.transform, false);
        var haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0f, 0f);
        haRT.anchorMax = new Vector2(1f, 1f);
        haRT.offsetMin = new Vector2(sliderH * 0.5f, 0f);
        haRT.offsetMax = new Vector2(-sliderH * 0.5f, 0f);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var hRT = handle.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(sliderH * 1.6f, sliderH * 1.6f);
        var hImg = handle.AddComponent<Image>();
        hImg.color = colorSliderKnob;

        var slider = parent.AddComponent<Slider>();
        slider.fillRect      = fillRT;
        slider.handleRect    = hRT;
        slider.targetGraphic = hImg;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = 0f;
        slider.maxValue      = 1f;
        slider.value         = 0f;
        return slider;
    }

    TextMeshProUGUI MakeLabel(GameObject parent, string goName, string text, float size)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize         = size;
        tmp.color            = colorText;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Ellipsis;
        tmp.text             = text;
        return tmp;
    }

    TextMeshProUGUI MakeSimpleLabel(GameObject parent, string text, float size)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize         = size;
        tmp.color            = colorText;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Ellipsis;
        tmp.text             = text;
        return tmp;
    }

    Button MakeButton(GameObject parent, string name, string label, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img; // required for click registration
        var cs  = btn.colors;
        cs.normalColor      = Color.white; // Image.color already sets the color, tint is a multiplier
        cs.highlightedColor = Lighten(Color.white, 0.15f);
        cs.pressedColor     = Darken(Color.white,  0.2f);
        cs.selectedColor    = Color.white;
        btn.colors = cs;
        MakeSimpleLabel(go, label, fontSize * 0.85f);
        return btn;
    }

    static Color Lighten(Color c, float a) =>
        new Color(Mathf.Clamp01(c.r + a), Mathf.Clamp01(c.g + a), Mathf.Clamp01(c.b + a), c.a);

    static Color Darken(Color c, float a) =>
        new Color(Mathf.Clamp01(c.r - a), Mathf.Clamp01(c.g - a), Mathf.Clamp01(c.b - a), c.a);
}
