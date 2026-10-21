using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyWavePopupAfterSlowMotion
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Flags).SetValue(target,value);
    public static object Run()
    {
        var scale=Time.timeScale;var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Wave popup ordering");SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(false);
        try
        {
            var manager=root.AddComponent<WaveManager>();var panel=new GameObject("Popup");panel.transform.SetParent(root.transform);panel.SetActive(false);
            var popup=panel.AddComponent<WaveProgressPopup>();Set(popup,"root",panel);Set(manager,"waveProgressPopup",popup);Set(manager,"enableDebugLogs",false);Set(manager,"wavePrefabs",new List<GameObject>{null});Set(manager,"slowMotionRemaining",.8f);Set(manager,"appliedSlowTimeScale",.3f);Set(manager,"previousTimeScale",1f);
            var routine=(IEnumerator)typeof(WaveManager).GetMethod("ActivateWaveRoutine",Flags).Invoke(manager,new object[]{true});Time.timeScale=.3f;
            if(!routine.MoveNext()||routine.Current!=null||panel.activeSelf)throw new Exception("Popup started during slow motion");
            typeof(WaveManager).GetMethod("TickWaveClearEffects",Flags).Invoke(manager,new object[]{.79f});
            if(!routine.MoveNext()||routine.Current!=null||panel.activeSelf)throw new Exception("Popup started before slowdown finished");
            Time.timeScale=0;Set(manager,"slowMotionRemaining",0f);
            if(!routine.MoveNext()||routine.Current!=null)throw new Exception("Popup started while paused");
            Time.timeScale=1;
            if(!routine.MoveNext()||!(routine.Current is IEnumerator show))throw new Exception("Popup did not start after normal time resumed");
            if(!show.MoveNext()||!panel.activeSelf||!(show.Current is WaitForSeconds))throw new Exception("Popup timer did not start with normal time");
            return new{passed=5};
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);Time.timeScale=scale;}
    }
}
