using UnityEngine;

public interface IHasAudio
{
    AudioClip AudioClip { get; }
    void PlayAudio(AudioSource source);
}
