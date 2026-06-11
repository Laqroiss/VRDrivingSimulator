using UnityEngine;

/// <summary>
/// Admin testing aid: an on-screen panel to skip ahead through the exam exercises.
///
/// Toggle it with F8. Available ONLY when the signed-in account is an admin
/// (User.isAdmin in the CRM, carried through the login response into AuthManager.IsAdmin)
/// and an exam is in progress. "Skip to Ex.N" marks every earlier exercise as completed,
/// unlocking N so you can drive straight to it during testing instead of properly passing
/// the ones before it.
///
/// Bootstraps itself on scene load - no scene wiring needed. It does nothing for non-admins.
/// </summary>
public class AdminDebugPanel : MonoBehaviour
{
    const KeyCode ToggleKey = KeyCode.F8;

    static AdminDebugPanel _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("AdminDebugPanel");
        _instance = go.AddComponent<AdminDebugPanel>();
        DontDestroyOnLoad(go);
    }

    bool _visible = false;
    Vector2 _scroll;

    void Update()
    {
        // Only an admin can summon the panel.
        if (!AuthManager.IsLoggedIn || !AuthManager.IsAdmin) return;
        if (LegacyInput.GetKeyDown(ToggleKey)) _visible = !_visible;
    }

    void OnGUI()
    {
        // Only for an admin account, only during an exam.
        if (!AuthManager.IsLoggedIn || !AuthManager.IsAdmin) return;
        var em = ExamManager.Instance;
        if (em == null || em.State != ExamManager.ExamState.InProgress) return;

        if (!_visible)
        {
            // Hint so the admin knows how to open the panel.
            GUI.Label(new Rect(Screen.width - 160f, 8f, 152f, 22f),
                      $"[{ToggleKey}] admin panel", RichLabel());
            return;
        }

        const float w = 300f;
        float h = 36f + 10f * 26f + 30f;
        GUILayout.BeginArea(new Rect(Screen.width - w - 12f, 12f, w, h), GUI.skin.box);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>ADMIN — skip exercises</b>  [{ToggleKey}]", RichLabel());
        if (GUILayout.Button("X", GUILayout.Width(26f)))
            _visible = false;
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll);
        for (int n = 1; n <= 10; n++)
        {
            GUILayout.BeginHorizontal();
            var st = em.ExerciseStatuses[n - 1];
            GUILayout.Label($"Ex.{n}: {st}", GUILayout.Width(180f));
            GUI.enabled = st != ExamManager.ExerciseStatus.Completed;
            if (GUILayout.Button("Skip to", GUILayout.Width(80f)))
                SkipTo(n);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    // Mark every exercise before 'target' as completed, so 'target' unlocks and the earlier ones
    // are not flagged as skipped at the finish.
    void SkipTo(int target)
    {
        var em = ExamManager.Instance;
        if (em == null || em.State != ExamManager.ExamState.InProgress) return;

        for (int n = 1; n < target; n++)
            if (em.ExerciseStatuses[n - 1] != ExamManager.ExerciseStatus.Completed)
                em.CompleteExercise(n);

        GameLog.Info($"[AdminDebugPanel] Skipped to Ex.{target} (marked Ex.1..{target - 1} completed)");
    }

    static GUIStyle _rich;
    static GUIStyle RichLabel()
    {
        if (_rich == null)
            _rich = new GUIStyle(GUI.skin.label) { richText = true };
        return _rich;
    }
}
