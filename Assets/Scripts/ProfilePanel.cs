using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

/// <summary>
/// Student cabinet (UI Toolkit): a short summary + a scrollable list of attempts
/// from CRM. The ▶ button launches a 3D replay (via OnReplay -> ReplayCRMSync).
/// Attach to the same object as UIDocument/MenuUIToolkit.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ProfilePanel : MonoBehaviour
{
    [Header("CRM")]
    public string crmUrl = "http://localhost:3000";

    /// <summary>attemptId -> launch a replay. MenuManager wires this to ReplayCRMSync.</summary>
    public event Action<string> OnReplay;
    /// <summary>attemptId -> continue an unfinished (abandoned) attempt. MenuManager wires this to ExamResume.</summary>
    public event Action<string> OnResumeAttempt;
    /// <summary>The "Log out" button was clicked.</summary>
    public event Action OnLogout;

    [Serializable] class Attempt
    {
        public string _id;
        public string timestamp;
        public bool   passed;
        public bool   completed;
        public bool   hasReplay;
        public bool   hasTrack;
        public int    totalPenaltyPoints;
        public float  examDuration;
    }
    [Serializable] class AttemptList { public Attempt[] items; }

    private bool _init;
    private VisualElement _overlay;
    private Label         _name, _summary;
    private ScrollView    _list;
    private Button        _close, _logout;

    void OnEnable() => EnsureInit();
    void Start()    => EnsureInit();

    void EnsureInit()
    {
        if (_init) return;
        var doc = GetComponent<UIDocument>();
        var r = doc != null ? doc.rootVisualElement : null;
        if (r == null) return;

        _overlay = r.Q("profile-overlay");
        _name    = r.Q<Label>("profile-name");
        _summary = r.Q<Label>("profile-summary");
        _list    = r.Q<ScrollView>("attempts-list");
        _close   = r.Q<Button>("profile-close");
        _logout  = r.Q<Button>("profile-logout");
        if (_overlay == null) return; // this layout has no profile

        if (_close  != null) _close.clicked  += Hide;
        if (_logout != null) _logout.clicked += () => { OnLogout?.Invoke(); Hide(); };
        _init = true;
    }

    // ── API ───────────────────────────────────────────────────────────────────
    public void Toggle()
    {
        EnsureInit();
        if (_overlay == null) return;
        if (_overlay.ClassListContains("hidden")) Show();
        else Hide();
    }

    public void Show()
    {
        EnsureInit();
        if (_overlay == null) return;
        _overlay.RemoveFromClassList("hidden");
        _overlay.BringToFront();
        Refresh();
    }

    public void Hide()
    {
        EnsureInit();
        if (_overlay != null) _overlay.AddToClassList("hidden");
    }

    public void Refresh()
    {
        string name = PlayerPrefs.GetString(AuthManager.KEY_FULL_NAME, "");
        if (_name != null) _name.text = string.IsNullOrEmpty(name) ? "PROFILE" : name;
        if (_summary != null) _summary.text = "Loading…";
        StopAllCoroutines();
        StartCoroutine(FetchAttempts(name));
    }

    // ── Loading from CRM ────────────────────────────────────────────────────────
    IEnumerator FetchAttempts(string studentName)
    {
        if (_list != null) _list.Clear();

        // Query by studentId (account binding) - matches the website and avoids pulling
        // other/orphaned records without an id. Name is just a fallback when there's no id.
        string studentId = PlayerPrefs.GetString(AuthManager.KEY_ID, "");
        string url = !string.IsNullOrEmpty(studentId)
            ? $"{crmUrl}/api/attempts?studentId={UnityWebRequest.EscapeURL(studentId)}"
            : $"{crmUrl}/api/attempts?student={UnityWebRequest.EscapeURL(studentName)}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            if (_summary != null) _summary.text = "CRM offline";
            AddEmpty("Couldn't load attempts (CRM server offline)");
            yield break;
        }

        AttemptList data = null;
        try { data = JsonUtility.FromJson<AttemptList>("{\"items\":" + req.downloadHandler.text + "}"); }
        catch (Exception e) { Debug.LogWarning($"[ProfilePanel] Response parse: {e.Message}"); }

        var items = data?.items;
        if (items == null || items.Length == 0)
        {
            if (_summary != null) _summary.text = "No attempts yet";
            AddEmpty("Your exam attempts will appear here");
            yield break;
        }

        // Summary
        int total = items.Length, passed = 0, best = int.MaxValue;
        foreach (var a in items)
        {
            if (a.completed && a.passed) passed++;        // "passed" only counts for attempts that reached the finish
            if (a.completed && a.totalPenaltyPoints < best) best = a.totalPenaltyPoints;
        }
        if (_summary != null)
            _summary.text = best == int.MaxValue
                ? $"Attempts: {total}   •   Passed: {passed}"
                : $"Attempts: {total}   •   Passed: {passed}   •   Best: {best} pts";

        foreach (var a in items)
            AddRow(a);
    }

    // ── Rows ──────────────────────────────────────────────────────────────────────
    void AddRow(Attempt a)
    {
        if (_list == null) return;

        var row = new VisualElement(); row.AddToClassList("attempt-row");

        var info = new VisualElement(); info.AddToClassList("attempt-info");
        var date = new Label(FormatDate(a.timestamp)); date.AddToClassList("attempt-date");
        var res  = new Label(a.completed ? (a.passed ? "PASSED" : "FAILED") : "INCOMPLETE");
        res.AddToClassList("attempt-result");
        res.AddToClassList(a.passed && a.completed ? "attempt-pass" : "attempt-fail");
        info.Add(date); info.Add(res);

        var score = new Label($"{a.totalPenaltyPoints} pts"); score.AddToClassList("attempt-score");

        var id  = a._id;
        row.Add(info); row.Add(score);

        // Abandoned attempt (didn't reach the finish) with a recorded track - offer "CONTINUE" from the save point
        if (!a.completed && a.hasTrack)
        {
            var resume = new Button(() => OnResumeAttempt?.Invoke(id)) { text = "CONTINUE" };
            resume.AddToClassList("attempt-resume");
            resume.tooltip = "Continue this exam from the last saved point";
            row.Add(resume);
        }

        var btn = new Button(() => OnReplay?.Invoke(id)) { text = "REPLAY" };
        btn.AddToClassList("attempt-replay");
        if (!a.hasReplay)                       // no replay recorded — nothing to play
        {
            btn.SetEnabled(false);
            btn.tooltip = "No replay for this attempt";
        }

        row.Add(btn);
        _list.Add(row);
    }

    void AddEmpty(string text)
    {
        if (_list == null) return;
        var l = new Label(text); l.AddToClassList("attempt-empty");
        _list.Add(l);
    }

    static string FormatDate(string iso)
    {
        if (DateTime.TryParse(iso, null, DateTimeStyles.AdjustToUniversal, out var dt))
            return dt.ToLocalTime().ToString("dd.MM.yyyy  HH:mm", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(iso) ? "—" : iso;
    }
}
