using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DirectedWaveEnemyOverride))]
public sealed class DirectedWaveEnemyOverrideEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EnemyShootingPreview.DrawPreviewControls();
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Локальная стрельба настраивается в Wave Attack Behaviour → Attack Pattern: выберите этот слот. Scene View показывает его точки выстрела, layout и Fan/Stack.", MessageType.Info);
        var wave = ((DirectedWaveEnemyOverride)target).GetComponentInParent<DirectedEnemySubWave>();
        if (wave != null && GUILayout.Button("Select Wave Attack Behaviour")) Selection.activeGameObject = wave.gameObject;
    }

}
