using UnityEngine;

/// <summary>
/// Single source of truth for the CRM server address.
///
/// Reads <c>StreamingAssets/crm.json</c> once on first access, so an operator can repoint a
/// shipped build at a different server (localhost during development, a VPS in production) by
/// editing one text file next to the .exe — no rebuild required. Falls back to localhost if the
/// file is missing or malformed.
///
/// Synchronous file read is used intentionally: this is a Windows PC VR product where
/// streamingAssetsPath is a real directory. (On Android, streamingAssets lives inside the APK and
/// would need UnityWebRequest — out of scope for this project.)
///
/// Only the base URL is configurable here. Local listener ports (auth callback, replay) stay as
/// per-component inspector fields because they are loopback concerns and must stay in sync with the
/// CRM-side redirect.
/// </summary>
public static class CrmConfig
{
    const string DefaultBaseUrl = "http://localhost:3000";

    [System.Serializable] class Data { public string baseUrl; }

    static string _baseUrl;

    /// <summary>CRM base URL with no trailing slash, e.g. "http://localhost:3000".</summary>
    public static string BaseUrl
    {
        get
        {
            if (_baseUrl == null) Load();
            return _baseUrl;
        }
    }

    static void Load()
    {
        _baseUrl = DefaultBaseUrl;
        try
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "crm.json");
            if (System.IO.File.Exists(path))
            {
                var d = JsonUtility.FromJson<Data>(System.IO.File.ReadAllText(path));
                if (d != null && !string.IsNullOrWhiteSpace(d.baseUrl))
                    _baseUrl = d.baseUrl.Trim().TrimEnd('/');
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CrmConfig] crm.json unreadable, using {DefaultBaseUrl}: {e.Message}");
        }
    }
}
