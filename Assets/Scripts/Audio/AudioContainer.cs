using UnityEngine;
using FMODUnity;
[CreateAssetMenu(menuName = "Audio/AudioDatabase")]
public class AudioDatabase : ScriptableObject
{
    [Header("Music")]
    public EventReference mainMenuMusic;
    public EventReference battleMusic;

    [Header("SFX")]
    public EventReference shoot;
    public EventReference explosion;
    public EventReference buttonClick;
    public EventReference LevelUp;

    [Header("Abilities")]
    public EventReference blackHole;
}