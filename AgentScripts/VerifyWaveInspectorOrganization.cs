using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Isolated IMGUI checks. No project assets or open scenes are saved by this probe.
public sealed class WaveInspectorOrganizationHost : ScriptableObject
{
    public EnemyBurstAttackSettings profile = new();
}

public sealed class WaveInspectorOrganizationWindow : EditorWindow
{
    public Action Draw;
    public Exception Error;
    public int Repaints;
    private Vector2 scroll;
    private void OnGUI()
    {
        if (Draw == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try { Draw(); }
        catch (Exception error) { Error = error; }
        finally { EditorGUILayout.EndScrollView(); }
        if (Event.current.type == EventType.Repaint) Repaints++;
    }
}

public static class VerifyWaveInspectorOrganization
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static int checks;
    private static readonly List<string> scenarios = new();
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static void Set(SerializedProperty p, string path, int value) => p.FindPropertyRelative(path).intValue = value;
    private static void Enable(SerializedProperty p, string path) => p.FindPropertyRelative(path).boolValue = true;
    private static string Key(SerializedProperty p, string section) =>
        "WaveInspector.Sections." + p.serializedObject.targetObject.GetInstanceID() + "." + p.propertyPath + "." + section;

    public static async Task<object> Verify()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle.");
        checks = 0; scenarios.Clear();
        var host = ScriptableObject.CreateInstance<WaveInspectorOrganizationHost>();
        host.hideFlags = HideFlags.HideAndDontSave;
        var projectilePreset = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        var patternPreset = ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();
        using var data = new SerializedObject(host);
        var window = ScriptableObject.CreateInstance<WaveInspectorOrganizationWindow>();
        var scene = EditorSceneManager.NewPreviewScene();
        var editors = new List<Editor>();
        try
        {
            window.position = new Rect(150, 120, 560, 800);
            window.ShowUtility(); window.Focus();
            var timing = data.FindProperty("profile");
            var shooting = timing.FindPropertyRelative("shooting");
            for (int mode = 0; mode < 8; mode++)
            {
                host.profile = new EnemyBurstAttackSettings(); host.profile.Validate(); data.Update();
                timing = data.FindProperty("profile"); shooting = timing.FindPropertyRelative("shooting");
                if (mode >= 1)
                {
                    Enable(shooting, "overrideBaseSize"); Enable(shooting, "overrideProjectileBaseSpeed");
                    Enable(shooting, "enableSpawnOffset"); Set(shooting, "fanMode", (int)EnemyProjectileFanMode.Fan);
                    Set(shooting, "fanProjectileCount", 7); Set(shooting, "shotGunMode", (int)EnemyProjectileShotGunMode.ShotGun);
                    Set(shooting, "shotGunProjectileCount", 3); Enable(shooting, "baseOffsetXRandom.enabled");
                    shooting.FindPropertyRelative("baseOffsetXRandom.min").floatValue = -.3f;
                    shooting.FindPropertyRelative("baseOffsetXRandom.max").floatValue = .75f;
                }
                if (mode == 2)
                {
                    Enable(shooting, "layoutRotationEnabled"); Set(shooting, "emissionLayout.type", (int)EnemyEmissionLayoutType.Sector);
                    Set(shooting, "emissionLayout.launchMode", (int)EnemySectorLaunchMode.Sequential);
                    Set(shooting, "emissionLayout.launchOrder", (int)EnemySectorLaunchOrder.PingPong);
                }
                if (mode == 3)
                {
                    Set(shooting, "projectileMovementPattern", (int)EnemyProjectileMovementPattern.Orbit);
                    Set(shooting, "projectileOrbitCenterMode", (int)EnemyProjectileOrbitCenterMode.DnaLike);
                    Set(shooting, "projectileSpeedBehavior", (int)EnemyProjectileSpeedBehavior.Customized);
                    Set(shooting, "projectileSizeBehavior", (int)EnemyProjectileSizeBehavior.Customized);
                    Enable(shooting, "overrideProjectileLifetime");
                }
                if (mode == 4) Set(shooting, "projectileType", (int)EnemyProjectileType.Composite);
                if (mode == 5)
                {
                    Set(shooting, "projectileType", (int)EnemyProjectileType.Laser);
                    Enable(shooting, "laser.enableTelegraph"); Enable(shooting, "laser.overrideTelegraphGeometry");
                }
                if (mode >= 6)
                {
                    Enable(shooting, "useChildAttackPattern");
                    shooting.FindPropertyRelative("childAttack").managedReferenceValue = EnemyChildAttackSettings.CreateDefault();
                    var child = shooting.FindPropertyRelative("childAttack");
                    Set(child, "triggerMode", (int)EnemyChildAttackTriggerMode.OnEvent);
                    Set(child, "eventType", (int)EnemyChildAttackEvent.OnScreenBoundaryReached);
                    Set(child, "repeatMode", mode == 6 ? (int)EnemyChildAttackRepeatMode.FixedCount : (int)EnemyChildAttackRepeatMode.UntilParentDestroyed);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                string json = EditorJsonUtility.ToJson(host);
                CheckFields(shooting);
                bool heightChecked = false;
                window.Draw = () =>
                {
                    data.Update();
                    float height = EnemyShootingSettingsDrawer.SettingsHeight(shooting, timing, true);
                    Rect rect = EditorGUILayout.GetControlRect(false, height);
                    float start = rect.y;
                    EnemyShootingSettingsDrawer.DrawSettings(ref rect, shooting, timing, true);
                    if (Event.current.type == EventType.Repaint && !heightChecked)
                    {
                        Check(Mathf.Abs(rect.y - start - height) < .1f, "Reserved and drawn height differ in mode " + mode);
                        heightChecked = true;
                    }
                    data.ApplyModifiedProperties();
                };
                await Paint(window);
                Check(heightChecked && EditorJsonUtility.ToJson(host) == json, "Repaint altered serialized settings in mode " + mode);
                scenarios.Add("Shooting configuration " + mode);
            }
            // Close one section, recreate the SerializedObject (selection-style rebuild), reopen it.
            var layout = shooting.FindPropertyRelative("emissionLayout");
            float expandedHeight = EditorGUI.GetPropertyHeight(layout, true);
            SessionState.SetBool(Key(layout, "Settings"), false);
            using (var rebuilt = new SerializedObject(host))
                Check(EditorGUI.GetPropertyHeight(rebuilt.FindProperty(layout.propertyPath), true) < expandedHeight, "Foldout state lost on Inspector rebuild");
            SessionState.SetBool(Key(layout, "Settings"), true);
            Check(Mathf.Abs(EditorGUI.GetPropertyHeight(layout, true) - expandedHeight) < .1f, "Foldout failed to reopen");

            // Linked preset values stay untouched; the same controls still render their resolved view.
            shooting.FindPropertyRelative("projectilePreset").objectReferenceValue = projectilePreset;
            Set(shooting, "projectilePresetMode", (int)EnemyPresetUseMode.Linked);
            shooting.FindPropertyRelative("attackPatternPreset").objectReferenceValue = patternPreset;
            Set(shooting, "attackPatternPresetMode", (int)EnemyPresetUseMode.Linked);
            data.ApplyModifiedPropertiesWithoutUndo();
            await Paint(window);
            Check(EnemyPresetInspector.Linked(shooting, true) && EnemyPresetInspector.Linked(shooting, false), "Linked preset mode changed");
            scenarios.Add("Linked presets and session foldouts");

            // Render existing wave/component inspectors on an unsaved prefab instance in a PreviewScene.
            GameObject prefab = null;
            foreach (string guid in AssetDatabase.FindAssets("2_2_extrime t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == "2_2_extrime") { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); break; }
            }
            Check(prefab != null, "Reference wave prefab missing");
            string prefabJson = EditorJsonUtility.ToJson(prefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var kinds = new HashSet<Type> { typeof(Wave), typeof(DirectedEnemySubWave), typeof(DirectedWaveFormationBehaviour),
                typeof(DirectedWavePhaseEntryBehaviour), typeof(DirectedWavePostBehaviour), typeof(DirectedWaveAttackBehaviour), typeof(WaveAttackCoordinator) };
            foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || !kinds.Remove(component.GetType())) continue;
                var editor = Editor.CreateEditor(component); editors.Add(editor);
                string before = EditorJsonUtility.ToJson(component);
                window.Draw = editor.OnInspectorGUI;
                await Paint(window);
                Check(EditorJsonUtility.ToJson(component) == before, "Inspector changed " + component.GetType().Name);
                scenarios.Add(component.GetType().Name + " Inspector repaint");
            }
            // This reference wave uses legacy data; also exercise the component wrappers
            // that delegate Formation / Entry / Post editing to the shared inspector.
            var subwave = instance.GetComponentInChildren<DirectedEnemySubWave>(true);
            foreach (var type in new[] { typeof(DirectedWaveFormationBehaviour), typeof(DirectedWavePhaseEntryBehaviour), typeof(DirectedWavePostBehaviour) })
            {
                var component = subwave.GetComponent(type) ?? subwave.gameObject.AddComponent(type);
                var editor = Editor.CreateEditor(component); editors.Add(editor);
                string before = EditorJsonUtility.ToJson(component);
                window.Draw = editor.OnInspectorGUI;
                await Paint(window);
                Check(EditorJsonUtility.ToJson(component) == before, "Wrapper Inspector changed " + type.Name);
                scenarios.Add(type.Name + " Inspector repaint");
            }
            Check(!kinds.Contains(typeof(Wave)) && !kinds.Contains(typeof(DirectedWaveAttackBehaviour)), "Reference inspectors were not exercised");
            var attack = instance.GetComponentInChildren<DirectedWaveAttackBehaviour>(true);
            using (var sequence = new SerializedObject(attack))
            {
                sequence.FindProperty("executionMode").intValue = (int)DirectedWaveAttackExecutionMode.Sequence;
                var steps = sequence.FindProperty("attackSequence.steps");
                steps.arraySize = 2;
                for (int i = 0; i < steps.arraySize; i++)
                {
                    var step = steps.GetArrayElementAtIndex(i);
                    step.managedReferenceValue = new DirectedWaveAttackSequenceStep();
                    step.isExpanded = true;
                }
                Set(steps.GetArrayElementAtIndex(1), "type", (int)DirectedWaveAttackSequenceStepType.RepeatBlock);
                var children = steps.GetArrayElementAtIndex(1).FindPropertyRelative("steps");
                children.arraySize = 1;
                children.GetArrayElementAtIndex(0).managedReferenceValue = new DirectedWaveAttackSequenceStep();
                sequence.ApplyModifiedPropertiesWithoutUndo();
                var editor = Editor.CreateEditor(attack); editors.Add(editor);
                string before = EditorJsonUtility.ToJson(attack);
                window.Draw = editor.OnInspectorGUI;
                await Paint(window);
                Check(EditorJsonUtility.ToJson(attack) == before, "Sequence Inspector changed its settings");
                scenarios.Add("Sequence and Repeat Block Inspector repaint");
            }
            using (var edit = new SerializedObject(attack))
            {
                const string path = "attackSettings.waveBurstSettings.shooting.numericRandomSeed";
                var field = edit.FindProperty(path); int original = field.intValue;
                Undo.IncrementCurrentGroup();
                field.intValue = original + 1; edit.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(attack);
                edit.Update();
                Check(edit.FindProperty(path).prefabOverride, "Prefab Override was not retained");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); edit.Update();
                Check(edit.FindProperty(path).intValue == original, "Undo failed");
                Undo.PerformRedo(); edit.Update();
                Check(edit.FindProperty(path).intValue == original + 1, "Redo failed");
                Undo.ClearUndo(attack);
            }
            Check(EditorJsonUtility.ToJson(prefab) == prefabJson, "Source prefab was altered");
            return new { passed = true, checks, scenarios = scenarios.ToArray(), scope = "Editor-only; no runtime or asset changes" };
        }
        finally
        {
            window.Draw = null; window.Close(); Object.DestroyImmediate(window);
            foreach (var editor in editors) Object.DestroyImmediate(editor);
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(host); Object.DestroyImmediate(projectilePreset); Object.DestroyImmediate(patternPreset);
        }
    }

    private static void CheckFields(SerializedProperty shooting)
    {
        foreach (string method in new[] { "BehaviourFields", "ProjectileFields" })
        {
            var fields = (IEnumerable<string>)typeof(EnemyShootingSettingsDrawer).GetMethod(method, PrivateStatic).Invoke(null, new object[] { shooting });
            var unique = new HashSet<string>();
            foreach (string name in fields)
            {
                if (name[0] == '#') continue;
                if (!unique.Add(name) || shooting.FindPropertyRelative(name) == null) throw new Exception("Invalid/duplicate UI field " + name);
            }
            Check(unique.Count > 0, "No fields in " + method);
        }
    }
    private static async Task Paint(WaveInspectorOrganizationWindow window)
    {
        int frame = window.Repaints;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (window.Repaints == frame && watch.ElapsedMilliseconds < 3000) { window.Repaint(); await Task.Delay(30); }
        if (window.Error != null) throw window.Error;
        if (window.Repaints == frame) throw new Exception("IMGUI repaint timeout");
    }
}
