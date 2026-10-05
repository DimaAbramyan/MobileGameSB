using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SoundManager
{
    Dictionary<EventReference, EventInstance> playingLoops;

    private readonly AudioVolumeService audioVolumeService;

    private Dictionary<EventReference, float> lastPlayTime = new Dictionary<EventReference, float>();

    public SoundManager(AudioVolumeService audioVolumeService)
    {
        this.audioVolumeService = audioVolumeService;
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
            audioVolumeService.PlaySfx(audio, position);
            lastPlayTime[audio] = now;
        }
    }
    public void PlayContiniousSound(EventReference audio, Vector3 position)
    {
        if (audio.IsNull)
            return;

        if (playingLoops.TryGetValue(audio, out var existingInstance))
        {
            audioVolumeService.StopAndRelease(existingInstance);
            playingLoops.Remove(audio);
        }

        EventInstance instance = audioVolumeService.PlaySfxLoop(audio, position);
        if (!instance.isValid())
            return;

        playingLoops[audio] = instance;
    }
    public void StopContiniousSound(EventReference audio, Vector3 position)
    {
        if (audio.IsNull)
            return;
        if (playingLoops.TryGetValue(audio, out var existingInstance))
        {
            audioVolumeService.StopAndRelease(existingInstance);
            playingLoops.Remove(audio);
        }
    }
}
