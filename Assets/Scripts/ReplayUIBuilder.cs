using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Программно строит Screen Space — Overlay UI для ReplaySystem.
/// Добавьте на любой GameObject в сцене — Canvas создаётся автоматически.
/// После Awake все ссылки заполняются в ReplaySystem автоматически.
/// </summary>
public class ReplayUIBuilder : MonoBehaviour
{
    [Header("Ссылка на ReplaySystem")]
    public ReplaySystem replaySystem;

    [Header("Шрифт (TMP)")]
    public TMP_FontAsset font;

    [Header("Цвета")]
    public Color colorBg         = new Color(0f,   0f,   0f,   0.72f);
    public Color colorBtn        = new Color(0.15f, 0.15f, 0.15f, 1f);
    public Color colorBtnPlay    = new Color(0.18f, 0.72f, 0.36f, 1f);
    public Color colorBtnStop    = new Color(0.75f, 0.18f, 0.18f, 1f);
    public Color colorBtnRecord  = new Color(0.18f, 0.72f, 0.36f, 1f);
    public Color colorText       = Color.white;
    public Color colorSliderBg   = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color colorSliderFill = new Color(0.24f, 0.71f, 1f,   1f);
    public Color colorSliderKnob = Color.white;

    [Header("Размеры (px на 1080p)")]
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

    private Transform _root; // корень внутри Canvas

    // ── Unity ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (replaySystem == null)
            replaySystem = FindAnyObjectByType<ReplaySystem>();

        if (replaySystem == null)
            replaySystem = gameObject.AddComponent<ReplaySystem>();

