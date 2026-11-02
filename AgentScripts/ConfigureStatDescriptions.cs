using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class ConfigureStatDescriptions
{
    static void Ref(UnityEngine.Object owner, string field, UnityEngine.Object value)
    { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedProperties(); EditorUtility.SetDirty(owner); }
    static void Connect(MonoBehaviour controller, StatBlocksView source, string textField, string viewField, StatDescriptionKind kind, List<string> result)
    {
        var text = (TMP_Text)new SerializedObject(controller).FindProperty(textField).objectReferenceValue;
        if (text == null) throw new Exception("Missing description TMP: " + textField);
        var view = text.GetComponent<StatDescriptionView>();
        if (view == null) view = Undo.AddComponent<StatDescriptionView>(text.gameObject);
        Ref(view, "source", source); Ref(view, "target", text); Ref(controller, viewField, view);
        var so = new SerializedObject(view); so.FindProperty("kind").intValue = (int)kind; so.ApplyModifiedProperties();
        Undo.RecordObject(text, "Enable description rich text"); text.richText = true; EditorUtility.SetDirty(text);
        result.Add(controller.name + "/" + textField);
    }
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new Exception("Editor must be idle");
        var scene = SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/NewMainMenu.unity") throw new Exception("Open NewMainMenu");
        var result = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var c in root.GetComponentsInChildren<WeaponSelectionInfoController>(true))
                if (c.StatBlocks != null) Connect(c, c.StatBlocks, "descriptionText", "descriptionView", StatDescriptionKind.Weapon, result);
            foreach (var c in root.GetComponentsInChildren<HullSelectionInfoController>(true))
                if (c.StatBlocks != null)
                {
                    Connect(c, c.StatBlocks, "activeAbilityDescriptionText", "activeDescriptionView", StatDescriptionKind.ActiveAbility, result);
                    Connect(c, c.StatBlocks, "passiveAbilityDescriptionText", "passiveDescriptionView", StatDescriptionKind.PassiveAbility, result);
                }
        }
        if (result.Count < 3) throw new Exception("Expected three description texts");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return new { saved = true, scene = scene.path, descriptions = result };
    }
}
