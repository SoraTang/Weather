using UnityEngine;

public class SceneClassifierTester : MonoBehaviour
{
    [TextArea(3, 6)]
    public string testInput;

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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestClassifyOnly();
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            TestClassifyAndLoadScene();
        }
    }

    public void TestClassifyOnly()
    {
        MemorySceneType result = classifier.Classify(testInput);
        Debug.Log("输入内容：" + testInput);
        Debug.Log("分类结果：" + result);
    }

    public void TestClassifyAndLoadScene()
    {
        MemorySceneType result = classifier.Classify(testInput);
        Debug.Log("输入内容：" + testInput);
        Debug.Log("分类结果：" + result);
        router.LoadSceneByType(result);
    }
}