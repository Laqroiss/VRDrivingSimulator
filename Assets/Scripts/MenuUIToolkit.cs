using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit menu frontend (UXML/USS). Gameplay logic (camera fly-by, cockpit
/// transition, pause, auth) lives in MenuManager - it calls this class's methods
/// and subscribes to the OnStart / OnQuit events.
///
/// Setup:
///   1. Create PanelSettings (Assets -> Create -> UI Toolkit -> Panel Settings).
///   2. Object with UIDocument: Source Asset = MainMenu.uxml, Panel Settings = yours.
///   3. Add MenuUIToolkit to the same object.
///   4. In MenuManager, set the "Menu UI" field -> this object.
/// Graphics/audio settings are saved to PlayerPrefs and applied at startup.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MenuUIToolkit : MonoBehaviour
{
    public event Action OnStart;
    public event Action OnQuit;
    public event Action OnLogin;
    public event Action<string, string> OnLoginSubmit;             // (phone, password)
    public event Action<string, string, string> OnRegisterSubmit; // (fullName, phone, password)

    const string K_VOLUME  = "Volume";
    const string K_QUALITY = "gfx_quality";
    const string K_VSYNC   = "gfx_vsync";
    const string K_FPS     = "gfx_fpsCap";
    const string K_INPUT   = "InputType";   // 0 = Keyboard, 1 = Wheel
    const string K_NAVHELP = "nav_help";     // 1 = show the route ribbon
    public const string K_RECORD = "exam_autoRecord"; // 1 = save the exam attempt (result + replay) to the DB

    /// <summary>Record the exam to the DB? When off, ExamResultSender writes no attempt (neither to the
    /// server nor the local backup), and ReplayCRMSync writes no replay. Default is on.</summary>
    public static bool AutoRecordExam => PlayerPrefs.GetInt(K_RECORD, 1) == 1;
    static readonly int[] FpsOptions = { 0, 30, 60, 72, 90, 120, 144 };

    private bool _init;
    private VisualElement _root, _overlay, _loginOverlay;
    private Button _btnStart, _btnSettings, _btnLogin, _btnQuit, _btnClose;
    private Button _loginSubmit, _loginClose, _loginSwitch;
    private TextField _loginPhone, _loginPassword, _loginFullname;
    private VisualElement _loginFullnameRow;
    private Label _keyHint, _startLabel, _loginStatus, _loginTitle, _loginSubmitLabel, _loginSwitchLabel;
    private Label _loginBtnLabel, _loginBtnSub;
    private bool _registerMode;
    private Slider _volume;
    private DropdownField _quality, _fps, _inputType;
    private Toggle _vsync, _navHelp, _recordReplay;

    // ── Apply saved settings at startup ───────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        if (PlayerPrefs.HasKey(K_QUALITY))
            QualitySettings.SetQualityLevel(
                Mathf.Clamp(PlayerPrefs.GetInt(K_QUALITY), 0, QualitySettings.names.Length - 1), true);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(K_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0);
        int fps = PlayerPrefs.GetInt(K_FPS, 0);
        Application.targetFrameRate = fps > 0 ? fps : -1;
        AudioListener.volume = PlayerPrefs.GetFloat(K_VOLUME, 1f);
    }

    void OnEnable()  => EnsureInit();
    void Start()     => EnsureInit();

    void EnsureInit()
    {
        if (_init) return;
        var doc = GetComponent<UIDocument>();
        var r = doc != null ? doc.rootVisualElement : null;
        if (r == null) return; // UIDocument hasn't built the tree yet - try again later

        _root        = r.Q("root") ?? r;
        _overlay     = r.Q("settings-overlay");
        _btnStart    = r.Q<Button>("start-button");
        _btnSettings = r.Q<Button>("settings-button");
        _btnLogin    = r.Q<Button>("login-button");
        _loginBtnLabel = _btnLogin?.Q<Label>(className: "btn__label");
        _loginBtnSub   = _btnLogin?.Q<Label>(className: "btn__sub");
        _btnQuit     = r.Q<Button>("quit-button");
        _btnClose    = r.Q<Button>("settings-close");
        _keyHint     = r.Q<Label>("key-hint");                 // not in this design - will be null, that's fine
        _startLabel  = _btnStart?.Q<Label>(className: "btn__label");
        _volume      = r.Q<Slider>("volume");
        _quality     = r.Q<DropdownField>("quality");
        _fps         = r.Q<DropdownField>("fps");
        _vsync       = r.Q<Toggle>("vsync");
        _inputType   = r.Q<DropdownField>("input-type");
        _navHelp     = r.Q<Toggle>("nav-help");
        _recordReplay = r.Q<Toggle>("record-replay");

        _loginOverlay     = r.Q("login-overlay");
        _loginPhone       = r.Q<TextField>("login-phone");
        _loginPassword    = r.Q<TextField>("login-password");
        _loginFullname    = r.Q<TextField>("login-fullname");
        _loginFullnameRow = r.Q("login-fullname-row");
        _loginSubmit      = r.Q<Button>("login-submit");
        _loginClose       = r.Q<Button>("login-close");
        _loginSwitch      = r.Q<Button>("login-switch");
        _loginStatus      = r.Q<Label>("login-status");
        _loginTitle       = r.Q<Label>("login-title");
        _loginSubmitLabel = r.Q<Label>("login-submit-label");
        _loginSwitchLabel = r.Q<Label>("login-switch-label");

        if (_btnStart    != null) _btnStart.clicked    += () => OnStart?.Invoke();
        if (_btnSettings != null) _btnSettings.clicked += ToggleSettings;
        if (_btnLogin    != null) _btnLogin.clicked    += () => OnLogin?.Invoke();
        if (_btnQuit     != null) _btnQuit.clicked     += () => OnQuit?.Invoke();
        if (_btnClose    != null) _btnClose.clicked    += () => SetSettingsVisible(false);
        if (_loginSubmit != null) _loginSubmit.clicked += SubmitLoginOrRegister;
        if (_loginClose  != null) _loginClose.clicked  += HideLogin;
        if (_loginSwitch != null) _loginSwitch.clicked += () => SetRegisterMode(!_registerMode);

        SetupSettings();
        _init = true;
    }

    // ── Public API for MenuManager ────────────────────────────────────────────
    public void ShowMenu() { EnsureInit(); if (_root != null) _root.style.display = DisplayStyle.Flex; }
    public void HideMenu() { EnsureInit(); if (_root != null) _root.style.display = DisplayStyle.None; }
    public void SetStartText(string t)
    {
        EnsureInit();
        if (_startLabel != null) _startLabel.text = t;
        else if (_btnStart != null) _btnStart.text = t;
    }
    public void SetKeyHint(string t)   { EnsureInit(); if (_keyHint != null) _keyHint.text = t; }

    /// <summary>Changes the profile button label (in game "Save & exit", in the menu "PROFILE").</summary>
    public void SetProfileButtonText(string label, string sub)
    {
        EnsureInit();
        if (_loginBtnLabel != null) _loginBtnLabel.text = label;
        if (_loginBtnSub   != null) _loginBtnSub.text   = sub;
    }

    public void ToggleSettings()
    {
        EnsureInit();
        SetSettingsVisible(_overlay != null && _overlay.ClassListContains("hidden"));
    }

    public void SetSettingsVisible(bool on)
    {
        EnsureInit();
        SetOverlay(_overlay, on);
    }

    public void ShowLogin()
    {
        EnsureInit();
        SetRegisterMode(false);            // always open in sign-in mode
        SetOverlay(_loginOverlay, true);
    }
    public void HideLogin() { EnsureInit(); SetOverlay(_loginOverlay, false); }
    public void SetLoginStatus(string t) { EnsureInit(); if (_loginStatus != null) _loginStatus.text = t; }

    // Submit button: sign in or register depending on the mode
    void SubmitLoginOrRegister()
    {
        string phone = _loginPhone?.value ?? "";
        string pass  = _loginPassword?.value ?? "";
        if (_registerMode)
            OnRegisterSubmit?.Invoke(_loginFullname?.value ?? "", phone, pass);
        else
            OnLoginSubmit?.Invoke(phone, pass);
    }

    // Toggle between "sign in" and "register"
    void SetRegisterMode(bool on)
    {
        _registerMode = on;
        if (_loginFullnameRow != null) _loginFullnameRow.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (_loginTitle       != null) _loginTitle.text       = on ? "SIGN UP" : "SIGN IN";
        if (_loginSubmitLabel != null) _loginSubmitLabel.text = on ? "SIGN UP" : "SIGN IN";
        if (_loginSwitchLabel != null) _loginSwitchLabel.text = on ? "Have an account? Sign in" : "No account? Sign up";
        if (_loginStatus      != null) _loginStatus.text = "";
    }

    static void SetOverlay(VisualElement ov, bool on)
    {
        if (ov == null) return;
        if (on) { ov.RemoveFromClassList("hidden"); ov.BringToFront(); }
        else    ov.AddToClassList("hidden");
    }

    // ── Settings ─────────────────────────────────────────────────────────────────
    void SetupSettings()
    {
        if (_volume != null)
        {
            _volume.value = PlayerPrefs.GetFloat(K_VOLUME, 1f);
            AudioListener.volume = _volume.value;
            _volume.RegisterValueChangedCallback(e =>
            { AudioListener.volume = e.newValue; PlayerPrefs.SetFloat(K_VOLUME, e.newValue); PlayerPrefs.Save(); });
        }

        if (_quality != null)
        {
            _quality.choices = QualitySettings.names.ToList();
            int q = Mathf.Clamp(PlayerPrefs.GetInt(K_QUALITY, QualitySettings.GetQualityLevel()), 0, _quality.choices.Count - 1);
            _quality.index = q;
            QualitySettings.SetQualityLevel(q, true);
            _quality.RegisterValueChangedCallback(_ =>
            { QualitySettings.SetQualityLevel(_quality.index, true); PlayerPrefs.SetInt(K_QUALITY, _quality.index); PlayerPrefs.Save(); });
        }

        if (_fps != null)
        {
            _fps.choices = FpsOptions.Select(f => f == 0 ? "Unlimited" : f.ToString()).ToList();
            int saved = PlayerPrefs.GetInt(K_FPS, 0);
            int idx = Array.IndexOf(FpsOptions, saved); if (idx < 0) idx = 0;
            _fps.index = idx;
            Application.targetFrameRate = saved > 0 ? saved : -1;
            _fps.RegisterValueChangedCallback(_ =>
            {
                int fps = FpsOptions[Mathf.Clamp(_fps.index, 0, FpsOptions.Length - 1)];
                Application.targetFrameRate = fps > 0 ? fps : -1;
                PlayerPrefs.SetInt(K_FPS, fps); PlayerPrefs.Save();
            });
        }

        if (_vsync != null)
        {
            bool on = PlayerPrefs.GetInt(K_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            _vsync.value = on;
            QualitySettings.vSyncCount = on ? 1 : 0;
            _vsync.RegisterValueChangedCallback(e =>
            { QualitySettings.vSyncCount = e.newValue ? 1 : 0; PlayerPrefs.SetInt(K_VSYNC, e.newValue ? 1 : 0); PlayerPrefs.Save(); });
        }

        if (_inputType != null)
        {
            _inputType.choices = new List<string> { "Keyboard", "Wheel" };
            var car = FindAnyObjectByType<Car>();
            int def = car != null ? (int)car.inputMode : 0;   // default - as set in the car inspector
            int saved = Mathf.Clamp(PlayerPrefs.GetInt(K_INPUT, def), 0, 1);
            _inputType.index = saved;
            ApplyInputType(saved);
            _inputType.RegisterValueChangedCallback(_ =>
            {
                int v = Mathf.Clamp(_inputType.index, 0, 1);
                ApplyInputType(v);
                PlayerPrefs.SetInt(K_INPUT, v); PlayerPrefs.Save();
            });
        }

        if (_navHelp != null)
        {
            // Navigation help (route ribbon). RouteRibbon reads this pref every frame,
            // so toggling works on the fly and doesn't depend on the 3-segment system.
            _navHelp.value = PlayerPrefs.GetInt(K_NAVHELP, 1) == 1;
            _navHelp.RegisterValueChangedCallback(e =>
            {
                PlayerPrefs.SetInt(K_NAVHELP, e.newValue ? 1 : 0);
                PlayerPrefs.Save();
            });
        }

        if (_recordReplay != null)
        {
            // Auto-record the exam replay to the DB. Turn off to avoid cluttering the
            // database with test attempts. ReplayCRMSync reads MenuUIToolkit.AutoRecordExam.
            _recordReplay.value = PlayerPrefs.GetInt(K_RECORD, 1) == 1;
            _recordReplay.RegisterValueChangedCallback(e =>
            {
                PlayerPrefs.SetInt(K_RECORD, e.newValue ? 1 : 0);
                PlayerPrefs.Save();
            });
        }
    }

    // Applies the input type to the car (0 = Keyboard, 1 = Wheel)
    static void ApplyInputType(int v)
    {
        var car = FindAnyObjectByType<Car>();
        if (car != null) car.inputMode = (Car.InputMode)v;
    }
}
