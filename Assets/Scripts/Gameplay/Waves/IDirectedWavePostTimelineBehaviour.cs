public interface IDirectedWavePostTimelineBehaviour
{
    bool RequiresPostTimeline { get; }

    void OnPostTimelineStarted(DirectedEnemySubWave wave);
    void TickPostTimeline();
    void OnPostTimelineStopped();
    void OnWaveEnemyDestroyed(Enemy enemy);
}
