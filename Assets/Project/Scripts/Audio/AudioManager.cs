using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Header("Scene Fade In")]
    [Tooltip("切换到新场景后，是否自动让场景中正在播放的声音渐入。")]
    public bool autoFadeInOnSceneLoaded = true;

    [Tooltip("进入新场景后，延迟多少秒再开始声音渐入。")]
    public float sceneFadeStartDelay = 0.2f;

    [Tooltip("场景声音渐入时间。")]
    public float sceneFadeInDuration = 3f;

    [Tooltip("只渐入正在播放或 Play On Awake 的声音，避免误触发雨刮、门铃等一次性音效。")]
    public bool fadeOnlyPlayingOrPlayOnAwakeSources = true;

    private Coroutine sceneFadeCoroutine;

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoFadeInOnSceneLoaded)
        {
            return;
        }

        if (sceneFadeCoroutine != null)
        {
            StopCoroutine(sceneFadeCoroutine);
        }

        sceneFadeCoroutine = StartCoroutine(FadeInSceneAudioRoutine());
    }

    private IEnumerator FadeInSceneAudioRoutine()
    {
        // 等一小段时间，确保新场景里的 SceneAudioController 已经 Awake / Start
        yield return new WaitForSeconds(sceneFadeStartDelay);

        if (SceneAudioController.Current == null)
        {
            Debug.LogWarning("场景中没有找到 SceneAudioController，无法执行场景声音渐入。");
            yield break;
        }

        if (SceneAudioController.Current.audioSources == null)
        {
            yield break;
        }

        foreach (var item in SceneAudioController.Current.audioSources)
        {
            if (item == null || item.source == null)
            {
                continue;
            }

            AudioSource source = item.source;

            bool shouldFade =
                !fadeOnlyPlayingOrPlayOnAwakeSources ||
                source.isPlaying ||
                source.playOnAwake;

            if (!shouldFade)
            {
                continue;
            }

            float targetVolume = source.volume * masterVolume;

            source.volume = 0f;

            if (!source.isPlaying && source.playOnAwake)
            {
                source.Play();
            }

            if (!string.IsNullOrEmpty(item.id))
            {
                FadeVolume(item.id, targetVolume, sceneFadeInDuration);
            }
            else
            {
                StartCoroutine(FadeAudioSourceVolume(source, 0f, targetVolume, sceneFadeInDuration));
            }
        }
    }

    private IEnumerator FadeAudioSourceVolume(AudioSource source, float from, float to, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            source.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }

        source.volume = to;
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