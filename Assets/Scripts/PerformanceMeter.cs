using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
///   0-100   100-0.
///    .   Canvas .
/// F5 = , F6 = .
/// </summary>
public class PerformanceMeter : MonoBehaviour
{
    [Header("")]
    public KeyCode keyAccel = KeyCode.F5;
    public KeyCode keyBrake = KeyCode.F6;
    public float   targetSpeedKmh = 100f;

    // вв refs ввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв
    public  Car              car;
    private TextMeshProUGUI  _txt;

    // вв   вввввввввввввввввввввввввввввввввввввввввввввввв
    private enum Mode { Idle, AccelWait, Accel, Brake }
    private Mode  _mode        = Mode.Idle;
    private float _timer       = 0f;
    private Vector3 _brakePos;

    private string _lastResult = "";

    // ввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void Awake()
    {
        BuildUI();
    }

    void Start()
    {
        if (car == null) Debug.LogError("[PerformanceMeter]  Car  Inspector!");
    }

    void Update()
    {
        if (car == null || car.rb == null) return;

        float kmh = car.rb.linearVelocity.magnitude * 3.6f;

        HandleInput(kmh);
        Tick(kmh);
        UpdateLabel(kmh);
    }

    // вв  ввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void HandleInput(float kmh)
    {
        if (LegacyInput.GetKeyDown(keyAccel))
        {
            if (_mode == Mode.Accel || _mode == Mode.AccelWait)
            {
                _mode = Mode.Idle;
                Log(" .");
            }
            else
            {
                _mode  = Mode.AccelWait;
                _timer = 0f;
                _lastResult = "";
                Log($"[]  вЂ”    ( {targetSpeedKmh} /)");
            }
        }

        if (LegacyInput.GetKeyDown(keyBrake))
        {
            if (_mode == Mode.Brake)
            {
                _mode = Mode.Idle;
                Log(" .");
            }
            else
            {
                if (kmh < targetSpeedKmh * 0.8f)
                {
                    Log($"  в‰Ґ{targetSpeedKmh * 0.8f:F0} /,  {kmh:F0} /");
                    return;
                }
                _mode      = Mode.Brake;
                _timer     = 0f;
                _brakePos  = car.rb.position;
                _lastResult = "";
                Log($"[] ! ({kmh:F0} / в†’ 0)");
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
                    Log($"[] ! в†’ {targetSpeedKmh} /");
                }
                break;

            case Mode.Accel:
                _timer += Time.deltaTime;
                if (kmh >= targetSpeedKmh)
                {
                    _lastResult = $"0 в†’ {targetSpeedKmh} /:  {_timer:F2} ";
                    Log($"[] {_lastResult}");
                    _mode = Mode.Idle;
                }
                break;

            case Mode.Brake:
                _timer += Time.deltaTime;
                if (kmh < 1f)
                {
                    float dist  = Vector3.Distance(_brakePos, car.rb.position);
                    _lastResult = $"{targetSpeedKmh} в†’ 0 /:  {dist:F1}   /  {_timer:F2} ";
                    Log($"[] {_lastResult}");
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
            Mode.AccelWait => $" ...",
            Mode.Accel     => $": {_timer:F2}   |  {kmh:F0}/{targetSpeedKmh} /",
            Mode.Brake     => $": {Vector3.Distance(_brakePos, car.rb.position):F1}   |  {_timer:F2} ",
            _              => $"{keyAccel}=    {keyBrake}="
        };

        int   gear = car.e != null ? car.e.getCurrentGear() : 0;
        float rpm  = car.e != null ? car.e.getRPM() : 0f;
        float inp  = car.wheels != null && car.wheels.Length > 1 ? car.wheels[1].input.y : 0f;

        _txt.text = $"<b></b>  {kmh:F1} /   <color=#88FFAA>G{gear}  {rpm:F0} RPM  inp={inp:F2}</color>\n{modeStr}"
                  + (_lastResult != "" ? $"\n<color=#FFE44D>{_lastResult}</color>" : "");
    }

    static void Log(string msg) => Debug.Log($"[PerformanceMeter] {msg}");

    // вв  UI ввввввввввввввввввввввввввввввввввввввввввввввввввввввв

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
