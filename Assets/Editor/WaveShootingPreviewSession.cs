using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// One disposable preview per wave inspector. All snapshots and objects belong to this session.
public sealed partial class WaveShootingPreviewSession : IDisposable
{
    public const float StepInterval = 1f / 30f;
    public const int MaximumEnemies = int.MaxValue;
    private const int MaximumDirections = 128;
    private const int MaximumVolley = 512;
    private readonly DirectedEnemySubWave wave;
    private readonly Transform previewParent;
    private readonly bool ownsProjectiles;
    private readonly bool formationOnly;
    private readonly bool stationaryFormation;
    private readonly bool hasPostMovement;
    private readonly bool usesAttackSequence;
    private readonly int attackConfigurationRevision;
    private readonly int presetConfigurationRevision = EnemyPresetRevision.Value;
    private readonly int numericPreviewRevision = NumericRandomPreviewControls.Revision;
    private readonly Dictionary<int, EnemyAttackNumericRegistry> numericRegistries = new();
    private readonly Dictionary<int, EnemySectorLaunchOrderRegistry> sectorOrderRegistries = new();
    public bool IsConfigurationCurrent => (wave.AttackPatternBehaviour != null ? wave.AttackPatternBehaviour.ConfigurationRevision : 0) == attackConfigurationRevision && numericPreviewRevision == NumericRandomPreviewControls.Revision && presetConfigurationRevision == EnemyPresetRevision.Value;
    private DirectedWaveAttackSequencePlayer sequencePlayer;
    public string SequenceStatus => sequencePlayer?.CurrentStep;
    public int SequenceChildWaitTimeouts => sequencePlayer?.ChildWaitTimeouts ?? 0;
    private int[] previewSpawnSteps;
    private bool positionsAreFinal;
    private readonly int[] spawnOrder;
    private readonly Dictionary<int, Vector3> positions = new();
    private readonly List<Slot> slots = new();
    private readonly List<Group> groups = new();
    private readonly List<EntranceSequence> entranceSequences = new();
    private readonly List<Volley> volleys = new(128);
    private readonly Stack<Volley> volleyPool = new(128);
    private readonly List<EnemyProjectileSpawnRequest> requests = new(MaximumVolley);
    private readonly EnemyProjectilePreviewWorld projectiles;
    private readonly Stopwatch workTimer = new();
    private readonly GameObject visualsRoot;
    private readonly GameObject targetObject;
    private readonly List<Renderer> visualRenderers = new();
    private readonly Dictionary<Transform, VisualPose> visualPoses = new();
    private bool renderersVisible = true;
    private Mesh targetMesh;
    private Material targetMaterial;
    private uint random = 0x92d68ca2;
    private float postStart;
    private int visualEnemyCount;
    private long omittedRays;
    private int remainingStepEmissions;
    private bool disposed;
    private readonly Action<WavePreviewEvent> eventSink;
    private readonly float eventTimeOffset;
    private readonly int eventCue;
    private readonly HashSet<int> observedSpawns = new();
    private string observedPhase;
    private float positionsTime = float.NaN;
    private Matrix4x4 positionsParentMatrix;
    public float Time { get; private set; }
    public int ProjectileCount => projectiles.Count;
    public int EnemyCount => visualEnemyCount;
    public long Omitted => omittedRays + projectiles.Omitted;
    public string Warning { get; private set; }
    public string PhaseName { get; private set; } = "Entrance / Formation";
    public Vector3 TargetPosition { get => targetObject != null ? targetObject.transform.position : Vector3.zero;
        set { if (targetObject != null) targetObject.transform.position = value; } }
    public GameObject TargetObject => targetObject;
    public EnemyProjectilePreviewWorld ProjectileWorld => projectiles;
    public int[] SpawnOrder => spawnOrder;
    public void SetVisible(bool visible)
    {
        if (disposed) return;
        if (ownsProjectiles) projectiles.SetVisible(visible);
        // Visibility must not change source liveness: Laser follows these transforms.
        if (renderersVisible == visible) return;
        renderersVisible = visible;
        foreach (var renderer in visualRenderers) renderer.forceRenderingOff = !visible;
        if (ownsProjectiles && targetObject != null && targetObject.TryGetComponent<Renderer>(out var targetRenderer))
            targetRenderer.forceRenderingOff = !visible;
    }

    private sealed class Slot
    {
        public int RemainingSequenceAttacks;
        public EnemyAttackNumericRegistry Numeric;
        public EnemySectorLaunchOrderRegistry SectorOrders;
        public int Index;
        public SimpleEnemyAttackExecutor Executor;
        public EnemyBullet[] Prefabs;
        public EnemyBurstAttackSettings Profile;
        public DirectedWaveAttackSettings Settings;
        public DirectedWaveAttackFireMode FireMode;
        public DirectedWaveAttackAimMode AimMode;
        public EnemyStationaryFireSettings Stationary;
        public EnemyRotationSettings RotationOverride;
        public Pattern[] Patterns;
        public Transform Visual;
        public VisualPose Pose;
        public bool Active;
        public bool RotationStarted;
        public float ReadyTime, CompletionTime, RotationStart, SpawnTime;
        public Vector3 Position;
        public DirectedWaveAttackAimState AttackAim;
        public Group Group;
        public bool Entrance;
    }
    private sealed class Pattern
    {
        public EnemyBurstAttackSettings Settings;
        public EnemyAttackNumericState Numeric;
        public EnemyAttackPatternSchedule Schedule;
        public EnemyProjectileInitialSpeedState Speed;
        public EnemyEmissionOriginCaptureState OriginCapture;
        public Vector3? LastOrigin;
        public DirectedWaveAttackAimState Aim;
        public Vector3[] CircleDirections;
        public bool CircleAimCaptured;
        public EnemyEmissionPreviewFrame InitialFrame;
        public Quaternion Basis;
        public bool OwnedPoint;
        public Vector3 PivotOffset;
    }
    private sealed class Group
    {
        public EnemyAttackNumericState Numeric;
        public int CycleAttacks;
        public int CoordinatorCycles;
        public bool CoordinatorCounted;
        public bool CoordinatorEntranceClockBound;
        public bool SequenceOwned;
        public EnemyAttackCompletionTracker Completion;
        public int PatternIndex;
        public readonly List<Slot> Slots = new();
        public DirectedWaveAttackSettings Settings;
        public float NextTime;
        public int NextSlot, Round, Active;
        public bool Entrance;
        public bool Autonomous;
        public readonly DirectedWaveAttackOrderPlan OrderPlan = new();
        public readonly List<DirectedWaveAttackOrderSlot> OrderSlots = new();
        public readonly Dictionary<int, Slot> OrderedSlots = new();
        public readonly List<Slot> OrderBatch = new();
        public bool OrderInitialized, OrderFinished, OrderBatchPending;
    }
    private sealed class Volley
    {
        public EnemyShootingSettings Shooting;
        public Slot Slot;
        public Pattern Pattern;
        public EnemyRandomSpeedSample NumericSpeed;
        public EnemySectorLaunchTicket LaunchOrder;
        public float Start, Speed, Spread;
        public int Order;
        public bool Aimed, Stationary;
        public DirectedWaveAttackAimMode AimMode;
        public EnemyStationaryFireSettings StationarySettings;
        public Vector3 Direction;
        public Vector3? Origin;
        public Vector3[] CircleDirections;
        public bool HasCircleDirections;
    }
    private sealed class EntranceSequence
    {
        public Slot Slot;
        public float Start, Duration, LoopDuration;
        public int Count, Next;
        public EnemyEmissionOriginAttackCapture OriginCapture;
    }

