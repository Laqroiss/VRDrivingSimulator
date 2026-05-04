using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
///    .
///      .
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("  ")]
    public string gameSceneName = "SampleScene";

    [Header("UI ")]
    public CanvasGroup menuGroup;       //     fade-in
    public Button      btnStart;
    public Button      btnSettings;
    public Button      btnQuit;

    [Header(" ()")]
    public GameObject  settingsPanel;
    public Slider      sliderVolume;
    public TMP_Dropdown dropdownQuality;

    [Header(" ")]
    public float fadeInDuration = 1.2f;

    void Start()
    {
        // 
        btnStart?.onClick.AddListener(OnStart);
        btnSettings?.onClick.AddListener(OnSettings);
        btnQuit?.onClick.AddListener(OnQuit);

        // 
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

        //   
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
        //     
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
