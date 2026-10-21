using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyWavePreviewFastSeek
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static readonly List<object> measurements = new();
    private static object Get(object obj, string name) => obj.GetType().GetField(name, F).GetValue(obj);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    private static object Fixture(string name, params object[] args) => typeof(WavePreviewSnapshotFixtures).GetMethod(name, S).Invoke(null, args);
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Set(object obj, string name, object value)
    {
        for (var t = obj.GetType(); t != null; t = t.BaseType)
        {
            var field = t.GetField(name, F); if (field != null) { field.SetValue(obj, value); return; }
            var property = t.GetProperty(name, F); if (property != null) { property.SetValue(obj, value); return; }
        }
        throw new Exception("Missing " + name);
    }
    private sealed class Identity : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
    // Canonicalize every field saved by the production managed memento and every
    // native component/buffer/enable bit. Resource identity is external to simulation.
    private sealed class Fingerprint
    {
        private readonly Dictionary<object, int> ids = new(new Identity());
        private readonly Dictionary<Entity, string> entities = new();
        private readonly EntityManager manager;
        public int Components;
        public Fingerprint(EntityManager manager) => this.manager = manager;
        private string EntityKey(Entity entity)
        {
            if (entity == Entity.Null) return "null";
            if (entities.TryGetValue(entity, out var key)) return key;
            key = !manager.Exists(entity) ? "expired" : manager.HasComponent<EnemyProjectileMovement>(entity)
                ? JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(entity))
                    + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileMovement>(entity))
                : "service";
            entities.Add(entity, key); return key;
        }
        private string Value(object value)
        {
            if (value == null) return "null";
            if (value is Entity entity) return "Entity[" + EntityKey(entity) + "]";
            if (value is Object unity)
            {
                if (unity == null) return "destroyed:" + unity.GetType().FullName;
                string path = AssetDatabase.GetAssetPath(unity);
                return unity.GetType().Name + ":" + (path.Length > 0 ? path + "/" + unity.name : unity.name);
            }
            var type = value.GetType();
            if (type.Namespace == "Unity.Entities") return type.FullName; // World/query handles are external services.
            if (type.IsEnum || value is string || value is bool || value is char) return type.Name + ":" + value;
            if (value is IFormattable scalar && (type.IsPrimitive || value is decimal))
                return scalar.ToString(value is float || value is double ? "R" : null, CultureInfo.InvariantCulture);
            if (value is AnimationCurve curve) return JsonUtility.ToJson(new CurveWrapper { curve = curve });
            if (value is Delegate) return "delegate";
            if (ids.TryGetValue(value, out int id)) return "@" + id;
            if (type.IsValueType)
            {
                var text = new StringBuilder(type.Name + "{");
                foreach (var field in type.GetFields(F)) text.Append(field.Name).Append('=').Append(Value(field.GetValue(value))).Append(';');
                return text.Append('}').ToString();
            }
            if (value is IEnumerable enumerable)
            {
                var text = new StringBuilder("[");
                foreach (var item in enumerable) text.Append(Value(item)).Append(';');
                return text.Append(']').ToString();
            }
            return type.FullName;
        }
        [Serializable] private class CurveWrapper { public AnimationCurve curve; }
        public string Managed(object[] roots)
        {
            var type = typeof(WavePreviewPlayer).Assembly.GetType("WavePreviewStateGraph");
            object graph = type.GetMethod("Capture", S).Invoke(null, new object[] { roots });
            var nodes = (IList)Get(graph, "nodes");
            var lookup = new Dictionary<object, object>(new Identity());
            var canonical = new List<object>();
            foreach (var node in nodes) lookup[Get(node, "Target")] = node;
            string SortValue(object value, int depth)
            {
                if (value == null) return "null";
                var t = value.GetType();
                if (t.IsPrimitive || t.IsEnum || value is string || value is Entity || value is Object) return Value(value);
                if (depth == 0) return t.FullName;
                var result = new StringBuilder(t.FullName);
                if (t.IsValueType)
                    foreach (var field in t.GetFields(F)) result.Append(field.Name).Append(SortValue(field.GetValue(value), depth - 1));
                else if (lookup.TryGetValue(value, out var row) && Get(row, "Kind").ToString() == "Fields")
                    foreach (var child in (object[])Get(row, "Values")) result.Append(SortValue(child, depth - 1));
                return result.ToString();
            }
            object[] Values(object node)
            {
                var values = (object[])Get(node, "Values");
                string kind = Get(node, "Kind").ToString();
                if (kind == "Dictionary")
                {
                    var pairs = new List<KeyValuePair<string, int>>();
                    for (int i = 0; i < values.Length; i += 2)
                    {
                        // RemoveDeadLineages prunes these at the next Tick. With no
                        // queued Burst request they have no logical identity or future.
                        if (values[i] is Entity retired && !manager.Exists(retired) && values[i + 1]?.GetType().Name == "Lineage")
                        {
                            using var requests = manager.CreateEntityQuery(typeof(EnemyProjectileBurstSpawnRequest));
                            using var registries = requests.ToEntityArray(Allocator.Temp);
                            bool pending = false;
                            foreach (var registry in registries)
                                foreach (var request in manager.GetBuffer<EnemyProjectileBurstSpawnRequest>(registry, true))
                                    pending |= request.SourceProjectile == retired;
                            if (!pending) continue;
                        }
                        pairs.Add(new(Value(values[i]) + SortValue(values[i + 1], 4), i));
                    }
                    pairs.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
                    var sorted = new object[pairs.Count * 2]; int j = 0;
                    foreach (var pair in pairs) { sorted[j++] = values[pair.Value]; sorted[j++] = values[pair.Value + 1]; }
                    return sorted;
                }
                if (kind == "Set")
                {
                    var sorted = (object[])values.Clone();
                    Array.Sort(sorted, (a, b) => StringComparer.Ordinal.Compare(Value(a), Value(b))); return sorted;
                }
                return values;
            }
            void Assign(object value)
            {
                if (value == null || ids.ContainsKey(value)) return;
                if (lookup.TryGetValue(value, out var node))
                {
                    ids.Add(value, canonical.Count); canonical.Add(node);
                    foreach (var child in Values(node)) Assign(child);
                }
                else if (value.GetType().IsValueType && !(value is Entity) && !value.GetType().IsPrimitive
                    && !value.GetType().IsEnum && value.GetType().Namespace != "Unity.Entities")
                    foreach (var field in value.GetType().GetFields(F)) Assign(field.GetValue(value));
            }
            foreach (var root in roots) Assign(root);
            var result = new StringBuilder();
            foreach (var node in canonical)
            {
                result.Append(Get(node, "Target").GetType().FullName).Append(':').Append(Get(node, "Kind"));
                var fields = Get(node, "Fields") as FieldInfo[];
                var values = Values(node);
                for (int i = 0; i < values.Length; i++)
                    result.Append('|').Append(fields != null ? fields[i].Name : i.ToString()).Append('=').Append(Value(values[i]));
                result.AppendLine();
            }
            return result.ToString();
        }
        public string Native(EnemyProjectilePreviewWorld world)
        {
            using var query = (EntityQuery)Call(world, "SnapshotEntities");
            using var source = query.ToEntityArray(Allocator.Temp);
            var rows = new List<string>();
            foreach (var entity in source)
            {
                var row = new List<string>();
                using var types = manager.GetComponentTypes(entity, Allocator.Temp);
                foreach (var type in types)
                {
                    if (type.GetManagedType() == typeof(Entity)) continue;
                    string name = type.GetManagedType().FullName;
                    // GPU chunk culling data is uninitialized without an Entities Graphics
                    // world. RenderMeshArray contains per-world mesh/material instance IDs.
                    if (name == "Unity.Rendering.ChunkWorldRenderBounds" || name == "Unity.Rendering.EntitiesGraphicsChunkInfo"
                        || name == "Unity.Rendering.RenderMeshArray") continue;
                    Components++;
                    row.Add(type.GetManagedType().FullName + ":" + Value(manager.Debug.GetComponentBoxed(entity, type))
                        + (type.IsEnableable ? "/enabled=" + manager.IsComponentEnabled(entity, type) : ""));
                }
                row.Sort(StringComparer.Ordinal); rows.Add(string.Join("|", row));
            }
            rows.Sort(StringComparer.Ordinal); return string.Join("\n", rows);
        }
    }
    private static string FullState(WaveConductorShootingPreview simulation, IReadOnlyList<WavePreviewEvent> events)
    {
        var fingerprint = new Fingerprint(simulation.ProjectileWorld.SimulationWorld.EntityManager);
        var roots = new List<object> { Get(simulation, "coordinatorPlayer"), Get(simulation, "observedGroups"),
            Get(simulation, "finishedObservations"), typeof(EnemyProjectilePreviewWorld).GetProperty("SnapshotManagedRoot", F).GetValue(simulation.ProjectileWorld) };
        for (int i = 0; i < simulation.CueCount; i++) if (simulation.GetSession(i) != null) roots.Add(simulation.GetSession(i));
        string managed = fingerprint.Managed(roots.ToArray()), native = fingerprint.Native(simulation.ProjectileWorld);
        var text = new StringBuilder(simulation.StepCount + "|" + simulation.Omitted + "|" + simulation.ProjectileWorld.Omitted
            + "|" + simulation.ProjectileWorld.SimulationWorld.Time.ElapsedTime.ToString("R", CultureInfo.InvariantCulture)
            + "\n" + managed + native);
        foreach (var item in events) text.Append($"\n{item.Time:R}|{item.Kind}|{item.Cue}|{item.Slot}|{item.Count}|{item.Detail}");
        foreach (var root in roots)
            if (root is WaveShootingPreviewSession session)
            {
                var poses = (IDictionary)Get(session, "visualPoses");
                foreach (DictionaryEntry pose in poses)
                {
                    var target = (Transform)pose.Key;
                    text.Append("\nVisual:").Append(target.position.ToString("R")).Append(target.rotation.ToString("R")).Append(target.gameObject.activeSelf);
                }
            }
        return text.ToString();
    }
    private static void Equal(string a, string b, string message)
    {
        if (a != b)
        {
            File.WriteAllText("AgentArtifacts/fast-seek-expected.txt", a);
            File.WriteAllText("AgentArtifacts/fast-seek-actual.txt", b);
        }
        Check(a == b, message);
    }
    private static async Task Seek(WavePreviewPlayer player, float time)
    {
        player.Seek(time); int guard = 0;
        if (player.IsSeeking)
        {
            var done = new TaskCompletionSource<bool>();
            void Tick()
            {
                if (done.Task.IsCompleted) return;
                try
                {
                    Check(++guard < 10000, "Seek progresses");
                    int presentation = player.PresentationVersion;
                    player.Update(0);
                    if (player.IsLoading) Check(player.PresentationVersion == presentation, "Unfinished seek does not publish a partial frame");
                    if (!player.IsSeeking) done.TrySetResult(true);
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            EditorApplication.update += Tick;
            try { await done.Task; } finally { EditorApplication.update -= Tick; }
        }
        Check(player.CurrentFrame == Mathf.RoundToInt(time / WavePreviewPlayer.StepInterval), "Exact target frame " + time);
    }
    public static async Task<object> Run()
    {
        checks = 0; measurements.Clear(); int worlds = World.All.Count;
        int seed = NumericRandomPreviewControls.SeedOffset; NumericRandomPreviewControls.SetSeedOffset(53);
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Fast Seek verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var mainScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); bool dirty = mainScene.isDirty;
        try
        {
            var wave = root.AddComponent<Wave>();
            var first = (DirectedEnemySubWave)Fixture("Subwave", root.transform, "Sector + Child", -1f, false);
            var second = (DirectedEnemySubWave)Fixture("Subwave", root.transform, "Laser", 1f, true);
            Fixture("Sequence", first);
            var sequenceSettings = (DirectedWaveAttackSettings)Get(first.AttackPatternBehaviour.AttackSequence.steps[0].pattern, "attackSettings");
            var grandchild = new EnemyChildAttackSettings();
            Set(grandchild, "triggerValue", .1f);
            Set(grandchild.Pattern, "attackShotCount", 1);
            Set(grandchild.Pattern, "attackShotInterval", .1f);
            grandchild.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, .3f);
            Set(grandchild.Pattern.Shooting, "overrideProjectileLifetime", true);
            Set(grandchild.Pattern.Shooting, "projectileLifetime", 1.5f);
            sequenceSettings.WaveBurstSettings.Shooting.ChildAttack.Pattern.Shooting.SetChildAttack(grandchild);
            var coordinator = root.AddComponent<WaveAttackCoordinator>();
            for (int i = 0; i < 2; i++) coordinator.Settings.groups.Add(new WaveAttackCoordinatorGroup
            { groupName = "Group " + i, completionMode = WaveAttackGroupCompletion.Timed, duration = 1.7f, delayAfterGroup = .2f, waitUntilReady = false,
                controlledSubWaves = new() { new WaveAttackSubWaveReference { scheduleIndex = i, subWavePrefab = i == 0 ? first.gameObject : second.gameObject } } });
            Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(first.gameObject, 0), new(second.gameObject, .1f) });
            using var player = new WavePreviewPlayer(wave, 65, new Vector3(100, 100));
            player.SetTargetPositionAtTime(5, new Vector3(-100, 100));
            player.SetTargetPositionAtTime(23, new Vector3(70, -80));
            var referenceEvents = new List<WavePreviewEvent>();
            using var reference = new WaveConductorShootingPreview(wave, new Vector3(100, 100),
                eventSink: referenceEvents.Add, spawnSeed: NumericRandomPreviewControls.SeedOffset);
            Call(reference, "EnableSnapshots"); reference.PrepareInitialState();
            var expected = new Dictionary<int, string>();
            foreach (int frame in new[] { 300, 450, 1350, 1800 })
            {
                while (reference.StepCount < frame)
                {
                    reference.AdvanceTo(Mathf.Min(frame, reference.StepCount + 1) * WavePreviewPlayer.StepInterval, 1000,
                        beforeStep: f => reference.TargetPosition = player.TargetHistory.Evaluate(f),
                        afterStep: f => reference.TargetPosition = player.TargetHistory.Evaluate(f));
                    if (reference.StepCount % 30 == 0) await Task.Yield();
                }
                expected.Add(frame, FullState(reference, referenceEvents));
            }
            bool secondGeneration = false;
            foreach (var state in expected.Values) secondGeneration |= state.Contains("Generation=2");
            Check(secondGeneration, "Grandchildren actually exist in captured logical state");
            await Seek(player, 60); Equal(expected[1800], FullState(player.Simulation, player.Events), "0→60: complete native + managed state equals ordinary fixed-step simulation");
            Check(player.StepsProcessed == 1800 && !player.IsPlaying && player.SeekWallTime > 0, "Seek diagnostics count actual fixed steps and retain pause");
            await Seek(player, 10); Equal(expected[300], FullState(player.Simulation, player.Events), "Backward reconstruction exact");
            await Seek(player, 45); Equal(expected[1350], FullState(player.Simulation, player.Events), "10→45 exact full state");
            await Seek(player, 15); Check(player.LastRestoreFrame >= 0 && player.LastRestoreFrame <= 450, "45→15 restores valid snapshot");
            Equal(expected[450], FullState(player.Simulation, player.Events), "45→15 exact full state after native entity remap");
            for (int i = 0; i < 2; i++)
            {
                await Seek(player, 45); Equal(expected[1350], FullState(player.Simulation, player.Events), "Repeated A/B forward");
                await Seek(player, 15); Equal(expected[450], FullState(player.Simulation, player.Events), "Repeated A/B backward");
            }
            player.MaxStepsPerUpdate = 1; player.ClearSnapshotCache(); player.Seek(60); player.Update(0);
            Check(player.IsLoading, "Long seek remains cancellable between batches");
            player.Seek(10); player.Seek(45); player.Seek(15); await Seek(player, 15);
            Equal(expected[450], FullState(player.Simulation, player.Events), "Only latest retarget wins");
            Check(player.StepsProcessed <= 450, "Retarget diagnostics exclude cancelled request");
            player.Seek(60); player.Update(0); player.Stop();
            Check(!player.IsSeeking && player.Simulation == null && player.SnapshotCount == 0, "Stop cancels and frees pending seek");
            player.MaxStepsPerUpdate = 2048; await Seek(player, 15);
            var live = player.Simulation; int frameBefore = player.CurrentFrame;
            player.Play(); player.Update(WavePreviewPlayer.StepInterval);
            Check(player.CurrentFrame == frameBefore + 1 && ReferenceEquals(live, player.Simulation), "Play resumes from sought world");
            player.Pause(); int paused = player.CurrentFrame; player.TargetPosition = new Vector3(-30, -20);
            player.Update(5); Check(player.CurrentFrame == paused && ReferenceEquals(live, player.Simulation) && player.RecordPlayerMovement, "Target editing after seek preserves world and records");
            player.Stop();
            foreach (float speed in new[] { 8f, 16f })
            {
                player.PlaybackSpeed = speed; player.Play(); player.Update(WavePreviewPlayer.StepInterval);
                while (player.IsSeeking) { player.Update(0); await Task.Yield(); }
                Check(player.CurrentFrame == Mathf.RoundToInt(speed), "Playback " + speed + "×"); player.Stop();
            }
            player.PlaybackSpeed = 1; player.MaxSpeed = true; player.MaxStepsPerUpdate = 5; player.Play(); player.Update(0);
            if (player.CurrentFrame == 0) player.Update(0);
            Check(player.CurrentFrame > 0 && player.CurrentFrame <= 5 && !player.IsLoading && player.PresentedTime == player.CurrentTime, "Max Speed advances without real time and publishes batches");
            player.Pause(); paused = player.CurrentFrame; player.Update(10); Check(player.CurrentFrame == paused, "Max Speed Pause cancels catch-up");
            player.Restart(); player.Seek(60); player.Update(0); player.Dispose();
            Check(player.Simulation == null && !player.IsSeeking, "Dispose cancels seek");
            Check(mainScene.isDirty == dirty, "Main scene unchanged");
        }
        finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); NumericRandomPreviewControls.SetSeedOffset(seed); }
        Check(World.All.Count == worlds, "No leaked simulation/snapshot worlds");
        var result = new { passed = true, checks, fullManagedAndNativeState = true, seek60 = true, retarget = true, transport = true, maxSpeed = true };
        File.WriteAllText("AgentArtifacts/fast-seek-verification.json", JsonUtility.ToJson(new Result { passed = true, checks = checks }));
        return result;
    }
    [Serializable] private class Result { public bool passed; public int checks; }

    public static async Task<object> Safety()
    {
        checks = 0; int worlds = World.All.Count;
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Fast Seek visibility");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        WavePreviewPlayerWindow window = null;
        try
        {
            var wave = root.AddComponent<Wave>();
            var first = (DirectedEnemySubWave)Fixture("Subwave", root.transform, "First Laser", -1f, true);
            var second = (DirectedEnemySubWave)Fixture("Subwave", root.transform, "Delayed Laser", 1f, true);
            Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(first.gameObject, 0), new(second.gameObject, 20) });
            using (var player = new WavePreviewPlayer(wave, 65, new Vector3(100, 100)))
            {
                await Seek(player, 30); player.Play(); await Seek(player, 15);
                Check(player.IsPlaying && player.CurrentFrame == 450, "Seeking while playing preserves transport state");
                player.Update(WavePreviewPlayer.StepInterval);
                Check(player.CurrentFrame == 451, "Playing resumes on the next fixed step after seek");
                player.Pause();
                Check(player.Simulation.GetSession(1) == null, "Future cached cue is dormant");
                player.Simulation.SetVisible(true);
                Check((bool)Get(player.Simulation.ProjectileWorld, "visible"), "Dormant cue cannot hide shared projectile world");
                var session = player.Simulation.GetSession(0);
                player.MaxStepsPerUpdate = 1; player.Seek(60); player.Update(0);
                var visuals = (GameObject)Get(session, "visualsRoot");
                Check(visuals.activeInHierarchy, "Seek hides renderers without killing Laser source hierarchy");
                foreach (var renderer in visuals.GetComponentsInChildren<Renderer>(true))
                    Check(renderer.forceRenderingOff, "Intermediate enemy renderers hidden");
                var old = player.Simulation;
                player.Restart();
                Check(player.CurrentFrame == 0 && player.IsPlaying && !ReferenceEquals(old, player.Simulation), "Restart cancels pending seek and replaces world");
                player.Pause();
            }
            window = ScriptableObject.CreateInstance<WavePreviewPlayerWindow>();
            Set(window, "followSelection", false); Set(window, "showAdvanced", true); Set(window, "showSeekDiagnostics", true);
            Call(window, "SetSource", wave); window.ShowUtility();
            var active = (WavePreviewPlayer)Call(window, "EnsurePlayer");
            active.MaxStepsPerUpdate = 1; active.Seek(60); active.Update(0);
            Call(window, "SetSource", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>());
            Check(active.Simulation == null && !active.IsSeeking && World.All.Count == worlds, "Wave change cancels unfinished work and releases all worlds");
            active = (WavePreviewPlayer)Call(window, "EnsurePlayer"); active.MaxStepsPerUpdate = 1; active.Seek(60); active.Update(0);
            window.Close(); if (window != null) Object.DestroyImmediate(window); window = null;
            Check(active.Simulation == null && !active.IsSeeking && World.All.Count == worlds, "Closing window cancels unfinished seek");
        }
        finally { if (window != null) { window.Close(); if (window != null) Object.DestroyImmediate(window); } Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        Check(World.All.Count == worlds, "Safety cleanup leaves no ECS worlds");
        return new { passed = true, checks, dormantVisibility = true, laserLiveness = true, pendingSourceChange = true, pendingClose = true, restart = true };
    }

    public static async Task<object> Present()
    {
        EnemyShootingPreview.ShowFormationVolley = true;
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/1/1_Extreme/1_4_extrime.prefab").GetComponent<Wave>();
        var window = WavePreviewPlayerWindow.Open(wave);
        Set(window, "followSelection", false); Set(window, "showAdvanced", true); Set(window, "showSeekDiagnostics", true);
        Set(window, "previewDuration", 65f);
        Set(window, "simulationTimeBudget", 24f);
        var player = (WavePreviewPlayer)Call(window, "EnsurePlayer");
        player.SimulationTimeBudget = 24d;
        player.Pause(); player.Duration = 65f; player.Seek(5f);
        while (player.IsLoading) await Task.Delay(16);
        window.Repaint(); SceneView.RepaintAll();
        return new { wave = wave.name, time = player.CurrentTime, playing = player.IsPlaying,
            budgetMilliseconds = player.SimulationTimeBudget, maximumSteps = player.MaxStepsPerUpdate,
            speedModes = (string[])typeof(WavePreviewPlayerWindow).GetField("SpeedLabels", S).GetValue(null),
            seekWallTime = player.SeekWallTime, steps = player.StepsProcessed };
    }

    public static async Task<object> Benchmark(int index)
    {
        var paths = new[] { "Assets/Waves/1/1_Extreme/1_1_extrime.prefab", "Assets/Waves/1/1_Extreme/1_4_extrime.prefab",
            "Assets/Waves/2/2_2_extrime.prefab", "Assets/Waves/2/2_3_extrime.prefab" };
        string path = paths[index]; byte[] before = File.ReadAllBytes(path); int worlds = World.All.Count;
        int seed = NumericRandomPreviewControls.SeedOffset; NumericRandomPreviewControls.SetSeedOffset(53);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var wave = root.GetComponent<Wave>();
            using var player = new WavePreviewPlayer(wave, 65, new Vector3(0, -3));
            var timer = Stopwatch.StartNew(); int calls = 0, maxSteps = 0, peakProjectiles = 0; double largestBatch = 0;
            player.Seek(60);
            File.WriteAllText("AgentArtifacts/fast-seek-benchmark-progress.txt", root.name + " fast seek");
            var finished = new TaskCompletionSource<bool>();
            void Tick()
            {
                if (finished.Task.IsCompleted) return;
                try
                {
                    int frame = player.CurrentFrame; var batch = Stopwatch.StartNew(); player.Update(0);
                    largestBatch = Math.Max(largestBatch, batch.Elapsed.TotalMilliseconds);
                    maxSteps = Math.Max(maxSteps, player.CurrentFrame - frame);
                    peakProjectiles = Math.Max(peakProjectiles, player.Simulation.ProjectileCount); calls++;
                    if (calls % 100 == 0) File.WriteAllText("AgentArtifacts/fast-seek-benchmark-progress.txt", root.name + " frame " + player.CurrentFrame);
                    if (!player.IsSeeking) finished.TrySetResult(true);
                }
                catch (Exception error) { finished.TrySetException(error); }
            }
            EditorApplication.update += Tick;
            try { await finished.Task; } finally { EditorApplication.update -= Tick; }
            double wall = timer.Elapsed.TotalSeconds; string final = FullState(player.Simulation, player.Events);
            int steps = player.StepsProcessed; double speed = player.SimulationSpeed;
            File.WriteAllText("AgentArtifacts/fast-seek-benchmark-progress.txt", root.name + " reference");
            var observed = new List<WavePreviewEvent>(); int omittedEvents = 0;
            using var reference = new WaveConductorShootingPreview(wave, new Vector3(0, -3),
                eventSink: e => { if (observed.Count < WavePreviewPlayer.MaximumEvents) observed.Add(e); else omittedEvents++; },
                spawnSeed: NumericRandomPreviewControls.SeedOffset);
            Call(reference, "EnableSnapshots"); reference.PrepareInitialState();
            var cacheType = typeof(WavePreviewPlayer).Assembly.GetType("WavePreviewSnapshotCache");
            using var cache = (IDisposable)Activator.CreateInstance(cacheType, true);
            Call(cache, "Capture", reference, observed, 0);
            timer.Restart();
            var referenceDone = new TaskCompletionSource<bool>();
            void ReferenceTick()
            {
                if (referenceDone.Task.IsCompleted) return;
                try
                {
                    var budget = Stopwatch.StartNew();
                    do
                    {
                        reference.AdvanceTo((reference.StepCount + 1) * WavePreviewPlayer.StepInterval, 1000,
                            afterStep: f => { if (f % 60 == 0) Call(cache, "Capture", reference, observed, omittedEvents); });
                    } while (reference.StepCount < 1800 && budget.Elapsed.TotalMilliseconds < 12);
                    if (reference.StepCount >= 1800) referenceDone.TrySetResult(true);
                    else ((Action)typeof(WavePreviewPlayer).GetField("signalEditorTick", S).GetValue(null))?.Invoke();
                }
                catch (Exception error) { referenceDone.TrySetException(error); }
            }
            EditorApplication.update += ReferenceTick;
            try { await referenceDone.Task; } finally { EditorApplication.update -= ReferenceTick; }
            double ordinaryWall = timer.Elapsed.TotalSeconds;
            // Production transport caps observed history without changing simulation.
            if (observed.Count > WavePreviewPlayer.MaximumEvents) observed.RemoveRange(WavePreviewPlayer.MaximumEvents, observed.Count - WavePreviewPlayer.MaximumEvents);
            Equal(FullState(reference, observed), final, "Existing wave full logical state matches ordinary simulation: " + path);
            int finalProjectiles = player.Simulation.ProjectileCount;
            long finalOmitted = player.Simulation.Omitted;
            double snapshotMB = player.SnapshotMemoryBytes / (1024d * 1024d);
            timer.Restart(); await Seek(player, 58);
            double cachedWall = timer.Elapsed.TotalMilliseconds;
            int cachedSteps = player.StepsProcessed, restoredFrame = player.LastRestoreFrame;
            var result = new BenchmarkResult { wave = root.name, wallSeconds = wall, simulationSpeed = speed, steps = steps,
                editorUpdates = calls, maximumBatchMilliseconds = largestBatch, maximumSteps = maxSteps,
                peakProjectiles = peakProjectiles, finalProjectiles = finalProjectiles,
                omitted = finalOmitted, snapshotMB = snapshotMB,
                ordinaryWallSeconds = ordinaryWall, fullStateEqual = true, cachedRestoreMilliseconds = cachedWall,
                cachedRestoreSteps = cachedSteps, cachedRestoreFrame = restoredFrame };
            File.WriteAllText("AgentArtifacts/fast-seek-benchmark-" + index + ".json", JsonUtility.ToJson(result, true));
            return result;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root); NumericRandomPreviewControls.SetSeedOffset(seed);
            Check(Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(path)), "Benchmark prefab unchanged");
            Check(World.All.Count == worlds, "Benchmark releases worlds");
        }
    }
    [Serializable] private class BenchmarkResult
    {
        public string wave; public double wallSeconds, simulationSpeed, maximumBatchMilliseconds, snapshotMB, ordinaryWallSeconds;
        public int steps, editorUpdates, maximumSteps, peakProjectiles, finalProjectiles; public long omitted; public bool fullStateEqual;
        public double cachedRestoreMilliseconds; public int cachedRestoreSteps, cachedRestoreFrame;
    }
}
public static class WavePreviewSnapshotFixtures
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static int checks;
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    private static void Set(object value,string field,object data)
    {for(var type=value.GetType();type!=null;type=type.BaseType){var f=type.GetField(field,Flags);if(f!=null){f.SetValue(value,data);return;}var p=type.GetProperty(field,Flags);if(p!=null){p.SetValue(value,data);return;}}throw new Exception("Missing field "+field);}
    private static object Get(object value,string field)=>value.GetType().GetField(field,Flags).GetValue(value);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Flags).Invoke(value,args);
    private static void Range(object value,string field,float min,float max)
    {var range=Get(value,field+"Random");if(range is RandomFloatRange f){f.enabled=true;f.min=min;f.max=max;}else{var n=(RandomIntRange)range;n.enabled=true;n.min=(int)min;n.max=(int)max;}}
    private static void Seek(WavePreviewPlayer player,float time,double budget=1000d)
    {
        player.Seek(time);for(int guard=0;player.IsSeeking;guard++){if(guard>2000)throw new Exception("Seek stalled");player.Update(0,budget);}
        Check(Mathf.Abs(player.CurrentTime-time)<=WavePreviewPlayer.StepInterval*.51f,"Seek reaches requested frame "+time+" / "+player.CurrentTime);
    }
    private static string Capture(WavePreviewPlayer player,bool includeEvents=true)
    {
        var state=new List<string>();var simulation=player.Simulation;
        for(int cue=0;cue<simulation.CueCount;cue++)
        {
            var session=simulation.GetSession(cue);if(session==null)continue;
            var positions=new Dictionary<int,Vector3>();session.CopyPreviewPositionsTo(positions);
            foreach(var p in positions)state.Add("E"+cue+"/"+p.Key+":"+p.Value.ToString("R"));
        }
        var m=simulation.ProjectileWorld.SimulationWorld.EntityManager;
        using var query=m.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities)
        {
            string s=JsonUtility.ToJson(m.GetComponentData<LocalTransform>(entity))+JsonUtility.ToJson(m.GetComponentData<EnemyProjectileMovement>(entity));
            if(m.HasComponent<EnemyLaserState>(entity))s+=JsonUtility.ToJson(m.GetComponentData<EnemyLaserState>(entity));
            state.Add(s);
        }
        state.Sort(StringComparer.Ordinal);var result=new StringBuilder(string.Join("\n",state));
        if(includeEvents)foreach(var e in player.Events)result.Append($"\n{e.Time:R}|{e.Kind}|{e.Cue}|{e.Slot}|{e.Count}|{e.Detail}");
        return result.ToString();
    }
    private static DirectedEnemySubWave Subwave(Transform parent,string name,float x,bool laser=false)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,1);
        var wave=go.AddComponent<DirectedEnemySubWave>();Set(wave,"enemyPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
        Set(wave,"enemyCount",1);Set(wave,"settleDuration",0f);Set(wave,"spawnInterval",0f);Set(wave,"formationCoordinateSpace",DirectedWaveCoordinateSpace.LocalToSubWave);
        var attack=go.GetComponent<DirectedWaveAttackBehaviour>()??go.AddComponent<DirectedWaveAttackBehaviour>();
        Set(attack,"allowAutonomousAttackDuringEntrance",false);
        Configure((DirectedWaveAttackSettings)Get(attack,"attackSettings"),laser);return wave;
    }
    private static void Configure(DirectedWaveAttackSettings settings,bool laser=false)
    {
        Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);settings.StationaryFireSettings.ConfigureAngle(180);
        Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);Set(settings,"waitForPreviousAttack",true);Set(settings,"delayAfterAttack",.3f);
        var profile=settings.WaveBurstSettings;profile.ConfigureLegacySingleAttack(2,.5f,.2f);profile.Shooting.ConfigureLegacyFan(3,-45,45);
        var shooting=profile.Shooting;shooting.SetProjectileBaseSpeedOverride(true,1);Set(shooting,"overrideProjectileLifetime",true);Set(shooting,"projectileLifetime",3f);
        if(laser)
        {Set(shooting,"projectileType",EnemyProjectileType.Laser);Set(shooting.Laser.Geometry,"lengthMode",EnemyLaserLengthMode.Custom);Set(shooting.Laser.Geometry,"laserLength",3f);Set(shooting.Laser,"telegraphDuration",.5f);}
        else
        {
            Range(shooting,"fanProjectileCount",2,5);Range(shooting,"fanMinAngle",-60,-20);Range(shooting,"fanMaxAngle",20,60);
            Range(shooting,"projectileAngularSpeed",-25,25);Range(profile,"attackShotInterval",.1f,.3f);Range(profile,"attackShotCount",2,4);
            shooting.EmissionLayout.ConfigureSector(-40,40,4,EnemySectorLaunchMode.Sequential,.1f,.06f);
            shooting.EmissionLayout.ConfigureLaunchOrder(EnemySectorLaunchOrder.PingPong);
            Range(shooting.EmissionLayout,"directionsCount",2,4);
            var child=new EnemyChildAttackSettings();Set(child,"triggerValue",.2f);Set(child,"repeatCount",2);
            child.Pattern.Shooting.ConfigureLegacyFan(3,-10,10);child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true,.5f);
            child.Pattern.Shooting.ProjectileBaseSpeedRandom.CopyFrom(new RandomFloatRange{enabled=true,min=.5f,max=.5f});shooting.SetChildAttack(child);
        }
        settings.Validate();
    }
    public static object Run()
    {
        checks=0;int originalSeed=NumericRandomPreviewControls.SeedOffset;NumericRandomPreviewControls.SetSeedOffset(0);
        int worlds=World.All.Count;var realScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=realScene.isDirty;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Wave Player verification");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var wave=root.AddComponent<Wave>();var first=Subwave(root.transform,"First",-1);var second=Subwave(root.transform,"Second",1,true);
            Set(wave,"scheduledSubWaves",new List<WaveSubWaveCue>{new(first.gameObject,0),new(second.gameObject,.1f)});
            string authored=EditorJsonUtility.ToJson(first)+EditorJsonUtility.ToJson(first.AttackPatternBehaviour)+EditorJsonUtility.ToJson(second)+EditorJsonUtility.ToJson(wave);
            var random=JsonUtility.ToJson(UnityEngine.Random.state);
            using(var player=new WavePreviewPlayer(wave,30,new Vector3(100,100)))
            {
                player.Play();player.Update(.2,1000);Check(player.CurrentFrame==6,"Play fixed clock");player.Pause();string pause=Capture(player);player.Update(5,1000);Check(Capture(player)==pause&&player.CurrentFrame==6,"Pause freezes state and event history");
                player.Play();player.Update(.1,1000);Check(player.CurrentFrame==9,"Resume continues, no reset");player.Pause();
                player.Seek(20);player.Update(0,.1);int pausedFrame=player.CurrentFrame;player.Pause();string backlog=Capture(player);player.Update(1,1000);Check(player.CurrentFrame==pausedFrame&&Capture(player)==backlog,"Pause cancels a pending catch-up without advancing state");
                Seek(player,20);string twenty=Capture(player);int eventCount=player.Events.Count;Seek(player,5);Seek(player,20,.1d);
                Check(Capture(player)==twenty&&player.Events.Count==eventCount,"20 to 5 to 20 replays identical ECS/enemies/events with a different work budget");
                player.Step(-1);player.Update(0,1000);while(player.IsSeeking)player.Update(0,1000);Check(player.CurrentFrame==599,"Step Backward exactly one tick");
                player.Step(1);player.Update(0,1000);Check(player.CurrentFrame==600&&Capture(player)==twenty,"Step Forward restores exact state");
                Check(player.Simulation.ActiveCueCount==2,"Multiple subwaves share clock");
                float parentStart=0f;foreach(var e in player.Events)if(e.Cue==0&&e.Kind==WavePreviewEventKind.ProjectileSpawn){parentStart=e.Time;break;}
                Seek(player,parentStart+.6f);
                using(var children=player.Simulation.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))using(var values=children.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
                {bool child=false;foreach(var v in values)child|=Mathf.Abs(Unity.Mathematics.math.length(v.Value)-.5f)<.0001f;Check(child,"Child Attack actually emits its configured projectiles");}
                var kinds=new HashSet<WavePreviewEventKind>();foreach(var e in player.Events)kinds.Add(e.Kind);
                Check(kinds.Contains(WavePreviewEventKind.EnemySpawn)&&kinds.Contains(WavePreviewEventKind.AttackStart)&&kinds.Contains(WavePreviewEventKind.AttackEnd)&&kinds.Contains(WavePreviewEventKind.ProjectileSpawn),"Observed spawn/attack/projectile events");
                float laserStart=-1;foreach(var e in player.Events)if(e.Cue==1&&e.Kind==WavePreviewEventKind.ProjectileSpawn){laserStart=e.Time;break;}Check(laserStart>=0,"Laser spawn observed");
                float telegraphTime=Mathf.Round((laserStart+.2f)/WavePreviewPlayer.StepInterval)*WavePreviewPlayer.StepInterval;
                Seek(player,telegraphTime);var m=player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;using(var q=m.CreateEntityQuery(typeof(EnemyLaserState)))using(var values=q.ToComponentDataArray<EnemyLaserState>(Allocator.Temp)){bool telegraph=false;foreach(var v in values)telegraph|=v.Telegraph!=0;Check(telegraph,"Actual Laser Telegraph state at selected time");}
                string telegraphState=Capture(player);Seek(player,laserStart+.7f);using(var q=m.CreateEntityQuery(typeof(EnemyLaserState)))using(var values=q.ToComponentDataArray<EnemyLaserState>(Allocator.Temp)){bool active=false;foreach(var v in values)active|=v.Active!=0;Check(active,"Actual Laser active state");}
                Seek(player,telegraphTime);Check(Capture(player)==telegraphState,"Laser/Telegraph rewind exact");
                player.Stop();Check(player.Simulation==null&&player.CurrentFrame==0&&player.Events.Count==0,"Stop frees simulation and resets clock");
                foreach(float speed in new[]{.25f,.5f,1f,2f,4f}){player.Restart();player.PlaybackSpeed=speed;player.Update(WavePreviewPlayer.StepInterval*4d,1000);Check(player.CurrentFrame==Mathf.RoundToInt(4*speed),"Playback speed "+speed);player.Stop();}
                Check(World.All.Count==worlds,"Stop frees all owned ECS worlds");
                Check(authored==EditorJsonUtility.ToJson(first)+EditorJsonUtility.ToJson(first.AttackPatternBehaviour)+EditorJsonUtility.ToJson(second)+EditorJsonUtility.ToJson(wave),"Replay never edits authoring components");
                Sequence(first);player.Restart();Seek(player,4);string sequence=Capture(player);Seek(player,1);Seek(player,4);Check(sequence==Capture(player),"Sequence and child attacks replay exactly");player.Stop();
                var coordinator=root.AddComponent<WaveAttackCoordinator>();
                for(int i=0;i<2;i++)coordinator.Settings.groups.Add(new WaveAttackCoordinatorGroup{groupName="Group "+i,completionMode=WaveAttackGroupCompletion.Timed,duration=.7f,delayAfterGroup=.2f,waitUntilReady=false,controlledSubWaves=new(){new WaveAttackSubWaveReference{scheduleIndex=i,subWavePrefab=i==0?first.gameObject:second.gameObject}}});
                player.Restart();Seek(player,5);string coordinated=Capture(player);Seek(player,1);Seek(player,5);Check(coordinated==Capture(player),"Coordinator + Sequence + child timeline replays exactly");
                bool groupEvent=false;foreach(var e in player.Events)groupEvent|=e.Kind==WavePreviewEventKind.CoordinatorGroupChange;Check(groupEvent,"Real coordinator transitions observed");
            }
            Check(JsonUtility.ToJson(UnityEngine.Random.state)==random,"Global Unity Random state restored");
            // Sequence was deliberately configured in memory above; no source assets or scenes were saved.
            Check(EditorJsonUtility.ToJson(wave).Contains("scheduledSubWaves"),"Authoring configuration remains available");
            Check(realScene.isDirty==dirty,"Real scene dirty flag unchanged");
            return new{passed=true,checks,transport=true,replay20_5_20=true,sequenceChildren=true,laserTelegraph=true,coordinator=true,globalRandomPreserved=true};
        }
        finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);NumericRandomPreviewControls.SetSeedOffset(originalSeed);Check(World.All.Count==worlds,"Dispose leaves no worlds");}
    }
    private static void Sequence(DirectedEnemySubWave wave)
    {
        var behaviour=wave.AttackPatternBehaviour;Set(behaviour,"executionMode",DirectedWaveAttackExecutionMode.Sequence);var sequence=behaviour.AttackSequence;
        sequence.sequenceMode=DirectedWaveAttackSequenceMode.Loop;sequence.loopCount=0;sequence.steps.Clear();
        var first=new DirectedWaveAttackSequenceStep{name="Random Sector + Child"};Configure((DirectedWaveAttackSettings)Get(first.pattern,"attackSettings"));
        var second=new DirectedWaveAttackSequenceStep{name="Laser"};Configure((DirectedWaveAttackSettings)Get(second.pattern,"attackSettings"),true);
        sequence.steps.Add(first);sequence.steps.Add(new DirectedWaveAttackSequenceStep{type=DirectedWaveAttackSequenceStepType.Pause,pauseDuration=.1f});sequence.steps.Add(second);
    }
    public static object ExistingWaves()
        => ExistingRange(0,7);
    public static object ExistingWave(int index) => ExistingRange(index,1);
    private static object ExistingRange(int first,int count)
    {
        checks=0;var paths=new[]{"Assets/Waves/1/1_Extreme/1_1_extrime.prefab","Assets/Waves/1/1_Extreme/1_2_extrime.prefab","Assets/Waves/1/1_Extreme/1_3_extrime.prefab","Assets/Waves/1/1_Extreme/1_4_extrime.prefab","Assets/Waves/2/2_1_extrime.prefab","Assets/Waves/2/2_2_extrime.prefab","Assets/Waves/2/2_3_extrime.prefab"};
        int worlds=World.All.Count;
        for(int pathIndex=first;pathIndex<first+count;pathIndex++)
        {
            string path=paths[pathIndex];
            byte[] bytes=File.ReadAllBytes(path);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var wave=root.GetComponent<Wave>();Check(wave!=null,"Wave conductor "+path);
                var components=root.GetComponentsInChildren<Component>(true);var before=new List<string>();foreach(var c in components)before.Add(c!=null?EditorJsonUtility.ToJson(c):null);
                using var player=new WavePreviewPlayer(wave,30,new Vector3(0,-3));Seek(player,20);string state=Capture(player);Seek(player,5);Seek(player,20);Check(Capture(player)==state,"Existing wave exact replay "+path);
                for(int i=0;i<components.Length;i++)if(components[i]!=null)Check(EditorJsonUtility.ToJson(components[i])==before[i],"Serialized component unchanged "+path+" / "+components[i].GetType().Name);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Check(Convert.ToBase64String(bytes)==Convert.ToBase64String(File.ReadAllBytes(path)),"Prefab bytes unchanged "+path);
        }
        Check(World.All.Count==worlds,"Existing waves release all preview worlds");return new{passed=true,checks,waves=count,exactReplay=true,sourceUnchanged=true};
    }
}