    public WaveShootingPreviewSession(DirectedEnemySubWave wave, bool formationOnly, bool allEnemies,
        int selectedSlot, Vector3 target, Sprite targetSprite = null, int[] suppliedSpawnOrder = null,
        Transform previewParent = null, EnemyProjectilePreviewWorld sharedProjectiles = null,
        GameObject sharedTarget = null, int maximumEnemies = MaximumEnemies,
        Action<WavePreviewEvent> eventSink = null, float eventTimeOffset = 0f, int eventCue = -1)
    {
        this.wave = wave;
        this.eventSink = eventSink; this.eventTimeOffset = eventTimeOffset; this.eventCue = eventCue;
        this.previewParent = previewParent;
        ownsProjectiles = sharedProjectiles == null;
        projectiles = sharedProjectiles ?? new EnemyProjectilePreviewWorld();
        this.formationOnly = formationOnly;
        attackConfigurationRevision = wave.AttackPatternBehaviour != null ? wave.AttackPatternBehaviour.ConfigurationRevision : 0;
        usesAttackSequence = wave.AttackPatternBehaviour != null && wave.AttackPatternBehaviour.enabled
            && wave.AttackPatternBehaviour.UsesAttackSequence;
        hasPostMovement = wave.HasPostBehaviour;
        stationaryFormation = formationOnly || !wave.HasPhaseEntry && !hasPostMovement;
        spawnOrder = suppliedSpawnOrder ?? BuildSpawnOrder(wave);
        postStart = formationOnly ? 0f : wave.GetSimulationPreviewPostStartTime();
        float[] checkpointTimes = wave.GetEditorPreviewCheckpointTimes(out int loopStart, out float loopDuration);
        if (!formationOnly && loopDuration > 0f) postStart = float.PositiveInfinity;
        visualsRoot = new GameObject("Wave Preview (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        var scene = previewParent != null && previewParent.gameObject.scene.IsValid() ? previewParent.gameObject.scene
            : wave.gameObject.scene.IsValid() ? wave.gameObject.scene : UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visualsRoot, scene);
        targetObject = sharedTarget;
        if (targetObject == null)
        {
            targetObject = new GameObject("Preview Player") { hideFlags = HideFlags.HideAndDontSave };
            targetObject.transform.SetParent(visualsRoot.transform);
            TargetPosition = target;
            CreateTargetVisual(targetObject, targetSprite, out targetMesh, out targetMaterial);
        }
        AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        try
        {
            int count = wave.GetConfiguredEnemySlotCount();
            int limit = Mathf.Clamp(maximumEnemies, 1, MaximumEnemies);
            int shown = allEnemies ? Mathf.Min(count, limit) : Mathf.Min(count, 1);
            previewSpawnSteps = new int[shown];
            for (int sample = 0; sample < shown; sample++)
            {
                int index = allEnemies ? SampleIndex(sample, shown, count) : Mathf.Clamp(selectedSlot, 0, count - 1);
                previewSpawnSteps[sample] = Array.IndexOf(spawnOrder, index);
                var slot = BuildSlot(index);
                if (slot == null) continue;
                visualEnemyCount++;
                if (!formationOnly) BuildEntrance(index, slot.Visual, checkpointTimes, loopStart, loopDuration);
            }
            if (allEnemies && count > limit)
                Warning = $"Preview samples {limit}/{count} enemies.";
            if (usesAttackSequence)
            {
                sequencePlayer = new DirectedWaveAttackSequencePlayer(wave.AttackPatternBehaviour.AttackSequence, new SequenceHost(this));
                sequencePlayer.Begin(postStart, true);
            }
            UpdatePositions(0f);
        }
        catch { Dispose(); throw; }
    }

    public static int SampleIndex(int sample, int samples, int total)
        => samples <= 1 ? 0 : (int)((long)sample * (total - 1) / (samples - 1));

    public static int[] BuildSpawnOrder(DirectedEnemySubWave wave, int? seed = null)
    {
        var saved = UnityEngine.Random.state;
        try { UnityEngine.Random.InitState(seed ?? wave.GetInstanceID()); return wave.GetSimulationSpawnOrder(); }
        finally { UnityEngine.Random.state = saved; }
    }

