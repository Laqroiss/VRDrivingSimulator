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


    [Header("UI")]
    public Canvas      menuCanvas;   // root menu Canvas - for VR positioning
    public CanvasGroup menuPanel;
    public Button      btnStart;
    public Button      btnSettings;
    public Button      btnQuit;
    public GameObject  settingsPanel;
    public TextMeshProUGUI keyHintText;

    [Header("Settings")]
    public Slider      volumeSlider;
    public TMP_Dropdown qualityDropdown;

    [Header("Car")]
    public Car car;

    [Header("XR")]
    [Tooltip("DriverHeadAnchor or XR Origin - head tracking is disabled during the menu")]
    public GameObject xrHeadAnchor;
    [Tooltip("Distance from camera to menu in VR (meters)")]
    public float vrMenuDistance = 2f;
    [Tooltip("Menu scale in VR")]
    public float vrMenuScale    = 0.001f;

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

        // 
        btnStart?.onClick.AddListener(StartGame);
        btnSettings?.onClick.AddListener(ToggleSettings);
        btnQuit?.onClick.AddListener(QuitGame);

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

        // Quality
        if (qualityDropdown != null)
        {
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.onValueChanged.AddListener(QualitySettings.SetQualityLevel);
        }

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
            StartGame();
        else if (LegacyInput.GetKeyDown(KeyCode.Tab))
            ToggleSettings();
        else if (LegacyInput.GetKeyDown(KeyCode.Escape))
            QuitGame();
    }


    // тт Cinematic ттттттттттттттттттттттттттттттттттттттттттттттттттттттттт

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

    // тт Start the game тттттттттттттттттттттттттттттттттттттттттттттттттттттт

    void StartGame()
    {
        if (!_menuActive) return;
        _menuActive = false;
        StartCoroutine(TransitionToCockpit());
    }

    IEnumerator TransitionToCockpit()
    {
        // Hide the menu
        yield return StartCoroutine(FadeMenu(1f, 0f, fadeOutDuration));
        if (menuPanel != null)
            menuPanel.gameObject.SetActive(false);
        else if (_cam != null)
        {
            // Fallback: hide the whole Canvas under the camera
            var c = _cam.GetComponentInChildren<Canvas>();
            if (c != null) c.gameObject.SetActive(false);
        }

        //       (XR  )
        yield return StartCoroutine(FlyTo(_cockpitWorldPos, _cockpitWorldRot, cockpitFlyTime));

        //   XR   
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

        // Leave Park - the driver can shift to Drive and go
        if (car != null)
            car.transmissionMode = Car.TransmissionMode.Neutral;

        // Switch to pause mode (ESC during the game)
        _inGame = true;

        //   Start  Resume
        var startLabel = btnStart?.GetComponentInChildren<TextMeshProUGUI>();
        if (startLabel != null) startLabel.text = "Resume";

        if (keyHintText != null)
            keyHintText.text = "[Esc] Resume     [Tab] Settings     [Q] Quit";

        // Rebind btnStart to ResumeGame
        btnStart?.onClick.RemoveAllListeners();
        btnStart?.onClick.AddListener(ResumeGame);
    }

    // тт Pause ттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттттт

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

    // тт Settings / Quit ттттттттттттттттттттттттттттттттттттттттттттттттттттт

    void ToggleSettings()
    {
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
