using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Control-line placer.
/// Q - add a point, Z - remove the last, Generate - build the lines.
/// Each segment gets a BoxCollider (Is Trigger) + ControlLineTrigger.
/// </summary>
public class ControlLinePlacer : MonoBehaviour
{
    [Header("Path points (Q - add, Z - remove)")]
    public List<Transform> points = new List<Transform>();

    [Header("Line parameters")]
    public float lineSegmentLength = 1f;    // max length of one segment
    public float lineWidth         = 0.15f; // strip width
    public float lineHeight        = 0.04f; // height (visual = collider)
    public float yOffset           = 0.01f; // lift slightly above the ground

    [Header("Penalty")]
    [Tooltip("Exercise number: 2 = intersections, 5 = reverse parking, 6 = parallel")]
    public int exerciseNum = 5;

    [HideInInspector]
    public List<GameObject> generated = new List<GameObject>();

    public void AddPointAtPosition(Vector3 worldPos)
    {
        var pt = new GameObject($"Point{points.Count + 1}");
        pt.transform.position = worldPos;
        pt.transform.parent   = transform;
        points.Add(pt.transform);
    }

    public void RemoveLastPoint()
    {
        if (points.Count == 0) return;
        var last = points[points.Count - 1];
        points.RemoveAt(points.Count - 1);
        if (last != null)
#if UNITY_EDITOR
            DestroyImmediate(last.gameObject);
#else
            Destroy(last.gameObject);
#endif
    }

    public void Generate()
    {
        Clear();

        if (points.Count < 2)
        {
            Debug.LogWarning("ControlLinePlacer: at least 2 points are required");
            return;
        }

        var mat      = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color    = new Color(1f, 0.85f, 0f);
        mat.enableInstancing = true;

        for (int i = 0; i < points.Count - 1; i++)
        {
            if (points[i] == null || points[i + 1] == null) continue;

            Vector3 from   = points[i].position;
            Vector3 to     = points[i + 1].position;
            Vector3 dir    = (to - from).normalized;
            float   segLen = Vector3.Distance(from, to);
            int     count  = Mathf.Max(1, Mathf.RoundToInt(segLen / lineSegmentLength));
            float   actLen = segLen / count;

            for (int j = 0; j < count; j++)
            {
                float   t   = (j + 0.5f) / count;
                Vector3 pos = Vector3.Lerp(from, to, t);
                pos.y      += lineHeight * 0.5f + yOffset;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name                 = $"ControlLine_{generated.Count:D3}";
                go.transform.parent     = transform;
                go.transform.position   = pos;
                go.transform.rotation   = Quaternion.LookRotation(dir);
                go.transform.localScale = new Vector3(lineWidth, lineHeight, actLen);
                go.GetComponent<Renderer>().sharedMaterial = mat;

                // Collider matches the visual line - used for wheel checks
                var col   = go.GetComponent<BoxCollider>();
                col.size  = Vector3.one;
                col.center = Vector3.zero;

                var trig          = go.AddComponent<ControlLineTrigger>();
                trig.exerciseNum  = exerciseNum;
                trig.cooldown     = 5f;

                generated.Add(go);
            }
        }

        Debug.Log($"ControlLinePlacer: created {generated.Count} segments (Ex.{exerciseNum})");
    }

    public void Clear()
    {
        foreach (var g in generated)
            if (g != null)
#if UNITY_EDITOR
                DestroyImmediate(g);
#else
                Destroy(g);
#endif
        generated.Clear();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ControlLinePlacer))]
public class ControlLinePlacerEditor : Editor
{
    private bool _placing = false;

    public override void OnInspectorGUI()
    {
        var p = (ControlLinePlacer)target;
        serializedObject.Update();

        // Point-mode button
        GUI.backgroundColor = _placing ? new Color(1f, 0.85f, 0f) : new Color(0.3f, 0.6f, 1f);
        if (GUILayout.Button(_placing
                ? "🟡 Point mode ON  -  Q add  /  Z remove  /  click again to exit"
                : "✏️  Enable point placement mode",
            GUILayout.Height(38)))
        {
            _placing = !_placing;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField($"Points: {p.points.Count}", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // Remaining fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("points"), true);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Line parameters", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lineSegmentLength"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lineWidth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lineHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("yOffset"));
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Penalty", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("exerciseNum"));

        EditorGUILayout.Space(6);

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button("▶  Generate Control Lines", GUILayout.Height(34)))
        {
            Undo.RegisterFullObjectHierarchyUndo(p.gameObject, "Generate");
            p.Generate();
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("✕  Clear All", GUILayout.Height(26)))
        {
            Undo.RegisterFullObjectHierarchyUndo(p.gameObject, "Clear");
            p.Clear();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            _placing
                ? "Q - place a point under the mouse cursor in the Scene\nZ - remove the last point"
                : "Click the button above and place points in the Scene with Q.",
            _placing ? MessageType.Warning : MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    void OnSceneGUI()
    {
        var p = (ControlLinePlacer)target;

        // Draw the path
        if (p.points != null && p.points.Count > 1)
        {
            Handles.color = new Color(1f, 0.85f, 0f, 0.9f);
            for (int i = 0; i < p.points.Count - 1; i++)
                if (p.points[i] != null && p.points[i + 1] != null)
                    Handles.DrawLine(p.points[i].position, p.points[i + 1].position, 3f);
        }

        // Draw point numbers
        if (p.points != null)
        {
            Handles.color = Color.yellow;
            for (int i = 0; i < p.points.Count; i++)
                if (p.points[i] != null)
                    Handles.Label(p.points[i].position + Vector3.up * 0.3f, $"{i + 1}");
        }

        if (!_placing) return;

        // Get the position under the cursor
        Event e    = Event.current;
        Ray   ray  = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Vector3 pos;
        if (Physics.Raycast(ray, out RaycastHit hit))
            pos = hit.point;
        else
        {
            new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d);
            pos = ray.GetPoint(d);
        }

        // Cross at the next point's location
        Handles.color = Color.yellow;
        float sz = HandleUtility.GetHandleSize(pos) * 0.12f;
        Handles.DrawLine(pos - Vector3.right   * sz, pos + Vector3.right   * sz, 2f);
        Handles.DrawLine(pos - Vector3.forward * sz, pos + Vector3.forward * sz, 2f);

        // Dotted line from the last point to the cursor
        if (p.points.Count > 0 && p.points[p.points.Count - 1] != null)
        {
            Handles.color = new Color(1f, 1f, 0f, 0.4f);
            Handles.DrawDottedLine(p.points[p.points.Count - 1].position, pos, 4f);
        }

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Q)
            {
                Undo.RegisterFullObjectHierarchyUndo(p.gameObject, "Add Point");
                p.AddPointAtPosition(pos);
                EditorUtility.SetDirty(p);
                e.Use();
            }
            if (e.keyCode == KeyCode.Z && !e.control)
            {
                Undo.RegisterFullObjectHierarchyUndo(p.gameObject, "Remove Point");
                p.RemoveLastPoint();
                EditorUtility.SetDirty(p);
                e.Use();
            }
        }

        HandleUtility.Repaint();
    }
}
#endif
