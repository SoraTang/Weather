using UnityEngine;
using UnityEngine.Windows.Speech;

public class VoiceFanController : MonoBehaviour
{
    [Header("Fan Control")]
    public FanSerialController fanController;

    [Header("Debug")]
    public bool autoRestartDictation = true;
    public bool enableDebugLog = true;

    [Header("Voice Command PWM")]
    public int startPwm = 200;
    public int stopPwm = 0;

    private DictationRecognizer dictationRecognizer;
    private bool isRestarting = false;

    void Start()
    {
        if (fanController == null)
        {
            Debug.LogError("VoiceFanController: fanController 没有绑定。");
            return;
        }

        DebugLog("Speech supported: " + PhraseRecognitionSystem.isSupported);
        DebugLog("Speech status before start: " + PhraseRecognitionSystem.Status);

        PhraseRecognitionSystem.OnError += OnSpeechError;
        PhraseRecognitionSystem.OnStatusChanged += OnSpeechStatusChanged;

        CreateAndStartDictation();
    }

    void CreateAndStartDictation()
    {
        if (dictationRecognizer != null)
        {
            CleanupDictation();
        }

        dictationRecognizer = new DictationRecognizer();
        dictationRecognizer.InitialSilenceTimeoutSeconds = 5f;
        dictationRecognizer.AutoSilenceTimeoutSeconds = 3f;

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
        DebugLog("Hypothesis: " + text);
    }

private void OnDictationResult(string text, ConfidenceLevel confidence)
{
    DebugLog("Result: " + text + " | Confidence: " + confidence);

    if (string.IsNullOrEmpty(text))
        return;

    string normalized = text.Replace(" ", "");

    // ===== 停止 =====
    if (normalized.Contains("停") || normalized.Contains("关"))
    {
        DebugLog("Command: STOP");
        fanController.SetPwm(0);
        return;
    }

    // ===== 最大风 =====
    if (normalized.Contains("最大") || normalized.Contains("最强"))
    {
        DebugLog("Command: MAX WIND");
        fanController.SetPwm(255);
        return;
    }

    // ===== 大风 =====
    if (normalized.Contains("大") || normalized.Contains("强"))
    {
        DebugLog("Command: STRONG WIND");
        fanController.SetPwm(200);
        return;
    }

    // ===== 小风 =====
    if (normalized.Contains("小") || normalized.Contains("微"))
    {
        DebugLog("Command: LIGHT WIND");
        fanController.SetPwm(120);
        return;
    }

    // ===== 启动 =====
    if (normalized.Contains("开") || normalized.Contains("开始") || normalized.Contains("吹"))
    {
        DebugLog("Command: START");
        fanController.SetPwm(startPwm);
        return;
    }
}

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        DebugLog("Dictation complete: " + cause);

        if (!autoRestartDictation) return;
        if (!gameObject.activeInHierarchy) return;
        if (isRestarting) return;

        // 正常情况下有时会自己结束，这里自动重启
        if (cause != DictationCompletionCause.Complete &&
            cause != DictationCompletionCause.TimeoutExceeded &&
            cause != DictationCompletionCause.PauseLimitExceeded)
        {
            RestartDictation();
            return;
        }

        RestartDictation();
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Dictation error: " + error + " | HResult: " + hresult);

        if (autoRestartDictation && gameObject.activeInHierarchy && !isRestarting)
        {
            RestartDictation();
        }
    }

    private void RestartDictation()
    {
        StartCoroutine(RestartNextFrame());
    }

    private System.Collections.IEnumerator RestartNextFrame()
    {
        isRestarting = true;

        CleanupDictation();
        yield return null;

        if (gameObject.activeInHierarchy)
        {
            CreateAndStartDictation();
        }

        isRestarting = false;
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

        CleanupDictation();
    }
}