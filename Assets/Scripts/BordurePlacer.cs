using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BordurePlacer : MonoBehaviour
{
    public enum PlacementMode { Line, Arc }

    [Header("Режим расстановки")]
    public PlacementMode mode = PlacementMode.Line;

    [Header("Режим Line — точки вручную")]
    public List<Transform> points = new List<Transform>();

    [Header("Режим Arc — автоматическая дуга")]
    public Transform arcCenter;
    public float arcRadius = 5f;
    public float arcStartAngle = 0f;
    public float arcEndAngle = 90f;
    public int arcSegments = 8;

    [Header("Параметры бордюра")]
    public float bordureLength = 2f;
    public float bordureHeight = 0.15f;
    public float bordureWidth = 0.3f;
    public float yOffset = 0f;
    public Material bordureMaterial;

    [HideInInspector]
    public List<GameObject> generatedBordures = new List<GameObject>();

    // Авто-поиск дочерних точек
    public void AutoFindChildPoints()
    {
        points.Clear();
        List<Transform> found = new List<Transform>();
        foreach (Transform child in transform)
            if (child.name.StartsWith("Point") || child.name.StartsWith("point"))
                found.Add(child);

        found.Sort((a, b) => ExtractNumber(a.name).CompareTo(ExtractNumber(b.name)));
        points = found;
        Debug.Log($"BordurePlacer: найдено {points.Count} точек");
    }

    // Добавить точку в нужную позицию
    public void AddPointAtPosition(Vector3 worldPos)
    {
        int index = points.Count + 1;
        GameObject pt = new GameObject($"Point{index}");
        pt.transform.position = worldPos;
        pt.transform.parent = transform;
        points.Add(pt.transform);
    }

    // Удалить последнюю точку
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
        Debug.Log($"BordurePlacer: удалена последняя точка, осталось {points.Count}");
    }

    int ExtractNumber(string name)
    {
        string digits = "";
        foreach (char c in name)
            if (char.IsDigit(c)) digits += c;
        int result;
        return int.TryParse(digits, out result) ? result : 0;
    }

    public void Generate()
    {
        Clear();
        List<Vector3> positions = new List<Vector3>();

        if (mode == PlacementMode.Line)
        {
            foreach (var p in points)
                if (p != null) positions.Add(p.position);
            if (positions.Count < 2)
            {
                Debug.LogWarning("BordurePlacer: нужно минимум 2 точки");
                return;
            }
        }
        else
        {
            if (arcCenter == null) { Debug.LogWarning("Укажи Arc Center"); return; }
            for (int i = 0; i <= arcSegments; i++)
            {
                float t = (float)i / arcSegments;
                float angle = Mathf.Lerp(arcStartAngle, arcEndAngle, t) * Mathf.Deg2Rad;
                positions.Add(arcCenter.position + new Vector3(
                    Mathf.Cos(angle) * arcRadius, 0f,
                    Mathf.Sin(angle) * arcRadius));
            }
        }

        PlaceBorduresAlongPath(positions);
        Debug.Log($"BordurePlacer: создано {generatedBordures.Count} бордюров");
    }

    void PlaceBorduresAlongPath(List<Vector3> pathPoints)
    {
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Vector3 from = pathPoints[i];
            Vector3 to = pathPoints[i + 1];
            Vector3 dir = (to - from).normalized;
            float segLen = Vector3.Distance(from, to);
            int count = Mathf.Max(1, Mathf.RoundToInt(segLen / bordureLength));
            float actualLen = segLen / count;

            for (int j = 0; j < count; j++)
            {
                float t = (j + 0.5f) / count;
                Vector3 pos = Vector3.Lerp(from, to, t);
                pos.y += bordureHeight / 2f + yOffset;

                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Bordure_{generatedBordures.Count:D3}";
                b.transform.position = pos;
                b.transform.rotation = Quaternion.LookRotation(dir);
                b.transform.localScale = new Vector3(bordureWidth, bordureHeight, actualLen);
                b.transform.parent = transform;

                if (bordureMaterial != null)
                    b.GetComponent<Renderer>().material = bordureMaterial;
                else
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.75f, 0.75f, 0.75f);
                    b.GetComponent<Renderer>().material = mat;
                }
                generatedBordures.Add(b);
            }
        }
    }

    public void Clear()
    {
        foreach (var b in generatedBordures)
            if (b != null)
#if UNITY_EDITOR
                DestroyImmediate(b);
#else
                Destroy(b);
#endif
        generatedBordures.Clear();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BordurePlacer))]
public class BordurePlacerEditor : Editor
{
    private bool _placingMode = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        BordurePlacer placer = (BordurePlacer)target;

        EditorGUILayout.Space(8);

        // ——— Режим расстановки точек ———
        GUI.backgroundColor = _placingMode
            ? new Color(1f, 0.6f, 0.1f)   // оранжевый — режим активен
            : new Color(0.3f, 0.6f, 1f);   // синий — режим выключен

