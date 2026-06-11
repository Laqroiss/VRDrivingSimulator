using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Net;
using System.Threading;
using System.Collections;
using System.Text;

/// <summary>
/// Browser-based authentication.
///
/// Unity setup (called from MenuManager before the game starts):
/// 1. Add this script to AuthPanel.
/// 2. Wire up OpenBrowserButton and StatusText.
/// 3. Set CRM Url to the site address (localhost or VPS).
/// </summary>
public class AuthManager : MonoBehaviour
{
    [Header("CRM")]
    [Tooltip("Local port the browser sign-in redirects back to. Must match the CRM redirect.")]
    public int    callbackPort = 7777;

    [Header("UI")]
    public GameObject authPanel;
    public Button     openBrowserButton;
    public TMP_Text   statusText;

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
            HideAuth();
    }

    // Called by MenuManager - opens the browser, invokes the callback on success
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
        SetStatus("Waiting for browser sign-in...", false);
        if (openBrowserButton != null) openBrowserButton.interactable = false;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{callbackPort}/");
        _listener.Start();

        Application.OpenURL($"{CrmConfig.BaseUrl}/game-login?port={callbackPort}");

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

                // Close the browser tab
                string html = @"<html><head><meta charset='utf-8'></head><body>
                    <p style='font-family:sans-serif;text-align:center;margin-top:40px;font-size:20px'>
                    ✅ Signed in! You can close this window.</p>
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
        { SetStatus($"Error: {error}", true); yield break; }

        if (string.IsNullOrEmpty(userId))
        { SetStatus("Failed to get data", true); yield break; }

        PlayerPrefs.SetInt(KEY_LOGGED_IN, 1);
        PlayerPrefs.SetString(KEY_ID,        userId);
        PlayerPrefs.SetString(KEY_PHONE,     phone);
        PlayerPrefs.SetString(KEY_FULL_NAME, fullName);
        PlayerPrefs.SetString("StudentName", fullName);
        PlayerPrefs.Save();

        SetStatus($"Welcome, {FirstName(fullName)}!", false);
        yield return new WaitForSeconds(1f);

        if (_onSuccess != null)
        {
            // Called from MenuManager - just start the game
            var cb = _onSuccess;
            _onSuccess = null;
            cb.Invoke();
        }
        else
        {
            HideAuth();
        }
    }

    void HideAuth() { if (authPanel != null) authPanel.SetActive(false); }

    [System.Serializable] class LoginResp { public string id; public string phone; public string fullName; public string error; }

    /// <summary>In-game sign-in (no browser): POST to CRM /api/auth/login.</summary>
    public void LoginInline(string phone, string password, System.Action onSuccess, System.Action<string> onError)
    {
        StartCoroutine(LoginInlineRoutine(phone, password, onSuccess, onError));
    }

    IEnumerator LoginInlineRoutine(string phone, string password, System.Action onSuccess, System.Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
        { onError?.Invoke("Enter phone and password"); yield break; }

        string json = "{\"phone\":\"" + EscapeJson(phone.Trim()) + "\",\"password\":\"" + EscapeJson(password) + "\"}";
        using var req = new UnityWebRequest($"{CrmConfig.BaseUrl}/api/auth/login", "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        LoginResp resp = null;
        try { resp = JsonUtility.FromJson<LoginResp>(req.downloadHandler.text); } catch { }

        if (resp != null && !string.IsNullOrEmpty(resp.id))
        {
            PlayerPrefs.SetInt(KEY_LOGGED_IN, 1);
            PlayerPrefs.SetString(KEY_ID,        resp.id);
            PlayerPrefs.SetString(KEY_PHONE,     resp.phone);
            PlayerPrefs.SetString(KEY_FULL_NAME, resp.fullName);
            PlayerPrefs.Save();
            if (authPanel != null) authPanel.SetActive(false);
            GameLog.Info($"[AuthManager] Signed in: {resp.fullName}");
            onSuccess?.Invoke();
        }
        else
        {
            string msg = resp != null && !string.IsNullOrEmpty(resp.error)
                ? resp.error : "Sign-in failed (CRM offline?)";
            onError?.Invoke(msg);
        }
    }

    /// <summary>In-game registration: POST to CRM /api/auth/register, then sign in.</summary>
    public void RegisterInline(string fullName, string phone, string password, System.Action onSuccess, System.Action<string> onError)
    {
        StartCoroutine(RegisterInlineRoutine(fullName, phone, password, onSuccess, onError));
    }

    IEnumerator RegisterInlineRoutine(string fullName, string phone, string password, System.Action onSuccess, System.Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
        { onError?.Invoke("Fill in all fields"); yield break; }

        string json = "{\"fullName\":\"" + EscapeJson(fullName.Trim()) + "\",\"phone\":\"" + EscapeJson(phone.Trim())
                    + "\",\"password\":\"" + EscapeJson(password) + "\"}";
        using var req = new UnityWebRequest($"{CrmConfig.BaseUrl}/api/auth/register", "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        LoginResp resp = null;
        try { resp = JsonUtility.FromJson<LoginResp>(req.downloadHandler.text); } catch { }

        if (resp != null && !string.IsNullOrEmpty(resp.id))
        {
            PlayerPrefs.SetInt(KEY_LOGGED_IN, 1);
            PlayerPrefs.SetString(KEY_ID,        resp.id);
            PlayerPrefs.SetString(KEY_PHONE,     resp.phone);
            PlayerPrefs.SetString(KEY_FULL_NAME, resp.fullName);
            PlayerPrefs.Save();
            if (authPanel != null) authPanel.SetActive(false);
            GameLog.Info($"[AuthManager] Registered: {resp.fullName}");
            onSuccess?.Invoke();
        }
        else
        {
            string msg = resp != null && !string.IsNullOrEmpty(resp.error)
                ? resp.error : "Registration failed (CRM offline?)";
            onError?.Invoke(msg);
        }
    }

    static string EscapeJson(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    /// <summary>Log out: clear the saved session and show the sign-in screen.</summary>
    public void Logout()
    {
        PlayerPrefs.SetInt(KEY_LOGGED_IN, 0);
        PlayerPrefs.DeleteKey(KEY_ID);
        PlayerPrefs.DeleteKey(KEY_PHONE);
        PlayerPrefs.DeleteKey(KEY_FULL_NAME);
        PlayerPrefs.Save();
        if (authPanel != null) authPanel.SetActive(true);
        GameLog.Info("[AuthManager] Logged out");
    }

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
