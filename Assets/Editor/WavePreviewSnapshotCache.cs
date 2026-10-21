using System;
using System.Collections.Generic;

internal sealed class WavePreviewSnapshotCache : IDisposable
{
    internal sealed class Entry : IDisposable
    {
        public readonly WaveConductorShootingPreview.Snapshot State;
        public readonly WavePreviewEvent[] Events;
        public readonly int OmittedEvents;
        public int Frame => State.Frame;
        public long Bytes => State.EstimatedBytes + Events.Length * 64L;
        public Entry(WaveConductorShootingPreview owner, List<WavePreviewEvent> events, int omitted)
        { State = owner.CaptureSnapshot(); Events = events.ToArray(); OmittedEvents = omitted; }
        public void Restore() => State.Restore();
        public void Dispose() => State.Dispose();
    }
    private readonly List<Entry> entries = new();
    public int Count => entries.Count;
    public long Bytes { get; private set; }
    public long MaximumBytes { get; set; } = 128L * 1024L * 1024L;
    public int MaximumCount { get; set; } = 32;
    public bool Contains(int frame) { foreach (var entry in entries) if (entry.Frame == frame) return true; return false; }
    public Entry Find(int frame)
    {
        for (int i = entries.Count - 1; i >= 0; i--) if (entries[i].Frame <= frame) return entries[i];
        return null;
    }
    public void Capture(WaveConductorShootingPreview owner, List<WavePreviewEvent> events, int omitted)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SnapshotCapture);
        if (Contains(owner.StepCount)) return;
        var entry = new Entry(owner, events, omitted);
        if (entry.Bytes > MaximumBytes && entry.Frame != 0) { entry.Dispose(); return; }
        int index = 0; while (index < entries.Count && entries[index].Frame < entry.Frame) index++;
        entries.Insert(index, entry); Bytes += entry.Bytes;
        Trim();
    }
    public void Trim()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.CacheTrim);
        while (entries.Count > 1 && (entries.Count > MaximumCount || Bytes > MaximumBytes)) Remove(1);
    }
    private void Remove(int index)
    { var entry = entries[index]; entries.RemoveAt(index); Bytes -= entry.Bytes; entry.Dispose(); }
    public void InvalidateAfter(int frame)
    { for (int i = entries.Count - 1; i >= 0; i--) if (entries[i].Frame > frame) Remove(i); }
    public void Clear(bool keepInitial = true)
    { for (int i = entries.Count - 1; i >= 0; i--) if (!keepInitial || entries[i].Frame != 0) Remove(i); }
    public void Dispose() => Clear(false);
}
