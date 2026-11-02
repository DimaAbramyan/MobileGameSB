using System;
using System.Collections.Generic;

// One observation per completed fixed step, after all coordinator handoffs.
// Group indexes identify authored groups; a restarted instance of the same
// group does not create an event unless the active set actually changes.
public sealed class WavePreviewCoordinatorEvent
{
    public int Frame { get; }
    // Coordinator.Tick runs at the beginning of the fixed step. Navigation
    // restores its completed state; labels retain the actual coordinator time.
    public float Time => Math.Max(0, Frame - 1) * WavePreviewPlayer.StepInterval;
    public float StateTime => Frame * WavePreviewPlayer.StepInterval;
    public IReadOnlyList<int> ActivatedGroups { get; }
    public IReadOnlyList<int> DeactivatedGroups { get; }
    public IReadOnlyList<int> ActiveGroups { get; }
    public string Activated { get; }
    public string Deactivated { get; }
    public string Active { get; }
    public string Summary { get; }

    internal WavePreviewCoordinatorEvent(int frame, List<int> before, List<int> after, WaveAttackCoordinatorSettings settings)
    {
        Frame = frame;
        var added = Difference(after, before);
        var removed = Difference(before, after);
        ActivatedGroups = added; DeactivatedGroups = removed; ActiveGroups = after.ToArray();
        Activated = Names(added, settings); Deactivated = Names(removed, settings); Active = Names(after, settings);
        if (added.Length > 0 && removed.Length > 0) Summary = $"{Names(before, settings)} → {Active}";
        else if (added.Length > 0) Summary = $"{Activated} activated ({Active} active)";
        else Summary = after.Count == 0 ? $"{Deactivated} deactivated (no active groups)"
            : $"{Deactivated} deactivated ({Active} active)";
    }

    private static int[] Difference(List<int> values, List<int> other)
    {
        var result = new List<int>();
        foreach (int value in values) if (!other.Contains(value)) result.Add(value);
        return result.ToArray();
    }
    private static string Names(IReadOnlyList<int> indexes, WaveAttackCoordinatorSettings settings)
    {
        if (indexes.Count == 0) return "None";
        var names = new string[indexes.Count];
        for (int i = 0; i < indexes.Count; i++)
        {
            int index = indexes[i];
            string name = settings != null && index < settings.groups.Count ? settings.groups[index]?.groupName : null;
            names[i] = string.IsNullOrWhiteSpace(name) ? $"Group {index + 1}" : name;
            if (string.IsNullOrWhiteSpace(name) || settings == null) continue;
            for (int other = 0; other < settings.groups.Count; other++)
                if (other != index && string.Equals(settings.groups[other]?.groupName, name, StringComparison.Ordinal))
                { names[i] = $"{index + 1}. {name}"; break; }
        }
        return string.Join(" + ", names);
    }
}

internal sealed class WavePreviewCoordinatorEventIndex
{
    private readonly List<WavePreviewCoordinatorEvent> events = new();
    public IReadOnlyList<WavePreviewCoordinatorEvent> Events => events;
    public int ScannedThroughFrame { get; private set; }
    public bool LimitReached { get; private set; }

    public void Observe(int frame, List<int> before, List<int> after, Wave source)
    {
        bool changed = before.Count != after.Count;
        for (int i = 0; !changed && i < before.Count; i++) changed = before[i] != after[i];
        if (changed)
        {
            int index = LowerBound(frame);
            if (index == events.Count || events[index].Frame != frame)
            {
                if (events.Count >= WavePreviewPlayer.MaximumEvents) { LimitReached = true; return; }
                source.TryGetComponent(out WaveAttackCoordinator coordinator);
                events.Insert(index, new WavePreviewCoordinatorEvent(frame, before, after, coordinator != null ? coordinator.Settings : null));
            }
        }
        if (frame == ScannedThroughFrame + 1) ScannedThroughFrame = frame;
    }
    public WavePreviewCoordinatorEvent Find(int frame, int direction)
    {
        int index = LowerBound(frame);
        if (direction < 0) index--;
        else if (index < events.Count && events[index].Frame == frame) index++;
        return index >= 0 && index < events.Count ? events[index] : null;
    }
    private int LowerBound(int frame)
    {
        int low = 0, high = events.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (events[middle].Frame < frame) low = middle + 1; else high = middle;
        }
        return low;
    }
    public void InvalidateAfter(int frame)
    {
        int first = LowerBound(frame + 1);
        if (first < events.Count) events.RemoveRange(first, events.Count - first);
        ScannedThroughFrame = Math.Min(ScannedThroughFrame, frame); LimitReached = false;
    }
    public bool Contains(WavePreviewCoordinatorEvent observation)
    {
        int index = LowerBound(observation.Frame);
        return index < events.Count && ReferenceEquals(events[index], observation);
    }
    public void Clear() { events.Clear(); ScannedThroughFrame = 0; LimitReached = false; }
}

internal sealed class WavePreviewCoordinatorObserver
{
    private List<int> previous = new(), current = new();
    public void Reset(WaveConductorShootingPreview simulation)
    { Read(simulation, previous); current.Clear(); }
    public void Observe(WaveConductorShootingPreview simulation, Wave source, int frame, WavePreviewCoordinatorEventIndex index)
    {
        Read(simulation, current);
        index.Observe(frame, previous, current, source);
        (previous, current) = (current, previous);
    }
    private static void Read(WaveConductorShootingPreview simulation, List<int> result)
    {
        result.Clear();
        var groups = simulation?.CoordinatorPlayer?.ActiveGroups;
        if (groups == null) return;
        foreach (var group in groups)
            // Pending/start-delay groups have not begun attacking. Draining
            // groups remain active until their current attacks have finished.
            if (!float.IsNaN(group.StartedAt) && !result.Contains(group.Index)) result.Add(group.Index);
        if (result.Count > 1) result.Sort();
    }
}
