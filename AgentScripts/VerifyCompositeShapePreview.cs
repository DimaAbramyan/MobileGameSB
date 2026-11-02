using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyCompositeShapePreview
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static EnemyShootingSettings Settings(EnemyCompositeShape shape)
    {
        var s = new EnemyShootingSettings(); s.Validate(); s.SetProjectileType(EnemyProjectileType.Composite); s.Composite.SetShape(shape);
        JsonUtility.FromJsonOverwrite("{\"componentCount\":8,\"numberOfSides\":5,\"projectilesPerSide\":3,\"segmentCount\":3,\"projectilesPerSegment\":4,\"gapAngle\":35,\"radius\":0.4,\"startAngle\":12,\"initialRotation\":20,\"componentScale\":1.1,\"overrideHitRadius\":true,\"hitRadius\":0.055}", s.Composite);
        if (shape == EnemyCompositeShape.RegularPolygon) JsonUtility.FromJsonOverwrite("{\"radiusMode\":1}", s.Composite);
        return s;
    }
    public static async Task<object> Run()
    {
        checks = 0;
        Check((int)EnemyProjectileType.Default == 0 && (int)EnemyProjectileType.Laser == 3
            && (int)EnemyProjectileType.Composite == 4 && (int)EnemyProjectileType.CompositeCircle == 4
            && (int)EnemyProjectileType.CompositePolygon == 5 && (int)EnemyProjectileType.CompositeSegmentedRing == 6, "Old numeric enum IDs retained");
        var popup = (GUIContent[])typeof(EnemyShootingSettingsDrawer).GetField("ProjectileTypeLabels", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Check(popup.Length == 5 && popup[4].text == "Composite", "One Composite choice in Inspector");
        var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        for (int legacy = 4; legacy <= 6; legacy++)
        {
            var old = JsonUtility.FromJson<EnemyShootingSettings>("{\"projectileType\":" + legacy + ",\"composite\":{\"radiusMode\":1,\"radius\":1.73,\"componentCount\":8,\"numberOfSides\":5,\"projectilesPerSide\":3,\"segmentCount\":3,\"projectilesPerSegment\":4}}");
            Check(old.ProjectileType == EnemyProjectileType.Composite && (int)old.Composite.Shape == legacy - 4
                && Mathf.Abs(old.Composite.Resolve(old.ProjectileType).Radius - 1.73f) < .0001f, "Legacy type migrates without losing fields: " + legacy);
            var copy = new EnemyShootingSettings(); copy.CopyFrom(old);
            var preset = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
            try
            {
                preset.SetSettings(copy); string json = EditorJsonUtility.ToJson(preset); EditorJsonUtility.FromJsonOverwrite(json, preset);
                Check(preset.Settings.ProjectileType == EnemyProjectileType.Composite && preset.Settings.Composite.Shape == old.Composite.Shape, "Preset copy and serialization keep migrated shape");
                EditorJsonUtility.FromJsonOverwrite("{\"projectile\":{\"projectileType\":" + legacy + "}}", preset);
                using var serializedPreset = new SerializedObject(preset);
                Check(serializedPreset.FindProperty("projectile.projectileType").intValue == 4
                    && serializedPreset.FindProperty("projectile.composite.shape").intValue == legacy - 4,
                    "Old nested preset data migrates before Inspector access");
            }
            finally { Object.DestroyImmediate(preset); }
        }
        foreach (var shape in new[] { EnemyCompositeShape.Circle, EnemyCompositeShape.RegularPolygon, EnemyCompositeShape.SegmentedRing })
        {
            var shooting = Settings(shape);
            Check(EnemyShootingPreview.TryGetCompositeSilhouette(shooting, bullet, Vector3.down, out var silhouette), "Silhouette geometry available");
            Check((int)silhouette.Spec.Type == 4 + (int)shape && silhouette.Spec.ComponentCount == (shape == EnemyCompositeShape.Circle ? 8 : shape == EnemyCompositeShape.RegularPolygon ? 10 : 12), "Shape selects its existing geometry and component count");
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, Vector3.zero, Vector3.zero, Vector3.down, 1, 1, 0, new EnemyProjectilePrefabSet(bullet));
            using var world = new EnemyProjectilePreviewWorld(); world.Spawn(requests);
            var manager = world.SimulationWorld.EntityManager;
            using var query = manager.CreateEntityQuery(typeof(EnemyCompositeCenter)); using var centers = query.ToEntityArray(Allocator.Temp);
            Check(centers.Length == 1, "Unified type still spawns one ECS center");
            var members = manager.GetBuffer<EnemyCompositeMember>(centers[0]); bool equal = members.Length == silhouette.Spec.ComponentCount;
            for (int i = 0; i < members.Length; i++)
                equal &= Vector3.Distance(silhouette.ComponentPosition(Vector3.zero, i), manager.GetComponentData<LocalTransform>(members[i].Entity).Position) < .0001f;
            Check(equal, "Silhouette positions match existing ECS spawn geometry: " + shape);
        }
        var fan = Settings(EnemyCompositeShape.RegularPolygon); fan.ConfigureLegacyFan(7, -30, 30);
        Check(EnemyShootingPreview.RepresentativeCompositeProjectile(fan) == 3, "Middle Fan member"); fan.ConfigureLegacyFan(6, -30, 30);
        Check(EnemyShootingPreview.RepresentativeCompositeProjectile(fan) == 2, "Stable lower middle for even Fan");
        JsonUtility.FromJsonOverwrite("{\"rotationMode\":3}", fan.Composite);
        EnemyShootingPreview.TryGetCompositeSilhouette(fan, bullet, Vector3.right, out var aligned);
        Check(Mathf.Abs(aligned.Angle - 110f) < .0001f, "Silhouette follows launch heading through shared Align To Movement rule");
        var child = new EnemyChildAttackSettings(); child.Pattern.AuthoredShooting.CopyFrom(fan); fan.SetChildAttack(child);
        var nested = JsonUtility.FromJson<EnemyShootingSettings>(JsonUtility.ToJson(fan));
        Check(nested.ChildAttack.Pattern.Shooting.ProjectileType == EnemyProjectileType.Composite
            && nested.ChildAttack.Pattern.Shooting.Composite.Shape == EnemyCompositeShape.RegularPolygon, "Child attack serialization preserves shape");
        await Render();
        return new { passed = true, checks, geometryMatchesEcs = true, migratedTypes = 3, sceneCaptures = 5 };
    }
    private static async Task Render()
    {
        var view = SceneView.lastActiveSceneView; if (view == null) throw new Exception("Scene View required");
        var selection = Selection.objects; Selection.objects = Array.Empty<Object>();
        var pivot = view.pivot; var orientation = view.rotation; float size = view.size; bool ortho = view.orthographic;
        bool show = EnemyShootingPreview.ShowFormationVolley, all = EnemyShootingPreview.ShowAllCompositeSilhouettes, paths = EnemyShootingPreview.ShowTrajectories;
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Transient composite silhouette verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var executor = root.AddComponent<SimpleEnemyAttackExecutor>(); executor.BurstAttackSettings.Validate();
        Action<SceneView> draw = null;
        try
        {
            EnemyShootingPreview.ShowFormationVolley = true; EnemyShootingPreview.ShowTrajectories = false;
            view.orthographic = true; view.LookAtDirect(Vector3.zero, Quaternion.identity, 3.2f);
            for (int test = 0; test < 5; test++)
            {
                var source = Settings((EnemyCompositeShape)(test < 3 ? test : 1));
                source.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle, 4, 1f);
                if (test == 4)
                {
                    source.EmissionLayout.Configure(EnemyEmissionLayoutType.Point); source.ConfigureLegacyFan(7, -30, 30);
                    source.SetLayoutRotationEnabled(true); source.Rotation.Configure(FourWayEnemyRotationDirection.Clockwise, FourWayEnemyRotationMode.ByAngle,
                        90, AnimationCurve.Linear(0, 0, 1, 1), 90, 0, true);
                    JsonUtility.FromJsonOverwrite("{\"rotationMode\":3}", source.Composite);
                }
                executor.BurstAttackSettings.AuthoredShooting.CopyFrom(source);
                using var serialized = new SerializedObject(executor); var property = serialized.FindProperty("burstAttackSettings.shooting.composite");
                var fields = (IEnumerable<string>)typeof(EnemyCompositeProjectileSettingsDrawer).GetMethod("Shape", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { property });
                var expected = source.Composite.Shape == EnemyCompositeShape.Circle ? "componentCount" : source.Composite.Shape == EnemyCompositeShape.RegularPolygon ? "numberOfSides" : "segmentCount";
                Check(fields.Contains("shape") && fields.Contains(expected), "Inspector only selects current shape fields");
                EnemyShootingPreview.ShowAllCompositeSilhouettes = test == 3;
                string before = EditorJsonUtility.ToJson(executor); int frames = 0; var done = new TaskCompletionSource<bool>();
                draw = current =>
                {
                    if (current != view || Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
                    try
                    {
                        if (Event.current.type == EventType.Repaint)
                        {
                            EnemyShootingPreview.DrawExecutorPattern(executor, executor.BurstAttackSettings.Shooting, Vector3.zero, 0, staticLayoutPose: true);
                            Handles.BeginGUI(); GUI.Label(new Rect(12, 35, 650, 45), source.Composite.Shape + (EnemyShootingPreview.ShowAllCompositeSilhouettes ? " — all silhouettes" : " — one silhouette + centers"), EditorStyles.whiteLargeLabel); Handles.EndGUI();
                            if (++frames >= 3) done.TrySetResult(true);
                        }
                    }
                    catch (Exception error) { done.TrySetException(error); }
                };
                SceneView.duringSceneGui += draw; var watch = System.Diagnostics.Stopwatch.StartNew();
                while (!done.Task.IsCompleted && watch.ElapsedMilliseconds < 10000) { view.Repaint(); await Task.Delay(60); }
                if (!done.Task.IsCompleted) throw new Exception("Scene View repaint timeout"); await done.Task;
                var rt = view.camera.targetTexture; var old = RenderTexture.active; var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                try { RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply(); File.WriteAllBytes("AgentArtifacts/composite-shape-preview-" + test + ".png", image.EncodeToPNG()); }
                finally { RenderTexture.active = old; Object.DestroyImmediate(image); }
                Check(before == EditorJsonUtility.ToJson(executor), "Preview leaves authored settings unchanged");
                SceneView.duringSceneGui -= draw; draw = null;
            }
        }
        finally
        {
            if (draw != null) SceneView.duringSceneGui -= draw;
            EnemyShootingPreview.ShowFormationVolley = show; EnemyShootingPreview.ShowAllCompositeSilhouettes = all; EnemyShootingPreview.ShowTrajectories = paths;
            view.orthographic = ortho; view.LookAtDirect(pivot, orientation, size);
            Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); Selection.objects = selection; SceneView.RepaintAll();
        }
    }
}
