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

    [Header("Listening Control")]
    [Tooltip("是否进入场景后自动开始监听。正式流程建议关闭，由旁白结束后调用 StartListening。")]
    public bool autoStartListening = false;

    [Header("Scene Selection")]
    [Tooltip("如果没有绑定 Transition Audio Timeline，则是否直接切场景。正式流程建议关闭。")]
    public bool autoLoadScene = false;

    [Header("Transition Flow")]
    [Tooltip("LLM 判断完成后，把目标场景交给这个脚本播放旁白2、淡出并切换。")]
    public TransitionAudioTimeline transitionAudioTimeline;

    [Header("LLM Settings")]
    [Tooltip("开启后优先使用 LLM 分类；失败时自动回退到本地关键词规则。")]
    public bool useLLMClassifier = true;

    [Header("Speech End Detection")]
    [Tooltip("最后一次检测到语音后，持续沉默多少秒才判定玩家说完。建议 6-8 秒。")]
    public float finalSilenceToSubmit = 7.0f;

    [Tooltip("开始监听后至少等待多少秒，避免刚启动就误判结束。")]
    public float minimumListeningDuration = 2.0f;

    [Tooltip("是否在最终发送时，把最新的 Hypothesis 也作为备份文本合并进去。建议开启。")]
    public bool includeLatestHypothesisOnSubmit = true;

    [Tooltip("过短文本是否忽略。例如只识别到“听”“嗯”等。")]
    public bool ignoreVeryShortText = true;

    [Tooltip("短文本过滤长度。小于等于这个长度会被忽略。")]
    public int veryShortTextLength = 1;

    [Header("Windows Dictation Settings")]
    [Tooltip("开始监听后，玩家开始说话前最多可以沉默多久。")]
    public float initialSilenceTimeout = 25f;

    [Tooltip("玩家停顿多久后，Windows 认为一小段语音结束，并返回一次 Result。这个不是最终发送判定。")]
    public float autoSilenceTimeout = 3.5f;

    private DictationRecognizer dictationRecognizer;

    private bool isRestarting = false;
    private bool hasRouted = false;
    private bool isProcessingRoute = false;
    private bool isListening = false;
    private bool heardAnySpeech = false;

    private float listeningStartTime = -1f;
    private float lastSpeechHeardTime = -1f;

    private string accumulatedText = "";
    private string lastSegmentText = "";
    private string latestHypothesisText = "";

    private Coroutine silenceMonitorCoroutine;

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

        if (transitionAudioTimeline == null)
        {
            transitionAudioTimeline = GetComponent<TransitionAudioTimeline>();
        }

        DebugLog("Speech supported: " + PhraseRecognitionSystem.isSupported);
        DebugLog("Speech status before start: " + PhraseRecognitionSystem.Status);
        DebugLog("当前场景：" + SceneManager.GetActiveScene().name);

        PhraseRecognitionSystem.OnError += OnSpeechError;
        PhraseRecognitionSystem.OnStatusChanged += OnSpeechStatusChanged;

        if (IsInActiveScene())
        {
            if (autoStartListening)
            {
                CreateAndStartDictation(true);
            }
            else
            {
                DebugLog("当前在中转场景，但 Auto Start Listening 关闭，等待外部脚本启动监听。");
            }
        }
        else
        {
            DebugLog("当前不在中转场景，不启动语音监听。");
        }
    }

    public void StartListening()
    {
        if (!IsInActiveScene())
        {
            DebugLog("当前不在中转场景，不能启动语音监听。");
            return;
        }

        if (isListening || dictationRecognizer != null)
        {
            DebugLog("语音监听已经存在，不重复启动。");
            return;
        }

        DebugLog("外部触发：开始语音监听。");
        CreateAndStartDictation(true);
    }

    public void StopListening()
    {
        StopListeningBeforeSceneLoad();
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

        if (resetAccumulatedText)
        {
            accumulatedText = "";
            lastSegmentText = "";
            latestHypothesisText = "";
            heardAnySpeech = false;
            listeningStartTime = Time.time;
            lastSpeechHeardTime = -1f;
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
            isListening = true;

            DebugLog("Dictation started.");
            DebugLog("Speech status after start: " + PhraseRecognitionSystem.Status);

            StartSilenceMonitorIfNeeded();
        }
        catch (System.Exception e)
        {
            Debug.LogError("启动 DictationRecognizer 失败: " + e.Message);
            isListening = false;
        }
    }

    private void StartSilenceMonitorIfNeeded()
    {
        if (silenceMonitorCoroutine != null)
        {
            return;
        }

        silenceMonitorCoroutine = StartCoroutine(SilenceMonitorRoutine());
    }

    private IEnumerator SilenceMonitorRoutine()
    {
        DebugLog("语音结束监测启动。");

        while (!hasRouted && !isProcessingRoute && IsInActiveScene())
        {
            if (heardAnySpeech && lastSpeechHeardTime > 0f)
            {
                float silenceDuration = Time.time - lastSpeechHeardTime;
                float listeningDuration = Time.time - listeningStartTime;

                if (listeningDuration >= minimumListeningDuration && silenceDuration >= finalSilenceToSubmit)
                {
                    DebugLog("检测到长时间沉默，判定玩家说完。沉默时长：" + silenceDuration);

                    silenceMonitorCoroutine = null;
                    SubmitAccumulatedTextToLLM();
                    yield break;
                }
            }

            yield return null;
        }

        silenceMonitorCoroutine = null;
    }

    private void OnDictationHypothesis(string text)
    {
        if (!IsInActiveScene())
        {
            return;
        }

        if (hasRouted || isProcessingRoute)
        {
            return;
        }

        string normalized = NormalizeText(text);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (ShouldIgnoreText(normalized))
        {
            DebugLog("Hypothesis 过短，忽略：" + normalized);
            return;
        }

        latestHypothesisText = normalized;
        MarkSpeechHeard();

        DebugLog("Hypothesis: " + normalized);
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

        if (ShouldIgnoreText(normalized))
        {
            DebugLog("Result 过短，忽略：" + normalized);
            return;
        }

        MarkSpeechHeard();

        AddTextSegment(normalized);

        latestHypothesisText = "";

        DebugLog("新增语音片段：" + normalized);
        DebugLog("当前累积文本：" + accumulatedText);
        DebugLog("等待更长沉默后统一发送...");
    }

    private void MarkSpeechHeard()
    {
        heardAnySpeech = true;
        lastSpeechHeardTime = Time.time;
    }

    private void AddTextSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return;
        }

        if (segment == lastSegmentText)
        {
            DebugLog("检测到重复语音片段，忽略：" + segment);
            return;
        }

        if (!string.IsNullOrWhiteSpace(accumulatedText))
        {
            if (accumulatedText.Contains(segment))
            {
                DebugLog("累积文本中已包含该片段，忽略：" + segment);
                lastSegmentText = segment;
                return;
            }

            if (segment.Contains(accumulatedText))
            {
                DebugLog("新片段包含已有累积文本，使用更完整的新片段替换。");
                accumulatedText = segment;
                lastSegmentText = segment;
                return;
            }
        }

        lastSegmentText = segment;

        if (string.IsNullOrWhiteSpace(accumulatedText))
        {
            accumulatedText = segment;
        }
        else
        {
            accumulatedText += "，" + segment;
        }
    }

    private void SubmitAccumulatedTextToLLM()
    {
        if (hasRouted || isProcessingRoute)
        {
            return;
        }

        string finalText = BuildFinalTextForSubmit();

        if (string.IsNullOrWhiteSpace(finalText))
        {
            DebugLog("最终文本为空，不执行分类。");
            return;
        }

        if (ShouldIgnoreText(finalText))
        {
            DebugLog("最终文本过短，不执行分类：" + finalText);
            return;
        }

        isProcessingRoute = true;

        CleanupDictation();

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

                    HandleSceneResult(sceneType);
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

                    HandleSceneResult(fallbackResult);
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

            HandleSceneResult(result);
        }
    }

    private string BuildFinalTextForSubmit()
    {
        string finalText = accumulatedText;

        if (includeLatestHypothesisOnSubmit && !string.IsNullOrWhiteSpace(latestHypothesisText))
        {
            string hypothesis = latestHypothesisText;

            if (string.IsNullOrWhiteSpace(finalText))
            {
                finalText = hypothesis;
            }
            else if (hypothesis.Contains(finalText))
            {
                finalText = hypothesis;
            }
            else if (!finalText.Contains(hypothesis))
            {
                finalText += "，" + hypothesis;
            }
        }

        return finalText;
    }

    private bool ShouldIgnoreText(string text)
    {
        if (!ignoreVeryShortText)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.Length <= veryShortTextLength;
    }

    private void HandleSceneResult(MemorySceneType sceneType)
    {
        StopListeningBeforeSceneLoad();

        if (transitionAudioTimeline != null)
        {
            DebugLog("将目标场景交给 TransitionAudioTimeline：" + sceneType);
            transitionAudioTimeline.BeginOutroAndLoadScene(sceneType);
            return;
        }

        if (autoLoadScene)
        {
            DebugLog("未绑定 TransitionAudioTimeline，直接切换场景：" + sceneType);
            router.LoadSceneByType(sceneType);
        }
        else
        {
            Debug.LogWarning("已获得目标场景，但未绑定 TransitionAudioTimeline，且 Auto Load Scene 关闭。");
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

        isListening = false;

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

        RestartDictationWithoutClearingText();
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Dictation error: " + error + " | HResult: " + hresult);

        isListening = false;

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
            CreateAndStartDictation(false);
        }

        isRestarting = false;
    }

    private void StopListeningBeforeSceneLoad()
    {
        StopSilenceMonitor();

        CleanupDictation();

        accumulatedText = "";
        lastSegmentText = "";
        latestHypothesisText = "";
        heardAnySpeech = false;
        lastSpeechHeardTime = -1f;

        DebugLog("已停止语音监听。");
    }

    private void StopSilenceMonitor()
    {
        if (silenceMonitorCoroutine != null)
        {
            StopCoroutine(silenceMonitorCoroutine);
            silenceMonitorCoroutine = null;
        }
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
        if (dictationRecognizer == null)
        {
            isListening = false;
            return;
        }

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
        isListening = false;
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

        StopSilenceMonitor();
        CleanupDictation();
    }
}