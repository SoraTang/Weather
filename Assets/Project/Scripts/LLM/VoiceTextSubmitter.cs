using TMPro;
using UnityEngine;

public class VoiceTextSubmitter : MonoBehaviour
{
    public TMP_InputField inputField;

    private SimpleSceneClassifier classifier;
    private MemorySceneRouter router;

    void Awake()
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
    }

    public void Submit()
    {
        if (inputField == null)
        {
            Debug.LogError("没有绑定 Input Field。");
            return;
        }

        string text = inputField.text;

        Debug.Log("模拟语音识别文本：" + text);

        MemorySceneType result = classifier.Classify(text);

        Debug.Log("分类结果：" + result);

        router.LoadSceneByType(result);
    }
}