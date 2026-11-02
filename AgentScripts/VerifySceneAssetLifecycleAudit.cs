using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public static class VerifySceneAssetLifecycleAudit
{
    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        const string path = "Assets/Scenes/Fighting/Fighting.unity";
        if (SceneManager.GetSceneByPath(path).isLoaded)
            return new { skipped = true, reason = "Fighting already open: preserve user scene state." };
        var active = SceneManager.GetActiveScene();
        bool dirty = active.isDirty;
        int sceneCount = SceneManager.sceneCount, worlds = World.All.Count;
        byte[] before = Digest(path);
        var cycles = new List<object>();
        for (int cycle = 0; cycle < 2; cycle++)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int contexts = 0, installers = 0, missing = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    contexts += root.GetComponentsInChildren<SceneContext>(true).Length;
                    installers += root.GetComponentsInChildren<FightingInstaller>(true).Length;
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
                if (contexts != 1 || installers != 1) throw new InvalidOperationException("Scene Context/Installer configuration is incomplete.");
                cycles.Add(new { cycle, contexts, installers, missingScripts = missing });
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            if (SceneManager.sceneCount != sceneCount || SceneManager.GetActiveScene() != active || active.isDirty != dirty)
                throw new InvalidOperationException("Original scene state changed.");
            if (World.All.Count != worlds) throw new InvalidOperationException("Editor-only scene load leaked a World.");
        }
        var after = Digest(path);
        for (int i = 0; i < before.Length; i++) if (before[i] != after[i]) throw new InvalidOperationException("Scene file changed.");
        return new { passed = true, cycles, originalScenePreserved = true, sceneFileUnchanged = true,
            editorLoadUnloadOnly = true, playModeInjectionNotExecuted = true };
    }
    private static byte[] Digest(string path)
    { using var algorithm = SHA256.Create(); return algorithm.ComputeHash(File.ReadAllBytes(path)); }
}
