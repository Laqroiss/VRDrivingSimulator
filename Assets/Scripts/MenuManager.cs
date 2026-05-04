using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.XR;
using TMPro;
using System.Collections;

/// <summary>
/// Главное меню — Main Camera летает по трассе как кинематика.
/// При нажатии Start плавно перелетает в кабину машины.
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Header("Кинематика — точки облёта")]
    public Transform[] cinematicPoints;  // точки по трассе
    public float       travelTime  = 4f; // время перелёта между точками
    public float       holdTime    = 2f; // время на каждой точке


    [Header("UI")]
    public CanvasGroup menuPanel;
    public Button      btnStart;
    public Button      btnSettings;
    public Button      btnQuit;
    public GameObject  settingsPanel;
    public TextMeshProUGUI keyHintText;

    [Header("Настройки")]
    public Slider      volumeSlider;
    public TMP_Dropdown qualityDropdown;

    [Header("Машина")]
    public Car car;

    [Header("XR")]
    [Tooltip("DriverHeadAnchor или XR Origin — отключаем трекинг головы во время меню")]
    public GameObject xrHeadAnchor;

    [Header("Анимация")]
    public float fadeInDuration   = 1.5f;
    public float fadeOutDuration  = 0.8f;
    public float cockpitFlyTime   = 2.5f; // время перелёта в кабину

    private Camera     _cam;
    private bool       _menuActive      = true;
    private Vector3    _cockpitWorldPos;
    private Quaternion _cockpitWorldRot;

    void Start()
    {
        _cam = Camera.main;

        // Ставим машину на Park — стоит физически, но не заморожена
        if (car != null)
            car.transmissionMode = Car.TransmissionMode.Park;

        // Запоминаем позицию кабины ДО отключения XR
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

        // Отключаем XR трекинг головы — иначе HMD будет перебивать кинематику
        if (xrHeadAnchor != null)
            xrHeadAnchor.SetActive(false);
        else
        {
            // Попытка найти TrackedPoseDriver автоматически
            var tpd = _cam?.GetComponent<TrackedPoseDriver>();
            if (tpd != null) tpd.enabled = false;
        }

        // Ставим камеру на первую точку
        if (_cam != null && cinematicPoints.Length > 0)
        {
            _cam.transform.SetParent(null); // отвязываем от XR иерархии
            _cam.transform.position = cinematicPoints[0].position;
            _cam.transform.rotation = cinematicPoints[0].rotation;
        }

        // Кнопки
        btnStart?.onClick.AddListener(StartGame);
        btnSettings?.onClick.AddListener(ToggleSettings);
        btnQuit?.onClick.AddListener(QuitGame);

        // Громкость
        if (volumeSlider != null)
        {
            volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
            volumeSlider.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat("Volume", v);
            });
        }

        // Качество
        if (qualityDropdown != null)
        {
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.onValueChanged.AddListener(QualitySettings.SetQualityLevel);
        }

        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (keyHintText != null)
            keyHintText.text = "[Enter] — Начать     [Tab] — Настройки     [Esc] — Выход";

        // Появление меню
        if (menuPanel != null)
        {
            menuPanel.alpha = 0f;
            StartCoroutine(FadeMenu(0f, 1f, fadeInDuration));
        }

        // Запускаем кинематику
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

    // ── Кинематика ────────────────────────────────────────────────────────

    IEnumerator CinematicRoutine()
    {
        int   index   = 0;
        float holdTimer = 0f;

        while (true)
        {
            // Держимся на точке — проверяем _menuActive каждый кадр
            holdTimer = 0f;
            while (holdTimer < holdTime)
            {
                if (!_menuActive) yield break; // Enter нажат — мгновенно выходим
                holdTimer += Time.deltaTime;
                yield return null;
            }

            int next = (index + 1) % cinematicPoints.Length;
            Vector3    startPos = _cam.transform.position;
            Quaternion startRot = _cam.transform.rotation;
            float t = 0f;

            // Летим к следующей точке — тоже покадрово с проверкой
            while (t < travelTime)
            {
                if (!_menuActive) yield break; // Enter нажат — мгновенно выходим
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

    // ── Запуск игры ───────────────────────────────────────────────────────

    void StartGame()
    {
        if (!_menuActive) return;
        _menuActive = false;
        StartCoroutine(TransitionToCockpit());
    }

    IEnumerator TransitionToCockpit()
    {
        // Прячем меню
        yield return StartCoroutine(FadeMenu(1f, 0f, fadeOutDuration));
        if (menuPanel != null) menuPanel.gameObject.SetActive(false);

        // Плавный перелёт к запомненной позиции кабины (XR ещё выключен)
        yield return StartCoroutine(FlyTo(_cockpitWorldPos, _cockpitWorldRot, cockpitFlyTime));

        // Теперь включаем XR и репарентим камеру
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

        // Снимаем Park — водитель может переключить в Drive и поехать
        if (car != null)
            car.transmissionMode = Car.TransmissionMode.Neutral;

        // Экзамен запускается автоматически через триггер StartLine в сцене
        Destroy(gameObject);
    }

    // ── Настройки / Выход ─────────────────────────────────────────────────

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
