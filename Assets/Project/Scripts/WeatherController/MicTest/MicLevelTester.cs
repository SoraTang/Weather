using UnityEngine;
using UnityEngine.UI;

public class MicLevelTester : MonoBehaviour
{
    public Slider levelSlider;      // 可选：拖一个 UI Slider 进来观察
    public Text levelText;          // 可选：老版 UI Text

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
        Debug.Log("Mic Level: " + level);

        if (levelSlider != null)
            levelSlider.value = level;

        string msg = "Mic Level: " + level.ToString("F3");

        if (levelText != null)
            levelText.text = msg;

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
}