using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
///      RouteRibbon.
///        : A —  , D — .
/// RouteRibbon      -.
/// </summary>
public class RoutePlacer : MonoBehaviour
{
    [Header("  ( )")]
    public List<Transform> points = new List<Transform>();

    [Tooltip(",      ()")]
    public RouteRibbon ribbon;

    // Auto-find child points Point1, Point2...
    public void AutoFindChildPoints()
    {
        List<Transform> found = new List<Transform>();
        foreach (Transform child in transform)
            if (child.name.StartsWith("Point") || child.name.StartsWith("point"))
                found.Add(child);

        found.Sort((a, b) => ExtractNumber(a.name).CompareTo(ExtractNumber(b.name)));
        points = found;
        Debug.Log($"RoutePlacer:  {points.Count} ");
    }

    public void AddPointAtPosition(Vector3 worldPos)
    {
        int index = points.Count + 1;
        GameObject pt = new GameObject($"Point{index}");
        pt.transform.position = worldPos;
        pt.transform.parent   = transform;
        points.Add(pt.transform);
    }

    public void RemoveLastPoint()
    {
        if (points.Count == 0) return;
        Transform last = points[points.Count - 1];
        points.RemoveAt(points.Count - 1);
        if (last != null)
#if UNITY_EDITOR
            DestroyImmediate(last.gameObject);
#else
            Destroy(last.gameObject);
#endif
        Debug.Log($"RoutePlacer:   ,  {points.Count}");
    }

    int ExtractNumber(string name)
    {
        string digits = "";
        foreach (char c in name)
            if (char.IsDigit(c)) digits += c;
        return int.TryParse(digits, out int result) ? result : 0;
    }

    void OnDrawGizmos()
    {
        if (points == null || points.Count < 1) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null) continue;
            Gizmos.DrawSphere(points[i].position, 0.4f);
            if (i < points.Count - 1 && points[i + 1] != null)
                Gizmos.DrawLine(points[i].position, points[i + 1].position);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RoutePlacer))]
public class RoutePlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        RoutePlacer placer = (RoutePlacer)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"  : {placer.points.Count}", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // /    (  )
        GUI.backgroundColor = RoutePlacerSceneInput.Placing
            ? new Color(1f, 0.6f, 0.1f) : new Color(0.3f, 0.6f, 1f);
        if (GUILayout.Button(RoutePlacerSceneInput.Placing
                ? "�     (A — ,  D — )"
                : "��     (A — ,  D — )",
                GUILayout.Height(36)))
        {
            RoutePlacerSceneInput.Placing = !RoutePlacerSceneInput.Placing;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(4);
        if (GUILayout.Button("�    ( Play)", GUILayout.Height(24)))
        {
            var rib = placer.ribbon != null ? placer.ribbon : FindAnyObjectByType<RouteRibbon>();
            if (rib != null) { rib.BuildSpline(); Debug.Log("RoutePlacer:  "); }
            else Debug.LogWarning("RoutePlacer: RouteRibbon  ");
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "1.   .\n" +
            "2.      Scene-   A —    .\n" +
            "   D —  .\n" +
            "  /   —    .\n" +
            "  =  .",
            MessageType.Info);
    }
}

/// <summary>
///   Scene-      .
///    ,   (   OnSceneGUI -),
///      : A —    , D — .
/// </summary>
[InitializeOnLoad]
static class RoutePlacerSceneInput
{
    public static bool Placing;

    const string MenuPath = "Tools/Route Placer/  (A=, D=)";

    static RoutePlacerSceneInput()
    {
        SceneView.duringSceneGui += OnScene;
    }

    [MenuItem(MenuPath)]
    static void ToggleMenu() { Placing = !Placing; SceneView.RepaintAll(); }

    [MenuItem(MenuPath, true)]
    static bool ToggleMenuValidate() { Menu.SetChecked(MenuPath, Placing); return true; }

    static void OnScene(SceneView sv)
    {
        if (!Placing) return;

        var placer = Object.FindAnyObjectByType<RoutePlacer>();

        //    
        if (placer != null && placer.points != null)
        {
            for (int i = 0; i < placer.points.Count; i++)
            {
                if (placer.points[i] == null) continue;
                Handles.color = Color.cyan;
                Handles.SphereHandleCap(0, placer.points[i].position, Quaternion.identity, 0.6f, EventType.Repaint);
                Handles.Label(placer.points[i].position + Vector3.up * 0.8f, (i + 1).ToString());
                if (i < placer.points.Count - 1 && placer.points[i + 1] != null)
                    Handles.DrawLine(placer.points[i].position, placer.points[i + 1].position, 2f);
            }
        }

        Event e = Event.current;
        Vector3 pos = PointerWorldPos(e.mousePosition);

        //   
        Handles.color = Color.yellow;
        float s = HandleUtility.GetHandleSize(pos) * 0.18f;
        Handles.DrawLine(pos - Vector3.right * s, pos + Vector3.right * s, 2f);
        Handles.DrawLine(pos - Vector3.forward * s, pos + Vector3.forward * s, 2f);
        if (placer != null && placer.points.Count > 0 && placer.points[placer.points.Count - 1] != null)
        {
            Handles.color = new Color(1f, 1f, 0f, 0.5f);
            Handles.DrawDottedLine(placer.points[placer.points.Count - 1].position, pos, 4f);
        }

        //   
        Handles.BeginGUI();
        var st = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.yellow } };
        GUI.Label(new Rect(8, 8, 600, 22),
            $"RoutePlacer: A — , D —   |  : {(placer != null ? placer.points.Count : 0)}  (:     Tools/Route Placer)", st);
        Handles.EndGUI();

        if (e.type == EventType.KeyDown && !e.alt)
        {
            if (e.keyCode == KeyCode.A && placer != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Add Route Point");
                placer.AddPointAtPosition(pos);
                EditorUtility.SetDirty(placer);
                e.Use();
            }
            else if (e.keyCode == KeyCode.D && placer != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Remove Route Point");
                placer.RemoveLastPoint();
                EditorUtility.SetDirty(placer);
                e.Use();
            }
        }

        sv.Repaint(); //   
    }

    static Vector3 PointerWorldPos(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.point;
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        ground.Raycast(ray, out float dist);
        return ray.GetPoint(dist);
    }
}
#endif
