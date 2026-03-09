using UnityEngine;
using UnityEngine.Windows.Speech;

public class VoiceFanController : MonoBehaviour
{
    [Header("Controllers")]
    public FanSerialController fanController;
    public RainController rainController;

    [Header("Debug")]
    public bool autoRestartDictation = true;
    public bool enableDebugLog = true;

    [Header("Default Wind PWM")]
    public int startPwm = 200;
    public int stopPwm = 0;
    public int lightWindPwm = 120;
    public int strongWindPwm = 200;
    public int maxWindPwm = 255;

    [Header("Rain-Wind Link")]
    public bool enableRainWindLink = true;

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
        dictationRecognizer.InitialSilenceTimeoutSeconds = 1f;
        dictationRecognizer.AutoSilenceTimeoutSeconds = 0.8f;

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

        string normalized = NormalizeText(text);
        DebugLog("Normalized: " + normalized);

        // ===== 雨控制（带联动） =====
        if (rainController != null)
        {
            if (normalized.Contains("晴") || normalized.Contains("雨停") || normalized.Contains("不要下雨"))
            {
                rainController.SetRainOff();
                DebugLog("Command: Rain OFF");

                if (enableRainWindLink)
                {
                    fanController.SetPwm(0);
                    DebugLog("Rain-Wind Link: Wind OFF");
                }
                return;
            }

            if (normalized.Contains("暴雨") || normalized.Contains("大暴雨"))
            {
                rainController.SetRainVeryHeavy();
                DebugLog("Command: Rain VERY HEAVY");

                if (enableRainWindLink)
                {
                    fanController.SetPwm(maxWindPwm);
                    DebugLog("Rain-Wind Link: Wind MAX");
                }
                return;
            }

            if (normalized.Contains("大雨"))
            {
                rainController.SetRainHeavy();
                DebugLog("Command: Rain HEAVY");

                if (enableRainWindLink)
                {
                    fanController.SetPwm(strongWindPwm);
                    DebugLog("Rain-Wind Link: Wind STRONG");
                }
                return;
            }

            if (normalized.Contains("中雨") || normalized.Contains("下雨"))
            {
                rainController.SetRainMedium();
                DebugLog("Command: Rain MEDIUM");

                if (enableRainWindLink)
                {
                    fanController.SetPwm(startPwm);
                    DebugLog("Rain-Wind Link: Wind START");
                }
                return;
            }

            if (normalized.Contains("小雨"))
            {
                rainController.SetRainLight();
                DebugLog("Command: Rain LIGHT");

                if (enableRainWindLink)
                {
                    fanController.SetPwm(lightWindPwm);
                    DebugLog("Rain-Wind Link: Wind LIGHT");
                }
                return;
            }
        }

        // ===== 单独风控制（优先级放在雨控制后面，这样你仍然可以单独改风） =====
        if (normalized.Contains("停") || normalized.Contains("风停下来") || normalized.Contains("不刮风"))
        {
            fanController.SetPwm(0);
            DebugLog("Command: Wind STOP");
            return;
        }

        if (normalized.Contains("最大风") || normalized.Contains("最强风") || normalized.Contains("风最大"))
        {
            fanController.SetPwm(maxWindPwm);
            DebugLog("Command: Wind MAX");
            return;
        }

        if (normalized.Contains("大风") || normalized.Contains("强风") || normalized.Contains("风大一点") || normalized.Contains("风强一点"))
        {
            fanController.SetPwm(strongWindPwm);
            DebugLog("Command: Wind STRONG");
            return;
        }

        if (normalized.Contains("小风") || normalized.Contains("微风") || normalized.Contains("风小一点"))
        {
            fanController.SetPwm(lightWindPwm);
            DebugLog("Command: Wind LIGHT");
            return;
        }

        if (normalized.Contains("开始吹风") || normalized.Contains("开风") || normalized.Contains("吹风"))
        {
            fanController.SetPwm(startPwm);
            DebugLog("Command: Wind START");
            return;
        }

        DebugLog("没有匹配到命令");
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
            .Replace("?", "")
            .Replace("？", "")
            .Replace("\n", "")
            .Replace("\r", "");
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        DebugLog("Dictation complete: " + cause);

        if (!autoRestartDictation) return;
        if (!gameObject.activeInHierarchy) return;
        if (isRestarting) return;

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