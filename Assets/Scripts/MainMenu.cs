using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Главное меню симулятора вождения.
/// Работает совместно с AuthManager: кнопки меню скрыты до авторизации.
/// AuthManager вызывает ShowMenu() после успешного входа.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Сцена с симулятором")]
    public string gameSceneName = "SampleScene";

    [Header("UI элементы")]
    public CanvasGroup menuGroup;
    public Button      btnStart;
    public Button      btnSettings;
    public Button      btnQuit;

    [Header("Настройки (панель)")]
    public GameObject  settingsPanel;
    public Slider      sliderVolume;
    public TMP_Dropdown dropdownQuality;

    [Header("Приветствие")]
    public TMP_Text    greetingText;   // "Добро пожаловать, <ФИО>"

    [Header("Анимация появления")]
    public float fadeInDuration = 1.2f;

    void Start()
    {
        btnStart?.onClick.AddListener(OnStart);
        btnSettings?.onClick.AddListener(OnSettings);
        btnQuit?.onClick.AddListener(OnQuit);

        if (sliderVolume != null)
        {
            sliderVolume.value = PlayerPrefs.GetFloat("Volume", 1f);
            sliderVolume.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat("Volume", v);
            });
        }

        if (dropdownQuality != null)
        {
            dropdownQuality.value = QualitySettings.GetQualityLevel();
            dropdownQuality.onValueChanged.AddListener(v => QualitySettings.SetQualityLevel(v));
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Меню изначально скрыто — AuthManager решает когда показать
        HideMenu();
    }

    // Вызывается AuthManager после успешного входа / регистрации
    public void ShowMenu()
    {
        if (menuGroup != null)
        {
            menuGroup.gameObject.SetActive(true);
            menuGroup.alpha          = 0f;
            menuGroup.interactable   = true;
            menuGroup.blocksRaycasts = true;
            StartCoroutine(FadeIn());
        }

        // Приветствие
        if (greetingText != null)
        {
            string name = PlayerPrefs.GetString(AuthManager.KEY_FULL_NAME, "");
            greetingText.text = string.IsNullOrEmpty(name) ? "" : $"Добро пожаловать, {name}";
        }
    }

    // Вызывается AuthManager при выходе из аккаунта
    public void HideMenu()
    {
        if (menuGroup != null)
        {
            menuGroup.alpha          = 0f;
            menuGroup.interactable   = false;
            menuGroup.blocksRaycasts = false;
        }
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    void OnStart()
    {
        if (!AuthManager.IsLoggedIn) return;
        StartCoroutine(LoadGame());
    }

    void OnSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    void OnQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            menuGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        menuGroup.alpha = 1f;
    }

    IEnumerator LoadGame()
    {
        if (menuGroup != null)
        {
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                menuGroup.alpha = 1f - Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
        }
        SceneManager.LoadScene(gameSceneName);
    }
}
