using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Планшет с состоянием всех 10 упражнений + экран результатов по окончании экзамена.
/// Строки генерируются автоматически в Awake.
/// </summary>
public class StatusPanel : MonoBehaviour
{
    [Header("Корень панели")]
    public GameObject statusPanel;

    [Header("Шрифт и иконки")]
    public TMP_FontAsset font;
    public Sprite iconChecked;
    public Sprite iconUnchecked;
    public Sprite iconFailed;

    [Header("Размеры — статус")]
    public float rowHeight    = 120f;
    public float fontSize     = 100f;
    public float iconSize     =  90f;
    public float paddingLeft  = 160f;
    public float paddingOther =  40f;
    public float spacing      =  10f;

    [Header("Размеры — результаты")]
    public float resultTitleSize  = 140f;
    public float resultHeaderSize =  80f;
    public float resultRowSize    =  70f;

    [Header("Цвета")]
    public Color colorDone    = Color.white;
    public Color colorFailed  = new Color(1f, 0.40f, 0.40f);
    public Color colorPending = new Color(0.7f, 0.7f, 0.7f);
    public Color colorActive  = new Color(0.24f, 0.71f, 1f);
    public Color colorPass    = new Color(0.2f, 0.9f, 0.4f);

    // ── runtime ──────────────────────────────────────────────────────────
    private Image[]           _icons;
    private TextMeshProUGUI[] _labels;
    private TextMeshProUGUI   _totalLabel;

    private GameObject _statusRoot;
    private GameObject _resultsRoot;
    private bool _resultsShown = false;

    private static readonly string[] Names =
    {
        "1.  Старт",
        "2.  Нерег. перекрёстки",
        "3.  Рег. перекрёсток",
        "4.  Пешеходный переход",
        "5.  Разворот и парковка",
        "6.  Параллельная парковка",
        "7.  ЖД переезд",
        "8.  Аварийная остановка",
        "9.  Подъём и спуск",
        "10. Финиш"
    };

    // ── Unity ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (statusPanel == null) statusPanel = gameObject;

        var oldCsf = statusPanel.GetComponent<ContentSizeFitter>();
        if (oldCsf != null) DestroyImmediate(oldCsf);

        for (int i = statusPanel.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(statusPanel.transform.GetChild(i).gameObject);

        BuildStatusPanel();
        BuildResultsPanel();
    }

    void Start()
    {
        TrySubscribe();
    }

    void TrySubscribe()
    {
        if (ExamManager.Instance == null) return;
        // Снимаем возможную старую подписку и ставим заново — защита от двойных вызовов
        ExamManager.Instance.OnExamFinish.RemoveListener(ShowResults);
        ExamManager.Instance.OnExamFinish.AddListener(ShowResults);
    }

    void Update()
    {
        if (ExamManager.Instance == null) return;

        // На случай если подписаться не удалось при Start (Instance был null)
        if (ExamManager.Instance.State == ExamManager.ExamState.Finished)
        {
            if (!_resultsShown) ShowResults();
            return;
        }

        RefreshStatus(ExamManager.Instance);
    }

    // ════════════════════════════════════════════════════════════════════
    // СТАТУС-ПАНЕЛЬ
    // ════════════════════════════════════════════════════════════════════

    void BuildStatusPanel()
    {
        _statusRoot = MakeContainer("StatusRoot");

        var vlg = _statusRoot.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.spacing                = spacing;
        vlg.padding                = new RectOffset((int)paddingLeft, (int)paddingOther,
                                                    (int)paddingOther, (int)paddingOther);
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        _icons  = new Image[Names.Length];
        _labels = new TextMeshProUGUI[Names.Length];

        for (int i = 0; i < Names.Length; i++)
        {
            var row = MakeRow(_statusRoot, $"Ex{i + 1}");
            _icons[i]  = MakeIcon(row);
            _labels[i] = MakeLabel(row, Names[i], fontSize, colorPending);
        }

        MakeSeparator(_statusRoot);

        var totalRow = MakeRow(_statusRoot, "TotalRow");
        MakeSpacer(totalRow);
        _totalLabel = MakeLabel(totalRow, "Штраф: 0 б.", fontSize, colorDone);
        _totalLabel.fontStyle = FontStyles.Bold;
    }

