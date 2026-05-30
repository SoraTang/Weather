using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DoorReactiveCarAudio : MonoBehaviour
{
    [Header("References")]
    public ConvenienceStoreAutoDoor door;

    [Header("Volume")]
    [Range(0f, 1f)] public float closedVolume = 0.03f;
    [Range(0f, 1f)] public float openedVolume = 0.45f;

    [Header("Smoothing")]
    public float smoothSpeed = 8f;

    private AudioSource audioSource;
    private float currentVolume;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        currentVolume = closedVolume;
        audioSource.volume = currentVolume;
    }

    private void Update()
    {
        if (door == null) return;

        float targetVolume = Mathf.Lerp(closedVolume, openedVolume, door.DoorExposure01);
        currentVolume = Mathf.Lerp(currentVolume, targetVolume, Time.deltaTime * smoothSpeed);
        audioSource.volume = currentVolume;
    }
}