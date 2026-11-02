using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class InspectBattleScaling
{
 public static string Run(){
  var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  var data=new {
   screen=new{Screen.width,Screen.height,safe=Screen.safeArea.ToString()},
   cameras=Camera.allCameras.Select(c=>new{c.name,c.orthographic,c.orthographicSize,c.aspect,rect=c.rect.ToString(),pixels=c.pixelRect.ToString(),pos=c.transform.position.ToString(),components=c.GetComponents<Component>().Select(x=>x.GetType().FullName).ToArray()}).ToArray(),
   viewports=UnityEngine.Object.FindObjectsByType<BattleCameraViewport>(FindObjectsSortMode.None).Select(c=>new{name=c.name,scene=c.gameObject.scene.path,json=EditorJsonUtility.ToJson(c),available=typeof(BattleCameraViewport).GetMethod("GetAvailableScreenRect",flags).Invoke(c,null).ToString()}).ToArray(),
   widths=UnityEngine.Object.FindObjectsByType<CameraConstantWidth>(FindObjectsSortMode.None).Select(c=>new{c.name,c.enabled,json=EditorJsonUtility.ToJson(c)}).ToArray(),
   canvases=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None).Select(c=>new{c.name,c.uiScaleMode,reference=c.referenceResolution.ToString(),c.screenMatchMode,c.matchWidthOrHeight,scale=c.GetComponent<Canvas>().scaleFactor}).ToArray(),
   player=UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Select(c=>new{c.name,pos=c.transform.position.ToString(),scale=c.transform.lossyScale.ToString()}).ToArray(),
   virtualCameras=UnityEngine.Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None).Select(c=>new{c.name,json=EditorJsonUtility.ToJson(c)}).ToArray()
  };
  string result=(string)Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{data});
  System.IO.File.WriteAllText("AgentArtifacts/battle-scaling-observed.json",result);return result;
 }
}

