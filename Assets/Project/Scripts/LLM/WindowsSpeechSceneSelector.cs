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
    public string activeOnlyInSceneName = "TransitionScene";

    [Header("Scene Selection")]
    public bool autoLoadScene = true;

    [Header("Sentence End Detection")]
    [Tooltip("识别到文字后，再等待多少秒才认为玩家说完。")]
    public float silenceConfirmDelay = 2.0f;

    [Tooltip("玩家开始说话前最多等待多久。")]
    public float initialSilenceTimeout = 10f;

    [Tooltip("玩家停顿多久后，Windows 认为一句话结束。")]
    public float autoSilenceTimeout = 1.8f;

    private DictationRecognizer dictationRecognizer;

    private bool isRestarting = false;
    private bool hasRouted = false;

    private string pendingText = "";
    private Coroutine pendingRouteCoroutine;

    private SimpleSceneClassifier classifier;
    private MemorySceneRouter router;

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

        DebugLog("Speech supported: " + PhraseRecognitionSystem.isSupported);
        DebugLog("Speech status before start: " + PhraseRecognitionSystem.Status);
        DebugLog("当前场景：" + SceneManager.GetActiveScene().name);

        PhraseRecognitionSystem.OnError += OnSpeechError;
        PhraseRecognitionSystem.OnStatusChanged += OnSpeechStatusChanged;

        if (IsInActiveScene())
        {
            CreateAndStartDictation();
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

    private void CreateAndStartDictation()
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

        // Hypothesis 是实时猜测结果，只用于调试显示，不在这里切场景
        DebugLog("Hypothesis: " + text);
    }

    private void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        if (!IsInActiveScene())
        {
            DebugLog("当前不在中转场景，忽略语音识别结果。");
            return;
        }

        if (hasRouted)
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

        pendingText = normalized;

        DebugLog("暂存识别文本：" + pendingText);
        DebugLog("等待玩家是否继续说话...");

        if (pendingRouteCoroutine != null)
        {
            StopCoroutine(pendingRouteCoroutine);
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

        if (hasRouted)
        {
            yield break;
        }

        string finalText = pendingText;

        if (string.IsNullOrWhiteSpace(finalText))
        {
            DebugLog("最终文本为空，不执行分类。");
            yield break;
        }

        hasRouted = true;

        DebugLog("玩家句子结束，最终用于分类的文本：" + finalText);

        MemorySceneType result = classifier.Classify(finalText);

        DebugLog("语音场景分类结果：" + result);

        StopListeningBeforeSceneLoad();

        if (autoLoadScene)
        {
            router.LoadSceneByType(result);
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

        if (hasRouted)
        {
            return;
        }

        if (!autoRestartDictation) return;
        if (!gameObject.activeInHierarchy) return;
        if (isRestarting) return;

        RestartDictation();
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Dictation error: " + error + " | HResult: " + hresult);

        if (!IsInActiveScene())
        {
            return;
        }

        if (hasRouted)
        {
            return;
        }

        if (autoRestartDictation && gameObject.activeInHierarchy && !isRestarting)
        {
            RestartDictation();
        }
    }

    private void RestartDictation()
    {
        StartCoroutine(RestartNextFrame());
    }

    private IEnumerator RestartNextFrame()
    {
        isRestarting = true;

        CleanupDictation();

        yield return null;

        if (gameObject.activeInHierarchy && !hasRouted && IsInActiveScene())
        {
            CreateAndStartDictation();
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