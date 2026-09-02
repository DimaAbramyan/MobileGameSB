using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

[Serializable]
public sealed class AudioVolumeSettings
{
    [SerializeField, Range(0, 100)] private int musicVolume = 100;
    [SerializeField, Range(0, 100)] private int sfxVolume = 100;
    [SerializeField] private bool musicMuted;
    [SerializeField] private bool sfxMuted;

    public int MusicVolume
    {
        get => Mathf.Clamp(musicVolume, 0, 100);
        set => musicVolume = Mathf.Clamp(value, 0, 100);
    }

    public int SfxVolume
    {
        get => Mathf.Clamp(sfxVolume, 0, 100);
        set => sfxVolume = Mathf.Clamp(value, 0, 100);
    }

    public bool MusicMuted
    {
        get => musicMuted;
        set => musicMuted = value;
    }

    public bool SfxMuted
    {
        get => sfxMuted;
        set => sfxMuted = value;
    }

    public void Validate()
    {
        musicVolume = Mathf.Clamp(musicVolume, 0, 100);
        sfxVolume = Mathf.Clamp(sfxVolume, 0, 100);
    }
}

public sealed class AudioVolumeService : IDisposable
{
    private readonly AudioVolumeSettings settings;
    private readonly List<EventInstance> musicInstances = new();
    private readonly List<EventInstance> sfxLoopInstances = new();

    public int MusicVolume => settings.MusicVolume;
    public int SfxVolume => settings.SfxVolume;
    public bool MusicMuted => settings.MusicMuted;
    public bool SfxMuted => settings.SfxMuted;

    public event Action SettingsChanged;

    public AudioVolumeService(AudioVolumeSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.settings.Validate();
    }

    public void SetMusicVolume(int value)
    {
        int clamped = Mathf.Clamp(value, 0, 100);
        if (settings.MusicVolume == clamped)
            return;

        settings.MusicVolume = clamped;
        ApplyVolume(musicInstances, GetMusicVolume01());
        SettingsChanged?.Invoke();
    }

    public void SetSfxVolume(int value)
    {
        int clamped = Mathf.Clamp(value, 0, 100);
        if (settings.SfxVolume == clamped)
            return;

        settings.SfxVolume = clamped;
        ApplyVolume(sfxLoopInstances, GetSfxVolume01());
        SettingsChanged?.Invoke();
    }

    public void SetMusicMuted(bool muted)
    {
        if (settings.MusicMuted == muted)
            return;

        settings.MusicMuted = muted;
        ApplyVolume(musicInstances, GetMusicVolume01());
        SettingsChanged?.Invoke();
    }

    public void SetSfxMuted(bool muted)
    {
        if (settings.SfxMuted == muted)
            return;

        settings.SfxMuted = muted;
        ApplyVolume(sfxLoopInstances, GetSfxVolume01());
        SettingsChanged?.Invoke();
    }

    public void PlaySfx(EventReference audio, Vector3 position)
    {
        if (audio.IsNull
            || !RuntimeManager.IsInitialized
            || GetSfxVolume01() <= 0f)
        {
            return;
        }

        EventInstance instance = RuntimeManager.CreateInstance(audio);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        instance.setVolume(GetSfxVolume01());
        instance.start();
        instance.release();
    }

    public EventInstance PlaySfxLoop(EventReference audio, Vector3 position)
    {
        if (audio.IsNull || !RuntimeManager.IsInitialized)
            return default;

        EventInstance instance = RuntimeManager.CreateInstance(audio);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        instance.setVolume(GetSfxVolume01());
        instance.start();
        sfxLoopInstances.Add(instance);
        return instance;
    }

    public EventInstance PlayMusic(EventReference audio)
    {
        if (audio.IsNull || !RuntimeManager.IsInitialized)
            return default;

        EventInstance instance = RuntimeManager.CreateInstance(audio);
        instance.setVolume(GetMusicVolume01());
        instance.start();
        musicInstances.Add(instance);
        return instance;
    }

    public void StopAndRelease(EventInstance instance)
    {
        RemoveTrackedInstance(musicInstances, instance);
        RemoveTrackedInstance(sfxLoopInstances, instance);

        if (!instance.isValid())
            return;

        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
    }

    public void StopAllPlayback()
    {
        StopAndReleaseAll(musicInstances);
        StopAndReleaseAll(sfxLoopInstances);

        if (RuntimeManager.IsInitialized)
        {
            RuntimeManager.StudioSystem.getBus(
                "bus:/",
                out FMOD.Studio.Bus masterBus);
            if (masterBus.isValid())
                masterBus.stopAllEvents(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        AudioSource[] audioSources = UnityEngine.Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < audioSources.Length; i++)
            audioSources[i].Stop();
    }

    public void Dispose()
    {
        StopAllPlayback();
        SettingsChanged = null;
    }

    private float GetMusicVolume01()
    {
        return settings.MusicMuted ? 0f : settings.MusicVolume / 100f;
    }

    private float GetSfxVolume01()
    {
        return settings.SfxMuted ? 0f : settings.SfxVolume / 100f;
    }

    private static void ApplyVolume(
        List<EventInstance> instances,
        float volume)
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            EventInstance instance = instances[i];
            if (!instance.isValid())
            {
                instances.RemoveAt(i);
                continue;
            }

            instance.setVolume(volume);
        }
    }

    private static void RemoveTrackedInstance(
        List<EventInstance> instances,
        EventInstance target)
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            EventInstance instance = instances[i];
            if (!instance.isValid() || instance.handle == target.handle)
                instances.RemoveAt(i);
        }
    }

    private static void StopAndReleaseAll(List<EventInstance> instances)
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            EventInstance instance = instances[i];
            if (!instance.isValid())
                continue;

            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }

        instances.Clear();
    }
}
