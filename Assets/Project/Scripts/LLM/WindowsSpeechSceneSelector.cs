using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows.Speech;

public class WindowsSpeechSceneSelector : MonoBehaviour
{
    [Header("Debug")]
    public bool autoRestartDictation = true;
    public bool enableDebugLog = false;

    [Header("Active Scene Limit")]
    [Tooltip("只有在这个场景中才会启动和处理语音识别。请填写你的中转场景名。")]
    public string activeOnlyInSceneName = "TestScene";

    [Header("Warmup")]
    [Tooltip("进入目标场景后是否自动预热语音识别器。建议开启。")]
    public bool prewarmOnStart = true;

    [Tooltip("进入场景后延迟多久开始预热，避免和场景初始化同帧。")]
    public float prewarmDelay = 0.5f;

    [Tooltip("识别器完成/异常后，延迟多久再重启，避免同帧抖动。")]
    public float restartDelay = 0.2f;

    [Header("Listening Control")]
    [Tooltip("是否进入场景后自动开始接收玩家语音。正式流程建议关闭，由旁白结束前调用 StartListening。")]
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
    [Tooltip("最后一次检测到语音后，持续沉默多少秒才判定玩家说完。建议 5-8 秒。")]
    public float finalSilenceToSubmit = 6.0f;

    [Tooltip("第一次检测到语音后，至少等待多少秒，才允许判定玩家说完。")]
    public float minimumDurationAfterFirstSpeech = 2.0f;

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

    private bool isRecognizerPrepared = false;
    private bool isRecognizerRunning = false;
    private bool isRestarting = false;

    private bool hasRouted = false;
    private bool isProcessingRoute = false;

    // 这一层才是“当前是否接收玩家这一次的输入”
    private bool isCaptureWindowOpen = false;
    private bool heardAnySpeech = false;

    private float firstSpeechHeardTime = -1f;
    private float lastSpeechHeardTime = -1f;

    private string accumulatedText = "";
    private string lastSegmentText = "";
    private string latestHypothesisText = "";

    private Coroutine silenceMonitorCoroutine;
    private Coroutine restartCoroutine;
    private Coroutine prewarmCoroutine;

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

        PhraseRecognitionSystem.OnError += OnSpeechError;
        PhraseRecognitionSystem.OnStatusChanged += OnSpeechStatusChanged;

        if (!IsInActiveScene())
        {
            DebugLog("当前不在中转场景，不预热语音识别。");
            return;
        }

