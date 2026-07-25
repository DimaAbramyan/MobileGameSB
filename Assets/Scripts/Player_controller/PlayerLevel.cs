using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLevel: MonoBehaviour
{
    public List<bool> levelList;

    public void LvlUp()
    {
        Debug.LogWarning(
            $"{nameof(PlayerLevel)} is deprecated. Use {nameof(LevelProgressService)} instead.",
            this);
    }

    public int GetLvl()
    {
        LevelProgressService progress = new LevelProgressService();
        return progress.CompletedCount;
    }
}
