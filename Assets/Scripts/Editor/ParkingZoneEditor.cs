#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParkingZone))]
public class ParkingZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var zone = (ParkingZone)target;
        serializedObject.Update();

        EditorGUILayout.LabelField("Тип парковки", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parkingType"));

        if (zone.parkingType == ParkingZone.ParkingType.Parallel)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parallelSide"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Время на упражнение", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeLimit"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Критерии фиксации", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("holdTime"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("holdSpeedMax"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fixationTolerance"));

        EditorGUILayout.Space(4);

        if (zone.parkingType == ParkingZone.ParkingType.Rear)
        {
            EditorGUILayout.LabelField("Линия фиксации (Rear)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fixationCollider"));
        }
        else
        {
            EditorGUILayout.LabelField("Линии фиксации (Parallel — до 3 мест)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Машина встанет в любое из перечисленных мест.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parallelFixationColliders"), true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
