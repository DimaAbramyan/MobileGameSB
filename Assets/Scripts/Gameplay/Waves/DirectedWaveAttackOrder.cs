using System;
using System.Collections.Generic;
using UnityEngine;

public enum DirectedWaveAttackOrderMode
{
    Simultaneous, Sequential, Random, CenterToOutside, OutsideToCenter,
    LeftToRight, RightToLeft, Clockwise, CounterClockwise, PingPong,
    SymmetricalPairs, Manual
}

public enum DirectedWaveAttackOrderSource { FormationSlotOrder, SpatialOrder }

[Serializable]
public sealed class DirectedWaveAttackOrderSettings
{
    public bool enabled;
    public DirectedWaveAttackOrderMode mode = DirectedWaveAttackOrderMode.Sequential;
    [Min(0f)] public float attackIntervalBetweenEnemies = .15f;
    [Min(1)] public int enemiesPerBatch = 1;
    [Min(-1), Tooltip("Formation slot ID. -1 starts at the first entry of the chosen order.")]
    public int startSlot = -1;
    public bool reverseOrder;
    public bool loopOrder = true;
    public int randomSeed = 12345;
    public DirectedWaveAttackOrderSource orderSource;
    public bool waitForPreviousEnemy;
    public bool resetOrderEachCycle = true;
    public List<int> manualSlots = new();

    public void CopyFrom(DirectedWaveAttackOrderSettings source)
    {
        if (ReferenceEquals(source, this)) return;
        source ??= new DirectedWaveAttackOrderSettings();
        enabled = source.enabled; mode = source.mode;
        attackIntervalBetweenEnemies = source.attackIntervalBetweenEnemies;
        enemiesPerBatch = source.enemiesPerBatch; startSlot = source.startSlot;
        reverseOrder = source.reverseOrder; loopOrder = source.loopOrder;
        randomSeed = source.randomSeed; orderSource = source.orderSource;
        waitForPreviousEnemy = source.waitForPreviousEnemy; resetOrderEachCycle = source.resetOrderEachCycle;
        manualSlots ??= new List<int>(); manualSlots.Clear();
        if (source.manualSlots != null) manualSlots.AddRange(source.manualSlots);
    }
}

public readonly struct DirectedWaveAttackOrderSlot
{
    public readonly int Index;
    public readonly Vector2 Position;
    public DirectedWaveAttackOrderSlot(int index, Vector2 position) { Index = index; Position = position; }
}

public readonly struct DirectedWaveAttackOrderEntry
{
    public readonly int Slot, Batch;
    public DirectedWaveAttackOrderEntry(int slot, int batch) { Slot = slot; Batch = batch; }
}

// Selection only: the existing group controller still owns dispatch time, attacks and cooldowns.
// Runtime, animated Preview and Scene guides use the same plan and stable slot tie-breakers.
public sealed class DirectedWaveAttackOrderPlan
{
    private readonly List<DirectedWaveAttackOrderSlot> snapshot = new();
    private readonly List<DirectedWaveAttackOrderSlot> ordered = new();
    private readonly List<int> sweep = new();
    private readonly List<Vector2Int> pairs = new();
    private readonly HashSet<int> pairedSlots = new();
    private readonly List<DirectedWaveAttackOrderEntry> entries = new();
    private System.Random random;
    private Vector2 center;
    private DirectedWaveAttackOrderSettings settings;
    private readonly Comparison<DirectedWaveAttackOrderSlot> compare;
    public IReadOnlyList<DirectedWaveAttackOrderEntry> Entries => entries;
    public int CycleCount { get; private set; }
    public int Cursor { get; set; }
    public bool Exhausted => Cursor >= entries.Count;
    public DirectedWaveAttackOrderPlan() => compare = Compare;

    public void Reset()
    { snapshot.Clear(); entries.Clear(); random = null; CycleCount = Cursor = 0; }