        string btnLabel = _placingMode
            ? "🟠 Режим точек ВКЛЮЧЁН  (Q — добавить,  Z — отменить,  нажми снова чтобы выйти)"
            : "✏️  Включить режим расстановки точек";

        if (GUILayout.Button(btnLabel, GUILayout.Height(36)))
        {
            _placingMode = !_placingMode;
            if (_placingMode)
                Debug.Log("BordurePlacer: режим точек включён. Q = добавить точку под курсором, Z = удалить последнюю.");
            else
                Debug.Log("BordurePlacer: режим точек выключен.");
        }

        EditorGUILayout.LabelField($"Точек в списке: {placer.points.Count}", EditorStyles.miniLabel);

        EditorGUILayout.Space(4);

        // ——— Авто-поиск ———
        GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
        if (GUILayout.Button("Find Child Points", GUILayout.Height(28)))
        {
            Undo.RecordObject(placer, "Auto-find Points");
            placer.AutoFindChildPoints();
            EditorUtility.SetDirty(placer);
        }

        EditorGUILayout.Space(4);

        // ——— Генерация ———
        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button("▶  Generate Bordures", GUILayout.Height(36)))
        {
            Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Generate Bordures");
            placer.Generate();
        }

        // ——— Очистка ———
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("✕  Clear All", GUILayout.Height(28)))
        {
            Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Clear Bordures");
            placer.Clear();
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        if (_placingMode)
            EditorGUILayout.HelpBox(
                "Q — добавить точку под курсором мыши\n" +
                "Z — удалить последнюю точку\n" +
                "Кликай в Scene вью по снимку!",
                MessageType.Warning);
        else
            EditorGUILayout.HelpBox(
                "Включи режим точек и кликай Q по снимку.\n" +
                "Arc: укажи Center, Radius и углы Start/End для дуг.",
                MessageType.Info);
    }

    void OnSceneGUI()
    {
        BordurePlacer placer = (BordurePlacer)target;

        // Рисуем линию между точками
        if (placer.points != null && placer.points.Count > 1)
        {
            Handles.color = Color.cyan;
            for (int i = 0; i < placer.points.Count - 1; i++)
                if (placer.points[i] != null && placer.points[i + 1] != null)
                    Handles.DrawLine(placer.points[i].position, placer.points[i + 1].position, 2f);
        }

        // Рисуем дугу
        if (placer.mode == BordurePlacer.PlacementMode.Arc && placer.arcCenter != null)
        {
            Handles.color = new Color(0f, 1f, 1f, 0.5f);
            Vector3 from = placer.arcCenter.position + new Vector3(
                Mathf.Cos(placer.arcStartAngle * Mathf.Deg2Rad) * placer.arcRadius, 0f,
                Mathf.Sin(placer.arcStartAngle * Mathf.Deg2Rad) * placer.arcRadius);
            Handles.DrawWireArc(placer.arcCenter.position, Vector3.up,
                from - placer.arcCenter.position,
                placer.arcEndAngle - placer.arcStartAngle,
                placer.arcRadius, 2f);
        }

        if (!_placingMode) return;

        // Перехватываем события клавиатуры в Scene вью
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            // Q — добавить точку
            if (e.keyCode == KeyCode.Q)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                RaycastHit hit;
                Vector3 pos;

                if (Physics.Raycast(ray, out hit))
                    pos = hit.point;
                else
                {
                    // Если нет коллайдера — ставим на плоскость Y=0
                    Plane ground = new Plane(Vector3.up, Vector3.zero);
                    float dist;
                    ground.Raycast(ray, out dist);
                    pos = ray.GetPoint(dist);
                }

                Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Add Point");
                placer.AddPointAtPosition(pos);
                EditorUtility.SetDirty(placer);
                e.Use(); // поглощаем событие
            }

            // Z — удалить последнюю точку
            if (e.keyCode == KeyCode.Z && !e.control)
            {
                Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Remove Last Point");
                placer.RemoveLastPoint();
                EditorUtility.SetDirty(placer);
                e.Use();
            }
        }

        // Показываем превью следующей точки под курсором
        Ray previewRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        RaycastHit previewHit;
        Vector3 previewPos;
        if (Physics.Raycast(previewRay, out previewHit))
            previewPos = previewHit.point;
        else
        {
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            float dist;
            ground.Raycast(previewRay, out dist);
            previewPos = previewRay.GetPoint(dist);
        }

        // Рисуем жёлтый крестик на месте следующей точки
        Handles.color = Color.yellow;
        float size = HandleUtility.GetHandleSize(previewPos) * 0.15f;
        Handles.DrawLine(previewPos - Vector3.right * size, previewPos + Vector3.right * size, 2f);
        Handles.DrawLine(previewPos - Vector3.forward * size, previewPos + Vector3.forward * size, 2f);

        // Линия от последней точки к курсору
        if (placer.points.Count > 0 && placer.points[placer.points.Count - 1] != null)
        {
            Handles.color = new Color(1f, 1f, 0f, 0.5f);
            Handles.DrawDottedLine(placer.points[placer.points.Count - 1].position, previewPos, 4f);
        }

        HandleUtility.Repaint();
    }
}
#endif
