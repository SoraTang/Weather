using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TransitionAudioTimeline : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource rainIntroSource;
    public AudioSource rainLoopSource;
    public AudioSource narrationSource;

    [Header("Outro Audio")]
    [Tooltip("玩家说完并识别完成后播放的第二段旁白")]
    public AudioSource outroNarrationSource;

    [Header("Fade Panel")]
    [Tooltip("黑场 Image。普通屏幕可用 Screen Space Canvas；VR 建议后续改成挂在相机前的 World Space Canvas。")]
    public Image fadePanel;

    [Header("Speech")]
    [Tooltip("第一段旁白接近结束时启动的语音识别脚本")]
    public WindowsSpeechSceneSelector speechSelector;

    [Header("Scene Router")]
    public MemorySceneRouter sceneRouter;

    [Header("Fan Control")]
    public ThreeFanSerialController fanController;

    [Range(0, 255)]
    public int transitionFanMaxPwm = 255;

    [Tooltip("风机是否跟随雨量一起增强，并在转场前降为0")]
    public bool enableFanFadeWithRain = true;

    [Header("Rain Particle Systems")]
    public ParticleSystem[] rainDropParticles;
    public ParticleSystem[] splashParticles;

    [Header("Rain Particle Amount")]
    public float rainDropStartRate = 0f;
    public float rainDropLowRate = 10f;
    public float rainDropTargetRate = 400f;

    public float splashStartRate = 0f;
    public float splashLowRate = 10f;
    public float splashTargetRate = 300f;

    [Tooltip("低雨量阶段持续时间：从 0 慢慢涨到 10")]
    public float lowRateFadeDuration = 7f;

    [Tooltip("高雨量阶段持续时间：从 10 涨到最终雨量")]
    public float highRateFadeDuration = 3f;

    [Header("Start Timing")]
    [Tooltip("进入场景后等待多久开始播放第一段雨声")]
    public float startDelay = 3f;

    [Header("Rain Intro Settings")]
    [Range(0f, 1f)]
    public float rainIntroTargetVolume = 1f;

    public float rainIntroFadeInDuration = 1.5f;

    [Tooltip("雨声音频1播放多少秒后开始切到雨声音频2")]
    public float rainCrossfadeStartTime = 10f;

    public float rainCrossfadeDuration = 4f;

    [Header("Rain Loop Settings")]
    [Range(0f, 1f)]
    public float rainLoopTargetVolume = 1f;

    [Tooltip("从场景开始计时，多少秒后开始压低雨声音频2")]
    public float narrationStartTime = 30f;

    [Range(0f, 1f)]
    public float rainLoopDuckedVolume = 0.25f;

    public float rainDuckDuration = 2f;

    [Header("Narration Settings")]
    [Range(0f, 1f)]
    public float narrationTargetVolume = 1f;

    public float narrationFadeInDuration = 1f;

    [Tooltip("雨声音频2开始降低后，等待多少秒开始播放旁白1。")]
    public float narrationDelayAfterRainDuckStart = 1.0f;

    [Tooltip("旁白1结束前多少秒提前启动语音识别。")]
    public float startListeningBeforeNarrationEnd = 0.8f;

    [Header("Outro Settings")]
    [Range(0f, 1f)]
    public float outroNarrationTargetVolume = 1f;

    public float outroNarrationFadeInDuration = 0.6f;

    [Tooltip("旁白2开始后多久开始黑场。0 表示旁白2开始同时黑场。")]
    public float fadeOutStartDelayAfterOutro = 0.2f;

    [Tooltip("黑场渐出时间")]
    public float screenFadeOutDuration = 2.5f;

    [Tooltip("雨声/环境声淡出时间")]
    public float audioFadeOutDuration = 3f;

    [Tooltip("切场景前额外停顿")]
    public float waitBeforeSceneLoad = 0.2f;

    [Header("Debug")]
    public bool playOnStart = true;
    public bool enableDebugLog = false;

    private Coroutine timelineCoroutine;
    private Coroutine particleFadeCoroutine;
    private Coroutine fanFadeCoroutine;
    private Coroutine outroCoroutine;

    private bool outroStarted = false;
    private bool listeningStarted = false;

    private int currentFanPwm = 0;

    void Start()
    {
        if (sceneRouter == null)
        {
            sceneRouter = GetComponent<MemorySceneRouter>();
        }

        if (speechSelector == null)
        {
            speechSelector = GetComponent<WindowsSpeechSceneSelector>();
        }

        if (fanController == null)
        {
            fanController = FindObjectOfType<ThreeFanSerialController>();
        }

        SetupAudioSources();
        SetupParticleSystems();
        SetupFadePanel();

        SetAllTransitionFans(0);

        if (playOnStart)
        {
            PlayTimeline();
        }
    }

    private void SetupAudioSources()
    {
        if (rainIntroSource != null)
        {
            rainIntroSource.playOnAwake = false;
            rainIntroSource.loop = false;
            rainIntroSource.volume = 0f;
        }

        if (rainLoopSource != null)
        {
            rainLoopSource.playOnAwake = false;
            rainLoopSource.loop = true;
            rainLoopSource.volume = 0f;
        }

        if (narrationSource != null)
        {
            narrationSource.playOnAwake = false;
            narrationSource.loop = false;
            narrationSource.volume = 0f;
        }

        if (outroNarrationSource != null)
        {
            outroNarrationSource.playOnAwake = false;
            outroNarrationSource.loop = false;
            outroNarrationSource.volume = 0f;
        }
    }

    private void SetupFadePanel()
    {
        if (fadePanel == null)
        {
            return;
        }

        SetFadeAlpha(0f);
    }

    private void SetupParticleSystems()
    {
        SetEmissionRate(rainDropParticles, rainDropStartRate);
        SetEmissionRate(splashParticles, splashStartRate);

        PlayParticleSystems(rainDropParticles);
        PlayParticleSystems(splashParticles);
    }

    public void PlayTimeline()
    {
        if (timelineCoroutine != null)
        {
            StopCoroutine(timelineCoroutine);
        }

        if (particleFadeCoroutine != null)
        {
            StopCoroutine(particleFadeCoroutine);
        }

        if (fanFadeCoroutine != null)
        {
            StopCoroutine(fanFadeCoroutine);
        }

        outroStarted = false;
        listeningStarted = false;
        currentFanPwm = 0;

        SetAllTransitionFans(0);

        timelineCoroutine = StartCoroutine(AudioTimelineRoutine());
    }

    public void BeginOutroAndLoadScene(MemorySceneType targetScene)
    {
        if (outroStarted)
        {
            DebugLog("旁白2流程已经开始，忽略重复触发。");
            return;
        }

        outroStarted = true;

        if (outroCoroutine != null)
        {
            StopCoroutine(outroCoroutine);
        }

        outroCoroutine = StartCoroutine(OutroAndLoadSceneRoutine(targetScene));
    }

    private IEnumerator AudioTimelineRoutine()
    {
        DebugLog("中转场景音频时间轴开始。");

        yield return new WaitForSeconds(startDelay);

        if (rainIntroSource != null)
        {
            rainIntroSource.volume = 0f;
            rainIntroSource.Play();

            DebugLog("雨声音频1开始播放。");

            particleFadeCoroutine = StartCoroutine(FadeRainParticlesInTwoStages());

            if (enableFanFadeWithRain)
            {
                float totalRainFadeDuration = lowRateFadeDuration + highRateFadeDuration;
                fanFadeCoroutine = StartCoroutine(FadeAllTransitionFans(
                    0,
                    transitionFanMaxPwm,
                    totalRainFadeDuration
                ));
            }

            yield return StartCoroutine(FadeVolume(
                rainIntroSource,
                0f,
                rainIntroTargetVolume,
                rainIntroFadeInDuration
            ));
        }

        float waitBeforeCrossfade = Mathf.Max(
            0f,
            rainCrossfadeStartTime - startDelay - rainIntroFadeInDuration
        );

        DebugLog("等待进入雨声交叉淡化阶段：" + waitBeforeCrossfade + " 秒");
        yield return new WaitForSeconds(waitBeforeCrossfade);

        if (rainLoopSource != null)
        {
            rainLoopSource.volume = 0f;
            rainLoopSource.loop = true;
            rainLoopSource.Play();
            DebugLog("雨声音频2开始播放并循环。");
        }

        DebugLog("雨声1 → 雨声2 交叉淡化开始。");

        float introStartVolume = rainIntroSource != null ? rainIntroSource.volume : 0f;

        Coroutine fadeOutIntro = null;
        Coroutine fadeInLoop = null;

        if (rainIntroSource != null)
        {
            fadeOutIntro = StartCoroutine(FadeVolume(
                rainIntroSource,
                introStartVolume,
                0f,
                rainCrossfadeDuration
            ));
        }

        if (rainLoopSource != null)
        {
            fadeInLoop = StartCoroutine(FadeVolume(
                rainLoopSource,
                0f,
                rainLoopTargetVolume,
                rainCrossfadeDuration
            ));
        }

        if (fadeOutIntro != null)
        {
            yield return fadeOutIntro;
        }

        if (fadeInLoop != null)
        {
            yield return fadeInLoop;
        }

        if (rainIntroSource != null)
        {
            rainIntroSource.Stop();
            rainIntroSource.volume = 0f;
        }

        SetEmissionRate(rainDropParticles, rainDropTargetRate);
        SetEmissionRate(splashParticles, splashTargetRate);

        DebugLog("雨声交叉淡化完成，雨声音频2循环播放中。粒子雨量保持稳定。");

        float elapsedApprox = startDelay + rainIntroFadeInDuration + waitBeforeCrossfade + rainCrossfadeDuration;
        float waitBeforeNarration = Mathf.Max(0f, narrationStartTime - elapsedApprox);

        DebugLog("等待旁白进入：" + waitBeforeNarration + " 秒");
        yield return new WaitForSeconds(waitBeforeNarration);

        Coroutine rainDuckCoroutine = null;

        if (rainLoopSource != null)
        {
            DebugLog("雨声音频2开始降低音量，为旁白让位。");

            rainDuckCoroutine = StartCoroutine(FadeVolume(
                rainLoopSource,
                rainLoopSource.volume,
                rainLoopDuckedVolume,
                rainDuckDuration
            ));
        }

        yield return new WaitForSeconds(narrationDelayAfterRainDuckStart);

        if (narrationSource != null)
        {
            narrationSource.volume = 0f;
            narrationSource.Play();

            DebugLog("旁白1开始播放。");

            yield return StartCoroutine(FadeVolume(
                narrationSource,
                0f,
                narrationTargetVolume,
                narrationFadeInDuration
            ));

            float totalNarrationLength = 0f;

            if (narrationSource.clip != null)
            {
                totalNarrationLength = narrationSource.clip.length;
            }

            float listenStartDelay = Mathf.Max(
                0f,
                totalNarrationLength - narrationFadeInDuration - startListeningBeforeNarrationEnd
            );

            DebugLog("等待旁白1接近结束后启动语音识别：" + listenStartDelay + " 秒");
            yield return new WaitForSeconds(listenStartDelay);

            StartSpeechListeningOnce();

            float remainingNarrationTime = Mathf.Max(0f, startListeningBeforeNarrationEnd);
            yield return new WaitForSeconds(remainingNarrationTime);
        }
        else
        {
            Debug.LogWarning("Narration Source 未绑定，直接启动语音识别。");
            StartSpeechListeningOnce();
        }

        if (rainDuckCoroutine != null)
        {
            yield return rainDuckCoroutine;
        }

        DebugLog("中转场景音频时间轴第一阶段完成。");
    }

    private void StartSpeechListeningOnce()
    {
        if (listeningStarted)
        {
            DebugLog("语音识别已经启动过，忽略重复启动。");
            return;
        }

        listeningStarted = true;

        DebugLog("启动语音识别。");

        if (speechSelector != null)
        {
            speechSelector.StartListening();
        }
        else
        {
            Debug.LogWarning("Speech Selector 未绑定，无法启动语音识别。");
        }
    }

    private IEnumerator OutroAndLoadSceneRoutine(MemorySceneType targetScene)
    {
        DebugLog("开始旁白2与转场流程，目标场景：" + targetScene);

        if (outroNarrationSource != null)
        {
            outroNarrationSource.volume = 0f;
            outroNarrationSource.Play();

            yield return StartCoroutine(FadeVolume(
                outroNarrationSource,
                0f,
                outroNarrationTargetVolume,
                outroNarrationFadeInDuration
            ));
        }
        else
        {
            Debug.LogWarning("Outro Narration Source 未绑定，将直接执行淡出转场。");
        }

        yield return new WaitForSeconds(fadeOutStartDelayAfterOutro);

        Coroutine screenFade = null;
        Coroutine rainFade = null;
        Coroutine introFade = null;
        Coroutine narrationFade = null;
        Coroutine fanFade = null;

        if (fadePanel != null)
        {
            screenFade = StartCoroutine(FadeScreenToBlack(screenFadeOutDuration));
        }

        if (rainLoopSource != null)
        {
            rainFade = StartCoroutine(FadeVolume(
                rainLoopSource,
                rainLoopSource.volume,
                0f,
                audioFadeOutDuration
            ));
        }

        if (rainIntroSource != null && rainIntroSource.isPlaying)
        {
            introFade = StartCoroutine(FadeVolume(
                rainIntroSource,
                rainIntroSource.volume,
                0f,
                audioFadeOutDuration
            ));
        }

        if (narrationSource != null && narrationSource.isPlaying)
        {
            narrationFade = StartCoroutine(FadeVolume(
                narrationSource,
                narrationSource.volume,
                0f,
                audioFadeOutDuration
            ));
        }

        if (enableFanFadeWithRain)
        {
            if (fanFadeCoroutine != null)
            {
                StopCoroutine(fanFadeCoroutine);
            }

            fanFade = StartCoroutine(FadeAllTransitionFans(
                currentFanPwm,
                0,
                audioFadeOutDuration
            ));
        }

        float outroRemainingTime = 0f;

        if (outroNarrationSource != null && outroNarrationSource.clip != null)
        {
            outroRemainingTime = Mathf.Max(
                0f,
                outroNarrationSource.clip.length - outroNarrationSource.time
            );
        }

        DebugLog("等待旁白2播放完成：" + outroRemainingTime + " 秒");
        yield return new WaitForSeconds(outroRemainingTime);

        if (screenFade != null)
        {
            yield return screenFade;
        }

        if (rainFade != null)
        {
            yield return rainFade;
        }

        if (introFade != null)
        {
            yield return introFade;
        }

        if (narrationFade != null)
        {
            yield return narrationFade;
        }

        if (fanFade != null)
        {
            yield return fanFade;
        }

        SetAllTransitionFans(0);

        yield return new WaitForSeconds(waitBeforeSceneLoad);

        DebugLog("旁白2结束，完成场景切换：" + targetScene);

        if (sceneRouter != null)
        {
            sceneRouter.LoadSceneByType(targetScene);
        }
        else
        {
            Debug.LogError("Scene Router 未绑定，无法切换场景。");
        }
    }

    private IEnumerator FadeRainParticlesInTwoStages()
    {
        DebugLog("雨量粒子低雨量阶段开始：0 → 10。");

        yield return StartCoroutine(FadeParticleRates(
            rainDropStartRate,
            rainDropLowRate,
            splashStartRate,
            splashLowRate,
            lowRateFadeDuration
        ));

        DebugLog("雨量粒子高雨量阶段开始：10 → 目标雨量。");

        yield return StartCoroutine(FadeParticleRates(
            rainDropLowRate,
            rainDropTargetRate,
            splashLowRate,
            splashTargetRate,
            highRateFadeDuration
        ));

        SetEmissionRate(rainDropParticles, rainDropTargetRate);
        SetEmissionRate(splashParticles, splashTargetRate);

        DebugLog("雨量粒子增强完成。");
    }

    private IEnumerator FadeParticleRates(
        float rainDropFrom,
        float rainDropTo,
        float splashFrom,
        float splashTo,
        float duration
    )
    {
        if (duration <= 0f)
        {
            SetEmissionRate(rainDropParticles, rainDropTo);
            SetEmissionRate(splashParticles, splashTo);
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            float rainDropRate = Mathf.Lerp(rainDropFrom, rainDropTo, t);
            float splashRate = Mathf.Lerp(splashFrom, splashTo, t);

            SetEmissionRate(rainDropParticles, rainDropRate);
            SetEmissionRate(splashParticles, splashRate);

            yield return null;
        }

        SetEmissionRate(rainDropParticles, rainDropTo);
        SetEmissionRate(splashParticles, splashTo);
    }

    private IEnumerator FadeAllTransitionFans(int from, int to, float duration)
    {
        if (fanController == null)
        {
            DebugLog("Fan Controller 未绑定，跳过风机控制。");
            yield break;
        }

        from = Mathf.Clamp(from, 0, 255);
        to = Mathf.Clamp(to, 0, 255);

        if (duration <= 0f)
        {
            SetAllTransitionFans(to);
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            SetAllTransitionFans(value);

            yield return null;
        }

        SetAllTransitionFans(to);
    }

    private void SetAllTransitionFans(int value)
    {
        currentFanPwm = Mathf.Clamp(value, 0, 255);

        if (fanController != null)
        {
            fanController.SetFans(currentFanPwm, currentFanPwm, currentFanPwm);
        }
    }

    private void SetEmissionRate(ParticleSystem[] particleSystems, float rate)
    {
        if (particleSystems == null)
        {
            return;
        }

        foreach (ParticleSystem ps in particleSystems)
        {
            SetEmissionRate(ps, rate);
        }
    }

    private void SetEmissionRate(ParticleSystem particleSystem, float rate)
    {
        if (particleSystem == null)
        {
            return;
        }

        var emission = particleSystem.emission;
        emission.enabled = true;

        ParticleSystem.MinMaxCurve rateCurve = emission.rateOverTime;
        rateCurve.mode = ParticleSystemCurveMode.Constant;
        rateCurve.constant = rate;
        emission.rateOverTime = rateCurve;
    }

    private void PlayParticleSystems(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null)
        {
            return;
        }

        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps != null)
            {
                ps.Play();
            }
        }
    }

    private IEnumerator FadeVolume(AudioSource source, float from, float to, float duration)
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

    private IEnumerator FadeScreenToBlack(float duration)
    {
        if (fadePanel == null)
        {
            yield break;
        }

        float startAlpha = fadePanel.color.a;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            SetFadeAlpha(Mathf.Lerp(startAlpha, 1f, t));
            yield return null;
        }

        SetFadeAlpha(1f);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadePanel == null)
        {
            return;
        }

        Color color = fadePanel.color;
        color.a = alpha;
        fadePanel.color = color;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void DebugLog(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log(message);
        }
    }
}