    public bool BuildCycle(List<DirectedWaveAttackOrderSlot> slots, DirectedWaveAttackOrderSettings configuration, int rounds, bool coordinatedCycle = false)
    {
        settings = configuration;
        if (CycleCount > 0 && !settings.loopOrder && !coordinatedCycle) return false;
        entries.Clear(); Cursor = 0;
        bool reset = CycleCount == 0 || settings.resetOrderEachCycle;
        if (reset) { snapshot.Clear(); snapshot.AddRange(slots); random = new System.Random(settings.randomSeed); }
        CycleCount++;
        ordered.Clear(); ordered.AddRange(snapshot);
        center = Vector2.zero;
        for (int i = 0; i < ordered.Count; i++) center += ordered[i].Position;
        if (ordered.Count > 0) center /= ordered.Count;
        ordered.Sort(compare);
        if (settings.mode == DirectedWaveAttackOrderMode.Manual)
        {
            sweep.Clear();
            if (settings.manualSlots != null)
                for (int i = 0; i < settings.manualSlots.Count; i++)
                    for (int j = 0; j < ordered.Count; j++)
                        if (ordered[j].Index == settings.manualSlots[i]) { sweep.Add(ordered[j].Index); break; }
        }
        else
        {
            if (settings.mode == DirectedWaveAttackOrderMode.Random)
                for (int i = ordered.Count - 1; i > 0; i--)
                { int j = random.Next(i + 1); (ordered[i], ordered[j]) = (ordered[j], ordered[i]); }
            sweep.Clear();
            if (settings.mode == DirectedWaveAttackOrderMode.SymmetricalPairs)
            {
                BuildSymmetricalPairs();
            }
            else for (int i = 0; i < ordered.Count; i++) sweep.Add(ordered[i].Index);
        }
        if (settings.mode == DirectedWaveAttackOrderMode.SymmetricalPairs)
        {
            if (settings.reverseOrder) pairs.Reverse();
            int firstPair = -1;
            if (settings.startSlot >= 0)
                for (int i = 0; i < pairs.Count; i++)
                    if (pairs[i].x == settings.startSlot || pairs[i].y == settings.startSlot) { firstPair = i; break; }
            if (firstPair > 0)
            { pairs.Reverse(0, firstPair); pairs.Reverse(firstPair, pairs.Count - firstPair); pairs.Reverse(); }
        }
        else
        {
            if (settings.reverseOrder) sweep.Reverse();
            int start = sweep.IndexOf(settings.startSlot);
            if (start > 0)
            { sweep.Reverse(0, start); sweep.Reverse(start, sweep.Count - start); sweep.Reverse(); }
        }
        if (settings.mode == DirectedWaveAttackOrderMode.PingPong)
            for (int i = sweep.Count - 2; i > 0; i--) sweep.Add(sweep[i]);
        int batch = 0;
        int batchSize = settings.mode == DirectedWaveAttackOrderMode.Simultaneous ? Mathf.Max(1, sweep.Count)
            : settings.mode == DirectedWaveAttackOrderMode.SymmetricalPairs ? Mathf.Max(2, settings.enemiesPerBatch / 2 * 2)
            : Mathf.Max(1, settings.enemiesPerBatch);
        for (int round = 0; round < Mathf.Max(1, rounds); round++)
        {
            if (settings.mode == DirectedWaveAttackOrderMode.SymmetricalPairs)
            {
                int members = 0;
                for (int i = 0; i < pairs.Count; i++)
                {
                    var pair = pairs[i]; int size = pair.y < 0 ? 1 : 2;
                    if (members > 0 && members + size > batchSize) { batch++; members = 0; }
                    entries.Add(new DirectedWaveAttackOrderEntry(pair.x, batch));
                    if (size == 2) entries.Add(new DirectedWaveAttackOrderEntry(pair.y, batch));
                    members += size;
                    if (members >= batchSize) { batch++; members = 0; }
                }
                if (members > 0) batch++;
                continue;
            }
            for (int i = 0; i < sweep.Count; i++)
                entries.Add(new DirectedWaveAttackOrderEntry(sweep[i], batch + i / batchSize));
            batch += (sweep.Count + batchSize - 1) / batchSize;
        }
        return entries.Count > 0;
    }

    private int Compare(DirectedWaveAttackOrderSlot a, DirectedWaveAttackOrderSlot b)
    {
        int result;
        switch (settings.mode)
        {
            case DirectedWaveAttackOrderMode.CenterToOutside:
            case DirectedWaveAttackOrderMode.OutsideToCenter:
                result = (a.Position - center).sqrMagnitude.CompareTo((b.Position - center).sqrMagnitude);
                if (settings.mode == DirectedWaveAttackOrderMode.OutsideToCenter) result = -result;
                break;
            case DirectedWaveAttackOrderMode.Clockwise:
            case DirectedWaveAttackOrderMode.CounterClockwise:
                // Start at the top; positive clockwise angle. Equal angles use radius, then slot ID.
                float angleA = Mathf.Repeat(Mathf.Atan2(a.Position.x - center.x, a.Position.y - center.y), Mathf.PI * 2f);
                float angleB = Mathf.Repeat(Mathf.Atan2(b.Position.x - center.x, b.Position.y - center.y), Mathf.PI * 2f);
                if (settings.mode == DirectedWaveAttackOrderMode.CounterClockwise)
                { angleA = Mathf.Repeat(-angleA, Mathf.PI * 2f); angleB = Mathf.Repeat(-angleB, Mathf.PI * 2f); }
                result = angleA.CompareTo(angleB);
                if (result == 0) result = (a.Position - center).sqrMagnitude.CompareTo((b.Position - center).sqrMagnitude);
                break;
            default:
                bool spatial = settings.orderSource == DirectedWaveAttackOrderSource.SpatialOrder
                    || settings.mode == DirectedWaveAttackOrderMode.LeftToRight
                    || settings.mode == DirectedWaveAttackOrderMode.RightToLeft
                    || settings.mode == DirectedWaveAttackOrderMode.SymmetricalPairs;
                result = spatial ? a.Position.x.CompareTo(b.Position.x) : a.Index.CompareTo(b.Index);
                if (spatial && result == 0) result = b.Position.y.CompareTo(a.Position.y);
                if (settings.mode == DirectedWaveAttackOrderMode.RightToLeft) result = -result;
                break;
        }
        return result != 0 ? result : a.Index.CompareTo(b.Index);
    }

    private void BuildSymmetricalPairs()
    {
        pairs.Clear(); pairedSlots.Clear();
        for (int i = 0; i < ordered.Count; i++)
        {
            var first = ordered[i];
            if (!pairedSlots.Add(first.Index)) continue;
            int partner = -1;
            float bestDistance = float.PositiveInfinity;
            float side = first.Position.x - center.x;
            Vector2 mirror = new(2f * center.x - first.Position.x, first.Position.y);
            if (Mathf.Abs(side) > .0001f)
                for (int j = 0; j < ordered.Count; j++)
                {
                    var candidate = ordered[j];
                    if (pairedSlots.Contains(candidate.Index) || (candidate.Position.x - center.x) * side >= 0f) continue;
                    float distance = (candidate.Position - mirror).sqrMagnitude;
                    if (distance < bestDistance || distance == bestDistance && (partner < 0 || candidate.Index < ordered[partner].Index))
                    { partner = j; bestDistance = distance; }
                }
            int second = partner < 0 ? -1 : ordered[partner].Index;
            if (second >= 0) pairedSlots.Add(second);
            pairs.Add(new Vector2Int(first.Index, second));
        }
    }
}
