using UnityEngine;
using UnityEngine.UI;

public class MicVoiceActivityTester : MonoBehaviour
{
    public Slider levelSlider;
    public Text statusText;
    public TMPro.TMP_Text tmpStatusText;

    public float threshold = 0.05f;

    private AudioClip micClip;
    private string micDevice;
    private int sampleWindow = 128;

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("没有检测到麦克风");
            return;
        }

        micDevice = Microphone.devices[0];
        Debug.Log("使用麦克风: " + micDevice);

        micClip = Microphone.Start(micDevice, true, 10, 44100);
    }

    void Update()
    {
        if (micClip == null) return;

        float level = GetMicLevel();

        if (levelSlider != null)
            levelSlider.value = level;

        bool isSpeaking = level > threshold;

        if (isSpeaking)
        {
            Debug.Log("检测到说话");
            SetStatus("检测到说话");
        }
        else
        {
            SetStatus("安静");
        }
    }

    float GetMicLevel()
    {
        int micPos = Microphone.GetPosition(micDevice) - sampleWindow;
        if (micPos < 0) return 0f;

        float[] waveData = new float[sampleWindow];
        micClip.GetData(waveData, micPos);

        float maxLevel = 0f;
        for (int i = 0; i < sampleWindow; i++)
        {
            float abs = Mathf.Abs(waveData[i]);
            if (abs > maxLevel)
                maxLevel = abs;
        }

        return maxLevel;
    }

    void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;

        if (tmpStatusText != null)
            tmpStatusText.text = msg;
    }
}