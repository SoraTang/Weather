using UnityEngine;

[System.Serializable]
public class CarSpawnEntry
{
    public string carName;
    public GameObject prefab;
    public float minInterval = 6f;
    public float maxInterval = 14f;

    [HideInInspector] public bool isRunning = false;
}