    private Slot BuildSlot(int index, bool entrance = false, Transform visual = null, bool autonomous = false,
        DirectedWaveAttackSettings suppliedSettings = null, Group suppliedGroup = null, float sequenceSpeed = 1f)
    {
        var prefab = wave.GetConfiguredEnemyPrefabForSlot(index);
        if (prefab == null || !prefab.TryGetComponent(out SimpleEnemyAttackExecutor executor))
        { Warning = "Some enemies have no Simple Enemy Attack Executor; their shooting cannot be previewed."; return null; }
        var settings = suppliedSettings ?? new DirectedWaveAttackSettings();
        int patternIndex = -1;
        bool sequenceBase = usesAttackSequence && !entrance && suppliedSettings == null
            && wave.AttackPatternBehaviour.ControlsSequenceSlot(index);
        bool resolved = suppliedSettings != null || !sequenceBase && !autonomous
            && wave.AttackPatternBehaviour != null && wave.AttackPatternBehaviour.enabled
            && wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(index, entrance, settings, out patternIndex);
        var local = wave.GetConfiguredEnemyOverrideForSlot(index);
        // ApplyTo changes the enemy's base rotation. An active Wave Override replaces
        // that profile in Runtime, so its rotation must not be overridden a second time.
        bool usesEnemyRotation = !resolved || EnemyAttackSettingsResolver.UsesEnemySettings(executor, settings.UsesEnemyBurstSettings);
        var source = resolved ? EnemyAttackSettingsResolver.Resolve(executor, settings.UsesEnemyBurstSettings, settings.WaveBurstSettings)
            : executor.BurstAttackSettings;
        var profile = EnemyAttackSettingsResolver.CreateRuntimeCopy(source, local,
            resolved && settings.HasRuntimeProjectileBaseSpeedOverride
                ? settings.RuntimeProjectileBaseSpeedOverride : (float?)null);
        EnemyAttackSettingsResolver.ApplyRuntimeSpeedRandomization(profile, local, settings);
        if (sequenceSpeed != 1f) profile.ScaleAttackTiming(sequenceSpeed);
        if (!numericRegistries.TryGetValue(index, out var registry))
        { registry = new EnemyAttackNumericRegistry(); registry.Configure(index, NumericRandomPreviewControls.SeedOffset); numericRegistries.Add(index, registry); }
        using var serialized = new SerializedObject(executor);
        if (!sectorOrderRegistries.TryGetValue(index, out var sectorRegistry))
        { sectorRegistry = new EnemySectorLaunchOrderRegistry(); sectorOrderRegistries.Add(index, sectorRegistry); }
        var slot = new Slot { Numeric = registry, SectorOrders = sectorRegistry, Index = index, Entrance = entrance, Executor = executor, Profile = profile, Settings = settings,
            SpawnTime = formationOnly ? 0f : Mathf.Max(0, Array.IndexOf(spawnOrder, index)) * wave.GetSimulationSpawnInterval(),
            FireMode = resolved ? local != null ? local.ResolveFireMode(settings.FireMode) : settings.FireMode : DirectedWaveAttackFireMode.Default,
            AimMode = local != null ? local.ResolveAimMode(settings.AimMode) : settings.AimMode,
            Stationary = local != null ? local.ResolveStationaryFireSettings(settings.StationaryFireSettings) : settings.StationaryFireSettings,
            RotationOverride = usesEnemyRotation && local != null ? local.RotationOverride : null,
            Prefabs = new[] { serialized.FindProperty("defaultProjectilePrefab").objectReferenceValue as EnemyBullet,
                serialized.FindProperty("homingProjectilePrefab").objectReferenceValue as EnemyBullet,
                serialized.FindProperty("burstAtPointProjectilePrefab").objectReferenceValue as EnemyBullet } };
        if (sequenceSpeed != 1f && slot.RotationOverride != null)
        {
            var scaledRotation = new EnemyRotationSettings(); scaledRotation.CopyFrom(slot.RotationOverride);
            scaledRotation.ScaleAttackTiming(sequenceSpeed); slot.RotationOverride = scaledRotation;
        }
        slot.Patterns = new Pattern[profile.PatternCount];
        for (int i = 0; i < slot.Patterns.Length; i++)
        {
            var pattern = new Pattern { Settings = profile.GetPattern(i) };
            pattern.InitialFrame = EnemyShootingPreview.CreateFrame(executor, pattern.Settings.Shooting,
                prefab.transform.position, 0f, slot.RotationOverride, out pattern.Basis);
            var point = serialized.FindProperty("projectileSpawnPoint").objectReferenceValue as Transform;
            var pivot = serialized.FindProperty("rotationTarget").objectReferenceValue as Transform;
            pattern.OwnedPoint = point == null || point == prefab.transform || point.IsChildOf(prefab.transform);
            pattern.PivotOffset = pivot != null ? pivot.position - prefab.transform.position : Vector3.zero;
            slot.Patterns[i] = pattern;
        }
        slot.Visual = visual != null ? visual : CreateEnemyVisual(prefab);
        if (!visualPoses.TryGetValue(slot.Visual, out slot.Pose))
        {
            slot.Pose = new VisualPose(slot.Visual);
            visualPoses.Add(slot.Visual, slot.Pose);
        }
        slots.Add(slot);
        if (sequenceBase) return slot;
        if (!resolved && !serialized.FindProperty("autonomousAttack").boolValue)
        { slot.FireMode = DirectedWaveAttackFireMode.None; return slot; }
        Group group = suppliedGroup;
        // Identically configured groups still own independent runtime controllers and clocks.
        if (resolved && group == null)
            for (int i = 0; i < groups.Count; i++)
                if (!groups[i].SequenceOwned && !groups[i].Autonomous && groups[i].Entrance == entrance && groups[i].PatternIndex == patternIndex)
                { group = groups[i]; break; }
        if (group == null)
        {
            group = new Group { PatternIndex = patternIndex, Settings = settings, Entrance = entrance, Autonomous = !resolved,
                Numeric = settings.CreateSchedulingNumeric(NumericRandomPreviewControls.SeedOffset) };
            group.NextTime = autonomous ? 0f : entrance ? float.PositiveInfinity : resolved ? postStart + settings.SampleStartDelay(group.Numeric) : 0f;
            groups.Add(group);
        }
        group.Slots.Add(slot); slot.Group = group;
        if (group.SequenceOwned) slot.RemainingSequenceAttacks = slot.FireMode == DirectedWaveAttackFireMode.None ? 0 : int.MaxValue;
        if (settings.UsesDiveMovement)
            Warning = "Enemy positions follow the movement Preview; player-dependent attack dives are not simulated.";
        return slot;
    }

    private void BuildEntrance(int index, Transform visual, float[] checkpoints, int loopStart, float loopDuration)
    {
        if (!wave.HasPhaseEntry) return;
        var behaviour = wave.AttackPatternBehaviour;
        if (behaviour == null || !behaviour.enabled) return;
        if (!behaviour.EditorPreviewEntranceSettings.IsEnabled)
        {
            if (behaviour.AllowAutonomousAttackDuringEntrance) BuildSlot(index, true, visual, true);
            return;
        }
        var slot = BuildSlot(index, true, visual);
        if (slot == null || slot.FireMode == DirectedWaveAttackFireMode.None) return;
        float delay = Mathf.Max(0, Array.IndexOf(spawnOrder, index)) * wave.GetSimulationSpawnInterval();
        var settings = behaviour.EditorPreviewEntranceSettings;
        foreach (var rule in settings.AtCheckpointRules)
        {
            if (rule == null || !rule.IsEnabled || !rule.AllowsEnemySlot(index) || rule.CheckpointIndex >= checkpoints.Length) continue;
            entranceSequences.Add(new EntranceSequence { Slot = slot, Start = delay + checkpoints[rule.CheckpointIndex],
                Count = rule.ShotCount, LoopDuration = rule.CheckpointIndex >= loopStart && loopStart >= 0 ? loopDuration : 0f });
        }
        foreach (var rule in settings.AcrossCheckpointRules)
        {
            if (rule == null || !rule.IsEnabled || !rule.AllowsEnemySlot(index)
                || rule.StartCheckpointIndex >= checkpoints.Length || rule.EndCheckpointIndex >= checkpoints.Length) continue;
            float duration = checkpoints[rule.EndCheckpointIndex] - checkpoints[rule.StartCheckpointIndex];
            if (duration <= 0f && loopDuration > 0f) duration += loopDuration;
            if (duration <= 0f) continue;
            int count = rule.AttackCount;
            if (rule.AttackCountMode == DirectedWaveEntranceAttackCountMode.TotalForGroup)
            {
                // The preview uses a stable formation-slot order instead of mutating Unity's random state.
                int eligible = 0, order = 0;
                for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
                    if (rule.AllowsEnemySlot(i)) { if (i < index) order++; eligible++; }
                count = Mathf.Max(0, (rule.AttackCount + eligible - 1 - order) / Mathf.Max(1, eligible));
            }
            if (count > 0) entranceSequences.Add(new EntranceSequence { Slot = slot,
                Start = delay + checkpoints[rule.StartCheckpointIndex], Duration = duration, Count = count,
                LoopDuration = rule.StartCheckpointIndex >= loopStart && loopStart >= 0 ? loopDuration : 0f });
        }
        var continuous = settings.ContinuousAttackRule;
        if (continuous.IsEnabled && slot.Group != null)
        {
            float start = continuous.StartMode == DirectedWaveContinuousEntranceAttackStartMode.Checkpoint
                ? continuous.CheckpointIndex < checkpoints.Length ? checkpoints[continuous.CheckpointIndex] : float.PositiveInfinity
                : loopDuration > 0f && checkpoints.Length > 0 ? checkpoints[checkpoints.Length - 1] : float.PositiveInfinity;
            slot.Group.NextTime = Mathf.Min(slot.Group.NextTime, delay + start);
        }
    }

