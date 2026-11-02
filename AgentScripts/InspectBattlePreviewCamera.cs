using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class InspectBattlePreviewCamera
{
 public static string Run()
 {
  var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var main=Camera.main;
  string path=EditorBuildSettings.scenes.Select(s=>s.path).First(s=>System.IO.Path.GetFileNameWithoutExtension(s)==LevelLoader.FightingSceneName);
  var scene=EditorSceneManager.OpenPreviewScene(path);
  try
  {
   var roots=scene.GetRootGameObjects();
   foreach(var camera in roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))camera.enabled=false;
   var viewport=roots.SelectMany(r=>r.GetComponentsInChildren<BattleCameraViewport>(true)).Single();
   foreach(var safe in roots.SelectMany(r=>r.GetComponentsInChildren<SafeArea>(true)))safe.Apply();
   foreach(var scaler in roots.SelectMany(r=>r.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true)))scaler.SendMessage("Update",SendMessageOptions.DontRequireReceiver);
   foreach(var canvas in roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).Where(c=>c.isRootCanvas))
   {
    canvas.enabled=false;
    var rect=(RectTransform)canvas.transform;float scale=canvas.scaleFactor;
    rect.sizeDelta=new Vector2(Screen.width/scale,Screen.height/scale);rect.localScale=Vector3.one*scale;
    rect.position=new Vector3(Screen.width*rect.pivot.x,Screen.height*rect.pivot.y,0);
    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
   }
   Canvas.ForceUpdateCanvases();viewport.Apply();
   foreach(var layout in roots.SelectMany(r=>r.GetComponentsInChildren<BattleTopHudLayout>(true)))layout.Apply();
   Canvas.ForceUpdateCanvases();viewport.Apply();
   string canvasInfo=string.Join(";",roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).Where(c=>c.isRootCanvas).Select(c=>$"{c.name} scale={c.scaleFactor} rect={((RectTransform)c.transform).rect} pos={c.transform.position} localScale={c.transform.localScale}"));
   var bottom=(RectTransform)new SerializedObject(viewport).FindProperty("bottomHudBoundary").objectReferenceValue;
   var corners=new Vector3[4];bottom.GetWorldCorners(corners);
   var so=new SerializedObject(viewport);var cameraSource=(Camera)so.FindProperty("targetCamera").objectReferenceValue;
   EnemyEmissionOriginSettings.TryGetGameplayBounds(cameraSource,0,out var bounds);
   return $"canvas={canvasInfo}; HUD={bottom.rect} corners={string.Join("/",corners.Select(c=>c.ToString()))}; source={path}; camera={cameraSource.name}; bounds={bounds}; pixelRect={cameraSource.pixelRect}; size={cameraSource.orthographicSize}; screen={Screen.width}x{Screen.height}; activeSceneUnchanged={active==UnityEngine.SceneManagement.SceneManager.GetActiveScene()}; mainUnchanged={main==Camera.main}";
  }
  finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
}
