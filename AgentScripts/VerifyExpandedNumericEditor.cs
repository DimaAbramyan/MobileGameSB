using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyExpandedNumericEditor
{
    private const string Folder="Assets/Configs/AttackPresets/Examples";
    private const string PatternPath=Folder+"/RandomFan_AttackPattern.asset";
    private const string ProjectilePath=Folder+"/RandomOrbit_Projectile.asset";
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    private static void Patch(object value,string json)=>JsonUtility.FromJsonOverwrite(json,value);
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Flags).SetValue(value,data);
    private static void Range(object value,string field,float min,float max,EnemyNumericRandomizeOn mode=EnemyNumericRandomizeOn.PerShot)
    {
        object range=value.GetType().GetField(field+"Random",Flags).GetValue(value);
        Patch(range,"{\"enabled\":true,\"min\":"+min.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"max\":"+max.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"initialized\":true,\"randomizeOn\":"+(int)mode+"}");
    }
    private static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private static void Metadata(EnemyReusablePreset asset,string title)
    {
        using var so=new SerializedObject(asset);so.FindProperty("displayName").stringValue=title;so.FindProperty("category").stringValue="Randomized Examples";
        so.FindProperty("description").stringValue="Seeded numeric ranges. Not assigned to existing waves.";so.ApplyModifiedProperties();
    }
    public static object CreateExamples()
    {
        EnsureFolder(Folder);
        if(AssetDatabase.LoadAssetAtPath<EnemyAttackPatternPreset>(PatternPath)==null)
        {
            var source=new EnemyBurstAttackSettings();source.Validate();source.Shooting.ConfigureLegacyFan(5,-45,45);
            Range(source.Shooting,"fanProjectileCount",3,8);Range(source.Shooting,"fanMinAngle",-65,-35);Range(source.Shooting,"fanMaxAngle",35,65);
            Range(source,"attackShotCount",2,4);Range(source,"attackShotInterval",.15f,.3f);Patch(source,"{\"useAttackStartDelay\":true}");Range(source,"attackStartDelay",.2f,.6f);
            source.AttackCooldownRandom.CopyFrom(new RandomFloatRange{enabled=true,min=1.5f,max=2.5f,initialized=true});
            var asset=ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();asset.SetSettings(source,new EnemyPresetFiringSettings{enabled=true,fireMode=DirectedWaveAttackFireMode.Default});
            Metadata(asset,"Random Fan");AssetDatabase.CreateAsset(asset,PatternPath);Undo.RegisterCreatedObjectUndo(asset,"Create random attack example");AssetDatabase.SaveAssetIfDirty(asset);
        }
        if(AssetDatabase.LoadAssetAtPath<EnemyProjectilePreset>(ProjectilePath)==null)
        {
            var bullet=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            var source=new EnemyShootingSettings();source.SetProjectileBaseSpeedOverride(true,3f);source.SetProjectileBaseSpeedRange(new RandomFloatRange{enabled=true,min=3,max=5,initialized=true},EnemySpeedRandomizeOn.PerProjectile);
            Patch(source,"{\"projectilePrefab\":{\"instanceID\":"+bullet.GetInstanceID()+"},\"projectileMovementPattern\":3,\"projectileOrbitRadius\":0.4,\"overrideProjectileLifetime\":true,\"projectileLifetime\":4}");
            Range(source,"projectileOrbitRadius",.2f,1f,EnemyNumericRandomizeOn.PerProjectile);Range(source,"projectileAngularSpeed",60,120,EnemyNumericRandomizeOn.PerProjectile);
            Range(source,"projectileOrbitRadialSpeed",.05f,.15f,EnemyNumericRandomizeOn.PerProjectile);Range(source,"projectileLifetime",3,6,EnemyNumericRandomizeOn.PerProjectile);
            var child=new EnemyChildAttackSettings();Range(child,"triggerValue",1,1.5f);Range(child,"repeatCount",1,2);Range(child,"repeatInterval",.2f,.5f);Range(child,"spawnOffsetX",-.1f,.1f);Range(child,"spawnOffsetY",.1f,.4f);
            child.Pattern.AuthoredShooting.SetProjectileBaseSpeedOverride(true,2.5f);child.Pattern.AuthoredShooting.ConfigureLegacyFan(3,-25,25);source.SetChildAttack(child);
            var asset=ScriptableObject.CreateInstance<EnemyProjectilePreset>();asset.SetSettings(source);Metadata(asset,"Random Orbit + Child");AssetDatabase.CreateAsset(asset,ProjectilePath);
            Undo.RegisterCreatedObjectUndo(asset,"Create random projectile example");AssetDatabase.SaveAssetIfDirty(asset);
        }
        return Reload();
    }
    public static object Reload()
    {
        checks=0;var pattern=AssetDatabase.LoadAssetAtPath<EnemyAttackPatternPreset>(PatternPath);var projectile=AssetDatabase.LoadAssetAtPath<EnemyProjectilePreset>(ProjectilePath);
        Check(pattern!=null&&projectile!=null,"Both example assets exist");
        Check(pattern.Pattern.RuntimeSettings.AttackShotCountRandom.enabled,"Saved attack count range");
        using(var so=new SerializedObject(pattern))Check(so.FindProperty("pattern.shooting.fanProjectileCountRandom.max").intValue==8,"Saved Fan integer range");
        using(var so=new SerializedObject(projectile))
        {Check(so.FindProperty("projectile.projectileOrbitRadiusRandom.randomizeOn").intValue==(int)EnemyNumericRandomizeOn.PerProjectile,"Saved projectile frequency");
            Check(so.FindProperty("projectile.childAttack.spawnOffsetXRandom.enabled").boolValue,"Saved nested Child offset range");}
        return new{passed=true,checks,pattern=PatternPath,projectile=ProjectilePath};
    }
    public static async Task<object> Run()
    {
        CreateExamples();checks=0;
        var scene=EditorSceneManager.NewPreviewScene();var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"),scene);
        var executor=root.GetComponent<SimpleEnemyAttackExecutor>();Editor editor=null;Action<SceneView> draw=null;EnemyPresetLibraryWindow window=null;
        try
        {
            var pattern=AssetDatabase.LoadAssetAtPath<EnemyAttackPatternPreset>(PatternPath);var projectile=AssetDatabase.LoadAssetAtPath<EnemyProjectilePreset>(ProjectilePath);
            string originalPattern=EditorJsonUtility.ToJson(pattern),originalProjectile=EditorJsonUtility.ToJson(projectile);
            var profile=new EnemyBurstAttackSettings();profile.ApplyPatternPreset(pattern,EnemyPresetUseMode.Copy);profile.AuthoredShooting.LinkProjectile(projectile,EnemyPresetUseMode.Copy);
            using var so=new SerializedObject(executor);so.FindProperty("burstAttackSettings").boxedValue=profile;so.ApplyModifiedProperties();so.Update();
            var fixedValue=so.FindProperty("burstAttackSettings.shooting.projectileOrbitRadius");var range=so.FindProperty("burstAttackSettings.shooting.projectileOrbitRadiusRandom");
            float before=fixedValue.floatValue, min=range.FindPropertyRelative("min").floatValue,max=range.FindPropertyRelative("max").floatValue;
            float enabledHeight=EditorGUI.GetPropertyHeight(fixedValue);Undo.IncrementCurrentGroup();Undo.RecordObject(executor,"Toggle numeric range");range.FindPropertyRelative("enabled").boolValue=false;so.ApplyModifiedProperties();Undo.FlushUndoRecordObjects();
            Check(EditorGUI.GetPropertyHeight(fixedValue)<enabledHeight,"Frequency row only shown while Random is enabled");Check(fixedValue.floatValue==before,"Fixed value preserved when disabling range");
            Undo.PerformUndo();so.Update();Check(range.FindPropertyRelative("enabled").boolValue,"Undo range toggle");Undo.PerformRedo();so.Update();Check(!range.FindPropertyRelative("enabled").boolValue,"Redo range toggle");
            range.FindPropertyRelative("enabled").boolValue=true;so.ApplyModifiedProperties();Check(range.FindPropertyRelative("min").floatValue==min&&range.FindPropertyRelative("max").floatValue==max,"Min Max preserved across toggle");
            Check(PrefabUtility.GetPropertyModifications(root).Length>0,"Numeric settings tracked as prefab overrides");
            var childOffset=so.FindProperty("burstAttackSettings.shooting.childAttack.spawnOffset");Check(EditorGUI.GetPropertyHeight(childOffset)>EditorGUIUtility.singleLineHeight,"Child X Y controls have independent rows");
            editor=Editor.CreateEditor(executor);var view=SceneView.lastActiveSceneView;Check(view!=null,"Scene View available");
            var done=new TaskCompletionSource<bool>();int rendered=0;
            draw=current=>{if(current!=view||(Event.current.type!=EventType.Layout&&Event.current.type!=EventType.Repaint))return;try{Handles.BeginGUI();GUILayout.BeginArea(new Rect(10,20,900,30000));editor.OnInspectorGUI();GUILayout.EndArea();Handles.EndGUI();if(Event.current.type==EventType.Repaint&&++rendered>=2)done.TrySetResult(true);}catch(Exception error){done.TrySetException(error);}};
            SceneView.duringSceneGui+=draw;var timeout=Task.Delay(10000);while(!done.Task.IsCompleted&&!timeout.IsCompleted){view.Repaint();await Task.Delay(50);}Check(done.Task.IsCompleted,"Numeric custom Inspector rendered");await done.Task;SceneView.duringSceneGui-=draw;draw=null;
            Check(EditorJsonUtility.ToJson(pattern)==originalPattern&&EditorJsonUtility.ToJson(projectile)==originalProjectile,"Local changes never mutate source presets");
            window=ScriptableObject.CreateInstance<EnemyPresetLibraryWindow>();window.position=new Rect(80,80,1050,760);window.ShowUtility();window.Focus();
            Set(window,"category",1);Set(window,"previewProjectile",projectile);window.GetType().GetMethod("Select",Flags).Invoke(window,new object[]{pattern});
            Set(window,"inspectorScroll",new Vector2(0,260));window.GetType().GetMethod("Restart",Flags).Invoke(window,null);window.Repaint();await Task.Delay(300);
            var preview=(EnemyPresetPreviewSession)window.GetType().GetField("preview",Flags).GetValue(window);preview.Advance(1.5f);Check(preview.Count>0,"Numeric library Preview emits actual projectiles");
            window.Focus();window.Repaint();await Task.Delay(200);
            var rect=window.position;var image=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGB24,false);
            try{image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x,rect.y),image.width,image.height));image.Apply();File.WriteAllBytes("AgentArtifacts/attack-randomization-library.png",image.EncodeToPNG());}finally{Object.DestroyImmediate(image);}
            AssetDatabase.ImportAsset(PatternPath,ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(ProjectilePath,ImportAssetOptions.ForceUpdate);
            Check(AssetDatabase.LoadAssetAtPath<EnemyAttackPatternPreset>(PatternPath).Pattern.RuntimeSettings.AttackShotCountRandom.enabled,"Asset reimport retains numeric ranges");
            return new{passed=true,checks,undoRedo=true,prefabOverrides=true,inspector=true,presetPersistence=true,examples=new[]{PatternPath,ProjectilePath}};
        }
        finally
        {SceneView.duringSceneGui-=draw;if(window!=null){window.Close();Object.DestroyImmediate(window);}if(editor!=null)Object.DestroyImmediate(editor);Undo.ClearUndo(executor);Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
