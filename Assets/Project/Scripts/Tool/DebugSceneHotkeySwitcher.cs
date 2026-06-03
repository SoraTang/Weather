using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugSceneHotkeySwitcher : MonoBehaviour
{
    [Header("Enable")]
    public bool enableHotkeys = true;

    [Header("Scene Names")]
    public string transitionSceneName = "TestScene";
    public string classroomSceneName = "ClassroomScene";
    public string carBackseatSceneName = "CarBackseatScene";
    public string convenienceStoreSceneName = "ConvenienceStoreScene";
    public string bedroomSceneName = "BedroomScene";

    [Header("Hotkeys")]
    public KeyCode transitionKey = KeyCode.Alpha0;
    public KeyCode classroomKey = KeyCode.Alpha1;
    public KeyCode carBackseatKey = KeyCode.Alpha2;
    public KeyCode convenienceStoreKey = KeyCode.Alpha3;
    public KeyCode bedroomKey = KeyCode.Alpha4;

    [Header("Options")]
    [Tooltip("切场景前是否停止当前场景中的所有 AudioSource，避免残留声音。")]
    public bool stopAllAudioBeforeLoad = true;

    [Tooltip("是否在 Console 输出调试信息。")]
    public bool enableDebugLog = true;

    void Update()
    {
        if (!enableHotkeys)
        {
            return;
        }

        if (Input.GetKeyDown(transitionKey))
        {
            LoadScene(transitionSceneName, "中转场景");
        }

        if (Input.GetKeyDown(classroomKey))
        {
            LoadScene(classroomSceneName, "教室场景");
        }

        if (Input.GetKeyDown(carBackseatKey))
        {
            LoadScene(carBackseatSceneName, "车后座场景");
        }

        if (Input.GetKeyDown(convenienceStoreKey))
        {
            LoadScene(convenienceStoreSceneName, "便利店场景");
        }

        if (Input.GetKeyDown(bedroomKey))
        {
            LoadScene(bedroomSceneName, "卧室场景");
        }
    }

    private void LoadScene(string sceneName, string label)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("目标场景名为空，无法切换。");
            return;
        }

        if (stopAllAudioBeforeLoad)
        {
            StopAllAudioSources();
        }

        DebugLog("后门切换：" + label + " -> " + sceneName);

        SceneManager.LoadScene(sceneName);
    }

    private void StopAllAudioSources()
    {
        AudioSource[] audioSources = FindObjectsOfType<AudioSource>();

        foreach (AudioSource source in audioSources)
        {
            if (source != null)
            {
                source.Stop();
            }
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log(message);
        }
    }
}