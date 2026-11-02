using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class VerifyPerformanceSceneSafety
{
    public static object Run()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Idle Editor required.");
        var original = SceneManager.GetActiveScene();
        bool originalDirty = original.isDirty;
        var test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            EditorSceneManager.MarkSceneDirty(test);
            bool rejected = false;
            try { PerformanceSuiteEditor.BuildAndroid(); }
            catch (InvalidOperationException error) { rejected = error.Message.StartsWith("Save scene changes manually", StringComparison.Ordinal); }
            return new { passed = rejected && test.isDirty && original.isDirty == originalDirty,
                rejectedBeforeBuild = rejected, testRemainsUnsaved = test.isDirty, originalDirtyUnchanged = original.isDirty == originalDirty };
        }
        finally
        {
            EditorSceneManager.CloseScene(test, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
    }
}
