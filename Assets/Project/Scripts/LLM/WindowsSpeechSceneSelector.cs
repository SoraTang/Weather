using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows.Speech;

public class WindowsSpeechSceneSelector : MonoBehaviour
{
    [Header("Debug")]
    public bool autoRestartDictation = true;
    public bool enableDebugLog = true;

    [Header("Active Scene Limit")]
    [Tooltip("只有在这个场景中才会启动和处理语音识别。请填写你的中转场景名。")]
    public string activeOnlyInSceneName = "TestScene";

    [Header("Scene Selection")]
    public bool autoLoadScene = true;

    [Header("LLM Settings")]
    [Tooltip("开启后优先使用 LLM 分类；失败时自动回退到本地关键词规则。")]
    public bool useLLMClassifier = true;

    [Header("Sentence End Detection")]
    [Tooltip("收到一段识别结果后，再等待多少秒才认为玩家回答结束。期间如果继续说话，会重新计时。")]
    public float silenceConfirmDelay = 4.5f;

    [Tooltip("开始监听后，玩家开始说话前最多可以沉默多久。")]
    public float initialSilenceTimeout = 15f;

    [Tooltip("玩家停顿多久后，Windows 认为一小段语音结束，并返回一次识别结果。")]
    public float autoSilenceTimeout = 2.8f;

    private DictationRecognizer dictationRecognizer;

    private bool isRestarting = false;
    private bool hasRouted = false;
    private bool isProcessingRoute = false;

    // 多段语音累积文本
    private string accumulatedText = "";
    private string lastSegmentText = "";

    private Coroutine pendingRouteCoroutine;

    private SimpleSceneClassifier classifier;
    private MemorySceneRouter router;
    private LLMSceneClassifier llmClassifier;

    void Start()
    {
        classifier = GetComponent<SimpleSceneClassifier>();
        if (classifier == null)
        {
            classifier = gameObject.AddComponent<SimpleSceneClassifier>();
        }

        router = GetComponent<MemorySceneRouter>();
        if (router == null)
        {
            router = gameObject.AddComponent<MemorySceneRouter>();
        }

        llmClassifier = GetComponent<LLMSceneClassifier>();
        if (llmClassifier == null)
        {
            llmClassifier = gameObject.AddComponent<LLMSceneClassifier>();
        }

        DebugLog("Speech supported: " + PhraseRecognitionSystem.isSupported);
        DebugLog("Speech status before start: " + PhraseRecognitionSystem.Status);
        DebugLog("当前场景：" + SceneManager.GetActiveScene().name);

        PhraseRecognitionSystem.OnError += OnSpeechError;
        PhraseRecognitionSystem.OnStatusChanged += OnSpeechStatusChanged;

        if (IsInActiveScene())
        {
            // 初次进入中转场景，清空旧文本并开始监听
            CreateAndStartDictation(true);
        }
        else
        {
            DebugLog("当前不在中转场景，不启动语音监听。");
        }
    }

    private bool IsInActiveScene()
    {
        return SceneManager.GetActiveScene().name == activeOnlyInSceneName;
    }

    private void CreateAndStartDictation(bool resetAccumulatedText = true)
    {
        if (!IsInActiveScene())
        {
            DebugLog("当前不在中转场景，不启动语音监听。");
            return;
        }

        if (dictationRecognizer != null)
        {
            CleanupDictation();
        }

        hasRouted = false;
        isProcessingRoute = false;

        // 只有初次开始监听时清空文本
        // 自动重启监听时不清空，避免 TimeoutExceeded 后丢失前面识别到的内容
        if (resetAccumulatedText)
        {
            accumulatedText = "";
            lastSegmentText = "";
        }

        dictationRecognizer = new DictationRecognizer();

        dictationRecognizer.InitialSilenceTimeoutSeconds = initialSilenceTimeout;
        dictationRecognizer.AutoSilenceTimeoutSeconds = autoSilenceTimeout;

        dictationRecognizer.DictationHypothesis += OnDictationHypothesis;
        dictationRecognizer.DictationResult += OnDictationResult;
        dictationRecognizer.DictationComplete += OnDictationComplete;
        dictationRecognizer.DictationError += OnDictationError;

        try
        {
            dictationRecognizer.Start();
            DebugLog("Dictation started.");
            DebugLog("Speech status after start: " + PhraseRecognitionSystem.Status);
        }
        catch (System.Exception e)
        {
            Debug.LogError("启动 DictationRecognizer 失败: " + e.Message);
        }
    }

