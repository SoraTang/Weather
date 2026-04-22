using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneAudioController : MonoBehaviour
{
    public static SceneAudioController Current;

    public List<SceneAudioSource> audioSources = new List<SceneAudioSource>();

    private Dictionary<string, AudioSource> sourceDict = new Dictionary<string, AudioSource>();
    private Dictionary<string, Coroutine> fadeDict = new Dictionary<string, Coroutine>();

    private void Awake()
    {
        Current = this;

        sourceDict.Clear();
        foreach (var item in audioSources)
        {
            if (!string.IsNullOrEmpty(item.id) && item.source != null)
            {
                sourceDict[item.id] = item.source;
            }
        }
    }

    public bool HasSource(string id)
    {
        return sourceDict.ContainsKey(id);
    }

    public void Play(string id)
    {
        if (!sourceDict.ContainsKey(id)) return;

        AudioSource src = sourceDict[id];
        if (!src.isPlaying)
            src.Play();
    }

    public void Stop(string id)
    {
        if (!sourceDict.ContainsKey(id)) return;

        AudioSource src = sourceDict[id];
        src.Stop();
    }

    public void SetVolume(string id, float volume)
    {
        if (!sourceDict.ContainsKey(id)) return;

        sourceDict[id].volume = Mathf.Clamp01(volume);
    }

    public void FadeVolume(string id, float targetVolume, float duration)
    {
        if (!sourceDict.ContainsKey(id)) return;

        if (fadeDict.ContainsKey(id) && fadeDict[id] != null)
        {
            StopCoroutine(fadeDict[id]);
        }

        fadeDict[id] = StartCoroutine(FadeCoroutine(id, targetVolume, duration));
    }

    public void FadeIn(string id, float targetVolume, float duration)
    {
        if (!sourceDict.ContainsKey(id)) return;

        AudioSource src = sourceDict[id];
        src.volume = 0f;
        if (!src.isPlaying)
            src.Play();

        FadeVolume(id, targetVolume, duration);
    }

    public void FadeOutAndStop(string id, float duration)
    {
        if (!sourceDict.ContainsKey(id)) return;

        if (fadeDict.ContainsKey(id) && fadeDict[id] != null)
        {
            StopCoroutine(fadeDict[id]);
        }

        fadeDict[id] = StartCoroutine(FadeOutAndStopCoroutine(id, duration));
    }

    private IEnumerator FadeCoroutine(string id, float targetVolume, float duration)
    {
        AudioSource src = sourceDict[id];
        float startVolume = src.volume;
        float time = 0f;

        if (duration <= 0f)
        {
            src.volume = Mathf.Clamp01(targetVolume);
            yield break;
        }

        while (time < duration)
        {
            time += Time.deltaTime;
            src.volume = Mathf.Lerp(startVolume, targetVolume, time / duration);
            yield return null;
        }

        src.volume = Mathf.Clamp01(targetVolume);
        fadeDict[id] = null;
    }

    private IEnumerator FadeOutAndStopCoroutine(string id, float duration)
    {
        AudioSource src = sourceDict[id];
        float startVolume = src.volume;
        float time = 0f;

        if (duration <= 0f)
        {
            src.volume = 0f;
            src.Stop();
            yield break;
        }

        while (time < duration)
        {
            time += Time.deltaTime;
            src.volume = Mathf.Lerp(startVolume, 0f, time / duration);
            yield return null;
        }

        src.volume = 0f;
        src.Stop();
        fadeDict[id] = null;
    }
}