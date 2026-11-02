using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class VerifyWavePreviewTargetDrag
{
    static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static SceneView probe;
    static Vector3 position;
    static Vector2 center, xGrip, yGrip;
    static bool old;
    static int changes;
    static void Draw(SceneView view)
    {
        if(view!=probe)return;

        float size=HandleUtility.GetHandleSize(position);
        if(Event.current.type==EventType.Repaint)
        {
            center=GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(position))-probe.position.position;
            xGrip=GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(position+Vector3.right*size*.55f))-probe.position.position;
            yGrip=GUIUtility.GUIToScreenPoint(HandleUtility.WorldToGUIPoint(position+Vector3.up*size*.55f))-probe.position.position;
        }
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        EditorGUI.BeginChangeCheck();
        if(old)position=Handles.PositionHandle(position,Quaternion.identity);
        else position=(Vector3)typeof(WaveEditor).Assembly.GetType("WavePreviewTargetHandle").GetMethod("Draw",Flags).Invoke(null,new object[]{position});
        if(EditorGUI.EndChangeCheck())changes++;
    }
    static async Task<Vector3> Drag(int grip, Vector2 delta)
    {
        position=new Vector3(300,300,0);changes=0;
        probe.LookAt(position,Quaternion.identity,5f,true,true);probe.Repaint();await Task.Delay(150);
        var start=grip==1?xGrip:grip==2?yGrip:center;
        start.y+=probe.position.height-probe.camera.pixelHeight/EditorGUIUtility.pixelsPerPoint;
        probe.Focus();
        probe.SendEvent(new Event{type=EventType.MouseMove,mousePosition=start});
        probe.Repaint();await Task.Delay(100);
        probe.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=start});
        Vector2 previous=start;
        for(int i=1;i<=5;i++)
        {
            Vector2 next=start+delta*i/5f;
            probe.SendEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=next,delta=next-previous});
            previous=next;await Task.Delay(20);
        }
        probe.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=start+delta});
        return position-new Vector3(300,300,0);
    }
    public static Task<object> Run()=>Verify(false);
    public static Task<object> ReproduceOld()=>Verify(true);
    static async Task<object> Verify(bool previous)
    {
        old=previous;var originalSelection=Selection.objects;var originalFocus=EditorWindow.focusedWindow;
        var results=new List<object>();
        probe=ScriptableObject.CreateInstance<SceneView>();probe.titleContent=new GUIContent("Preview XY drag verification");
        probe.ShowUtility();probe.position=new Rect(150,150,700,650);probe.in2DMode=true;
        SceneView.duringSceneGui+=Draw;
        try
        {
            await Task.Delay(250);
            foreach(var test in new[]{(Name:"diagonal",Grip:0,Delta:new Vector2(80,-60)),(Name:"oblique",Grip:0,Delta:new Vector2(-55,23)),(Name:"horizontal",Grip:1,Delta:new Vector2(80,0)),(Name:"vertical",Grip:2,Delta:new Vector2(0,-60))})
            {
                var displacement=await Drag(test.Grip,test.Delta);
                bool passed=test.Grip==0?Mathf.Abs(displacement.x)>.05f&&Mathf.Abs(displacement.y)>.05f
                    :test.Name=="horizontal"?Mathf.Abs(displacement.x)>.05f&&Mathf.Abs(displacement.y)<.001f
                    :Mathf.Abs(displacement.y)>.05f&&Mathf.Abs(displacement.x)<.001f;
                passed&=Mathf.Abs(displacement.z)<.0001f;
                results.Add(new{name=test.Name,displacement=displacement.ToString("F5"),changes,passed});
                if(!previous&&!passed)throw new Exception(test.Name+" drag failed: "+displacement+" changes="+changes);
            }
            var result=new{old=previous,results};
            return result;
        }
        finally
        {
            SceneView.duringSceneGui-=Draw;probe.Close();probe=null;Selection.objects=originalSelection;
            if(originalFocus!=null)originalFocus.Focus();
        }
    }
}