    private Transform CreateEnemyVisual(Enemy prefab)
    {
        var root = new GameObject(prefab.name + " Preview") { hideFlags = HideFlags.HideAndDontSave };
        root.transform.SetParent(visualsRoot.transform);
        root.transform.rotation = prefab.transform.rotation;
        root.transform.localScale = prefab.transform.lossyScale;
        var transforms = new Dictionary<Transform, Transform> { [prefab.transform] = root.transform };
        foreach (var source in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var renderer = VisualTransform(source.transform).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite; renderer.color = source.color;
            renderer.flipX = source.flipX; renderer.flipY = source.flipY;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            renderer.drawMode = source.drawMode; renderer.size = source.size; renderer.tileMode = source.tileMode;
            renderer.maskInteraction = source.maskInteraction; renderer.spriteSortPoint = source.spriteSortPoint;
            renderer.enabled = source.enabled;
            renderer.forceRenderingOff = !renderersVisible;
            visualRenderers.Add(renderer);
        }
        return root.transform;

        Transform VisualTransform(Transform source)
        {
            if (transforms.TryGetValue(source, out var existing)) return existing;
            var parent = VisualTransform(source.parent);
            var child = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(parent, false);
            // Preserve the authored hierarchy, including mirrored/nonuniform scales.
            // Flattening lossyScale loses shear and cannot safely divide a mirrored axis.
            child.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            child.transform.localScale = source.localScale;
            child.SetActive(source.gameObject.activeSelf);
            transforms.Add(source, child.transform);
            return child.transform;
        }
    }

