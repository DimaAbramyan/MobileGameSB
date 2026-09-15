using UnityEditor;
using UnityEngine;

public abstract class DirectedWaveSubWaveBehaviourEditorBase : Editor
{
    private Editor coordinatorEditor;

    protected DirectedEnemySubWave GetCoordinator()
    {
        Component behaviour = target as Component;
        return behaviour != null
            ? behaviour.GetComponent<DirectedEnemySubWave>()
            : null;
    }

    protected DirectedEnemySubWaveEditor GetCoordinatorEditor(
        DirectedEnemySubWave coordinator)
    {
        if (coordinator == null)
            return null;

        Editor.CreateCachedEditor(
            coordinator,
            typeof(DirectedEnemySubWaveEditor),
            ref coordinatorEditor);
        return coordinatorEditor as DirectedEnemySubWaveEditor;
    }

    protected bool TryGetCoordinatorEditor(
        out DirectedEnemySubWaveEditor editor)
    {
        DirectedEnemySubWave coordinator = GetCoordinator();
        editor = GetCoordinatorEditor(coordinator);
        if (editor != null)
            return true;

        EditorGUILayout.HelpBox(
            "This behaviour must be attached to the same GameObject as Directed Enemy Sub Wave.",
            MessageType.Error);
        return false;
    }

    protected void DrawSceneGUI()
    {
        DirectedEnemySubWaveEditor editor = GetCoordinatorEditor(
            GetCoordinator());
        editor?.DrawBehaviourSceneGUI();
    }

    protected virtual void OnDisable()
    {
        if (coordinatorEditor != null)
            DestroyImmediate(coordinatorEditor);
    }
}

[CustomEditor(typeof(DirectedWaveFormationBehaviour))]
public sealed class DirectedWaveFormationBehaviourEditor :
    DirectedWaveSubWaveBehaviourEditorBase
{
    public override void OnInspectorGUI()
    {
        if (TryGetCoordinatorEditor(out DirectedEnemySubWaveEditor editor))
            editor.DrawFormationBehaviourInspector();
    }

    private void OnSceneGUI()
    {
        DrawSceneGUI();
    }
}

[CustomEditor(typeof(DirectedWavePhaseEntryBehaviour))]
public sealed class DirectedWavePhaseEntryBehaviourEditor :
    DirectedWaveSubWaveBehaviourEditorBase
{
    public override void OnInspectorGUI()
    {
        if (TryGetCoordinatorEditor(out DirectedEnemySubWaveEditor editor))
            editor.DrawPhaseEntryBehaviourInspector();
    }

    private void OnSceneGUI()
    {
        DrawSceneGUI();
    }
}

[CustomEditor(typeof(DirectedWavePostBehaviour))]
public sealed class DirectedWavePostBehaviourEditor :
    DirectedWaveSubWaveBehaviourEditorBase
{
    public override void OnInspectorGUI()
    {
        if (TryGetCoordinatorEditor(out DirectedEnemySubWaveEditor editor))
            editor.DrawPostBehaviourInspector();
    }

    private void OnSceneGUI()
    {
        DrawSceneGUI();
    }
}

[CustomEditor(typeof(DirectedWaveCompletionBehaviour))]
public sealed class DirectedWaveCompletionBehaviourEditor :
    DirectedWaveSubWaveBehaviourEditorBase
{
    public override void OnInspectorGUI()
    {
        if (TryGetCoordinatorEditor(out DirectedEnemySubWaveEditor editor))
            editor.DrawCompletionBehaviourInspector();
    }

    private void OnSceneGUI()
    {
        DrawSceneGUI();
    }
}
