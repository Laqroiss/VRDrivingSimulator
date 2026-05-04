using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Замер разгона 0-100 и торможения 100-0.
/// Добавь на любой объект. Создаёт свой Canvas автоматически.
/// F5 = разгон, F6 = торможение.
/// </summary>
public class PerformanceMeter : MonoBehaviour
{
    [Header("Клавиши")]
    public KeyCode keyAccel = KeyCode.F5;
    public KeyCode keyBrake = KeyCode.F6;
    public float   targetSpeedKmh = 100f;

    // ── refs ─────────────────────────────────────────────────────────────
    public  Car              car;
    private TextMeshProUGUI  _txt;

    // ── состояние разгона ────────────────────────────────────────────────
    private enum Mode { Idle, AccelWait, Accel, Brake }
    private Mode  _mode        = Mode.Idle;
    private float _timer       = 0f;
    private Vector3 _brakePos;

    private string _lastResult = "";

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        BuildUI();
    }

    void Start()
    {
        if (car == null) Debug.LogError("[PerformanceMeter] Назначь Car в Inspector!");
    }

    void Update()
    {
        if (car == null || car.rb == null) return;

        float kmh = car.rb.linearVelocity.magnitude * 3.6f;

        HandleInput(kmh);
        Tick(kmh);
        UpdateLabel(kmh);
    }

    // ── логика ───────────────────────────────────────────────────────────

    void HandleInput(float kmh)
    {
        if (LegacyInput.GetKeyDown(keyAccel))
        {
            if (_mode == Mode.Accel || _mode == Mode.AccelWait)
            {
                _mode = Mode.Idle;
                Log("Разгон отменён.");
            }
            else
            {
                _mode  = Mode.AccelWait;
                _timer = 0f;
                _lastResult = "";
                Log($"[Разгон] Тронься — замер начнётся автоматически (цель {targetSpeedKmh} км/ч)");
            }
        }

        if (LegacyInput.GetKeyDown(keyBrake))
        {
            if (_mode == Mode.Brake)
            {
                _mode = Mode.Idle;
                Log("Торможение отменено.");
            }
            else
            {
                if (kmh < targetSpeedKmh * 0.8f)
                {
                    Log($"Разгонись до ≥{targetSpeedKmh * 0.8f:F0} км/ч, сейчас {kmh:F0} км/ч");
                    return;
                }
                _mode      = Mode.Brake;
                _timer     = 0f;
                _brakePos  = car.rb.position;
                _lastResult = "";
                Log($"[Торможение] Тормози! ({kmh:F0} км/ч → 0)");
            }
        }
    }

    void Tick(float kmh)
    {
        switch (_mode)
        {
            case Mode.AccelWait:
                if (kmh > 3f)
                {
                    _mode  = Mode.Accel;
                    _timer = 0f;
                    Log($"[Разгон] Старт! → {targetSpeedKmh} км/ч");
                }
                break;

            case Mode.Accel:
                _timer += Time.deltaTime;
                if (kmh >= targetSpeedKmh)
                {
                    _lastResult = $"0 → {targetSpeedKmh} км/ч:  {_timer:F2} сек";
                    Log($"[Результат] {_lastResult}");
                    _mode = Mode.Idle;
                }
                break;

            case Mode.Brake:
                _timer += Time.deltaTime;
                if (kmh < 1f)
                {
                    float dist  = Vector3.Distance(_brakePos, car.rb.position);
                    _lastResult = $"{targetSpeedKmh} → 0 км/ч:  {dist:F1} м  /  {_timer:F2} сек";
                    Log($"[Результат] {_lastResult}");
                    _mode = Mode.Idle;
                }
                break;
        }
    }

    void UpdateLabel(float kmh)
    {
        if (_txt == null) return;

        string modeStr = _mode switch
        {
            Mode.AccelWait => $"Ожидание старта...",
            Mode.Accel     => $"Разгон: {_timer:F2} сек  |  {kmh:F0}/{targetSpeedKmh} км/ч",
            Mode.Brake     => $"Торможение: {Vector3.Distance(_brakePos, car.rb.position):F1} м  |  {_timer:F2} сек",
            _              => $"{keyAccel}=разгон    {keyBrake}=торможение"
        };

        int   gear = car.e != null ? car.e.getCurrentGear() : 0;
        float rpm  = car.e != null ? car.e.getRPM() : 0f;
        float inp  = car.wheels != null && car.wheels.Length > 1 ? car.wheels[1].input.y : 0f;

        _txt.text = $"<b>Замер</b>  {kmh:F1} км/ч   <color=#88FFAA>G{gear}  {rpm:F0} RPM  inp={inp:F2}</color>\n{modeStr}"
                  + (_lastResult != "" ? $"\n<color=#FFE44D>{_lastResult}</color>" : "");
    }

    static void Log(string msg) => Debug.Log($"[PerformanceMeter] {msg}");

    // ── создание UI ───────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = new GameObject("PerfMeterCanvas");
        DontDestroyOnLoad(canvasGO);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var bg = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg   = bg.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.55f);
        var bgRect  = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 1);
        bgRect.anchorMax = new Vector2(0, 1);
        bgRect.pivot     = new Vector2(0, 1);
        bgRect.anchoredPosition = new Vector2(10, -10);
        bgRect.sizeDelta = new Vector2(370, 80);

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(bg.transform, false);
        _txt = txtGO.AddComponent<TextMeshProUGUI>();
        _txt.fontSize  = 16;
        _txt.color     = Color.white;
        _txt.alignment = TextAlignmentOptions.TopLeft;
        _txt.margin    = new Vector4(10, 8, 10, 8);

        var txtRect = txtGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
    }
}
