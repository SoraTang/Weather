using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class LLMClassifyRequest
{
    public string text;
}

[System.Serializable]
public class LLMClassifyResult
{
    public string scene;
    public float confidence;
    public string[] keywords;
    public string reason;
}

public class LLMSceneClassifier : MonoBehaviour
{
    [Header("LLM Server")]
    public string classifyUrl = "http://localhost:3000/classify";

    [Header("Debug")]
    public bool enableDebugLog = true;

    public void Classify(string inputText, System.Action<MemorySceneType, LLMClassifyResult> onSuccess, System.Action<string> onError = null)
    {
        StartCoroutine(ClassifyRoutine(inputText, onSuccess, onError));
    }

    private IEnumerator ClassifyRoutine(string inputText, System.Action<MemorySceneType, LLMClassifyResult> onSuccess, System.Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            string msg = "LLM 输入文本为空。";
            DebugLog(msg);
            onError?.Invoke(msg);
            yield break;
        }

        LLMClassifyRequest requestData = new LLMClassifyRequest
        {
            text = inputText
        };

        string jsonBody = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(classifyUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            DebugLog("发送给 LLM 的文本：" + inputText);
            DebugLog("请求地址：" + classifyUrl);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = "LLM 请求失败：" + request.error;
                Debug.LogError(error);
                onError?.Invoke(error);
                yield break;
            }

            string responseText = request.downloadHandler.text;
            DebugLog("LLM 返回：" + responseText);

            LLMClassifyResult result = null;

            try
            {
                result = JsonUtility.FromJson<LLMClassifyResult>(responseText);
            }
            catch (System.Exception e)
            {
                string error = "解析 LLM JSON 失败：" + e.Message;
                Debug.LogError(error);
                onError?.Invoke(error);
                yield break;
            }

            MemorySceneType sceneType = ConvertSceneId(result.scene);

            DebugLog("LLM 场景结果：" + result.scene);
            DebugLog("转换为 Unity 场景类型：" + sceneType);
            DebugLog("置信度：" + result.confidence);

            onSuccess?.Invoke(sceneType, result);
        }
    }

    private MemorySceneType ConvertSceneId(string sceneId)
    {
        switch (sceneId)
        {
            case "classroom":
                return MemorySceneType.Classroom;

            case "car_backseat":
                return MemorySceneType.CarBackseat;

            case "convenience":
                return MemorySceneType.ConvenienceStore;

            case "bedroom":
                return MemorySceneType.Bedroom;

            case "fallback":
                return MemorySceneType.Fallback;

            default:
                Debug.LogWarning("未知 LLM sceneId：" + sceneId + "，使用 Fallback。");
                return MemorySceneType.Fallback;
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