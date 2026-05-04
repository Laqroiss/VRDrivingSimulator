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

        EditorGUILayout.LabelField("Parking type", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parkingType"));

        if (zone.parkingType == ParkingZone.ParkingType.Parallel)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parallelSide"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Time limit", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeLimit"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Fixation criteria", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("holdTime"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("holdSpeedMax"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fixationTolerance"));

        EditorGUILayout.Space(4);

        if (zone.parkingType == ParkingZone.ParkingType.Rear)
        {
            EditorGUILayout.LabelField("Fixation line (Rear)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fixationCollider"));
        }
        else
        {
            EditorGUILayout.LabelField("Fixation lines (Parallel - up to 3 spots)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The car may park in any of the listed spots.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parallelFixationColliders"), true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
