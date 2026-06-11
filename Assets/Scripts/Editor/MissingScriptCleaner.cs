using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool: removes MonoBehaviour components whose script reference is missing
/// (the "The referenced script on this Behaviour is missing!" warning at scene load).
///
/// Run via menu Tools ▸ Cleanup ▸ Remove Missing Scripts In Scene, then save the scene.
/// It only removes components Unity itself reports as missing, so valid built-in components
/// (UIDocument, etc.) are left untouched.
/// </summary>
public static class MissingScriptCleaner
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Scene")]
    public static void RemoveInScene()
    {
        var scene = SceneManager.GetActiveScene();
        int objectsAffected = 0, removed = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            // Include inactive children - missing scripts hide on disabled objects too.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count == 0) continue;

                Debug.Log($"[MissingScriptCleaner] {count} missing script(s) on '{GetPath(t)}'", go);
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                objectsAffected++;
            }
        }

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MissingScriptCleaner] Removed {removed} missing script(s) from " +
                      $"{objectsAffected} object(s). Save the scene (Ctrl+S) to keep the change.");
        }
        else
        {
            Debug.Log("[MissingScriptCleaner] No missing scripts found in the active scene.");
        }
    }

    [MenuItem("Tools/Cleanup/Remove Missing Scripts In All Prefabs")]
    public static void RemoveInPrefabs()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int prefabsAffected = 0, removed = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var contents = PrefabUtility.LoadPrefabContents(path);
            int prefabRemoved = 0;

            foreach (var t in contents.GetComponentsInChildren<Transform>(true))
                prefabRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            if (prefabRemoved > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log($"[MissingScriptCleaner] Removed {prefabRemoved} missing script(s) from prefab '{path}'");
                removed += prefabRemoved;
                prefabsAffected++;
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(removed > 0
            ? $"[MissingScriptCleaner] Removed {removed} missing script(s) from {prefabsAffected} prefab(s)."
            : "[MissingScriptCleaner] No missing scripts found in any prefab.");
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
