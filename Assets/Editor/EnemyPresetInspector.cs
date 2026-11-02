using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Shared integration for Single, Multiple, local overrides, Sequence and nested child patterns.
[InitializeOnLoad]
public static class EnemyPresetInspector
{
    [Serializable] private sealed class ChildBuffer { [SerializeReference] public EnemyChildAttackSettings childAttack = new(); }
    private sealed class Buffer : ScriptableObject
    {
        public EnemyBurstAttackSettings configuration = new();
        public ChildBuffer wrapper = new();
        [NonSerialized] public Object RootOwner;
        [NonSerialized] public Cached Cache;
    }
    private readonly struct Key : IEquatable<Key>
    {
        private readonly int owner, timingOwner;
        private readonly string shooting, timing;
        public Key(SerializedProperty source, SerializedProperty context)
        {
            owner = source.serializedObject.targetObject.GetInstanceID();
            timingOwner = context == null ? 0 : context.serializedObject.targetObject.GetInstanceID();
            shooting = source.propertyPath;
            timing = context?.propertyPath;
        }
        public bool Equals(Key other) => owner == other.owner && timingOwner == other.timingOwner
            && shooting == other.shooting && timing == other.timing;
        public override bool Equals(object obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(owner, timingOwner, shooting, timing);
    }
    private sealed class Cached
    {
        public Object Owner, TimingOwner, RootOwner;
        public int Dirty, TimingDirty, PresetRevision, Revision;
        public uint ContentHash;
        public ulong LastUse;
        public EnemyBurstAttackSettings Profile;
        public string Root = "configuration";
        public Buffer Data;
        public SerializedObject Serialized;
        public SerializedProperty Shooting, Timing;
        public bool NeedsSerialization = true;
    }
    private const int MaximumCachedContexts = 32;
    private static readonly Dictionary<Key, Cached> buffers = new();
    private static readonly List<Key> removed = new();
    private static int revision;
    private static ulong lastUse;
    private static bool trimScheduled;
    private static readonly Unity.Profiling.ProfilerMarker CreateMarker = new("WaveEditor.Presets.CreateBuffer");
    private static readonly Unity.Profiling.ProfilerMarker RebuildMarker = new("WaveEditor.Presets.Rebuild");
    private static float Line => EditorGUIUtility.singleLineHeight + 2f;
    static EnemyPresetInspector()
    {
        AssemblyReloadEvents.beforeAssemblyReload += ClearBuffers;
        EditorApplication.quitting += ClearBuffers;
        Selection.selectionChanged += ScheduleTrim;
        EditorApplication.hierarchyChanged += ScheduleTrim;
        EditorApplication.projectChanged += Invalidate;
        PrefabUtility.prefabInstanceUpdated += OnPrefabUpdated;
        Undo.undoRedoPerformed += () => { EnemyPresetRevision.Changed(); Invalidate(); SceneView.RepaintAll(); };
    }
    private static void OnPrefabUpdated(GameObject instance) => Invalidate();
    private static void Invalidate() { unchecked { revision++; } ScheduleTrim(); }
    private static void ClearBuffers()
    {
        EditorApplication.delayCall -= Trim;
        trimScheduled = false;
        foreach (var cached in buffers.Values) Dispose(cached);
        buffers.Clear();
    }
    private static void Dispose(Cached cached)
    {
        cached.Shooting?.Dispose(); cached.Timing?.Dispose();
        cached.Serialized?.Dispose();
        if (cached.Data != null) Object.DestroyImmediate(cached.Data);
    }
    private static void ScheduleTrim()
    {
        if (trimScheduled) return;
        trimScheduled = true;
        EditorApplication.delayCall += Trim;
    }
    // Trim between GUI events, never while a nested drawer is borrowing a property.
    private static void Trim()
    {
        EditorApplication.delayCall -= Trim;
        trimScheduled = false;
        removed.Clear();
        foreach (var pair in buffers)
            if (pair.Value.Owner == null || pair.Value.RootOwner == null) removed.Add(pair.Key);
        foreach (var key in removed) { Dispose(buffers[key]); buffers.Remove(key); }
        removed.Clear();
        while (buffers.Count > MaximumCachedContexts)
        {
            bool found = false;
            Key oldest = default;
            ulong used = ulong.MaxValue;
            foreach (var pair in buffers)
            {
                Object root = pair.Value.RootOwner;
                bool selected = Selection.Contains(root)
                    || root is Component component && Selection.Contains(component.gameObject);
                if (selected || pair.Value.LastUse >= used) continue;
                oldest = pair.Key; used = pair.Value.LastUse; found = true;
            }
            if (!found) break; // A large selected Inspector must remain fully usable.
            Dispose(buffers[oldest]); buffers.Remove(oldest);
        }
    }
    private static string Field(bool projectile) => projectile ? "projectilePreset" : "attackPatternPreset";
    private static string Mode(bool projectile) => Field(projectile) + "Mode";
    private static string Overrides(bool projectile) => Field(projectile) + "Overrides";
    private static bool IsAssetRoot(SerializedProperty shooting) => shooting.serializedObject.targetObject is EnemyReusablePreset
        && !shooting.propertyPath.Contains("childAttack");
    public static bool Linked(SerializedProperty shooting, bool projectile) => !IsAssetRoot(shooting)
        && shooting.FindPropertyRelative(Field(projectile)).objectReferenceValue != null
        && shooting.FindPropertyRelative(Mode(projectile)).intValue == (int)EnemyPresetUseMode.Linked;

    private static readonly Unity.Profiling.ProfilerMarker ProfileReadProfile = new("WaveEditor.Presets.ReadProfile");
    public static EnemyBurstAttackSettings ReadProfile(SerializedProperty shooting, SerializedProperty timing)
    {
        using var profileScope = ProfileReadProfile.Auto();
        var profile = new EnemyBurstAttackSettings();
        // boxedValue can share managed-reference children with the authored object.
        // Detach the serialized graph before validation; CopyFrom validates its source.
        // This round-trip occurs only when materializing a changed view or an explicit action.
        string json = JsonUtility.ToJson((timing ?? shooting).boxedValue);
        if (timing != null) JsonUtility.FromJsonOverwrite(json, profile);
        else JsonUtility.FromJsonOverwrite(json, profile.AuthoredShooting);
        profile.Validate();
        return profile;
    }

    private static readonly Unity.Profiling.ProfilerMarker ProfileDisplay = new("WaveEditor.Presets.Display");
    private static Cached Display(SerializedProperty shooting, SerializedProperty timing)
    {
        using var profileScope = ProfileDisplay.Auto();
        var owner = shooting.serializedObject.targetObject;
        var timingOwner = timing?.serializedObject.targetObject;
        var key = new Key(shooting, timing);
        buffers.TryGetValue(key, out var cached);
        if (cached != null && !ReferenceEquals(cached.Owner, owner))
        {
            Dispose(cached); buffers.Remove(key); cached = null;
        }
        if (cached == null)
        {
            cached = new Cached { Owner = owner, TimingOwner = timingOwner,
                RootOwner = owner is Buffer parent ? parent.RootOwner : owner };
            buffers.Add(key, cached);
            if (buffers.Count > MaximumCachedContexts) ScheduleTrim();
        }
        cached.LastUse = ++lastUse;
        // Public native hash also catches direct field edits followed by SerializedObject.Update,
        // without materializing managed graphs or allocating a JSON string.
        uint contentHash = (timing ?? shooting).contentHash;
        int dirty = EditorUtility.GetDirtyCount(owner);
        int timingDirty = timingOwner != null ? EditorUtility.GetDirtyCount(timingOwner) : 0;
        // Unapplied changes have not incremented the object's dirty count yet.
        // Re-read them rather than serving stale values within the same IMGUI event.
        bool pending = shooting.serializedObject.hasModifiedProperties
            || timing != null && timing.serializedObject.hasModifiedProperties;
        if (cached.Profile == null || pending || cached.ContentHash != contentHash || cached.Dirty != dirty || cached.TimingDirty != timingDirty
            || cached.PresetRevision != EnemyPresetRevision.Value || cached.Revision != revision)
        {
            using var rebuildSample = RebuildMarker.Auto();
            cached.Profile = ReadProfile(shooting, timing).ResolvePresets();
            cached.Root = EnemyChildAttackSettingsDrawer.IsChildPattern(shooting) ? "wrapper.childAttack.pattern" : "configuration";
            cached.ContentHash = contentHash; cached.Dirty = dirty; cached.TimingDirty = timingDirty;
            cached.PresetRevision = EnemyPresetRevision.Value; cached.Revision = revision;
            cached.NeedsSerialization = true;
        }
        return cached;
    }
    private static void EnsureSerialized(Cached cached)
    {
        if (cached.Data == null)
        {
            using var createSample = CreateMarker.Auto();
            cached.Data = ScriptableObject.CreateInstance<Buffer>();
            cached.Data.hideFlags = HideFlags.HideAndDontSave;
            cached.Data.RootOwner = cached.RootOwner;
            cached.Data.Cache = cached;
            cached.Serialized = new SerializedObject(cached.Data);
        }
        if (!cached.NeedsSerialization) return;
        cached.Shooting?.Dispose(); cached.Timing?.Dispose();
        cached.Data.configuration = cached.Profile;
        if (cached.Root != "configuration") cached.Data.wrapper.childAttack.Pattern.CopyFrom(cached.Profile);
        EditorUtility.SetDirty(cached.Data); // Invalidates contexts borrowing nested buffer fields.
        cached.Serialized.Update();
        cached.Timing = cached.Serialized.FindProperty(cached.Root);
        cached.Shooting = cached.Timing.FindPropertyRelative("shooting");
        cached.NeedsSerialization = false;
    }
    public static SerializedProperty DisplayShooting(SerializedProperty shooting, SerializedProperty timing)
    {
        if (shooting.serializedObject.isEditingMultipleObjects || IsAssetRoot(shooting)) return shooting;
        var cached = Display(shooting, timing);
        EnsureSerialized(cached);
        return cached.Shooting.Copy();
    }
    public static SerializedProperty DisplayTiming(SerializedProperty shooting, SerializedProperty timing)
    {
        if (timing == null || shooting.serializedObject.isEditingMultipleObjects || IsAssetRoot(shooting)) return timing;
        var cached = Display(shooting, timing);
        EnsureSerialized(cached);
        return cached.Timing.Copy();
    }
    // Read-only access avoids boxedValue and does not allocate a ScriptableObject or
    // SerializedObject for Scene handles and informational timing summaries.
    public static EnemyBurstAttackSettings DisplayProfile(SerializedProperty shooting, SerializedProperty timing)
    {
        if (shooting.serializedObject.isEditingMultipleObjects) return ReadProfile(shooting, timing).ResolvePresets();
        if (shooting.serializedObject.targetObject is Buffer buffer
            && timing != null && timing.propertyPath == buffer.Cache.Root
            && !buffer.Cache.NeedsSerialization && !shooting.serializedObject.hasModifiedProperties)
            return buffer.Cache.Profile;
        return Display(shooting, timing).Profile;
    }
    public static EnemyShootingSettings DisplaySettings(SerializedProperty shooting, SerializedProperty timing)
        => DisplayProfile(shooting, timing).Shooting;

    public static float Height(SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        if (IsAssetRoot(shooting)) return 0f;
        float result = 4 * Line;
        var preset = shooting.FindPropertyRelative(Field(projectile)).objectReferenceValue;
        if (!projectile && preset is EnemyAttackPatternPreset { SuggestedProjectile: not null }) result += Line;
        if (Linked(shooting, projectile))
        {
            result += Line;
            var overrides = shooting.FindPropertyRelative(Overrides(projectile));
            for (int i = 0; i < overrides.arraySize; i++)
            {
                var local = FindLocal(shooting, timing, projectile, overrides.GetArrayElementAtIndex(i).stringValue);
                result += local == null ? Line : EditorGUI.GetPropertyHeight(local, true) + 2f;
            }
            result += Line;
        }
        return result;
    }

    public static void Draw(ref Rect position, SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        if (IsAssetRoot(shooting)) return;
        var choice = shooting.FindPropertyRelative(Field(projectile));
        position.height = EditorGUIUtility.singleLineHeight;
        using (new EditorGUI.DisabledScope(shooting.serializedObject.isEditingMultipleObjects))
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, choice, new GUIContent(projectile ? "Projectile Preset" : "Attack Pattern Preset"));
            if (EditorGUI.EndChangeCheck() && choice.objectReferenceValue != null) Apply(shooting, timing, projectile);
            position.y += Line;
            var mode = shooting.FindPropertyRelative(Mode(projectile));
            int oldMode = mode.intValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, mode, new GUIContent("Use Mode", "Linked inherits asset updates; Copy remains independent until Apply/Revert is used."));
            if (EditorGUI.EndChangeCheck() && choice.objectReferenceValue != null)
            {
                if (mode.intValue == (int)EnemyPresetUseMode.Copy && oldMode == (int)EnemyPresetUseMode.Linked)
                {
                    var asset = choice.objectReferenceValue;
                    mode.intValue = oldMode;
                    Unlink(shooting, timing, projectile);
                    choice.objectReferenceValue = asset; mode.intValue = (int)EnemyPresetUseMode.Copy;
                    shooting.serializedObject.ApplyModifiedProperties(); EnemyPresetRevision.Changed();
                }
                else Apply(shooting, timing, projectile);
            }
            position.y += Line;
            Rect row = EditorGUI.IndentedRect(position);
            float width = (row.width - 4f) / 3f;
            Button(row, 0, "Save As New", () => SaveNew(shooting, timing, projectile));
            using (new EditorGUI.DisabledScope(choice.objectReferenceValue == null))
            {
                Button(row, 1, "Apply Preset", () => Apply(shooting, timing, projectile));
                Button(row, 2, "Update Preset", () => UpdatePreset(shooting, timing, projectile));
            }
            position.y += Line; row.y = position.y;
            using (new EditorGUI.DisabledScope(choice.objectReferenceValue == null))
            {
                Button(row, 0, "Revert", () => Apply(shooting, timing, projectile));
                Button(row, 1, "Unlink", () => Unlink(shooting, timing, projectile));
            }
            Button(row, 2, "Library", () => EnemyPresetLibraryWindow.Open(shooting, timing, projectile));
            position.y += Line;
            if (!projectile && choice.objectReferenceValue is EnemyAttackPatternPreset { SuggestedProjectile: not null } pattern)
            {
                if (GUI.Button(EditorGUI.IndentedRect(position), "Apply linked Projectile Preset: " + pattern.SuggestedProjectile.DisplayName))
                {
                    shooting.FindPropertyRelative("projectilePreset").objectReferenceValue = pattern.SuggestedProjectile;
                    Apply(shooting, timing, true);
                }
                position.y += Line;
            }
            if (Linked(shooting, projectile)) DrawOverrides(ref position, shooting, timing, projectile);