        _root = BuildCanvas();
        BuildPlayerPanel();
        BuildListPanel();
        BuildRecordButton();
    }

    // ══════════════════════════════════════════════════════════════════════
    // CANVAS
    // ══════════════════════════════════════════════════════════════════════

    Transform BuildCanvas()
    {
        var go = new GameObject("ReplayCanvas");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // поверх остального UI

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ПЛЕЕР-ПАНЕЛЬ  (снизу по центру, появляется при воспроизведении)
    // ══════════════════════════════════════════════════════════════════════

    void BuildPlayerPanel()
    {
        var panel = MakePanel("ReplayPlayerPanel", playerPanelW, playerPanelH);
        // Снизу по центру
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

        // Слайдер
        var sliderObj = new GameObject("ReplaySlider");
        sliderObj.transform.SetParent(panel, false);
        var sliderLE = sliderObj.AddComponent<LayoutElement>();
        sliderLE.preferredHeight = sliderH + 16f;
        sliderLE.minHeight       = sliderH + 16f;
        replaySystem.replaySlider = BuildSlider(sliderObj);

        // Метка времени
        var timeLbl = MakeLabel(panel.gameObject, "TimeLabel", "0.0s / 0.0s", smallFontSize);
        timeLbl.alignment = TextAlignmentOptions.Center;
        var timeLblLE = timeLbl.gameObject.AddComponent<LayoutElement>();
        timeLblLE.preferredHeight = smallFontSize + 8f;
        replaySystem.replayTimeLabel = timeLbl;

        // Кнопки Play / Stop
        var btnRow = MakeHRow(panel.gameObject, "BtnRow", btnH);
        replaySystem.btnReplayPlay = MakeButton(btnRow, "BtnPlay", "▶  Воспроизвести", colorBtnPlay);
        replaySystem.btnReplayStop = MakeButton(btnRow, "BtnStop", "■  Стоп",          colorBtnStop);

        replaySystem.replayPlayerPanel = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ПАНЕЛЬ СПИСКА ПОВТОРОВ  (справа сверху)
    // ══════════════════════════════════════════════════════════════════════

    void BuildListPanel()
    {
        // Панель растёт вниз по мере добавления записей (ContentSizeFitter)
        var panelGO = new GameObject("ReplayListPanel");
        panelGO.transform.SetParent(_root, false);

        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = colorBg;

        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1f, 1f);
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-16f, -16f);
        panelRT.sizeDelta        = new Vector2(listPanelW, 0f); // высота авто

        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        vlg.spacing                = spacing;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = panelGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Заголовок
        var header = MakeLabel(panelGO, "Header", "Повторы", fontSize);
        header.fontStyle = FontStyles.Bold;
        header.alignment = TextAlignmentOptions.Center;
        var hLE = header.gameObject.GetComponent<LayoutElement>();
        hLE.preferredHeight = hLE.minHeight = fontSize + 12f;

        MakeSeparator(panelGO);

        // Список — прямой дочерний контейнер, без ScrollRect
        var listGO = new GameObject("List");
        listGO.transform.SetParent(panelGO.transform, false);

        var listVLG = listGO.AddComponent<VerticalLayoutGroup>();
        listVLG.spacing               = spacing;
        listVLG.childControlWidth     = true;
        listVLG.childControlHeight    = true;
        listVLG.childForceExpandWidth  = true;
        listVLG.childForceExpandHeight = false;

        var listCSF = listGO.AddComponent<ContentSizeFitter>();
        listCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        replaySystem.replayListParent  = listGO.transform;
        replaySystem.replayEntryPrefab = BuildEntryPrefab();
    }

    // ══════════════════════════════════════════════════════════════════════
    // КНОПКА ЗАПИСИ  (правый нижний угол)
    // ══════════════════════════════════════════════════════════════════════

    void BuildRecordButton()
    {
        var go = new GameObject("BtnRecord");
        go.transform.SetParent(_root, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.sizeDelta        = new Vector2(260f, btnH);
        rt.anchoredPosition = new Vector2(-16f, 16f);

        var img = go.AddComponent<Image>();
        img.color = colorBtnRecord;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic   = img;
        var cs  = btn.colors;
        cs.normalColor      = Color.white;
        cs.highlightedColor = Lighten(Color.white, 0.15f);
        cs.pressedColor     = Darken(Color.white,  0.2f);
        cs.selectedColor    = Color.white;
        btn.colors = cs;

        var lbl = MakeSimpleLabel(go, "[o] Начать запись", fontSize * 0.85f);
        lbl.alignment = TextAlignmentOptions.Center;

        replaySystem.btnRecord      = btn;
        replaySystem.btnRecordLabel = lbl;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ПРЕФАБ СТРОКИ ПОВТОРА
    // ══════════════════════════════════════════════════════════════════════

    GameObject BuildEntryPrefab()
    {
        var go = new GameObject("ReplayEntryPrefab");
        go.transform.SetParent(_root, false);
        go.SetActive(false);

        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, btnH);

        var img = go.AddComponent<Image>();
        img.color = colorBtn;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic   = img;
        var cs  = btn.colors;
        cs.normalColor      = Color.white;
        cs.highlightedColor = Lighten(Color.white, 0.2f);
        cs.pressedColor     = Darken(Color.white, 0.15f);
        cs.selectedColor    = Color.white;
        btn.colors = cs;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = btnH;
        le.minHeight       = btnH;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset((int)padding, (int)padding, 0, 0);
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlHeight     = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth  = true;

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize         = smallFontSize;
        tmp.color            = colorText;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Ellipsis;
        tmp.text             = "Попытка 1  —  0.0 сек";

        return go;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ВСПОМОГАТЕЛЬНЫЕ
    // ══════════════════════════════════════════════════════════════════════

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
        btn.targetGraphic = img; // обязательно для регистрации кликов
        var cs  = btn.colors;
        cs.normalColor      = Color.white; // Image.color уже задаёт цвет, tint множитель
        cs.highlightedColor = Lighten(Color.white, 0.15f);
        cs.pressedColor     = Darken(Color.white,  0.2f);
        cs.selectedColor    = Color.white;
        btn.colors = cs;
        MakeSimpleLabel(go, label, fontSize * 0.85f);
        return btn;
    }

    void MakeSeparator(GameObject parent)
    {
        var go = new GameObject("Sep");
        go.transform.SetParent(parent.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 2f;
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
    }

    static Color Lighten(Color c, float a) =>
        new Color(Mathf.Clamp01(c.r + a), Mathf.Clamp01(c.g + a), Mathf.Clamp01(c.b + a), c.a);

    static Color Darken(Color c, float a) =>
        new Color(Mathf.Clamp01(c.r - a), Mathf.Clamp01(c.g - a), Mathf.Clamp01(c.b - a), c.a);
}