    void RefreshStatus(ExamManager exam)
    {
        for (int i = 0; i < Names.Length; i++)
        {
            if (_icons[i] == null || _labels[i] == null) continue;

            Color  col;
            Sprite spr;
            float  alpha;

            switch (exam.ExerciseStatuses[i])
            {
                case ExamManager.ExerciseStatus.Completed:
                    col = colorDone;   spr = iconChecked;  alpha = 1f;    break;
                case ExamManager.ExerciseStatus.Failed:
                    col = colorFailed; spr = iconFailed != null ? iconFailed : iconChecked; alpha = 1f; break;
                case ExamManager.ExerciseStatus.Active:
                    col = colorActive; spr = iconUnchecked != null ? iconUnchecked : iconChecked; alpha = 1f; break;
                default:
                    col = colorPending; spr = iconUnchecked != null ? iconUnchecked : iconChecked; alpha = 0.35f; break;
            }

            _icons[i].sprite  = spr;
            _icons[i].color   = new Color(col.r, col.g, col.b, alpha);
            _icons[i].enabled = spr != null;
            _labels[i].color  = col;
        }

        if (_totalLabel != null)
        {
            int pts = exam.TotalPenaltyPoints;
            _totalLabel.text  = $"Штраф: {pts} б.";
            _totalLabel.color = pts >= 100 ? colorFailed
                              : pts >  0   ? new Color(1f, 0.7f, 0.3f)
                              :              colorDone;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // ПАНЕЛЬ РЕЗУЛЬТАТОВ
    // ════════════════════════════════════════════════════════════════════

    void BuildResultsPanel()
    {
        _resultsRoot = MakeContainer("ResultsRoot");
        _resultsRoot.SetActive(false); // скрыта до конца экзамена

        var vlg = _resultsRoot.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.spacing                = spacing + 1f;
        vlg.padding                = new RectOffset((int)paddingLeft, (int)paddingOther,
                                                    (int)paddingOther, (int)paddingOther);
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
    }

    void ShowResults()
    {
        var exam = ExamManager.Instance;
        if (exam == null) return;
        if (_resultsShown) return; // защита от повторного построения
        _resultsShown = true;

        Debug.Log($"[StatusPanel] ShowResults called. Penalty={exam.TotalPenaltyPoints}");

        bool passed = exam.TotalPenaltyPoints < 100;

        // Прячем статус, показываем результаты
        if (_statusRoot  != null) _statusRoot.SetActive(false);
        if (_resultsRoot != null) _resultsRoot.SetActive(true);

        // Очищаем старые данные (если ShowResults вдруг вызвали повторно)
        for (int i = _resultsRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(_resultsRoot.transform.GetChild(i).gameObject);

        var vlg = _resultsRoot.GetComponent<VerticalLayoutGroup>();

        // ── Заголовок СДАЛ / НЕ СДАЛ ────────────────────────────────
        string  verdict     = passed ? "СДАЛ" : "НЕ СДАЛ";
        Color   verdictCol  = passed ? colorPass : colorFailed;

        var titleRow = MakeRow(_resultsRoot, "Title");
        var titleRowLE = titleRow.GetComponent<LayoutElement>();
        titleRowLE.preferredHeight = titleRowLE.minHeight = resultTitleSize + 30f;

        var title = MakeLabel(titleRow, verdict, resultTitleSize, verdictCol);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;

        MakeSeparator(_resultsRoot);

        // ── Итоговые баллы ───────────────────────────────────────────
        var scoreRow   = MakeRow(_resultsRoot, "Score");
        var scoreLabel = MakeLabel(scoreRow, $"Штрафные баллы: {exam.TotalPenaltyPoints}", resultHeaderSize,
                                   passed ? colorDone : colorFailed);
        scoreLabel.fontStyle = FontStyles.Bold;

        // ── Итог по упражнениям ──────────────────────────────────────
        MakeSeparator(_resultsRoot);
        AddSimpleRow(_resultsRoot, "Упражнения:", resultHeaderSize, colorPending);

        for (int i = 0; i < 10; i++)
        {
            var status = exam.ExerciseStatuses[i];
            if (status == ExamManager.ExerciseStatus.Pending) continue; // не добрались — пропускаем

            string mark;
            Color  col;
            switch (status)
            {
                case ExamManager.ExerciseStatus.Completed: mark = "[+]"; col = colorDone;   break;
                case ExamManager.ExerciseStatus.Failed:    mark = "[x]"; col = colorFailed; break;
                default:                                   mark = "[ ]"; col = colorPending; break;
            }

            var exRow = MakeRow(_resultsRoot, $"ExR{i}");

            // Метка статуса — фиксированная узкая колонка, выравнивание по левому краю
            var markLbl = MakeLabel(exRow, mark, resultRowSize, col);
            markLbl.alignment = TextAlignmentOptions.MidlineLeft;
            var markLE = markLbl.GetComponent<LayoutElement>();
            markLE.minWidth = markLE.preferredWidth = iconSize * 1.5f;
            markLE.flexibleWidth = 0;

            // Описание — растягивается на оставшуюся ширину
            var nameLbl = MakeLabel(exRow, Names[i].TrimStart(), resultRowSize, col);
            nameLbl.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // ── Список штрафов ───────────────────────────────────────────
        if (exam.Penalties.Count > 0)
        {
            MakeSeparator(_resultsRoot);
            AddSimpleRow(_resultsRoot, "Штрафы:", resultHeaderSize, colorPending);

            string failedHex = ColorUtility.ToHtmlStringRGB(colorFailed);
            foreach (var p in exam.Penalties)
            {
                var pRow = MakePenaltyRow(_resultsRoot, "Penalty");

                // Один TMP на всю строку: префикс с очками + описание. Wrap работает естественно.
                string richText = $"<b><color=#{failedHex}>-{p.points}</color></b>   {p.description}";
                var lbl = MakeLabel(pRow, richText, resultRowSize, colorPending);
                lbl.alignment = TextAlignmentOptions.TopLeft;
                lbl.textWrappingMode = TextWrappingModes.Normal;
                lbl.overflowMode = TextOverflowModes.Overflow;
                lbl.richText = true;
            }
        }
    }

    /// Строка штрафа: автоподстраиваемая высота для длинных описаний с переносом.
    GameObject MakePenaltyRow(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight       = resultRowSize + 10f;
        le.preferredHeight = -1f;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 24f;            // явный отступ между -100 и описанием
        hlg.childAlignment         = TextAnchor.UpperLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;            // даём флексу описания работать
        hlg.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        return go;
    }

    // ════════════════════════════════════════════════════════════════════
    // ВСПОМОГАТЕЛЬНЫЕ
    // ════════════════════════════════════════════════════════════════════

    /// Полноразмерный дочерний контейнер (stretch-заполнение родителя)
    GameObject MakeContainer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(statusPanel.transform, false);

        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go;
    }

    GameObject MakeRow(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = rowHeight;
        le.minHeight       = rowHeight;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing               = 4f;
        hlg.childAlignment        = TextAnchor.MiddleLeft;
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        return go;
    }

    Image MakeIcon(GameObject row)
    {
        var go = new GameObject("Icon");
        go.transform.SetParent(row.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = iconSize;
        le.flexibleWidth = 0;

        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.sprite  = iconUnchecked != null ? iconUnchecked : iconChecked;
        img.color   = new Color(colorPending.r, colorPending.g, colorPending.b, 0.35f);
        img.enabled = iconChecked != null || iconUnchecked != null;
        return img;
    }

    void MakeSpacer(GameObject row)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = iconSize;
        le.flexibleWidth = 0;
    }

    TextMeshProUGUI MakeLabel(GameObject row, string text, float size, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(row.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize           = size;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode   = TextWrappingModes.NoWrap;
        tmp.overflowMode       = TextOverflowModes.Ellipsis;
        tmp.text               = text;
        tmp.color              = color;
        return tmp;
    }

    void AddSimpleRow(GameObject parent, string text, float size, Color color)
    {
        var row = MakeRow(parent, "SimpleRow");
        MakeLabel(row, text, size, color);
    }

    void MakeSeparator(GameObject parent)
    {
        var go = new GameObject("Sep");
        go.transform.SetParent(parent.transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 1f;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f);
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