            void Button(Rect rect, int index, string text, Action action)
            {
                rect.x += index * (width + 2f); rect.width = width;
                if (GUI.Button(rect, text, EditorStyles.miniButton)) action();
            }
        }
    }

    private static void DrawOverrides(ref Rect position, SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        var overrides = shooting.FindPropertyRelative(Overrides(projectile));
        EditorGUI.LabelField(position, "Local Overrides (inherited values below are read-only)", EditorStyles.miniBoldLabel);
        position.y += Line;
        for (int i = 0; i < overrides.arraySize; i++)
        {
            string path = overrides.GetArrayElementAtIndex(i).stringValue;
            var field = FindLocal(shooting, timing, projectile, path);
            position.height = field == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(field, true);
            Rect value = position; value.width -= 50f;
            if (field != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(value, field, new GUIContent("● " + ObjectNames.NicifyVariableName(path), path), true);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (string suffix in new[] { "Random", "XRandom", "YRandom" })
                    {
                        string companion = path + suffix;
                        if (FindLocal(shooting, timing, projectile, companion) != null && !HasOverride(overrides, companion))
                        { overrides.arraySize++; overrides.GetArrayElementAtIndex(overrides.arraySize - 1).stringValue = companion; }
                    }
                }
            }
            else EditorGUI.LabelField(value, "Unknown parameter: " + path);
            Rect reset = new(position.xMax - 48f, position.y, 48f, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(reset, "Reset", EditorStyles.miniButton))
            { overrides.DeleteArrayElementAtIndex(i); shooting.serializedObject.ApplyModifiedProperties(); EnemyPresetRevision.Changed(); break; }
            position.y += position.height + 2f;
        }
        position.height = EditorGUIUtility.singleLineHeight;
        if (GUI.Button(EditorGUI.IndentedRect(position), "Override Parameter…", EditorStyles.miniButton))
            ShowOverrideMenu(shooting, timing, projectile);
        position.y += Line;
    }

    private static SerializedProperty FindLocal(SerializedProperty shooting, SerializedProperty timing, bool projectile, string path)
    {
        if (projectile) return shooting.FindPropertyRelative(path);
        return path.StartsWith("shooting.", StringComparison.Ordinal) ? shooting.FindPropertyRelative(path.Substring(9)) : timing?.FindPropertyRelative(path);
    }

    private static void ShowOverrideMenu(SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        var cached = Display(shooting, timing);
        EnsureSerialized(cached);
        var display = cached.Serialized.FindProperty(cached.Root);
        var source = display.FindPropertyRelative("shooting");
        var menu = new GenericMenu();
        foreach (string name in projectile ? EnemyPresetFields.Projectile : EnemyPresetFields.Layout)
            if (name != "independentProjectileSpeed" && name != "projectilePatternVersion" && name != "emissionLayoutVersion" && name != "shootingType" && name != "rotate" && name != "directionCount")
                Add(source.FindPropertyRelative(name), projectile ? name : "shooting." + name);
        if (projectile) Add(source.FindPropertyRelative("childAttack"), "childAttack", true);
        else if (timing != null) foreach (string name in EnemyPresetFields.Timing) Add(display.FindPropertyRelative(name), name);
        menu.ShowAsContext();

        void Add(SerializedProperty field, string path, bool atomic = false)
        {
            if (field == null) return;
            if (!atomic && field.propertyType == SerializedPropertyType.Generic && field.hasVisibleChildren && !field.isArray)
            {
                var child = field.Copy(); var end = child.GetEndProperty();
                bool enter = true;
                while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                { enter = false; Add(child.Copy(), path + "." + child.name); }
                return;
            }
            string captured = path;
            var data = (EnemyShootingSettings)shooting.boxedValue;
            bool exists = EnemyPresetFields.Contains(projectile ? data.ProjectilePresetOverrides : data.AttackPatternPresetOverrides, path);
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(path.Replace('.', '/'))), exists, () =>
            {
                var local = FindLocal(shooting, timing, projectile, captured);
                var inherited = projectile ? source.FindPropertyRelative(captured)
                    : display.FindPropertyRelative(captured);
                if (local == null || inherited == null) return;
                Undo.RecordObjects(shooting.serializedObject.targetObjects, "Override preset parameter");
                CopyValue(local, inherited);
                var list = shooting.FindPropertyRelative(Overrides(projectile));
                if (!exists) { list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = captured; }
                shooting.serializedObject.ApplyModifiedProperties(); EnemyPresetRevision.Changed(); SceneView.RepaintAll();
            });
        }
    }

    public static void Apply(SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        shooting.serializedObject.ApplyModifiedProperties();
        var profile = ReadProfile(shooting, timing);
        var data = profile.AuthoredShooting;
        var preset = projectile ? (EnemyReusablePreset)data.ProjectilePreset : data.AttackPatternPreset;
        if (preset == null) return;
        if (EnemyPresetDependencies.HasCycle(preset))
        {
            shooting.FindPropertyRelative(Field(projectile)).objectReferenceValue = null;
            shooting.serializedObject.ApplyModifiedProperties();
            Debug.LogWarning("Cannot apply a cyclic projectile/pattern preset.", preset); return;
        }
        if (projectile) data.LinkProjectile((EnemyProjectilePreset)preset, data.ProjectilePresetMode);
        else profile.ApplyPatternPreset((EnemyAttackPatternPreset)preset, data.AttackPatternPresetMode);
        WriteCategory(shooting, timing, profile, projectile);
    }

    public static void WriteCategory(SerializedProperty shooting, SerializedProperty timing, EnemyBurstAttackSettings profile, bool projectile)
    {
        Undo.RecordObjects(shooting.serializedObject.targetObjects, "Apply reusable attack preset");
        var buffer = ScriptableObject.CreateInstance<Buffer>();
        try
        {
            buffer.configuration = profile;
            var so = new SerializedObject(buffer); var root = so.FindProperty("configuration");
            var src = root.FindPropertyRelative("shooting");
            foreach (string name in projectile ? EnemyPresetFields.Projectile : EnemyPresetFields.Layout)
                CopyValue(shooting.FindPropertyRelative(name), src.FindPropertyRelative(name));
            if (projectile) CopyValue(shooting.FindPropertyRelative("childAttack"), src.FindPropertyRelative("childAttack"));
            foreach (string name in new[] { Field(projectile), Mode(projectile), Overrides(projectile) })
                CopyValue(shooting.FindPropertyRelative(name), src.FindPropertyRelative(name));
            if (!projectile && timing != null) foreach (string name in EnemyPresetFields.Timing)
                CopyValue(timing.FindPropertyRelative(name), root.FindPropertyRelative(name));
            shooting.serializedObject.ApplyModifiedProperties(); EnemyPresetRevision.Changed(); SceneView.RepaintAll();
        }
        finally { Object.DestroyImmediate(buffer); }
    }

    private static void CopyValue(SerializedProperty to, SerializedProperty from)
    {
        if (to == null || from == null) return;
        if (from.propertyType == SerializedPropertyType.ManagedReference)
        { to.managedReferenceValue = EnemyChildAttackSettings.Copy(from.managedReferenceValue as EnemyChildAttackSettings); return; }
        if (from.isArray && from.propertyType != SerializedPropertyType.String)
        { to.arraySize = from.arraySize; for (int i = 0; i < from.arraySize; i++) CopyValue(to.GetArrayElementAtIndex(i), from.GetArrayElementAtIndex(i)); return; }
        if (from.propertyType == SerializedPropertyType.Generic)
        {
            var child = from.Copy(); var end = child.GetEndProperty(); bool enter = true;
            while (child.Next(enter) && !SerializedProperty.EqualContents(child, end))
            { enter = false; CopyValue(to.FindPropertyRelative(child.name), child); }
            return;
        }
        to.boxedValue = from.boxedValue;
    }

    private static void Unlink(SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        shooting.serializedObject.ApplyModifiedProperties();
        var profile = ReadProfile(shooting, timing); profile.UnlinkPreset(projectile);
        WriteCategory(shooting, timing, profile, projectile);
    }
    private static void SaveNew(SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        string path = EditorUtility.SaveFilePanelInProject("Save reusable preset", projectile ? "ProjectilePreset" : "AttackPatternPreset", "asset", "Choose a preset asset location.");
        if (string.IsNullOrEmpty(path)) return;
        shooting.serializedObject.ApplyModifiedProperties();
        var profile = ReadProfile(shooting, timing).ResolvePresets();
        EnemyReusablePreset asset = projectile ? ScriptableObject.CreateInstance<EnemyProjectilePreset>() : ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();
        SetAsset(asset, profile, shooting); AssetDatabase.CreateAsset(asset, path); Undo.RegisterCreatedObjectUndo(asset, "Create reusable preset");
        AssetDatabase.SaveAssetIfDirty(asset);
        shooting.FindPropertyRelative(Field(projectile)).objectReferenceValue = asset;
        Apply(shooting, timing, projectile);
    }
    private static void UpdatePreset(SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        shooting.serializedObject.ApplyModifiedProperties();
        var asset = shooting.FindPropertyRelative(Field(projectile)).objectReferenceValue as EnemyReusablePreset;
        if (asset == null) return;
        var profile = ReadProfile(shooting, timing).ResolvePresets();
        string before = EditorJsonUtility.ToJson(asset);
        Undo.RecordObject(asset, "Update reusable preset"); SetAsset(asset, profile, shooting);
        if (EnemyPresetDependencies.HasCycle(asset))
        { EditorJsonUtility.FromJsonOverwrite(before, asset); Debug.LogWarning("Update rejected: cyclic preset references.", asset); return; }
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); SceneView.RepaintAll();
    }
    private static void SetAsset(EnemyReusablePreset asset, EnemyBurstAttackSettings profile, SerializedProperty shooting)
    {
        if (asset is EnemyProjectilePreset projectile)
        {
            var data = new EnemyShootingSettings(); data.CopyFrom(profile.Shooting);
            if (data.ResolveProjectilePrefab(null) == null)
            {
                var prefab = ProjectileContext(shooting, data.ProjectileType);
                if (prefab != null) JsonUtility.FromJsonOverwrite("{\"projectilePrefab\":{\"instanceID\":" + prefab.GetInstanceID() + "}}", data);
            }
            projectile.SetSettings(data);
        }
        else ((EnemyAttackPatternPreset)asset).SetSettings(profile, profile.Shooting.PresetFiring.enabled ? profile.Shooting.PresetFiring : FiringContext(shooting));
    }

    private static EnemyPresetFiringSettings FiringContext(SerializedProperty shooting)
    {
        string path = shooting.propertyPath;
        while (path.LastIndexOf('.') is int dot && dot >= 0)
        {
            path = path.Substring(0, dot);
            var owner = shooting.serializedObject.FindProperty(path);
            if (owner == null) continue;
            var fire = owner.FindPropertyRelative("fireMode");
            if (fire == null) continue;
            var result = new EnemyPresetFiringSettings { enabled = true, fireMode = (DirectedWaveAttackFireMode)fire.intValue };
            var aim = owner.FindPropertyRelative("aimMode"); if (aim != null) result.aimMode = (DirectedWaveAttackAimMode)aim.intValue;
            if (owner.FindPropertyRelative("stationaryFireSettings")?.boxedValue is EnemyStationaryFireSettings stationary) result.stationaryFireSettings.CopyFrom(stationary);
            var angle = owner.FindPropertyRelative("forwardFireHalfAngle"); if (angle != null) result.forwardFireHalfAngle = angle.floatValue;
            return result;
        }
        return new EnemyPresetFiringSettings { enabled = true, fireMode = DirectedWaveAttackFireMode.Default };
    }

    private static bool HasOverride(SerializedProperty overrides, string path)
    {
        for (int i = 0; i < overrides.arraySize; i++)
            if (overrides.GetArrayElementAtIndex(i).stringValue == path) return true;
        return false;
    }

    private static EnemyBullet ProjectileContext(SerializedProperty shooting, EnemyProjectileType type)
    {
        var component = shooting.serializedObject.targetObject as Component;
        var executor = component != null ? component.GetComponent<SimpleEnemyAttackExecutor>() : null;
        if (executor == null && component != null)
        {
            var wave = component.GetComponentInParent<DirectedEnemySubWave>();
            if (wave != null && wave.GetConfiguredEnemySlotCount() > 0)
            {
                var enemy = wave.GetConfiguredEnemyPrefabForSlot(0);
                if (enemy != null) executor = enemy.GetComponent<SimpleEnemyAttackExecutor>();
            }
        }
        if (executor == null) return null;
        using var serialized = new SerializedObject(executor);
        string field = type == EnemyProjectileType.Homing ? "homingProjectilePrefab" : type == EnemyProjectileType.BurstAtPoint ? "burstAtPointProjectilePrefab" : "defaultProjectilePrefab";
        return serialized.FindProperty(field).objectReferenceValue as EnemyBullet;
    }
}
