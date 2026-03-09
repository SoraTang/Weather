using UnityEngine;
using UnityEngine.Windows.Speech;

public class DictationTest : MonoBehaviour
{
    private DictationRecognizer dictationRecognizer;

    void Start()
    {
        dictationRecognizer = new DictationRecognizer();

        dictationRecognizer.InitialSilenceTimeoutSeconds = 5f;
        dictationRecognizer.AutoSilenceTimeoutSeconds = 5f;

        dictationRecognizer.DictationHypothesis += OnHypothesis;
        dictationRecognizer.DictationResult += OnResult;
        dictationRecognizer.DictationComplete += OnComplete;
        dictationRecognizer.DictationError += OnError;

        dictationRecognizer.Start();

        Debug.Log("Dictation started.");
    }

    private void OnHypothesis(string text)
    {
        Debug.Log("Hypothesis: " + text);
    }

    private void OnResult(string text, ConfidenceLevel confidence)
    {
        Debug.Log("Result: " + text + " | Confidence: " + confidence);
    }

    private void OnComplete(DictationCompletionCause cause)
    {
        Debug.Log("Dictation complete: " + cause);

        // 为了方便连续测试，结束后自动重启
        if (dictationRecognizer != null)
        {
            dictationRecognizer.Stop();
            dictationRecognizer.Start();
        }
    }

    private void OnError(string error, int hresult)
    {
        Debug.LogError("Dictation error: " + error + " | HResult: " + hresult);
    }

    void OnDestroy()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.DictationHypothesis -= OnHypothesis;
            dictationRecognizer.DictationResult -= OnResult;
            dictationRecognizer.DictationComplete -= OnComplete;
            dictationRecognizer.DictationError -= OnError;

            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                dictationRecognizer.Stop();
            }

            dictationRecognizer.Dispose();
        }
    }
}