    private void OnDictationHypothesis(string text)
    {
        if (!IsInActiveScene())
        {
            return;
        }

        // Hypothesis 是实时猜测结果，只用于调试显示，不参与最终分类
        DebugLog("Hypothesis: " + text);
    }

    private void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        if (!IsInActiveScene())
        {
            DebugLog("当前不在中转场景，忽略语音识别结果。");
            return;
        }

        if (hasRouted || isProcessingRoute)
        {
            return;
        }

        DebugLog("Result: " + text + " | Confidence: " + confidence);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string normalized = NormalizeText(text);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        // 避免 Windows 偶尔重复返回同一句
        if (normalized == lastSegmentText)
        {
            DebugLog("检测到重复语音片段，忽略：" + normalized);
            return;
        }

        lastSegmentText = normalized;

        // 核心逻辑：不覆盖，而是把多段识别结果累积起来
        if (string.IsNullOrWhiteSpace(accumulatedText))
        {
            accumulatedText = normalized;
        }
        else
        {
            accumulatedText += "，" + normalized;
        }

        DebugLog("新增语音片段：" + normalized);
        DebugLog("当前累积文本：" + accumulatedText);
        DebugLog("等待玩家是否继续说话...");

        // 每次收到新片段，都重新开始最终确认倒计时
        if (pendingRouteCoroutine != null)
        {
            StopCoroutine(pendingRouteCoroutine);
            pendingRouteCoroutine = null;
        }

