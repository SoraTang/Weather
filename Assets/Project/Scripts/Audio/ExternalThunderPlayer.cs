using UnityEngine;
using NAudio.Wave;
using System.IO;

public class ExternalThunderPlayer : MonoBehaviour
{
    [Header("外部输出设备关键词")]
    [Tooltip("例如 Spark / MINI / Positive Grid。启动时会在输出设备列表中查找包含该关键词的设备。")]
    public string outputDeviceKeyword = "Spark";

    [Header("雷声音频文件")]
    [Tooltip("文件需放在 Assets/StreamingAssets/Audio/ 下")]
    public string thunderFileName = "thunder.wav";

    [Header("启动设置")]
    public bool playOnStart = true;

    [Tooltip("延迟几秒后播放，避免设备刚初始化时没出声。")]
    public float startDelay = 0.5f;

    [Header("音量")]
    [Range(0f, 1f)]
    public float volume = 1.0f;

    private int outputDeviceIndex = -1;
    private WaveOutEvent outputDevice;
    private AudioFileReader audioFile;

    private void Start()
    {
        PrintOutputDevices();

        outputDeviceIndex = FindOutputDevice(outputDeviceKeyword);

        if (outputDeviceIndex < 0)
        {
            Debug.LogError($"[ExternalThunderPlayer] 没有找到包含关键词 [{outputDeviceKeyword}] 的音频输出设备。");
            return;
        }

        outputDevice = new WaveOutEvent
        {
            DeviceNumber = outputDeviceIndex,
            DesiredLatency = 100
        };

        Debug.Log($"[ExternalThunderPlayer] 外部雷声输出设备设置完成，设备编号：{outputDeviceIndex}");

        if (playOnStart)
        {
            Invoke(nameof(PlayThunder), startDelay);
        }
    }

    public void PlayThunder()
    {
        if (outputDevice == null)
        {
            Debug.LogWarning("[ExternalThunderPlayer] 输出设备未初始化，无法播放雷声。");
            return;
        }

        StopThunder();

        string path = Path.Combine(Application.streamingAssetsPath, "Audio", thunderFileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[ExternalThunderPlayer] 找不到雷声音频文件：{path}");
            return;
        }

        audioFile = new AudioFileReader(path)
        {
            Volume = volume
        };

        outputDevice.Init(audioFile);
        outputDevice.Play();

        Debug.Log("[ExternalThunderPlayer] 播放外部雷声：" + thunderFileName);
    }

    public void StopThunder()
    {
        if (outputDevice != null)
        {
            outputDevice.Stop();
        }

        if (audioFile != null)
        {
            audioFile.Dispose();
            audioFile = null;
        }
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (audioFile != null)
        {
            audioFile.Volume = volume;
        }
    }

    private int FindOutputDevice(string keyword)
    {
        keyword = keyword.ToLower();

        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            WaveOutCapabilities caps = WaveOut.GetCapabilities(i);
            string deviceName = caps.ProductName;

            if (deviceName.ToLower().Contains(keyword))
            {
                Debug.Log($"[ExternalThunderPlayer] 匹配到输出设备：{i} - {deviceName}");
                return i;
            }
        }

        return -1;
    }

    private void PrintOutputDevices()
    {
        Debug.Log("========== 当前可用音频输出设备 ==========");

        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            WaveOutCapabilities caps = WaveOut.GetCapabilities(i);
            Debug.Log($"Audio Output Device {i}: {caps.ProductName}");
        }

        Debug.Log("=========================================");
    }

    private void OnDestroy()
    {
        CancelInvoke();
        StopThunder();

        if (outputDevice != null)
        {
            outputDevice.Dispose();
            outputDevice = null;
        }
    }
}