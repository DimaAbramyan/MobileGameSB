using FMODUnity;
using UnityEngine;

public class AudioManager
{
    private readonly AudioDatabase db;
    private readonly FMODAttenuationService fmodService;

    public AudioManager(AudioDatabase db, FMODAttenuationService fmodService)
    {
        this.db = db;
        this.fmodService = fmodService;
    }

    public void PlayOneShot(EventReference audio, Vector3 worldPosition)
    {
        RuntimeManager.PlayOneShot(audio, worldPosition);
    }
}