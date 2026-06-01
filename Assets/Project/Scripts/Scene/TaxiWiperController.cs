using System.Collections;
using UnityEngine;

public class TaxiWiperController : MonoBehaviour
{
    [System.Serializable]
    public class Wiper
    {
        [Header("Wiper Object")]
        public Transform wiper;

        [Header("Local Rotation A")]
        public Vector3 rotationA;

        [Header("Local Rotation B")]
        public Vector3 rotationB;
    }

    [Header("Wipers")]
    public Wiper leftWiper;
    public Wiper rightWiper;

    [Header("Motion")]
    [Tooltip("从 A 端刮到 B 端的时间")]
    public float sweepDuration = 0.65f;

    [Tooltip("从 B 端回到 A 端的时间")]
    public float returnDuration = 0.55f;

    [Tooltip("到达端点后的停顿时间")]
    public float endPause = 0.08f;

    [Header("Audio")]
    [Tooltip("刮过去时播放的声音 ID")]
    public string sweepAudioId = "wiper1";

    [Tooltip("回来的时候播放的声音 ID")]
    public string returnAudioId = "wiper2";

    [Tooltip("是否播放雨刮器声音")]
    public bool enableAudio = true;

    [Header("Loop")]
    public bool playOnStart = true;
    public bool loop = true;

    [Header("Natural Motion")]
    [Tooltip("两个雨刮器共用同一个时间曲线，保证同步到达端点")]
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine wiperRoutine;

    private void Start()
    {
        if (playOnStart)
        {
            StartWipers();
        }
    }

    public void StartWipers()
    {
        StopWipers();
        wiperRoutine = StartCoroutine(WiperLoop());
    }

    public void StopWipers()
    {
        if (wiperRoutine != null)
        {
            StopCoroutine(wiperRoutine);
            wiperRoutine = null;
        }
    }

    private IEnumerator WiperLoop()
    {
        SetWipersAtA();

        while (true)
        {
            // A → B：刮过去
            PlayWiperSound(sweepAudioId);
            yield return RotateBothWipers(0f, 1f, sweepDuration);

            yield return new WaitForSeconds(endPause);

            // B → A：回来
            PlayWiperSound(returnAudioId);
            yield return RotateBothWipers(1f, 0f, returnDuration);

            yield return new WaitForSeconds(endPause);

            if (!loop)
            {
                break;
            }
        }
    }

    private IEnumerator RotateBothWipers(float fromT, float toT, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float rawT = Mathf.Clamp01(timer / duration);
            float curvedT = motionCurve.Evaluate(rawT);

            float t = Mathf.Lerp(fromT, toT, curvedT);

            ApplyWiperRotation(leftWiper, t);
            ApplyWiperRotation(rightWiper, t);

            yield return null;
        }

        // 强制校准端点，避免浮点误差
        ApplyWiperRotation(leftWiper, toT);
        ApplyWiperRotation(rightWiper, toT);
    }

    private void ApplyWiperRotation(Wiper wiperData, float t)
    {
        if (wiperData == null || wiperData.wiper == null)
            return;

        Quaternion rotA = Quaternion.Euler(wiperData.rotationA);
        Quaternion rotB = Quaternion.Euler(wiperData.rotationB);

        wiperData.wiper.localRotation = Quaternion.Slerp(rotA, rotB, t);
    }

    private void SetWipersAtA()
    {
        ApplyWiperRotation(leftWiper, 0f);
        ApplyWiperRotation(rightWiper, 0f);
    }

    private void PlayWiperSound(string audioId)
    {
        if (!enableAudio)
            return;

        if (string.IsNullOrEmpty(audioId))
            return;

        if (SceneAudioController.Current == null)
        {
            Debug.LogWarning("SceneAudioController.Current is null. 无法播放雨刮器声音。");
            return;
        }

        if (!SceneAudioController.Current.HasSource(audioId))
        {
            Debug.LogWarning($"SceneAudioController 中没有找到 Audio ID: {audioId}");
            return;
        }

        SceneAudioController.Current.Play(audioId);
    }
}