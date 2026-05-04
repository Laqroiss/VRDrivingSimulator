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

    [Header("Animation")]
    public float fadeInDuration   = 1.5f;
    public float fadeOutDuration  = 0.8f;
    public float cockpitFlyTime   = 2.5f; // fly-into-cockpit time

    private Camera     _cam;
    private bool       _menuActive      = true;
    private Vector3    _cockpitWorldPos;
    private Quaternion _cockpitWorldRot;

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
        else
        {
            //   TrackedPoseDriver 
            var tpd = _cam?.GetComponent<TrackedPoseDriver>();
            if (tpd != null) tpd.enabled = false;
        }

        // Place the camera at the first point
        if (_cam != null && cinematicPoints.Length > 0)
        {
            _cam.transform.SetParent(null); //   XR 
            _cam.transform.position = cinematicPoints[0].position;
            _cam.transform.rotation = cinematicPoints[0].rotation;
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
        if (menuPanel != null) menuPanel.gameObject.SetActive(false);

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
        else
        {
            var tpd = _cam?.GetComponent<TrackedPoseDriver>();
            if (tpd != null) tpd.enabled = true;
        }

        // Leave Park - the driver can shift to Drive and go
        if (car != null)
            car.transmissionMode = Car.TransmissionMode.Neutral;

        //      StartLine  
        Destroy(gameObject);
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
