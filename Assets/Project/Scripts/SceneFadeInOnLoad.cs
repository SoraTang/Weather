using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneFadeInOnLoad : MonoBehaviour
{
    [Header("Fade Panel")]
    [Tooltip("用于黑场的 UI Image。进入场景时会从黑色不透明逐渐变透明。")]
    public Image fadePanel;

    [Header("Fade Settings")]
    [Tooltip("进入场景后延迟多少秒开始淡入。")]
    public float startDelay = 0.1f;

    [Tooltip("黑场淡入时间。")]
    public float fadeInDuration = 2f;

    [Tooltip("淡入完成后是否禁用 Fade Panel，避免遮挡交互。")]
    public bool disablePanelAfterFade = true;

    [Tooltip("是否使用不受 Time.timeScale 影响的时间。")]
    public bool useUnscaledTime = false;

    [Header("Debug")]
    public bool enableDebugLog = true;

    private Coroutine fadeCoroutine;

    void Start()
    {
        if (fadePanel == null)
        {
            Debug.LogWarning("SceneFadeInOnLoad：没有绑定 Fade Panel。");
            return;
        }

        StartFadeIn();
    }

    public void StartFadeIn()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        if (fadePanel == null)
        {
            yield break;
        }

        fadePanel.gameObject.SetActive(true);

        SetAlpha(1f);

        DebugLog("场景黑场淡入开始。");

        if (startDelay > 0f)
        {
            yield return Wait(startDelay);
        }

        float timer = 0f;

        while (timer < fadeInDuration)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = Mathf.Clamp01(timer / fadeInDuration);
            float alpha = Mathf.Lerp(1f, 0f, t);

            SetAlpha(alpha);

            yield return null;
        }

        SetAlpha(0f);

        if (disablePanelAfterFade)
        {
            fadePanel.gameObject.SetActive(false);
        }

        DebugLog("场景黑场淡入完成。");
    }

    private IEnumerator Wait(float duration)
    {
        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(duration);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
    }

    private void SetAlpha(float alpha)
    {
        if (fadePanel == null)
        {
            return;
        }

        Color color = fadePanel.color;
        color.a = alpha;
        fadePanel.color = color;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log(message);
        }
    }
}