using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class VerifyWaveCameraDisplayContexts
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    const BindingFlags I=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Type Provider=typeof(WaveEditor).Assembly.GetType("WavePreviewGameplayCamera");
    static readonly Type Display=typeof(WaveEditor).Assembly.GetType("WavePreviewDisplay");
    static int checks;
    static Camera Camera=>(Camera)Provider.GetProperty("GameplayCamera",S).GetValue(null);
    static int Revision=>(int)Provider.GetProperty("Revision",S).GetValue(null);
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static Rect Bounds(){Check(EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera,0,out var b),"Camera bounds available");return b;}
    static void Same(Rect a,Rect b,string name)=>Check((a.position-b.position).sqrMagnitude<.000001f&&(a.size-b.size).sqrMagnitude<.000001f,name+": "+a+" / "+b);
    public static async Task<object> Run()
    {
        checks=0;var log=new List<string>();
        var play=typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
        var view=(EditorWindow)play.GetMethod("GetMainPlayModeView",S).Invoke(null,null);
        var main=view.GetType().GetField("m_Main",I)?.GetValue(view);
        Check(main!=null,"Device Simulator is available for hover-context regression");
        var screenProperty=main.GetType().GetProperty("ScreenSimulation",I);
        object ScreenSim()=>screenProperty.GetValue(main);
        var screen=ScreenSim();var screenType=screen.GetType();
        var enable=screenType.GetMethod("Enable",I);var disable=screenType.GetMethod("Disable",I);
        var active=screenType.GetMethod("IsActive",I);
        bool wasActive=(bool)active.Invoke(screen,null);
        var device=main.GetType().GetProperty("deviceIndex",I);int original=(int)device.GetValue(main);
        var originalBounds=Bounds();int revision=Revision;
        int sceneGuiSamples=0;string sceneError=null;bool sawDifferentContext=false;
        void Probe(string context)
        {
            // Force layout as well as reusing the cached camera, to exercise every explicit-display input.
            Provider.GetField("width",S).SetValue(null,0);
            var actual=Bounds();Same(originalBounds,actual,"Stable bounds "+context);
            Check(Revision==revision,"No snapshot invalidation "+context);
            int w=UnityEngine.Screen.width,h=UnityEngine.Screen.height;
            var sample=Display.GetMethod("Read",S).Invoke(null,null);
            int targetW=(int)sample.GetType().GetField("Width",I).GetValue(sample);
            int targetH=(int)sample.GetType().GetField("Height",I).GetValue(sample);
            sawDifferentContext|=w!=targetW||h!=targetH;
            log.Add($"{context}: Screen={w}x{h}; display={targetW}x{targetH}; world={actual.width:F5}x{actual.height:F5}; revision={Revision}");
        }
        Action<SceneView> observe=v=>
        {
            if(Event.current.type!=EventType.Repaint||sceneGuiSamples>=4)return;
            try
            {
                bool restore=(bool)active.Invoke(screen,null);
                try {disable.Invoke(screen,null);Probe("Scene GUI / simulator disabled");enable.Invoke(screen,null);Probe("Scene GUI / simulator enabled");}
                finally{(restore?enable:disable).Invoke(screen,null);}
                sceneGuiSamples++;
            }
            catch(Exception e){sceneError=e.ToString();}
        };
        try
        {
            disable.Invoke(screen,null);Probe("Editor update / simulator disabled");
            enable.Invoke(screen,null);Probe("Editor update / simulator enabled");
            SceneView.duringSceneGui+=observe;
            for(int i=0;i<6;i++){SceneView.RepaintAll();await Task.Delay(100);}
            SceneView.duringSceneGui-=observe;
            Check(sceneError==null,"Scene GUI: "+sceneError);Check(sceneGuiSamples>0,"Observed actual Scene View repaint");
            Check(sawDifferentContext,"Exercised different native Screen and play-display dimensions");
            foreach(int index in new[]{5,6})
            {
                device.SetValue(main,index);await Task.Delay(250);
                var changed=Bounds();Check(changed.width>0&&changed.height>0,"Valid geometry after device change");
                Check(Revision!=revision,"Actual device change invalidates cached geometry");
                var sample=Display.GetMethod("Read",S).Invoke(null,null);
                int w=(int)sample.GetType().GetField("Width",I).GetValue(sample);
                int h=(int)sample.GetType().GetField("Height",I).GetValue(sample);
                log.Add($"Device {index}: {w}x{h}; world={changed.width:F5}x{changed.height:F5}");
                Check(index==5?w==750&&h==1334:w==1080&&h==2340,"Display follows actual device resolution");
            }
        }
        finally
        {
            SceneView.duringSceneGui-=observe;
            device.SetValue(main,original);await Task.Delay(250);
            screen=ScreenSim();(wasActive?enable:disable).Invoke(screen,null);
        }
        Same(originalBounds,Bounds(),"Original device geometry restored");
        File.WriteAllLines("AgentArtifacts/wave-camera-display-contexts.txt",log);
        return new{passed=true,checks,sceneGuiSamples,sawDifferentContext,log};
    }
}
