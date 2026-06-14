#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public static class RoadMarkingTool
{
    const string SolidPrefabPath  = "Assets/Prefabs/RoadMarkings/Line_Solid.prefab";
    const string DashedPrefabPath = "Assets/Prefabs/RoadMarkings/Line_Dashed.prefab";

    [MenuItem("Road Markings/Convert Selected → Solid Line")]
    static void MenuSolid() => ConvertInstantiate(SolidPrefabPath, 1f, "Solid");

    [MenuItem("Road Markings/Convert Selected → Dashed Line")]
    static void MenuDashed() => ConvertInstantiate(DashedPrefabPath, 2f, "Dashed");

    [MenuItem("Road Markings/Convert Selected → Solid Line", true)]
    [MenuItem("Road Markings/Convert Selected → Dashed Line", true)]
    static bool ValidateSelection() =>
        Selection.activeGameObject != null &&
        Selection.activeGameObject.GetComponent<SplineContainer>() != null;

    static void ConvertInstantiate(string prefabPath, float spacing, string label)
    {
        var go = Selection.activeGameObject;
        if (go == null) return;

        Undo.IncrementCurrentGroup();

        var oldExtrude = go.GetComponent<SplineExtrude>();
        if (oldExtrude != null) Undo.DestroyObjectImmediate(oldExtrude);
        var oldMF = go.GetComponent<MeshFilter>();
        if (oldMF != null) Undo.DestroyObjectImmediate(oldMF);
        var oldMR = go.GetComponent<MeshRenderer>();
        if (oldMR != null) Undo.DestroyObjectImmediate(oldMR);

        var inst = go.GetComponent<SplineInstantiate>();
        if (inst == null)
            inst = Undo.AddComponent<SplineInstantiate>(go);

        Undo.RecordObject(inst, "Configure SplineInstantiate");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            inst.itemsToInstantiate = new[]
            {
                new SplineInstantiate.InstantiableItem { Prefab = prefab, Probability = 1f }
            };
        }

        inst.InstantiateMethod = SplineInstantiate.Method.SpacingDistance;
        inst.MinSpacing = spacing;
        inst.MaxSpacing = spacing;
        inst.ForwardAxis = SplineInstantiate.AlignAxis.ZAxis;
        inst.UpAxis = SplineInstantiate.AlignAxis.YAxis;

        EnsureYPosition(go);
        Undo.SetCurrentGroupName($"Convert to {label} Line");
        Debug.Log($"[RoadMarking] '{go.name}' → {label}");
    }

    static void EnsureYPosition(GameObject go)
    {
        if (go.transform.position.y < 0.02f)
        {
            Undo.RecordObject(go.transform, "Set Y");
            var p = go.transform.position;
            go.transform.position = new Vector3(p.x, 0.02f, p.z);
        }
    }
}
#endif
