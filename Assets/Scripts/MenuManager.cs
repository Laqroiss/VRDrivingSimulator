using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.XR;
using TMPro;
using System.Collections;

/// <summary>
/// Main menu - the Main Camera flies along the track as a cinematic.
/// Pressing Start smoothly flies it into the car cockpit.
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Header("Cinematic - fly-by points")]
    public Transform[] cinematicPoints;  // points along the track
    public float       travelTime  = 4f; // travel time between points
    public float       holdTime    = 2f; // time held at each point


    [Header("UI Toolkit menu")]
    [Tooltip("Object with UIDocument + MenuUIToolkit (new menu). The legacy uGUI fields below can stay empty")]
    public MenuUIToolkit menuUI;
    [Tooltip("Student cabinet (ProfilePanel on the same object as UIDocument)")]
    public ProfilePanel  profilePanel;
    [Tooltip("ReplayCRMSync (to launch a replay from the cabinet)")]
    public ReplayCRMSync replaySync;

    [Header("UI (legacy uGUI - optional)")]
    public Canvas      menuCanvas;   // root menu Canvas - for VR positioning
    public CanvasGroup menuPanel;
    public Button      btnStart;
    public Button      btnSettings;
    public Button      btnQuit;
    public GameObject  settingsPanel;
    public TextMeshProUGUI keyHintText;

    [Header("Settings")]
    public Slider      volumeSlider;

    [Header("Car")]
    public Car car;

    [Header("XR")]
    [Tooltip("DriverHeadAnchor or XR Origin - head tracking is disabled during the menu")]
    public GameObject xrHeadAnchor;
    [Tooltip("Distance from camera to menu in VR (meters)")]
    public float vrMenuDistance = 2f;
    [Tooltip("Menu scale in VR")]
    public float vrMenuScale    = 0.001f;

    [Header("Authentication")]
    [Tooltip("Drag the object with the AuthManager component here")]
    public AuthManager authManager;

    [Header("Exam resume")]
    [Tooltip("ExamResume (usually on the ExamManager object) - continue from the last DB checkpoint")]
    public ExamResume examResume;

    [Header("Animation")]
    public float fadeInDuration   = 1.5f;
    public float fadeOutDuration  = 0.8f;
    public float cockpitFlyTime   = 2.5f; // fly-into-cockpit time

    private Camera     _cam;
    private Canvas     _menuCanvas;
    private bool       _menuActive      = true;
    private bool       _paused          = false;
    private bool       _inGame          = false;
    private Vector3    _cockpitWorldPos;
    private Quaternion _cockpitWorldRot;
    private Vector3    _frozenCamPos;
    private Quaternion _frozenCamRot;

    void Start()
    {
        _cam = Camera.main;

        // Put the car in Park - physically stopped, but not frozen
        if (car != null)
            car.transmissionMode = Car.TransmissionMode.Park;

        // Remember the cockpit pose BEFORE disabling XR
        var pitchObj = GameObject.Find("HeadPitch");
        if (pitchObj != null)
        {
            _cockpitWorldPos = pitchObj.transform.position;
            _cockpitWorldRot = pitchObj.transform.rotation;
        }
        else if (_cam != null)
        {
            _cockpitWorldPos = _cam.transform.position;
            _cockpitWorldRot = _cam.transform.rotation;
        }

        // Disable XR head tracking - otherwise the HMD overrides the cinematic
        if (xrHeadAnchor != null)
            xrHeadAnchor.SetActive(false);

        // Disable every TrackedPoseDriver in the scene (including on inactive objects)
        foreach (var tpd in FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include))
            tpd.enabled = false;

        // Place the camera at the first point
        if (_cam != null && cinematicPoints.Length > 0)
        {
            _cam.transform.SetParent(null);
            _cam.transform.position = cinematicPoints[0].position;
            _cam.transform.rotation = cinematicPoints[0].rotation;
        }

        // Find the Canvas and detach it from the hierarchy
        _menuCanvas = menuCanvas;
        if (_menuCanvas == null && menuPanel != null)
            _menuCanvas = menuPanel.GetComponentInParent<Canvas>();

        if (_menuCanvas != null)
        {
            _menuCanvas.gameObject.SetActive(true);
            _menuCanvas.renderMode  = RenderMode.WorldSpace;
            _menuCanvas.worldCamera = _cam;
        }

        // Buttons (legacy uGUI)
        btnStart?.onClick.AddListener(OnStartClicked);
        btnSettings?.onClick.AddListener(ToggleSettings);
        btnQuit?.onClick.AddListener(QuitGame);

        // New UI Toolkit menu
        if (menuUI != null)
        {
            menuUI.OnStart += HandleStartButton;
            menuUI.OnQuit  += QuitGame;
            menuUI.OnLogin += HandleLogin;
            menuUI.OnLoginSubmit += HandleLoginSubmit;
            menuUI.OnRegisterSubmit += HandleRegisterSubmit;
            menuUI.ShowMenu();
            menuUI.SetKeyHint("[Enter] Start     [Tab] Settings     [Esc] Close");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        if (profilePanel != null)
        {
            profilePanel.OnReplay        += PlayAttemptReplay;
            profilePanel.OnResumeAttempt += HandleResumeAttempt;
            profilePanel.OnLogout        += HandleLogout;
        }
        ReplayCRMSync.OnReplayFinished += OnReplayFinished;

        if (examResume == null) examResume = FindAnyObjectByType<ExamResume>();

        // Volume
        if (volumeSlider != null)
        {
            volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
            volumeSlider.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat("Volume", v);
            });
        }

        // Quality/VSync/FPS    GraphicsSettings (  Settings)

        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (keyHintText != null)
            keyHintText.text = "[Enter] Start     [Tab] Settings     [Esc] Quit";

        // Fade the menu in
        if (menuPanel != null)
        {
            menuPanel.alpha = 0f;
            StartCoroutine(FadeMenu(0f, 1f, fadeInDuration));
        }

        // Start the cinematic
        if (cinematicPoints.Length > 1)
            StartCoroutine(CinematicRoutine());
    }

    void Update()
    {
        // In the menu and while paused the cursor is always visible (StopReplay may have locked it)
        if ((!_inGame || _paused) && Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        if (_inGame)
        {
            if (LegacyInput.GetKeyDown(KeyCode.Escape))
            {
                if (_paused) ResumeGame();
                else         PauseGame();
            }
            return;
        }

        if (!_menuActive) return;
        if (LegacyInput.GetKeyDown(KeyCode.Return) || LegacyInput.GetKeyDown(KeyCode.KeypadEnter))
            OnStartClicked();
        else if (LegacyInput.GetKeyDown(KeyCode.Tab))
            ToggleSettings();
        else if (LegacyInput.GetKeyDown(KeyCode.Escape))
        {
            // ESC closes open panels rather than quitting (use the QUIT button to exit)
            menuUI?.SetSettingsVisible(false);
            menuUI?.HideLogin();
            profilePanel?.Hide();
        }
    }


    // вв Cinematic ввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    IEnumerator CinematicRoutine()
    {
        int   index   = 0;
        float holdTimer = 0f;

        while (true)
        {
            // Hold at the point - check _menuActive every frame
            holdTimer = 0f;
            while (holdTimer < holdTime)
            {
                if (!_menuActive) yield break; // Enter pressed - exit immediately
                holdTimer += Time.deltaTime;
                yield return null;
            }

            int next = (index + 1) % cinematicPoints.Length;
            Vector3    startPos = _cam.transform.position;
            Quaternion startRot = _cam.transform.rotation;
            float t = 0f;

            // Fly to the next point - also frame-by-frame with the check
            while (t < travelTime)
            {
                if (!_menuActive) yield break; // Enter pressed - exit immediately
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / travelTime);
                _cam.transform.position = Vector3.Lerp(startPos, cinematicPoints[next].position, s);
                _cam.transform.rotation = Quaternion.Slerp(startRot, cinematicPoints[next].rotation, s);
                yield return null;
            }

            index = next;
        }
    }

    IEnumerator FlyTo(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        if (_cam == null) yield break;
        Vector3    startPos = _cam.transform.position;
        Quaternion startRot = _cam.transform.rotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t / duration);
            _cam.transform.position = Vector3.Lerp(startPos, targetPos, s);
            _cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, s);
            yield return null;
        }
        _cam.transform.position = targetPos;
        _cam.transform.rotation = targetRot;
    }

    // вв Start the game вввввввввввввввввввввввввввввввввввввввввввввввввввввв

    // Start/Continue button from the UI Toolkit menu: in game = Resume, otherwise = Start
    void HandleStartButton()
    {
        if (_inGame) { if (_paused) ResumeGame(); }
        else OnStartClicked();
    }

    private bool _resumeOnEnter; // next cockpit entry is a resume, not a fresh start

    // "CONTINUE" on an abandoned attempt in the cabinet - continue THAT exact attempt
    // from the last saved checkpoint.
    void HandleResumeAttempt(string attemptId)
    {
        if (_inGame || !_menuActive || examResume == null) return;
        profilePanel?.Hide();
        menuUI?.SetSettingsVisible(false);
        examResume.LoadResumable(attemptId, ok =>
        {
            if (!ok) { Debug.LogWarning("[MenuManager] Failed to load the attempt to continue"); return; }
            _resumeOnEnter = true;
            StartGame();
        });
    }

    // LOG IN / LOG OUT button:
    //  - not logged in -> authenticate, then open the cabinet on success
    //  - logged in     -> open/close the student cabinet
    void HandleLogin()
    {
        // During an exam the cabinet/replays are unavailable - the button acts as "Save & exit to menu".
        // A replay can only be launched from the main menu (where the camera flies), after exiting.
        if (_inGame) { ExitToMenu(); return; }

        if (AuthManager.IsLoggedIn)
            profilePanel?.Toggle();          // logged in -> open/close the cabinet
        else
        {
            _afterLogin = () => profilePanel?.Show();   // not logged in -> login form, then cabinet
            menuUI?.ShowLogin();
        }
    }

    // Saves the current attempt (as unfinished - it can be continued later) and returns to the
    // main menu by reloading the scene. From the menu you can open the cabinet and launch a replay.
    void ExitToMenu()
    {
        FindAnyObjectByType<ExamResultSender>()?.SaveNow();
        Time.timeScale = 1f;                 // timeScale survives a scene reload - reset it
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
    }

    // Log out from the cabinet
    void HandleLogout()
    {
        authManager?.Logout();
        profilePanel?.Hide();
    }

    private bool _replayCtxInGame;   // replay launched from in-game pause (true) or from the menu (false)

    // Launch a 3D replay from the cabinet
    void PlayAttemptReplay(string attemptId)
    {
        _replayCtxInGame = _inGame;
        profilePanel?.Hide();
        menuUI?.HideMenu();
        menuUI?.SetSettingsVisible(false);
        Time.timeScale = 1f;            // replays always run in real time (otherwise frames freeze)
        replaySync?.PlayReplayById(attemptId);
    }

    // When the replay ends - restore the menu/pause and cursor
    void OnReplayFinished()
    {
        Debug.Log($"[MenuManager] Replay finished -> returning (inGame context={_replayCtxInGame})");
        StartCoroutine(AfterReplay());
    }

    IEnumerator AfterReplay()
    {
        // let ReplaySystem.StopReplay finish (it locks the cursor/camera into the cockpit)
        for (int i = 0; i < 4; i++) yield return null;

        menuUI?.ShowMenu();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        // from in-game (cabinet opened from pause) - stay paused; from the menu - normal time
        Time.timeScale = _replayCtxInGame ? 0f : 1f;
    }

    void OnDestroy()
    {
        ReplayCRMSync.OnReplayFinished -= OnReplayFinished;
    }

    void OnStartClicked()
    {
        _resumeOnEnter = false;            // normal start - a new exam, not a resume
        if (authManager != null && !AuthManager.IsLoggedIn)
        {
            _afterLogin = StartGame;       // sign in right here, then start
            menuUI?.ShowLogin();
            return;
        }
        StartGame();
    }

    // вв In-game sign-in (no browser) ввввввввввввввввввввввввввввввввввввввв
    private System.Action _afterLogin;

    void HandleLoginSubmit(string phone, string password)
    {
        if (authManager == null) return;
        menuUI?.SetLoginStatus("Signing inвЂ¦");
        authManager.LoginInline(phone, password,
            onSuccess: () =>
            {
                menuUI?.HideLogin();
                var a = _afterLogin; _afterLogin = null;
                a?.Invoke();
            },
            onError: err => menuUI?.SetLoginStatus(err));
    }

    void HandleRegisterSubmit(string fullName, string phone, string password)
    {
        if (authManager == null) return;
        menuUI?.SetLoginStatus("Creating accountвЂ¦");
        authManager.RegisterInline(fullName, phone, password,
            onSuccess: () =>
            {
                menuUI?.HideLogin();
                var a = _afterLogin; _afterLogin = null;
                a?.Invoke();
            },
            onError: err => menuUI?.SetLoginStatus(err));
    }

    void StartGame()
    {
        if (!_menuActive) return;
        _menuActive = false;
        StartCoroutine(TransitionToCockpit());
    }

    // Enables XR and seats the camera in the cockpit (HeadPitch). Used both for a normal start
    // (after the fly-by) and for a resume (instantly, no fly-by).
    void SnapCameraToCockpit()
    {
        if (xrHeadAnchor != null)
        {
            xrHeadAnchor.SetActive(true);
            var pitch = GameObject.Find("HeadPitch");
            if (pitch != null && _cam != null)
            {
                _cam.transform.SetParent(pitch.transform, false);
                _cam.transform.localPosition = Vector3.zero;
                _cam.transform.localRotation = Quaternion.identity;
            }
        }

        // Re-enable TrackedPoseDriver - player is in the cockpit, head drives the camera again
        foreach (var tpd in FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include))
            tpd.enabled = true;
    }

    IEnumerator TransitionToCockpit()
    {
        // Hide the menu
        menuUI?.HideMenu();

        yield return StartCoroutine(FadeMenu(1f, 0f, fadeOutDuration));
        if (menuPanel != null)
            menuPanel.gameObject.SetActive(false);
        else if (_cam != null)
        {
            // Fallback: hide the whole Canvas under the camera
            var c = _cam.GetComponentInChildren<Canvas>();
            if (c != null) c.gameObject.SetActive(false);
        }

        if (_resumeOnEnter && examResume != null)
        {
            // Resume - NO camera fly-by. Place the car at the checkpoint, restore the exam
            // (statuses/gates) RIGHT THEN, and hand over control immediately. Otherwise during
            // the camera fly-by the car already sits inside an exercise zone while statuses
            // aren't restored yet - a trigger (railway crossing etc.) fires with the wrong
            // state, "eats" the entry and stays locked, since the car is already inside and
            examResume.TeleportCarToCheckpoint();
            examResume.RestoreExamState();
            _resumeOnEnter = false;
            SnapCameraToCockpit();
        }
        else
        {
            // Normal start - smooth fly-by to the remembered cockpit pose (XR still off)
            yield return StartCoroutine(FlyTo(_cockpitWorldPos, _cockpitWorldRot, cockpitFlyTime));
            SnapCameraToCockpit();
        }

        // Leave Park - the driver can shift to Drive and go
        if (car != null)
            car.transmissionMode = Car.TransmissionMode.Neutral;

        // Switch to pause mode (ESC during the game)
        _inGame = true;

        // Change the Start button to "Resume" (UI Toolkit and legacy uGUI)
        menuUI?.SetStartText("RESUME");
        menuUI?.SetKeyHint("[Esc] Resume     [Tab] Settings     [Q] Quit");

        // In game the profile button = "Save & exit to menu" (cabinet/replay only from the menu)
        menuUI?.SetProfileButtonText("SAVE & EXIT", "ATTEMPT в†’ MAIN MENU");

        var startLabel = btnStart?.GetComponentInChildren<TextMeshProUGUI>();
        if (startLabel != null) startLabel.text = "Resume";

        if (keyHintText != null)
            keyHintText.text = "[Esc] Resume     [Tab] Settings     [Q] Quit";

        // Rebind btnStart to ResumeGame
        btnStart?.onClick.RemoveAllListeners();
        btnStart?.onClick.AddListener(ResumeGame);
    }

    // вв Pause ввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void PauseGame()
    {
        if (_paused) return;
        _paused = true;
        Time.timeScale = 0f;

        // Detach the camera from HeadPitch - it stays put in the world
        if (_cam != null)
            _cam.transform.SetParent(null, true);

        // Disable XR tracking
        foreach (var tpd in FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include))
            tpd.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        menuUI?.ShowMenu();

        if (menuPanel != null)
        {
            menuPanel.gameObject.SetActive(true);
            menuPanel.alpha = 0f;
            StartCoroutine(FadeMenuUnscaled(0f, 1f, fadeInDuration));
        }
    }

    void ResumeGame()
    {
        if (!_paused) return;
        _paused = false;
        StartCoroutine(ResumeRoutine());
    }

    IEnumerator ResumeRoutine()
    {
        menuUI?.SetSettingsVisible(false);
        menuUI?.HideMenu();
        yield return StartCoroutine(FadeMenuUnscaled(1f, 0f, fadeOutDuration));
        if (menuPanel != null)
            menuPanel.gameObject.SetActive(false);

        // Return the camera back into HeadPitch
        var pitch = GameObject.Find("HeadPitch");
        if (pitch != null && _cam != null)
        {
            _cam.transform.SetParent(pitch.transform, false);
            _cam.transform.localPosition = Vector3.zero;
            _cam.transform.localRotation = Quaternion.identity;
        }

        // Enable XR tracking
        foreach (var tpd in FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Include))
            tpd.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        Time.timeScale = 1f;
    }

    IEnumerator FadeMenuUnscaled(float from, float to, float duration)
    {
        if (menuPanel == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            menuPanel.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        menuPanel.alpha = to;
    }

    // вв Settings / Quit ввввввввввввввввввввввввввввввввввввввввввввввввввввв

    void ToggleSettings()
    {
        menuUI?.ToggleSettings();
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator FadeMenu(float from, float to, float duration)
    {
        if (menuPanel == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            menuPanel.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        menuPanel.alpha = to;
    }
}
