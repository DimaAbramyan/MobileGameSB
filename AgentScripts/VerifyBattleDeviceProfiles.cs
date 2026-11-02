using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
public static class VerifyBattleDeviceProfiles
{
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Task Frames(int count)
    {
        var done=new TaskCompletionSource<bool>();
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{UnityEditorInternal.InternalEditorUtility.RepaintAllViews();if(--count<=0){EditorApplication.update-=tick;done.TrySetResult(true);}};
        EditorApplication.update+=tick;return done.Task;
    }
    public static async Task<string> Run()
    {
        if(!Application.isPlaying || UnityEngine.Object.FindFirstObjectByType<BattleCameraViewport>()==null)return "No battle currently running; device state unchanged.";
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
        var window=(EditorWindow)type.GetMethod("GetMainPlayModeView",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
        if(window.GetType().FullName!="UnityEditor.DeviceSimulation.SimulatorWindow")return "Current play view is not Device Simulator; unchanged.";
        var main=window.GetType().GetField("m_Main",Instance).GetValue(window);
        var index=main.GetType().GetProperty("deviceIndex",Instance);
        int original=(int)index.GetValue(main);
        var results=new List<string>();
        try
        {
            foreach(int device in new[]{5,6})
            {
                index.SetValue(main,device);window.Repaint();await Frames(25);
                if(!Application.isPlaying)break;
                var viewport=UnityEngine.Object.FindFirstObjectByType<BattleCameraViewport>();
                if(viewport==null)break;
                Canvas.ForceUpdateCanvases();viewport.Apply();await Frames(5);
                var camera=Camera.main;
                var available=(Rect)typeof(BattleCameraViewport).GetMethod("GetAvailableScreenRect",Instance).Invoke(viewport,null);
                string row=$"device {device}: screen {Screen.width}x{Screen.height}; safe {Screen.safeArea}; available {available}; camera {camera.pixelRect}; aspect {camera.aspect}; size {camera.orthographicSize}; HUD scale {UnityEngine.Object.FindFirstObjectByType<UnityEngine.UI.CanvasScaler>()?.GetComponent<Canvas>().scaleFactor}";
                results.Add(row);
                if(Mathf.Abs(camera.pixelRect.width-available.width)>1f)throw new Exception("Camera does not fill available width: "+row);
                var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
                try{System.IO.File.WriteAllBytes($"AgentArtifacts/battle-scaling-device-{device}.png",screenshot.EncodeToPNG());}
                finally{UnityEngine.Object.DestroyImmediate(screenshot);}
            }
        }
        finally{index.SetValue(main,original);window.Repaint();await Frames(8);}
        string result=string.Join("\n",results)+"\nRestored original device index "+original;
        System.IO.File.WriteAllText("AgentArtifacts/battle-scaling-devices.txt",result);return result;
    }
}
