using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using Zenject;
using FMOD.Studio;

public class SoundManager
{
    Dictionary<EventReference, EventInstance> playingLoops;

    private readonly AudioDatabase db;
    private readonly FMODAttenuationService fmodService;

    private Dictionary<EventReference, float> lastPlayTime = new Dictionary<EventReference, float>();

    public SoundManager(AudioDatabase db, [InjectOptional] FMODAttenuationService fmodService = null)
    {
        this.db = db;

        this.fmodService = fmodService;

        playingLoops = new Dictionary<EventReference, EventInstance>();

    }

    public void PlaySound(EventReference audio, Vector3 position, float cooldown = 0.1f)
    {
        if (audio.IsNull) return;

        float now = Time.time;
        if (!lastPlayTime.TryGetValue(audio, out float lastTime))
            lastTime = -Mathf.Infinity;

        if (now - lastTime >= cooldown)
        {
            if (RuntimeManager.IsInitialized)
            {
                RuntimeManager.PlayOneShot(audio, position);
                lastPlayTime[audio] = now;
            }
            else
            {
                Debug.LogWarning($"Cannot play audio {audio.Path}, FMOD not initialized");
            }
        }
    }
    public void PlayContiniousSound(EventReference audio, Vector3 position)
    {
        if (audio.IsNull)
            return;

        if (playingLoops.TryGetValue(audio, out var existingInstance))
        {
            existingInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            existingInstance.release();
            playingLoops.Remove(audio);
        }

        var instance = RuntimeManager.CreateInstance(audio);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        instance.start();

        playingLoops[audio] = instance;
    }
    public void StopContiniousSound(EventReference audio, Vector3 position)
    {
        if (audio.IsNull)
            return;
        if (playingLoops.TryGetValue(audio, out var existingInstance))
        {
            existingInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            existingInstance.release();
            playingLoops.Remove(audio);
        }
    }
}