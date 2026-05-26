using UnityEngine;
using UnityEngine.SceneManagement;

public class MemorySceneRouter : MonoBehaviour
{
    [Header("Scene Names")]
    public string transitionSceneName = "TransitionScene";
    public string classroomSceneName = "ClassroomScene";
    public string carBackseatSceneName = "CarBackseatScene";
    public string convenienceStoreSceneName = "ConvenienceStoreScene";
    public string bedroomSceneName = "BedroomScene";

    public void LoadSceneByType(MemorySceneType sceneType)
    {
        string targetSceneName = GetSceneName(sceneType);

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("没有找到对应场景，返回中转场景。");
            targetSceneName = transitionSceneName;
        }

        Debug.Log("准备切换到场景：" + targetSceneName);
        SceneManager.LoadScene(targetSceneName);
    }

    private string GetSceneName(MemorySceneType sceneType)
    {
        switch (sceneType)
        {
            case MemorySceneType.Classroom:
                return classroomSceneName;

            case MemorySceneType.CarBackseat:
                return carBackseatSceneName;

            case MemorySceneType.ConvenienceStore:
                return convenienceStoreSceneName;

            case MemorySceneType.Bedroom:
                return bedroomSceneName;

            case MemorySceneType.Fallback:
                return convenienceStoreSceneName;

            default:
                return transitionSceneName;
        }
    }
}