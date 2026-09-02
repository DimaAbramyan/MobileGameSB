using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DirectedEnemySubWave))]
public sealed class DirectedWaveAttackBehaviour : MonoBehaviour,
    IDirectedWavePostTimelineBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private DirectedWaveAttackSettings attackSettings =
        new DirectedWaveAttackSettings();

    [Header("Attack Preset")]
    [SerializeField] private DirectedWaveAttackPreset attackPreset;
    [SerializeField, InspectorName("Override Attacks Per Second")]
    private bool overridePresetAttacksPerSecond;
    [SerializeField, Min(0.01f), InspectorName("Attacks Per Second")]
    private float presetAttacksPerSecond = 1f;

    [Header("Post-Formation Attack Patterns")]
    [SerializeField] private List<DirectedWaveAttackPattern> attackPatterns = new();

    [Header("Entrance Attacks")]
    [SerializeField] private DirectedWaveEntranceAttackSettings
        entranceAttackSettings = new DirectedWaveEntranceAttackSettings();

    [Header("Autonomous Enemy Attacks")]
    [SerializeField, Tooltip(
        "Allows enemies with autonomous attack behaviour to fire before reaching their formation position.")]
    private bool allowAutonomousAttackDuringEntrance;

    [Header("Eligible Enemies")]
    [SerializeField, Tooltip(
        "When enabled, only the selected final formation slots can be controlled by this attack behaviour.")]
    private bool useSelectedEnemySlots;
    [SerializeField] private List<int> selectedEnemySlots = new();

    private DirectedEnemySubWave wave;
    private DirectedWaveAttackController attackController;
    private readonly List<DirectedWaveAttackController> attackPatternControllers = new();
    private readonly List<DirectedWaveAttackSettings> resolvedAttackPatternSettings = new();
    private DirectedWaveAttackController continuousEntranceAttackController;
    private DirectedWaveEntranceAttackController entranceAttackController;
    private readonly DirectedWaveAttackSettings resolvedAttackSettings =
        new DirectedWaveAttackSettings();
    private readonly HashSet<int> selectedEnemySlotSet = new();
    private readonly HashSet<Enemy> continuousEntranceAttackEnemies = new();
    private readonly Dictionary<Enemy, int> continuousEntranceEnemySlots = new();
    private bool isRegistered;
    private int lastContinuousEntranceAttackTickFrame = -1;

    public bool AllowAutonomousAttackDuringEntrance =>
        allowAutonomousAttackDuringEntrance;

    public bool UsesSelectedEnemySlots => useSelectedEnemySlots;

    public int AttackPatternCount => attackPatterns?.Count ?? 0;

    public void AddAttackPattern()
    {
        attackPatterns ??= new List<DirectedWaveAttackPattern>();
        attackPatterns.Add(new DirectedWaveAttackPattern());
    }

    public void RemoveAttackPattern(int patternIndex)
    {
        if (attackPatterns == null
            || patternIndex < 0
            || patternIndex >= attackPatterns.Count)
        {
            return;
        }

        attackPatterns.RemoveAt(patternIndex);
    }

    public void CopyResolvedAttackSettingsTo(DirectedWaveAttackPreset preset)
    {
        if (preset == null)
            return;

        RefreshResolvedAttackSettings();
        preset.SetAttackSettings(resolvedAttackSettings);
    }

    public void UsePresetAsLocalSettings()
    {
        RefreshResolvedAttackSettings();
        attackSettings.CopyFrom(resolvedAttackSettings);
        attackPreset = null;
        overridePresetAttacksPerSecond = false;
        RefreshResolvedAttackSettings();
    }

    public void CopyResolvedAttackPatternSettingsTo(
        int patternIndex,
        DirectedWaveAttackPreset preset)
    {
        if (!TryGetAttackPattern(patternIndex, out DirectedWaveAttackPattern pattern)
            || preset == null)
        {
            return;
        }

        pattern.CopyResolvedSettingsTo(preset);
    }

    public void UseAttackPatternPresetAsLocalSettings(int patternIndex)
    {
        if (TryGetAttackPattern(patternIndex, out DirectedWaveAttackPattern pattern))
            pattern.UsePresetAsLocalSettings();
    }

    private bool RequiresPostTimeline =>
        isActiveAndEnabled
        && (attackPatterns != null && attackPatterns.Count > 0
            || attackSettings != null);

    bool IDirectedWavePostTimelineBehaviour.RequiresPostTimeline =>
        RequiresPostTimeline;

    private void Awake()
    {
        wave = GetComponent<DirectedEnemySubWave>();
        entranceAttackSettings ??= new DirectedWaveEntranceAttackSettings();
        RefreshResolvedAttackSettings();
        entranceAttackSettings.Validate();
        entranceAttackController = new DirectedWaveEntranceAttackController(
            wave,
            resolvedAttackSettings,
            entranceAttackSettings);
        RebuildSelectedEnemySlotSet();
    }

    private void OnEnable()
    {
        RegisterWithWave();
    }

    private void OnDisable()
    {
        attackController?.Stop();
        StopAttackPatternControllers();
        StopContinuousEntranceAttack();
        entranceAttackController?.Stop();
        UnregisterFromWave();
    }

    private void OnValidate()
    {
        entranceAttackSettings ??= new DirectedWaveEntranceAttackSettings();
        RefreshResolvedAttackSettings();
        entranceAttackSettings.Validate();
        RebuildSelectedEnemySlotSet();
        ValidateAttackPatterns();
    }

    void IDirectedWavePostTimelineBehaviour.OnPostTimelineStarted(
        DirectedEnemySubWave hostWave)
    {
        StopContinuousEntranceAttack();
        entranceAttackController?.Stop();

        if (!RequiresPostTimeline)
            return;

        if (wave != hostWave)
            wave = hostWave;

        RefreshResolvedAttackSettings();
        attackController?.Stop();
        if (UsesAttackPatterns())
        {
            StartAttackPatternControllers();
            return;
        }

        StopAttackPatternControllers();

        if (!wave.HasAttackTarget && resolvedAttackSettings.RequiresPlayerTarget)
        {
            Debug.LogWarning(
                "PlayerController was not injected. Directed Wave Attack Behaviour will not attack.",
                this);
        }

        attackController ??= new DirectedWaveAttackController(
            wave,
            resolvedAttackSettings,
            CanEnemyAttack);
        attackController.Begin();
    }

    void IDirectedWavePostTimelineBehaviour.TickPostTimeline()
    {
        attackController?.Tick();
        for (int i = 0; i < attackPatternControllers.Count; i++)
            attackPatternControllers[i]?.Tick();
    }

    void IDirectedWavePostTimelineBehaviour.OnPostTimelineStopped()
    {
        attackController?.Stop();
        StopAttackPatternControllers();
    }

    void IDirectedWavePostTimelineBehaviour.OnWaveEnemyDestroyed(Enemy enemy)
    {
        entranceAttackController?.NotifyEnemyDestroyed(enemy);
        StopContinuousEntranceAttackForEnemy(enemy);
        attackController?.NotifyEnemyDestroyed(enemy);
        for (int i = 0; i < attackPatternControllers.Count; i++)
            attackPatternControllers[i]?.NotifyEnemyDestroyed(enemy);
    }

    internal void BeginEntranceAttacks()
    {
        if (!isActiveAndEnabled)
        {
            entranceAttackController?.Stop();
            return;
        }

        wave ??= GetComponent<DirectedEnemySubWave>();
        entranceAttackSettings ??= new DirectedWaveEntranceAttackSettings();
        RefreshResolvedAttackSettings();
        entranceAttackSettings.Validate();
        StopContinuousEntranceAttack();
        entranceAttackController ??= new DirectedWaveEntranceAttackController(
            wave,
            resolvedAttackSettings,
            entranceAttackSettings);
        entranceAttackController.Begin();
    }

    internal void NotifyEntranceCheckpointReached(
        Enemy enemy,
        int enemySlotIndex,
        int checkpointIndex,
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        bool isLoopRestart = false)
    {
        entranceAttackController?.NotifyCheckpointReached(
            enemy,
            enemySlotIndex,
            checkpointIndex,
            checkpoints);

        if (entranceAttackController != null
            && entranceAttackController.ShouldActivateContinuousRouteAttack(
                enemy,
                checkpointIndex,
                isLoopRestart))
        {
            StartContinuousEntranceAttack(enemy, enemySlotIndex);
        }
    }

    internal void NotifyEntranceSegmentAdvanced(Enemy enemy, float deltaTime)
    {
        entranceAttackController?.NotifySegmentAdvanced(enemy, deltaTime);
        TickContinuousEntranceAttack();
    }

    internal void NotifyEnemyEntranceCompleted(Enemy enemy)
    {
        entranceAttackController?.NotifyEnemyEntranceCompleted(enemy);
        StopContinuousEntranceAttackForEnemy(enemy);
    }

    private void RegisterWithWave()
    {
        if (isRegistered)
            return;

        wave ??= GetComponent<DirectedEnemySubWave>();
        if (wave == null)
            return;

        wave.RegisterPostTimelineBehaviour(this);
        isRegistered = true;
    }

    private void UnregisterFromWave()
    {
        if (!isRegistered || wave == null)
            return;

        wave.UnregisterPostTimelineBehaviour(this);
        isRegistered = false;
    }

    private bool CanEnemyAttack(Enemy enemy)
    {
        if (!useSelectedEnemySlots)
            return true;

        return wave != null
            && wave.TryGetFormationIndex(enemy, out int slotIndex)
            && selectedEnemySlotSet.Contains(slotIndex);
    }

    private bool UsesAttackPatterns()
    {
        return attackPatterns != null && attackPatterns.Count > 0;
    }

    private bool TryGetAttackPattern(
        int patternIndex,
        out DirectedWaveAttackPattern pattern)
    {
        pattern = null;
        if (attackPatterns == null
            || patternIndex < 0
            || patternIndex >= attackPatterns.Count)
        {
            return false;
        }

        pattern = attackPatterns[patternIndex];
        return pattern != null;
    }

    private void StartAttackPatternControllers()
    {
        StopAttackPatternControllers();
        ValidateAttackPatterns();

        for (int i = 0; i < attackPatterns.Count; i++)
        {
            DirectedWaveAttackPattern pattern = attackPatterns[i];
            if (pattern == null || pattern.SelectedEnemySlots.Count == 0)
                continue;

            DirectedWaveAttackSettings resolvedSettings =
                new DirectedWaveAttackSettings();
            pattern.CopyResolvedSettingsTo(resolvedSettings);
            resolvedAttackPatternSettings.Add(resolvedSettings);

            if (!wave.HasAttackTarget && resolvedSettings.RequiresPlayerTarget)
            {
                Debug.LogWarning(
                    "PlayerController was not injected. Directed Wave Attack Behaviour will not attack.",
                    this);
            }

            HashSet<int> selectedSlots = new(pattern.SelectedEnemySlots);
            DirectedWaveAttackController controller = new(
                wave,
                resolvedSettings,
                enemy => CanEnemyAttack(enemy, selectedSlots));
            attackPatternControllers.Add(controller);
            controller.Begin();
        }
    }

    private void StopAttackPatternControllers()
    {
        for (int i = 0; i < attackPatternControllers.Count; i++)
            attackPatternControllers[i]?.Stop();

        attackPatternControllers.Clear();
        resolvedAttackPatternSettings.Clear();
    }

    private bool CanEnemyAttack(Enemy enemy, HashSet<int> selectedSlots)
    {
        return wave != null
            && selectedSlots != null
            && wave.TryGetFormationIndex(enemy, out int slotIndex)
            && selectedSlots.Contains(slotIndex);
    }

    private void ValidateAttackPatterns()
    {
        if (attackPatterns == null)
            return;

        HashSet<int> assignedSlots = new();
        for (int i = attackPatterns.Count - 1; i >= 0; i--)
        {
            DirectedWaveAttackPattern pattern = attackPatterns[i];
            if (pattern == null)
            {
                attackPatterns.RemoveAt(i);
                continue;
            }

            pattern.Validate();
        }

        for (int i = 0; i < attackPatterns.Count; i++)
            attackPatterns[i].RemoveSlotsAlreadyAssigned(assignedSlots);
    }

    private void RebuildSelectedEnemySlotSet()
    {
        selectedEnemySlotSet.Clear();
        if (selectedEnemySlots == null)
            return;

        for (int i = 0; i < selectedEnemySlots.Count; i++)
        {
            int slotIndex = selectedEnemySlots[i];
            if (slotIndex >= 0)
                selectedEnemySlotSet.Add(slotIndex);
        }
    }

    private void StartContinuousEntranceAttack(Enemy enemy, int enemySlotIndex)
    {
        if (enemy == null)
            return;

        RefreshResolvedAttackSettings();
        continuousEntranceAttackEnemies.Add(enemy);
        continuousEntranceEnemySlots[enemy] = enemySlotIndex;
        if (continuousEntranceAttackController != null)
            return;

        continuousEntranceAttackController = new DirectedWaveAttackController(
            wave,
            resolvedAttackSettings,
            CanEnemyPerformContinuousEntranceAttack);
        continuousEntranceAttackController.Begin();
    }

    private void TickContinuousEntranceAttack()
    {
        if (continuousEntranceAttackController == null
            || lastContinuousEntranceAttackTickFrame == Time.frameCount)
        {
            return;
        }

        lastContinuousEntranceAttackTickFrame = Time.frameCount;
        continuousEntranceAttackController.Tick();
    }

    private void StopContinuousEntranceAttackForEnemy(Enemy enemy)
    {
        if (enemy == null)
            return;

        continuousEntranceAttackEnemies.Remove(enemy);
        continuousEntranceEnemySlots.Remove(enemy);
        continuousEntranceAttackController?.NotifyEnemyDestroyed(enemy);
    }

    private void StopContinuousEntranceAttack()
    {
        continuousEntranceAttackController?.Stop();
        continuousEntranceAttackController = null;
        continuousEntranceAttackEnemies.Clear();
        continuousEntranceEnemySlots.Clear();
        lastContinuousEntranceAttackTickFrame = -1;
    }

    private bool CanEnemyPerformContinuousEntranceAttack(Enemy enemy)
    {
        if (enemy == null || !continuousEntranceAttackEnemies.Contains(enemy))
            return false;

        if (!useSelectedEnemySlots)
            return true;

        if (continuousEntranceEnemySlots.TryGetValue(enemy, out int slotIndex))
            return selectedEnemySlotSet.Contains(slotIndex);

        return wave != null
            && wave.TryGetFormationIndex(enemy, out slotIndex)
            && selectedEnemySlotSet.Contains(slotIndex);
    }

    private void RefreshResolvedAttackSettings()
    {
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.Validate();

        DirectedWaveAttackSettings source = attackPreset != null
            ? attackPreset.AttackSettings
            : attackSettings;
        resolvedAttackSettings.CopyFrom(source);

        if (attackPreset != null && overridePresetAttacksPerSecond)
        {
            resolvedAttackSettings.SetAttacksPerSecond(
                presetAttacksPerSecond);
        }
    }
}
