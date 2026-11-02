using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

// Owns only a temporary, unsaved empty Editor scene for the localization runtime diagnostic.
public static class LocalizationReleaseSession
{
    const string Key = "Agent.LocalizationRelease.";
    static string Serialize(object value) => (string)Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new[]{value});
    public static object Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Idle Edit Mode required");
        if(SessionState.GetBool(Key+"Owned",false))throw new InvalidOperationException("Restore the previous localization session first");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty || string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))throw new InvalidOperationException("Save unrelated scene changes manually before the isolated diagnostic");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        SessionState.SetString(Key+"Scenes",Serialize(setup));
        SessionState.SetString(Key+"Start",UnityEditor.AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key+"Remote",EditorSettings.unityRemoteDevice);
        SessionState.SetString(Key+"Locale",LocalizationSettings.Instance.GetSelectedLocale()?.Identifier.Code ?? "");
        SessionState.SetBool(Key+"Owned",true);
        try
        {
            EditorSettings.unityRemoteDevice="None";
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            return new {ready=true,temporaryScene=true,sceneCount=SceneManager.sceneCount,rootObjects=SceneManager.GetActiveScene().rootCount};
        }
        catch {Restore();throw;}
    }
    public static object Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before restoring the diagnostic session");
        if(!SessionState.GetBool(Key+"Owned",false))return new {restored=false,reason="No owned diagnostic session"};
        var setup=(SceneSetup[])Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("DeserializeObject",new[]{typeof(string),typeof(Type)}).Invoke(null,new object[]{SessionState.GetString(Key+"Scenes",""),typeof(SceneSetup[])});
        try {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        finally
        {
            EditorSettings.unityRemoteDevice=SessionState.GetString(Key+"Remote","");
            string path=SessionState.GetString(Key+"Start","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            string locale=SessionState.GetString(Key+"Locale","");
            LocalizationSettings.Instance.SetSelectedLocale(string.IsNullOrEmpty(locale)?null:LocalizationEditorSettings.GetLocale(locale));
        }
        SessionState.SetBool(Key+"Owned",false);
        return new {restored=true,scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,remote=EditorSettings.unityRemoteDevice};
    }
}