    public static void CreateTargetVisual(GameObject target, Sprite sprite, out Mesh mesh, out Material material)
    {
        mesh = null;
        material = null;
        if (sprite != null)
        {
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = 100;
            return;
        }
        mesh = new Mesh { name = "Preview Player Mesh", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[] { new Vector3(0, .35f), new Vector3(-.25f, -.25f), new Vector3(0, -.12f), new Vector3(.25f, -.25f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
        material.SetColor("_BaseColor", new Color(.2f, .95f, 1f));
        material.SetFloat("_Cull", 0f);
        target.AddComponent<MeshFilter>().sharedMesh = mesh;
        target.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingEditMode) Dispose(); }

    private void UpdatePositions(float time, bool deferPresentation = false)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Positions);
        Matrix4x4 parentMatrix = previewParent != null ? previewParent.localToWorldMatrix : Matrix4x4.identity;
        bool parentChanged = !positionsParentMatrix.Equals(parentMatrix);
        if (positionsTime == time && !parentChanged) return;
        positionsTime = time;
        positionsParentMatrix = parentMatrix;
        bool finalFormation = stationaryFormation || !hasPostMovement && time >= postStart;
        if (finalFormation && (!positionsAreFinal || positions.Count == 0 || parentChanged))
        {
            for (int i = 0; i < slots.Count; i++) positions[slots[i].Index] = wave.GetSimulationFormationPosition(slots[i].Index, previewParent);
            positionsAreFinal = true;
            PhaseName = "Formation";
        }
        else if (!finalFormation)
        {
            wave.EvaluateEditorPreviewSubsetNonAlloc(time, previewParent, spawnOrder, previewSpawnSteps, positions, out string phaseName);
            PhaseName = phaseName;
        }
        if (eventSink != null && observedPhase != PhaseName)
        {
            observedPhase = PhaseName;
            Observe(WavePreviewEventKind.PhaseChange, detail: PhaseName);
        }
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            bool visible = positions.TryGetValue(slot.Index, out var position);
            if (stationaryFormation && !formationOnly)
                visible &= time >= slot.SpawnTime;
            slot.Pose.Visible = visible;
            if (!visible)
            {
                if (slot.Pose.RequiresLiveSource) slot.Pose.UpdateAnchor();
                continue;
            }
            if (eventSink != null && observedSpawns.Add(slot.Index)) Observe(WavePreviewEventKind.EnemySpawn, slot.Index);
            slot.Position = position; slot.Pose.Position = position;
            if (slot.Pose.RequiresLiveSource) slot.Pose.UpdateAnchor();
            if (slot.Active)
            {
                slot.Pose.FacingSlot = slot;
                slot.Pose.FacingElapsed = Mathf.Max(0f, time - slot.RotationStart);
                slot.Pose.FacingAim = slot.FireMode == DirectedWaveAttackFireMode.Aimed
                    ? (TargetPosition - position).normalized : (Vector3?)null;
            }
        }
        if (!deferPresentation) PublishVisuals();
    }

    public void CopyPreviewPositionsTo(Dictionary<int, Vector3> result)
    {
        result.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.Pose.Visible && positions.TryGetValue(slot.Index, out var position))
                result[slot.Index] = position;
        }
    }

    public bool AdvanceTo(float destination, double budgetMilliseconds = 4d)
    {
        if (disposed) return true;
        if (!ownsProjectiles) throw new InvalidOperationException("Shared preview worlds must advance through the conductor.");
        workTimer.Restart();
        int steps = 0;
        while (Time + StepInterval <= destination + .00001f)
        {
            Tick(Time);
            float next = Time + StepInterval;
            projectiles.Step(next, StepInterval, TargetPosition);
            Time = next;
            if (++steps >= 120 || workTimer.Elapsed.TotalMilliseconds >= budgetMilliseconds) break;
        }
        UpdatePositions(Time);
        projectiles.CaptureRenderState();
        return destination - Time < StepInterval + .00001f;
    }

    // The conductor ticks every subwave's emissions, then advances the common world exactly once.
    public void PrepareSharedStep(float time, int emissionBudget, bool deferPresentation = false)
    {
        if (disposed || ownsProjectiles) return;
        TickWithBudget(time, emissionBudget, deferPresentation);
        Time = time + StepInterval;
        UpdatePositions(Time, deferPresentation);
    }

    private void Tick(float time)
        => TickWithBudget(time, MaximumVolley);

    private void TickWithBudget(float time, int emissionBudget, bool deferPresentation = false)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SessionTick);
        remainingStepEmissions = Mathf.Min(emissionBudget, Mathf.Max(0, EnemyProjectilePreviewWorld.MaximumProjectiles - projectiles.Count));
        UpdatePositions(time, deferPresentation);
        float worldTime = time;
        time = CoordinatorAttackTime(time);
        BindCoordinatorPostClock(worldTime, time);
        if (!coordinated || !coordinatorPermissions.Interrupted) TickEntrance(worldTime, time);
        UpdateCoordinatorCycles(time);
        if (coordinated && coordinatorPermissions.Interrupted) return;
        if (sequencePlayer != null) sequencePlayer.AllowNewAttacks = !coordinated || coordinatorPermissions.AllowNewAttacks;
        if (worldTime >= postStart) TickCoordinatorSequence(time);
        for (int i = 0; i < groups.Count; i++) if (!groups[i].SequenceOwned) TickGroup(groups[i], time);
        for (int i = 0; i < slots.Count; i++) if (slots[i].Group == null || !slots[i].Group.SequenceOwned) TickSlot(slots[i], time);
        for (int i = volleys.Count - 1; i >= 0; i--)
        {
            var volley = volleys[i];
            if (volley.Slot.Entrance && worldTime >= postStart)
            { volley.LaunchOrder.Cancel(); volleys.RemoveAt(i); volleyPool.Push(volley); continue; }
            var layout = volley.Shooting.EmissionLayout;
            int directions = Mathf.Min(MaximumDirections, layout.EmissionCount);
            while (volley.Order < directions)
            {
                int order = SampleIndex(volley.Order, directions, layout.EmissionCount);
                float delay = layout.IsSequential ? layout.StartDelay + order * layout.LaunchInterval : 0f;
                if (time + .00001f < volley.Start + delay) break;
                int direction = layout.IsSequential ? volley.LaunchOrder.GetLaunchIndex(layout, order) : order;
                EmitDirection(volley, direction, time);
                volley.Order++;
            }
            if (volley.Order < directions) continue;
            volley.LaunchOrder.Complete();
            volleys.RemoveAt(i); volleyPool.Push(volley);
        }
    }

    private void TickGroup(Group group, float time)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.AttackGroup);
        if (coordinated && (!coordinatorPermissions.AllowNewAttacks
            || !group.SequenceOwned && !group.Entrance && !group.Autonomous && coordinatorCycleLimit >= 0 && group.CoordinatorCycles >= coordinatorCycleLimit)) return;
        if (group.Entrance && (coordinated ? positionsTime : time) >= postStart) return;
        if (coordinated && group.Entrance && !group.CoordinatorEntranceClockBound)
        {
            if (positionsTime < group.NextTime) return;
            group.NextTime = time; group.CoordinatorEntranceClockBound = true;
        }
        if (!group.Autonomous && !group.Entrance && group.Settings.UsesAttackOrder)
        { TickOrderedGroup(group, time); return; }
        if (time < group.NextTime || group.Settings.WaitsForPreviousAttack && group.Active > 0) return;
        PrepareGroupCycle(group);
        for (int attempt = 0; attempt < group.Slots.Count; attempt++)
        {
            var slot = group.Slots[group.NextSlot];
            if (++group.NextSlot >= group.Slots.Count) { group.NextSlot = 0; group.Round++; }
            if ((group.SequenceOwned || coordinated && !group.Entrance && !group.Autonomous && coordinatorCycleLimit >= 0) && slot.RemainingSequenceAttacks <= 0) continue;
            if (slot.Active || (coordinated ? positionsTime : time) < slot.SpawnTime || time < slot.ReadyTime || !positions.ContainsKey(slot.Index) || slot.FireMode == DirectedWaveAttackFireMode.None) continue;
            StartPreviewAttack(slot, group, time);
            group.NextTime = time + (group.Autonomous ? StepInterval : 1f / group.Settings.SampleAttacksPerSecond(group.Numeric));
            return;
        }
    }

    private void StartPreviewAttack(Slot slot, Group group, float time)
    {
        Observe(WavePreviewEventKind.AttackStart, slot.Index);
        slot.Active = true; group.Active++; slot.AttackAim = default;
        if ((group.SequenceOwned || coordinated && !group.Entrance && !group.Autonomous) && !group.Settings.UsesAttackOrder) slot.RemainingSequenceAttacks--;
        slot.CompletionTime = time; BeginRotation(slot, time);
        for (int i = 0; i < slot.Patterns.Length; i++)
        {
            var pattern = slot.Patterns[i]; pattern.Aim = default; pattern.CircleAimCaptured = false; pattern.Speed = default;
            slot.SectorOrders.BeginAttack(pattern.Settings.Shooting.EmissionLayout);
            pattern.Numeric = slot.Numeric.Get(pattern.Settings); pattern.Numeric?.BeginSpeed();
            pattern.OriginCapture.Begin(pattern.Settings.Shooting.EmissionOrigin, TargetPosition, pattern.Numeric);
            pattern.Schedule.Begin(pattern.Settings, time, 1f, slot.Profile.HasMultiplePatterns || group.Autonomous || pattern.Settings.AttackStartDelayRandom.enabled, pattern.Numeric);
        }
    }

    private static void BeginRotation(Slot slot, float time)
    {
        // Runtime binds a rotation clock to the profile, not to each cooldown cycle.
        if (slot.RotationStarted) return;
        slot.RotationStarted = true; slot.RotationStart = time;
    }

    private void TickEntrance(float time, float? emissionTime = null)
    {
        for (int i = 0; i < entranceSequences.Count; i++)
        {
            var sequence = entranceSequences[i];
            if (time >= postStart || !positions.ContainsKey(sequence.Slot.Index)) continue;
            if (coordinated && !coordinatorPermissions.AllowNewAttacks && sequence.Next == 0) continue;
            if (time + .00001f >= sequence.Start && sequence.OriginCapture == null)
            {
                sequence.OriginCapture = new EnemyEmissionOriginAttackCapture(TargetPosition);
                BeginRotation(sequence.Slot, emissionTime ?? time);
            }
            int events = 0;
            while (sequence.Next < sequence.Count && events++ < 64)
            {
                float due = sequence.Start + sequence.Duration * (sequence.Next + 1f) / (sequence.Count + 1f);
                if (time + .00001f < due) break;
                if (sequence.Next == 0)
                {
                    sequence.Slot.AttackAim = default;
                    for (int p = 0; p < sequence.Slot.Patterns.Length; p++)
                    { sequence.Slot.Patterns[p].Aim = default; sequence.Slot.Patterns[p].CircleAimCaptured = false;
                        sequence.Slot.SectorOrders.BeginAttack(sequence.Slot.Patterns[p].Settings.Shooting.EmissionLayout);
                        sequence.Slot.Patterns[p].OriginCapture.Begin(sequence.Slot.Patterns[p].Settings.Shooting.EmissionOrigin, TargetPosition); }
                }
                for (int p = 0; p < sequence.Slot.Patterns.Length; p++)
                {
                    var pattern = sequence.Slot.Patterns[p];
                    bool burstStart = pattern.Settings.RepeatBurst && sequence.Next % pattern.Settings.BurstShotCount == 0;
                    QueueVolley(sequence.Slot, pattern, emissionTime ?? time, burstStart,
                        sequence.OriginCapture.Prepare(sequence.Slot.Profile, p, TargetPosition, burstStart, sequence.Slot.Numeric),
                        sequence.OriginCapture.RandomOffsets(p), sequence.OriginCapture.Numeric(p));
                }
                sequence.Next++;
            }
            if (sequence.Next == sequence.Count && sequence.LoopDuration > 0f)
            { sequence.Start += sequence.LoopDuration; sequence.Next = 0; sequence.OriginCapture = null; }
        }
    }

    private void TickSlot(Slot slot, float time)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.AttackSlot);
        if (!slot.Active) return;
        bool pending = false;
        for (int i = 0; i < slot.Patterns.Length; i++)
        {
            var pattern = slot.Patterns[i];
            int events = 0;
            while (pattern.Schedule.IsDue(time) && events++ < 64)
            {
                QueueVolley(slot, pattern, time, pattern.Schedule.IsBurstStart);
                pattern.Schedule.Advance(pattern.Settings, time, 1f);
            }
            pending |= !pattern.Schedule.IsComplete;
        }
        if (pending || time + .00001f < slot.CompletionTime) return;
        slot.Active = false; slot.Group.Active--;
        Observe(WavePreviewEventKind.AttackEnd, slot.Index);
        slot.ReadyTime = time + EnemyAttackCooldownSampler.Sample(slot.Profile, slot.Numeric,
            slot.Group.SequenceOwned || slot.Group.Settings.UsesAttackOrder || slot.Group.Settings.EnemyAttackCooldownRandom.enabled ? slot.Group.Settings : null);
        for (int i = 0; i < slot.Patterns.Length; i++) slot.Numeric.Release(slot.Patterns[i].Settings);
        if (slot.Group.Settings.WaitsForPreviousAttack && !slot.Group.Settings.UsesAttackOrder)
            slot.Group.NextTime = Mathf.Max(slot.Group.NextTime, time + slot.Group.Settings.SampleDelayAfterAttack(slot.Group.Numeric));
    }

    private EnemyEmissionPreviewFrame Frame(Slot slot, Pattern pattern, float time)
    {
        var frame = pattern.InitialFrame;
        Vector3 delta = slot.Position - frame.EnemyPosition;
        frame.EnemyPosition = slot.Position;
        if (pattern.OwnedPoint) frame.ShootingPoint += delta;
        var shooting = pattern.Settings.Shooting;
        if (shooting.EmissionLayout.RotateShootingPointOffset && shooting.Rotates)
        {
            Vector3 center = slot.Position + pattern.PivotOffset;
            frame.ShootingPoint = center + Quaternion.Euler(0, 0, shooting.EvaluateLayoutAngle(time, slot.RotationOverride))
                * (frame.ShootingPoint - center);
        }
        return frame;
    }

    private void QueueVolley(Slot slot, Pattern pattern, float time, bool burstStart, Vector3? capturedPlayer = null, Vector2? capturedOffsets = null, EnemyAttackNumericState numericOverride = null)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.QueueVolley);
        if (volleys.Count >= 128) { omittedRays += pattern.Settings.ProjectilesPerShot; return; }
        var shooting = pattern.Settings.Shooting;
        pattern.Numeric ??= slot.Numeric.Get(pattern.Settings);
        var numericSpeed = (numericOverride ?? pattern.Numeric)?.PrepareSpeed(shooting, burstStart && pattern.Settings.RepeatBurst,
            shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Consistent ? pattern.Speed.ConsistentDelta : 0f) ?? default;
        shooting = shooting.SampleGeometry(numericSpeed.Numeric);
        var prefab = shooting.ResolveProjectilePrefab(slot.Prefabs[shooting.ProjectileType == EnemyProjectileType.Laser ? 0 : (int)shooting.ProjectileType] ?? slot.Prefabs[0]);
        if (prefab == null && shooting.ProjectileType != EnemyProjectileType.Laser) { Warning = "Missing projectile prefab in the enemy executor."; return; }
        float rotationTime = Mathf.Max(0f, time - slot.RotationStart);
        var frame = Frame(slot, pattern, rotationTime);
        pattern.Numeric ??= slot.Numeric.Get(pattern.Settings);
        frame.PlayerPosition = pattern.OriginCapture.Prepare(shooting.EmissionOrigin, TargetPosition, burstStart, burstStart && pattern.Settings.RepeatBurst);
        frame.PlayerPosition = capturedPlayer ?? frame.PlayerPosition;
        if (EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera.main, slot.Position.z, out var bounds))
            frame.GameplayBounds = bounds;
        else if (shooting.EmissionOrigin.RequiresBounds)
        { Warning = "Emission Origin Boundary requires a gameplay camera."; return; }
        var offsets = capturedOffsets ?? pattern.OriginCapture.RandomOffsets;
        frame.Origin = offsets.HasValue ? shooting.EmissionOrigin.Resolve(frame.EnemyPosition, frame.PlayerPosition.GetValueOrDefault(), frame.GameplayBounds.GetValueOrDefault(), offsets.Value)
            : EnemyShootingPreviewGeometry.ResolveOrigin(shooting, frame);
        pattern.LastOrigin = frame.Origin;
        var firing = shooting.PresetFiring;
        var local = wave.GetConfiguredEnemyOverrideForSlot(slot.Index);
        var fireMode = firing.enabled ? local != null ? local.ResolveFireMode(firing.fireMode) : firing.fireMode : slot.FireMode;
        var aimMode = firing.enabled ? local != null ? local.ResolveAimMode(firing.aimMode) : firing.aimMode : slot.AimMode;
        var stationarySettings = firing.enabled ? local != null ? local.ResolveStationaryFireSettings(firing.stationaryFireSettings) : firing.stationaryFireSettings : slot.Stationary;
        bool aimed = fireMode == DirectedWaveAttackFireMode.Aimed;
        bool stationary = fireMode == DirectedWaveAttackFireMode.Stationary && !stationarySettings.PreservesLegacyForward;
        Vector3 direction = pattern.Basis * Vector3.up;
        if (aimed)
        {
            Vector3 aimOrigin = EnemyShootingPreviewGeometry.GetEmitter(shooting, 0, frame,
                pattern.Basis, rotationTime, slot.RotationOverride).Position;
            bool refresh = aimMode == DirectedWaveAttackAimMode.AimContinuous
                || aimMode == DirectedWaveAttackAimMode.AimOnBurst && burstStart;
            direction = aimMode == DirectedWaveAttackAimMode.AimOnStart
                ? slot.AttackAim.ResolveDirection(aimOrigin, TargetPosition, false)
                : pattern.Aim.ResolveDirection(aimOrigin, TargetPosition, refresh);
            if ((shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Circle || !shooting.EmissionOrigin.IsDefault)
                && (!pattern.CircleAimCaptured || refresh))
            {
                int count = Mathf.Min(MaximumDirections, shooting.DirectionCount);
                if (pattern.CircleDirections == null || pattern.CircleDirections.Length != count)
                    pattern.CircleDirections = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    int arm = SampleIndex(i, count, shooting.DirectionCount);
                    var emitter = EnemyShootingPreviewGeometry.GetEmitter(shooting, arm, frame, pattern.Basis, rotationTime, slot.RotationOverride);
                    pattern.CircleDirections[i] = (TargetPosition - emitter.Position).normalized;
                }
                pattern.CircleAimCaptured = true;
            }
        }
        if (stationary) direction = stationarySettings.Direction;
        if (fireMode == DirectedWaveAttackFireMode.None) return;
        if (fireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead
            && !slot.Settings.IsPlayerInForwardFireSector(slot.Position, direction, TargetPosition,
                firing.enabled ? local != null ? local.ResolveForwardFireHalfAngle(firing.forwardFireHalfAngle) : firing.forwardFireHalfAngle : (float?)null)) return;
        var volley = volleyPool.Count > 0 ? volleyPool.Pop() : new Volley();
        volley.LaunchOrder = slot.SectorOrders.Reserve(shooting.EmissionLayout);
        volley.Shooting = shooting;
        volley.Slot = slot; volley.Pattern = pattern; volley.Start = time; volley.Order = 0;
        volley.Origin = shooting.EmissionOrigin.IsDefault ? (Vector3?)null : frame.Origin;
        if (shooting.EmissionLayout.IsSequential && shooting.EmissionLayout.WaitUntilSectorReady)
            volley.Start = Mathf.Max(volley.Start, postStart);
        volley.Direction = direction; volley.Aimed = aimed; volley.Stationary = stationary;
        volley.AimMode = aimMode; volley.StationarySettings = stationarySettings;
        volley.HasCircleDirections = pattern.CircleAimCaptured;
        if (volley.HasCircleDirections)
        {
            if (volley.CircleDirections == null || volley.CircleDirections.Length != pattern.CircleDirections.Length)
                volley.CircleDirections = new Vector3[pattern.CircleDirections.Length];
            Array.Copy(pattern.CircleDirections, volley.CircleDirections, pattern.CircleDirections.Length);
        }
        var authoring = prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>() : null;
        volley.NumericSpeed = numericSpeed;
        volley.Speed = pattern.Speed.GetVolleySpeed(shooting, authoring != null ? authoring.BaseSpeed : 1f);
        if (shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Random)
            volley.Speed = Mathf.Lerp(shooting.RandomInitialSpeedMinimum, shooting.RandomInitialSpeedMaximum, Random01());
        pattern.Speed.AdvanceAfterVolley(shooting);
        volley.Spread = shooting.ShootingMode == EnemyProjectileFireMode.Random
            ? Mathf.Lerp(shooting.RandomMinAngle, shooting.RandomMaxAngle, Random01()) : 0f;
        volleys.Add(volley);
        slot.CompletionTime = Mathf.Max(slot.CompletionTime, volley.Start + shooting.EmissionLayout.LaunchDuration + StepInterval);
        if (shooting.DirectionCount > MaximumDirections)
            omittedRays += (long)(shooting.DirectionCount - MaximumDirections) * Mathf.Min(MaximumVolley, shooting.ProjectilesPerDirection);
    }

    private float Random01()
    {
        random ^= random << 13; random ^= random >> 17; random ^= random << 5;
        return (random & 0xffffff) / 16777216f;
    }

    private void EmitDirection(Volley volley, int arm, float time)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.EmitDirection);
        var slot = volley.Slot;
        if (!positions.ContainsKey(slot.Index)) return;
        var shooting = volley.Shooting.SampleGeometry(volley.NumericSpeed.Numeric.ForDirection(arm), true);
        if (shooting.ProjectileType == EnemyProjectileType.Laser)
        {
            slot.Pose.RequiresLiveSource = true;
            slot.Pose.UpdateAnchor();
        }
        if (remainingStepEmissions <= 0)
        { omittedRays += shooting.ProjectilesPerDirection; return; }
        float rotationTime = Mathf.Max(0f, time - slot.RotationStart);
        var frame = Frame(slot, volley.Pattern, rotationTime);
        frame.Origin = volley.Origin ?? slot.Position;
        bool point = volley.Stationary && volley.StationarySettings.DirectionMode == EnemyStationaryDirectionMode.WorldPoint;
        if (point) frame.Target = volley.StationarySettings.WorldPoint;
        bool continuous = volley.Aimed && volley.AimMode == DirectedWaveAttackAimMode.AimContinuous;
        if (continuous) frame.Target = TargetPosition;
        Vector3 requested = volley.Direction;
        if (volley.HasCircleDirections && !continuous)
        {
            int samples = volley.CircleDirections.Length;
            int sample = samples <= 1 ? 0 : Mathf.RoundToInt((float)arm * (samples - 1) / Mathf.Max(1, shooting.DirectionCount - 1));
            requested = volley.CircleDirections[sample];
        }
        var emitter = EnemyShootingPreviewGeometry.GetEmitter(shooting, arm, frame, volley.Pattern.Basis,
            rotationTime, slot.RotationOverride, requested, volley.Aimed || point, volley.Stationary);
        int perDirection = Mathf.Max(1, MaximumVolley / Mathf.Min(MaximumDirections, shooting.DirectionCount));
        int count = Mathf.Min(remainingStepEmissions, Mathf.Min(perDirection, shooting.ProjectilesPerDirection));
        remainingStepEmissions -= count;
        requests.Clear();
        if (shooting.UsesExtendedNumericRandomization)
        {
            EnemyAttackProjectileBuilder.AppendEmitter(requests, shooting.ResolveProjectilePrefab(slot.Prefabs[shooting.ProjectileType == EnemyProjectileType.Laser ? 0 : (int)shooting.ProjectileType] ?? slot.Prefabs[0]),
                shooting, emitter.Position, frame.Origin.Value, emitter.BaseDirection, 1f, volley.Speed, volley.Spread,
                new EnemyProjectilePrefabSet(slot.Prefabs[0], slot.Prefabs[1], slot.Prefabs[2]), maximum: count,
                randomSpeed: volley.NumericSpeed, projectileOffset: arm * shooting.ProjectilesPerDirection, directionIndex: arm,
                completionTracker: slot.Group?.Completion,
                laserOrigin: new EnemyLaserOrigin(slot.Visual, slot.Position, shooting.EmissionOrigin, Camera.main, player: targetObject.transform));
            omittedRays += shooting.ProjectilesPerDirection - requests.Count;
            SpawnObserved(slot.Index); return;
        }
        int fanCount = shooting.UsesFan ? shooting.FanProjectileCount : 1;
        int lastStack = -1;
        float spread = volley.Spread, speed = volley.Speed;
        for (int i = 0; i < count; i++)
        {
            int projectile = SampleIndex(i, count, shooting.ProjectilesPerDirection);
            int stack = projectile / fanCount;
            if (stack != lastStack && shooting.UsesShotGun && shooting.ShootingMode == EnemyProjectileFireMode.Random)
                spread = Mathf.Lerp(shooting.RandomMinAngle, shooting.RandomMaxAngle, Random01());
            if (!volley.NumericSpeed.Active && shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Random && (!shooting.UsesDnaOrbit || stack != lastStack))
                speed = Mathf.Lerp(shooting.RandomInitialSpeedMinimum, shooting.RandomInitialSpeedMaximum, Random01());
            if (volley.NumericSpeed.Active) speed = volley.NumericSpeed.Evaluate(arm * shooting.ProjectilesPerDirection + projectile);
            lastStack = stack;
            requests.Add(new EnemyProjectileSpawnRequest(shooting.ResolveProjectilePrefab(slot.Prefabs[shooting.ProjectileType == EnemyProjectileType.Laser ? 0 : (int)shooting.ProjectileType] ?? slot.Prefabs[0]),
                emitter.Position, shooting.GetProjectileDirection(emitter.BaseDirection, 0, projectile, spread), 1f,
                speed, shooting.SpeedBehavior, shooting.SpeedChangeY, shooting.SpeedByDistance,
                shooting.SizeBehavior, shooting.SizeChangeY, shooting.SizeChangeSpeed, shooting.SizeChangeCurve, shooting.FinalSize,
                frame.Origin.Value, shooting.MovementPattern, shooting.AngularSpeed, shooting.LateralSpeed,
                shooting.OrbitCenterMode, shooting.OrbitFixedWorldCenter, shooting.OrbitRadius, shooting.OrbitRadialSpeed,
                shooting.OverridesProjectileLifetime, shooting.ProjectileLifetime,
                shooting.UsesDnaOrbit ? shooting.GetDnaMedianDirection(emitter.BaseDirection, 0, spread) : Vector3.zero,
                shooting.UsesDnaOrbit ? shooting.GetDnaHalfCycleDuration() : 0f,
                shooting.OverwritesBaseColour,
                shooting.ProjectileColour,
                shooting.OverwritesProjectileCharacteristic,
                shooting.ProjectileCharacteristic, shooting.ChildAttack,
                new EnemyProjectilePrefabSet(slot.Prefabs[0], slot.Prefabs[1], slot.Prefabs[2]),
                speedCurveTarget: shooting.SpeedCurveTarget, completionTracker: slot.Group?.Completion, numericSeedKey: NumericRandomStream.Hash(volley.NumericSpeed.SeedKey ^ (uint)(arm * shooting.ProjectilesPerDirection + projectile)),
                laser: shooting.ProjectileType == EnemyProjectileType.Laser ? shooting.Laser : null,
                laserOrigin: shooting.ProjectileType == EnemyProjectileType.Laser
                    ? new EnemyLaserOrigin(slot.Visual, slot.Position, shooting.EmissionOrigin, Camera.main, player: targetObject.transform) : default));
        }
        omittedRays += shooting.ProjectilesPerDirection - count;
        SpawnObserved(slot.Index);
    }

    private void SpawnObserved(int slot)
    {
        int before = eventSink != null ? projectiles.Count : 0;
        projectiles.Spawn(requests);
        if (eventSink != null && projectiles.Count > before)
            Observe(WavePreviewEventKind.ProjectileSpawn, slot, projectiles.Count - before);
    }
    private void Observe(WavePreviewEventKind kind, int slot = -1, int count = 1, string detail = null)
        => eventSink?.Invoke(new WavePreviewEvent(eventTimeOffset + positionsTime, kind, eventCue, slot, count, detail));

    public void Draw(SceneView view)
    {
        if (disposed || Application.isPlaying || view == null) return;
        projectiles.RegisterCamera(view.camera);
        SetVisible(EnemyShootingPreview.ShowFormationVolley);
        if (!EnemyShootingPreview.ShowFormationVolley) return;
        if (Event.current.type == EventType.Repaint)
        {
            DrawEmissionOrigins();
            DrawAttackOrders();
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(TargetPosition, Vector3.forward, .4f);
            Handles.Label(TargetPosition + Vector3.down * .55f, "Preview Player · drag the handle");
        }
        EditorGUI.BeginChangeCheck();
        Vector3 next = Handles.PositionHandle(TargetPosition, Quaternion.identity);
        if (EditorGUI.EndChangeCheck()) { next.z = 0f; TargetPosition = next; view.Repaint(); }
    }

    private void DrawEmissionOrigins()
    {
        if (sequencePlayer != null)
            Handles.Label(wave.transform.position + Vector3.up * .6f, "Attack Sequence: " + sequencePlayer.CurrentStep);
        Color previousColor = Handles.color;
        Handles.color = new Color(1f, .35f, .9f, .95f);
        int drawn = 0;
        for (int i = 0; i < slots.Count && drawn < MaximumEnemies; i++)
        {
            var slot = slots[i];
            if (!slot.Pose.Visible) continue;
            for (int p = 0; p < slot.Patterns.Length && drawn < MaximumEnemies; p++)
            {
                var origin = slot.Patterns[p].LastOrigin;
                if (!origin.HasValue) continue;
                Handles.DrawWireDisc(origin.Value, Vector3.forward, .1f);
                Handles.DrawLine(origin.Value - Vector3.right * .15f, origin.Value + Vector3.right * .15f);
                Handles.DrawLine(origin.Value - Vector3.up * .15f, origin.Value + Vector3.up * .15f);
                Handles.Label(origin.Value + Vector3.up * .18f, $"Emission Origin · Slot {slot.Index}, Pattern {p + 1}");
                drawn++;
            }
        }
        Handles.color = previousColor;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        sequencePlayer?.Stop();
        sequenceRuns.Clear(); sequencePlayer = null;
        AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (ownsProjectiles) projectiles.Dispose();
        if (visualsRoot != null) Object.DestroyImmediate(visualsRoot);
        if (targetMesh != null) Object.DestroyImmediate(targetMesh);
        if (targetMaterial != null) Object.DestroyImmediate(targetMaterial);
        for (int i = 0; i < volleys.Count; i++) volleys[i].LaunchOrder.Cancel();
        sectorOrderRegistries.Clear();
        slots.Clear(); groups.Clear(); entranceSequences.Clear(); volleys.Clear(); volleyPool.Clear();
    }
}
