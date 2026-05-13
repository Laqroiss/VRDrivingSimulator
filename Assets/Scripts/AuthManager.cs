using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Net;
using System.Threading;
using System.Collections;

/// <summary>
/// Авторизация через браузер.
///
/// Настройка в Unity (сцена MainMenu):
/// 1. Добавь этот скрипт на AuthPanel.
/// 2. Привяжи кнопку OpenBrowserButton и StatusText.
/// 3. В CRM Url укажи адрес сайта (localhost или VPS).
/// </summary>
public class AuthManager : MonoBehaviour
{
    [Header("CRM")]
    public string crmUrl       = "http://localhost:3000/game-login";
    public int    callbackPort = 7777;

    [Header("UI")]
    public GameObject authPanel;
    public Button     openBrowserButton;
    public TMP_Text   statusText;

    [Header("Главное меню")]
    public MainMenu mainMenu;

    public const string KEY_LOGGED_IN  = "AuthLoggedIn";
    public const string KEY_ID         = "AuthUserId";
    public const string KEY_PHONE      = "AuthPhone";
    public const string KEY_FULL_NAME  = "AuthFullName";

    private HttpListener    _listener;
    private bool            _waiting = false;
    private System.Action   _onSuccess;

    void Start()
    {
        openBrowserButton?.onClick.AddListener(OnOpenBrowser);

        if (PlayerPrefs.GetInt(KEY_LOGGED_IN, 0) == 1)
        {
            HideAuth();
            mainMenu?.ShowMenu();
        }
    }

    // Вызывается MenuManager — открывает браузер, после успеха вызывает callback
    public void RequestAuthThenStart(System.Action onSuccess)
    {
        if (_waiting) return;
        _onSuccess = onSuccess;
        StartCoroutine(WaitForCallback());
    }

    void OnOpenBrowser()
    {
        if (_waiting) return;
        _onSuccess = null;
        StartCoroutine(WaitForCallback());
    }

    IEnumerator WaitForCallback()
    {
        _waiting = true;
        SetStatus("Ожидание входа в браузере...", false);
        if (openBrowserButton != null) openBrowserButton.interactable = false;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{callbackPort}/");
        _listener.Start();

        Application.OpenURL($"{crmUrl}?port={callbackPort}");

        string userId = null, fullName = null, phone = null, error = null;
        bool done = false;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var ctx   = _listener.GetContext();
                var query = ctx.Request.QueryString;
                userId   = query["id"];
                fullName = System.Uri.UnescapeDataString(query["name"]   ?? "");
                phone    = System.Uri.UnescapeDataString(query["phone"]  ?? "");
                error    = query["error"];

                // Закрываем вкладку браузера
                string html = @"<html><head><meta charset='utf-8'></head><body>
                    <p style='font-family:sans-serif;text-align:center;margin-top:40px;font-size:20px'>
                    ✅ Вход выполнен! Можете закрыть это окно.</p>
                    <script>setTimeout(()=>window.close(),1500)</script></body></html>";
                var buf = System.Text.Encoding.UTF8.GetBytes(html);
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.ContentType     = "text/html; charset=utf-8";
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                ctx.Response.OutputStream.Close();
            }
            catch (System.Exception e) { error = e.Message; }
            finally { done = true; }
        });

        while (!done) yield return null;

        _listener.Stop();
        _listener = null;
        _waiting  = false;
        if (openBrowserButton != null) openBrowserButton.interactable = true;

        if (!string.IsNullOrEmpty(error))
        { SetStatus($"Ошибка: {error}", true); yield break; }

        if (string.IsNullOrEmpty(userId))
        { SetStatus("Не удалось получить данные", true); yield break; }

        PlayerPrefs.SetInt(KEY_LOGGED_IN, 1);
        PlayerPrefs.SetString(KEY_ID,        userId);
        PlayerPrefs.SetString(KEY_PHONE,     phone);
        PlayerPrefs.SetString(KEY_FULL_NAME, fullName);
        PlayerPrefs.SetString("StudentName", fullName);
        PlayerPrefs.Save();

        SetStatus($"Добро пожаловать, {FirstName(fullName)}!", false);
        yield return new WaitForSeconds(1f);

        if (_onSuccess != null)
        {
            // Вызван из MenuManager — просто запускаем игру
            var cb = _onSuccess;
            _onSuccess = null;
            cb.Invoke();
        }
        else
        {
            HideAuth();
            mainMenu?.ShowMenu();
        }
    }

    void HideAuth() { if (authPanel != null) authPanel.SetActive(false); }

    void SetStatus(string msg, bool isError)
    {
        if (statusText == null) return;
        statusText.text  = msg;
        statusText.color = isError
            ? new Color(1f, 0.35f, 0.42f)
            : new Color(0.15f, 0.85f, 0.54f);
    }

    void OnDestroy() { _listener?.Stop(); }

    static string FirstName(string s) => s?.Split(' ')[0] ?? s ?? "";

    public static string CurrentUserId   => PlayerPrefs.GetString(KEY_ID,        "");
    public static string CurrentPhone    => PlayerPrefs.GetString(KEY_PHONE,     "");
    public static string CurrentFullName => PlayerPrefs.GetString(KEY_FULL_NAME, "");
    public static bool   IsLoggedIn      => PlayerPrefs.GetInt(KEY_LOGGED_IN, 0) == 1;
}
