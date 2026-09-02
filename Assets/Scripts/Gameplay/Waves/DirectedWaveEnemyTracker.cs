using System.Collections;
using System.Collections.Generic;

internal sealed class DirectedWaveEnemyTracker : IEnumerable<Enemy>
{
    private readonly HashSet<Enemy> enemies = new();

    public int Count => enemies.Count;

    public bool Add(Enemy enemy)
    {
        return enemy != null && enemies.Add(enemy);
    }

    public bool Remove(Enemy enemy)
    {
        return enemy != null && enemies.Remove(enemy);
    }

    public int RemoveWhere(System.Predicate<Enemy> predicate)
    {
        return enemies.RemoveWhere(predicate);
    }

    public void Clear()
    {
        enemies.Clear();
    }

    public bool RemoveDeadAndHasAny()
    {
        enemies.RemoveWhere(enemy => enemy == null || enemy.isDead);
        return enemies.Count > 0;
    }

    public bool CanComplete(bool spawningFinished)
    {
        return spawningFinished && !RemoveDeadAndHasAny();
    }

    public HashSet<Enemy>.Enumerator GetEnumerator()
    {
        return enemies.GetEnumerator();
    }

    IEnumerator<Enemy> IEnumerable<Enemy>.GetEnumerator()
    {
        return enemies.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return enemies.GetEnumerator();
    }
}
