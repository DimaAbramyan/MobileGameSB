using System.Collections.Generic;
using UnityEngine;

public sealed class WavePreviewTargetHistory
{
    public readonly struct Sample
    {
        public readonly int Frame;
        public readonly Vector3 Position;
        public Sample(int frame, Vector3 position) { Frame = frame; Position = position; }
    }
    private readonly List<Sample> samples = new();
    public IReadOnlyList<Sample> Samples => samples;
    public WavePreviewTargetHistory(Vector3 initial) => Clear(initial);
    public void Clear(Vector3 position) { samples.Clear(); samples.Add(new Sample(0, position)); }
    public Vector3 Evaluate(int frame)
    {
        int low = 0, high = samples.Count;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (samples[middle].Frame <= frame) low = middle; else high = middle;
        }
        return samples[low].Position;
    }
    // One sample per fixed frame; tiny mouse movements do not grow the track.
    // Editing the past starts a new branch and removes its previous future inputs.
    public bool Set(int frame, Vector3 position)
    {
        frame = Mathf.Max(0, frame);
        if ((Evaluate(frame) - position).sqrMagnitude < .000004f) return false;
        int index = samples.Count;
        while (index > 0 && samples[index - 1].Frame >= frame) index--;
        if (index < samples.Count) samples.RemoveRange(index, samples.Count - index);
        samples.Add(new Sample(frame, position)); return true;
    }
    public void FreezeAfter(int frame)
    {
        int first = samples.Count;
        while (first > 0 && samples[first - 1].Frame > frame) first--;
        if (first < samples.Count) samples.RemoveRange(first, samples.Count - first);
    }
}
