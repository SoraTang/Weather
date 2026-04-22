using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Range(0f, 1f)] public float masterVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Play(string id)
    {
        if (SceneAudioController.Current == null) return;
        SceneAudioController.Current.Play(id);
    }

    public void Stop(string id)
    {
        if (SceneAudioController.Current == null) return;
        SceneAudioController.Current.Stop(id);
    }

    public void SetVolume(string id, float volume)
    {
        if (SceneAudioController.Current == null) return;
        SceneAudioController.Current.SetVolume(id, volume * masterVolume);
    }

    public void FadeVolume(string id, float targetVolume, float duration)
    {
        if (SceneAudioController.Current == null) return;
        SceneAudioController.Current.FadeVolume(id, targetVolume * masterVolume, duration);
    }

    public void FadeIn(string id, float targetVolume, float duration)
    {
        if (SceneAudioController.Current == null) return;
        SceneAudioController.Current.FadeIn(id, targetVolume * masterVolume, duration);
    }

    public void FadeOutAndStop(string id, float duration)
    {
        if (SceneAudioController.Current == null) return;
        SceneAudioController.Current.FadeOutAndStop(id, duration);
    }
}