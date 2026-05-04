using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Главное меню симулятора вождения.
/// Поверх кинематики показывает название и кнопки.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Сцена с симулятором")]
    public string gameSceneName = "SampleScene";

    [Header("UI элементы")]
    public CanvasGroup menuGroup;       // весь блок меню для fade-in
    public Button      btnStart;
    public Button      btnSettings;
    public Button      btnQuit;

    [Header("Настройки (панель)")]
    public GameObject  settingsPanel;
    public Slider      sliderVolume;
    public TMP_Dropdown dropdownQuality;

    [Header("Анимация появления")]
    public float fadeInDuration = 1.2f;

    void Start()
    {
        // Кнопки
        btnStart?.onClick.AddListener(OnStart);
        btnSettings?.onClick.AddListener(OnSettings);
        btnQuit?.onClick.AddListener(OnQuit);

        // Настройки
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

        // Плавное появление меню
        if (menuGroup != null)
        {
            menuGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }
    }

    void OnStart()
    {
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
        // Плавное исчезновение меню перед загрузкой
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