        if (prewarmOnStart)
        {
            prewarmCoroutine = StartCoroutine(PrewarmRoutine());
        }
        else if (autoStartListening)
        {
            // 不预热但又要自动接收时，退化为直接启动
            StartListening();
        }
    }

    public void PrepareRecognizer()
    {
        if (!IsInActiveScene()) return;
        if (isRecognizerPrepared || prewarmCoroutine != null) return;

        prewarmCoroutine = StartCoroutine(PrewarmRoutine());
    }

    private IEnumerator PrewarmRoutine()
    {
        yield return new WaitForSeconds(prewarmDelay);

        if (!IsInActiveScene())
        {
            prewarmCoroutine = null;
            yield break;
        }

        EnsureRecognizerCreated();

        StartRecognizerIfNeeded();

        // 预热阶段不接收玩家输入
        CloseCaptureWindowOnly();

        isRecognizerPrepared = true;
        prewarmCoroutine = null;

        DebugLog("语音识别器预热完成。");

        if (autoStartListening)
        {
            OpenCaptureWindow();
        }
    }

    public void StartListening()
    {
        if (!IsInActiveScene())
        {
            DebugLog("当前不在中转场景，不能启动语音监听。");
            return;
        }

        EnsureRecognizerCreated();

        if (!isRecognizerRunning)
        {
            StartRecognizerIfNeeded();
        }

        OpenCaptureWindow();
        DebugLog("开始接收玩家语音窗口。");
    }

    public void StopListening()
    {
        StopListeningBeforeSceneLoad();
    }

    private bool IsInActiveScene()
    {
        return SceneManager.GetActiveScene().name == activeOnlyInSceneName;
    }

    private void EnsureRecognizerCreated()
    {
        if (dictationRecognizer != null)
        {
            return;
        }

        dictationRecognizer = new DictationRecognizer
        {
            InitialSilenceTimeoutSeconds = initialSilenceTimeout,
            AutoSilenceTimeoutSeconds = autoSilenceTimeout
        };

        dictationRecognizer.DictationHypothesis += OnDictationHypothesis;
        dictationRecognizer.DictationResult += OnDictationResult;
        dictationRecognizer.DictationComplete += OnDictationComplete;
        dictationRecognizer.DictationError += OnDictationError;

        DebugLog("DictationRecognizer 已创建。");
    }

    private void StartRecognizerIfNeeded()
    {
        if (dictationRecognizer == null)
        {
            return;
        }

        if (isRecognizerRunning)
        {
            return;
        }

        try
        {
            dictationRecognizer.Start();
            isRecognizerRunning = true;

            DebugLog("Dictation recognizer started.");
        }
        catch (System.Exception e)
        {
            isRecognizerRunning = false;
            Debug.LogError("启动 DictationRecognizer 失败: " + e.Message);
        }
    }

    private void OpenCaptureWindow()
    {
        hasRouted = false;
        isProcessingRoute = false;

        accumulatedText = "";
        lastSegmentText = "";
        latestHypothesisText = "";
        heardAnySpeech = false;
        firstSpeechHeardTime = -1f;
        lastSpeechHeardTime = -1f;

        isCaptureWindowOpen = true;

        StartSilenceMonitorIfNeeded();
    }

    private void CloseCaptureWindowOnly()
    {
        isCaptureWindowOpen = false;
        StopSilenceMonitor();

        accumulatedText = "";
        lastSegmentText = "";
        latestHypothesisText = "";
        heardAnySpeech = false;
        firstSpeechHeardTime = -1f;
        lastSpeechHeardTime = -1f;
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
        while (isCaptureWindowOpen && !hasRouted && !isProcessingRoute && IsInActiveScene())
        {
            if (heardAnySpeech && firstSpeechHeardTime > 0f && lastSpeechHeardTime > 0f)
            {
                float silenceDuration = Time.time - lastSpeechHeardTime;
                float durationAfterFirstSpeech = Time.time - firstSpeechHeardTime;

                if (durationAfterFirstSpeech >= minimumDurationAfterFirstSpeech &&
                    silenceDuration >= finalSilenceToSubmit)
                {
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
        if (!IsInActiveScene()) return;
        if (!isCaptureWindowOpen) return;
        if (hasRouted || isProcessingRoute) return;

        string normalized = NormalizeText(text);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (ShouldIgnoreText(normalized))
        {
            return;
        }

        latestHypothesisText = normalized;
        MarkSpeechHeard();
    }

    private void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        if (!IsInActiveScene()) return;
        if (!isCaptureWindowOpen) return;
        if (hasRouted || isProcessingRoute) return;
        if (string.IsNullOrWhiteSpace(text)) return;

        string normalized = NormalizeText(text);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (ShouldIgnoreText(normalized))
        {
            return;
        }

        MarkSpeechHeard();
        AddTextSegment(normalized);
        latestHypothesisText = "";
    }

    private void MarkSpeechHeard()
    {
        if (!heardAnySpeech)
        {
            firstSpeechHeardTime = Time.time;
        }

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
            return;
        }

        if (!string.IsNullOrWhiteSpace(accumulatedText))
        {
            if (accumulatedText.Contains(segment))
            {
                lastSegmentText = segment;
                return;
            }

            if (segment.Contains(accumulatedText))
            {
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
            CloseCaptureWindowOnly();
            return;
        }

        if (ShouldIgnoreText(finalText))
        {
            DebugLog("最终文本过短，不执行分类：" + finalText);
            CloseCaptureWindowOnly();
            return;
        }

        isProcessingRoute = true;
        isCaptureWindowOpen = false;
        StopSilenceMonitor();

        DebugLog("玩家回答结束，最终发送给 LLM 的完整文本：" + finalText);

        if (useLLMClassifier && llmClassifier != null)
        {
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
                    HandleSceneResult(fallbackResult);
                }
            );
        }
        else
        {
            MemorySceneType result = classifier.Classify(finalText);

            hasRouted = true;
            isProcessingRoute = false;
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
            transitionAudioTimeline.BeginOutroAndLoadScene(sceneType);
            return;
        }

        if (autoLoadScene)
        {
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
        isRecognizerRunning = false;

        if (!IsInActiveScene()) return;
        if (hasRouted || isProcessingRoute) return;
        if (!autoRestartDictation) return;
        if (!gameObject.activeInHierarchy) return;
        if (isRestarting) return;

        ScheduleRestart();
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Dictation error: " + error + " | HResult: " + hresult);
        isRecognizerRunning = false;

        if (!IsInActiveScene()) return;
        if (hasRouted || isProcessingRoute) return;
        if (!autoRestartDictation) return;
        if (!gameObject.activeInHierarchy) return;
        if (isRestarting) return;

        ScheduleRestart();
    }

    private void ScheduleRestart()
    {
        if (restartCoroutine != null)
        {
            StopCoroutine(restartCoroutine);
        }

        restartCoroutine = StartCoroutine(RestartRecognizerRoutine());
    }

    private IEnumerator RestartRecognizerRoutine()
    {
        isRestarting = true;

        yield return new WaitForSeconds(restartDelay);

        if (gameObject.activeInHierarchy && !hasRouted && !isProcessingRoute && IsInActiveScene())
        {
            StartRecognizerIfNeeded();
        }

        restartCoroutine = null;
        isRestarting = false;
    }

    private void StopListeningBeforeSceneLoad()
    {
        CloseCaptureWindowOnly();

        try
        {
            if (dictationRecognizer != null && isRecognizerRunning)
            {
                dictationRecognizer.Stop();
            }
        }
        catch { }

        isRecognizerRunning = false;
        DebugLog("已停止接收玩家语音。");
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

    private void CleanupRecognizer()
    {
        if (dictationRecognizer == null)
        {
            isRecognizerRunning = false;
            return;
        }

        dictationRecognizer.DictationHypothesis -= OnDictationHypothesis;
        dictationRecognizer.DictationResult -= OnDictationResult;
        dictationRecognizer.DictationComplete -= OnDictationComplete;
        dictationRecognizer.DictationError -= OnDictationError;

        try
        {
            if (isRecognizerRunning)
            {
                dictationRecognizer.Stop();
            }
        }
        catch { }

        dictationRecognizer.Dispose();
        dictationRecognizer = null;
        isRecognizerRunning = false;
        isRecognizerPrepared = false;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
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

        if (restartCoroutine != null)
        {
            StopCoroutine(restartCoroutine);
        }

        if (prewarmCoroutine != null)
        {
            StopCoroutine(prewarmCoroutine);
        }

        StopSilenceMonitor();
        CleanupRecognizer();
    }
}