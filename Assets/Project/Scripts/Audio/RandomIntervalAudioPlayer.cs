using System.Collections;
using UnityEngine;

public class RandomIntervalAudioPlayer : MonoBehaviour
{
    [Header("Target Audio ID")]
    public string audioID = "casher";

    [Header("Random Interval")]
    public float minInterval = 15f;
    public float maxInterval = 30f;

    [Header("Optional")]
    public bool playOnStart = true;

    private Coroutine routine;

    private void Start()
    {
        if (playOnStart)
        {
            routine = StartCoroutine(PlayRoutine());
        }
    }

    public void StartRandomPlay()
    {
        if (routine == null)
        {
            routine = StartCoroutine(PlayRoutine());
        }
    }

    public void StopRandomPlay()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator PlayRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            if (SceneAudioController.Current != null && SceneAudioController.Current.HasSource(audioID))
            {
                SceneAudioController.Current.Play(audioID);
            }
        }
    }
}