        pendingRouteCoroutine = StartCoroutine(RouteAfterSilence());
    }

    private IEnumerator RouteAfterSilence()
    {
        yield return new WaitForSeconds(silenceConfirmDelay);

        if (!IsInActiveScene())
        {
            DebugLog("等待期间已离开中转场景，不执行分类。");
            yield break;
        }

        if (hasRouted || isProcessingRoute)
        {
            yield break;
        }

        string finalText = accumulatedText;

        if (string.IsNullOrWhiteSpace(finalText))
        {
            DebugLog("最终文本为空，不执行分类。");
            yield break;
        }

        isProcessingRoute = true;

        DebugLog("玩家回答结束，最终发送给 LLM 的完整文本：" + finalText);

        if (useLLMClassifier && llmClassifier != null)
        {
            DebugLog("开始调用 LLM 分类...");

            llmClassifier.Classify(
                finalText,
                (sceneType, llmResult) =>
                {
                    if (hasRouted)
                    {
                        return;
                    }

                    hasRouted = true;
                    isProcessingRoute = false;

                    DebugLog("LLM 分类完成。");
                    DebugLog("LLM scene: " + llmResult.scene);
                    DebugLog("LLM confidence: " + llmResult.confidence);
                    DebugLog("LLM reason: " + llmResult.reason);

                    StopListeningBeforeSceneLoad();

                    if (autoLoadScene)
                    {
                        router.LoadSceneByType(sceneType);
                    }
                },
                (error) =>
                {
                    if (hasRouted)
                    {
                        return;
                    }

                    Debug.LogWarning("LLM 分类失败，改用本地关键词规则：" + error);

                    MemorySceneType fallbackResult = classifier.Classify(finalText);

                    hasRouted = true;
                    isProcessingRoute = false;

                    DebugLog("本地规则分类结果：" + fallbackResult);

                    StopListeningBeforeSceneLoad();

                    if (autoLoadScene)
                    {
                        router.LoadSceneByType(fallbackResult);
                    }
                }
            );
        }
        else
        {
            DebugLog("未启用 LLM，使用本地关键词规则。");

            MemorySceneType result = classifier.Classify(finalText);

            hasRouted = true;
            isProcessingRoute = false;

            DebugLog("本地规则分类结果：" + result);

            StopListeningBeforeSceneLoad();

            if (autoLoadScene)
            {
                router.LoadSceneByType(result);
            }
        }
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        return text
            .Replace(" ", "")
            .Replace("。", "")
            .Replace("，", "")
            .Replace(",", "")
            .Replace(".", "")
            .Replace("！", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace("？", "")
            .Replace("\n", "")
            .Replace("\r", "");
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        DebugLog("Dictation complete: " + cause);

        if (!IsInActiveScene())
        {
            return;
        }

        if (hasRouted || isProcessingRoute)
        {
            return;
        }

        if (!autoRestartDictation) return;
        if (!gameObject.activeInHierarchy) return;
        if (isRestarting) return;

        // 这里不要清空 accumulatedText
        // TimeoutExceeded 经常发生在识别到一句话之后
        // 我们要保留前面已经识别到的内容，继续等待玩家是否补充下一句
        RestartDictationWithoutClearingText();
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Dictation error: " + error + " | HResult: " + hresult);

        if (!IsInActiveScene())
        {
            return;
        }

        if (hasRouted || isProcessingRoute)
        {
            return;
        }

        if (autoRestartDictation && gameObject.activeInHierarchy && !isRestarting)
        {
            // 出错后也保留已经累积的文本，避免前半句丢失
            RestartDictationWithoutClearingText();
        }
    }

    private void RestartDictationWithoutClearingText()
    {
        StartCoroutine(RestartNextFrameWithoutClearingText());
    }

    private IEnumerator RestartNextFrameWithoutClearingText()
    {
        isRestarting = true;

        CleanupDictation();

        yield return null;

        if (gameObject.activeInHierarchy && !hasRouted && !isProcessingRoute && IsInActiveScene())
        {
            // false 表示：重启监听，但不清空已经累积的语音文本
            CreateAndStartDictation(false);
        }

        isRestarting = false;
    }

    private void StopListeningBeforeSceneLoad()
    {
        if (pendingRouteCoroutine != null)
        {
            StopCoroutine(pendingRouteCoroutine);
            pendingRouteCoroutine = null;
        }

        CleanupDictation();

        accumulatedText = "";
        lastSegmentText = "";

        DebugLog("即将切换场景，已停止语音监听。");
    }

    private void OnSpeechError(SpeechError errorCode)
    {
        Debug.LogError("Speech error: " + errorCode);
    }

    private void OnSpeechStatusChanged(SpeechSystemStatus status)
    {
        DebugLog("Speech status changed: " + status);
    }

    private void CleanupDictation()
    {
        if (dictationRecognizer == null) return;

        dictationRecognizer.DictationHypothesis -= OnDictationHypothesis;
        dictationRecognizer.DictationResult -= OnDictationResult;
        dictationRecognizer.DictationComplete -= OnDictationComplete;
        dictationRecognizer.DictationError -= OnDictationError;

        try
        {
            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                dictationRecognizer.Stop();
            }
        }
        catch { }

        dictationRecognizer.Dispose();
        dictationRecognizer = null;
    }

    private void DebugLog(string msg)
    {
        if (enableDebugLog)
        {
            Debug.Log(msg);
        }
    }

    void OnDestroy()
    {
        PhraseRecognitionSystem.OnError -= OnSpeechError;
        PhraseRecognitionSystem.OnStatusChanged -= OnSpeechStatusChanged;

        if (pendingRouteCoroutine != null)
        {
            StopCoroutine(pendingRouteCoroutine);
            pendingRouteCoroutine = null;
        }

        CleanupDictation();
    }
}