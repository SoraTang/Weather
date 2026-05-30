using System.Collections;
using UnityEngine;

public class CarTrafficSpawner : MonoBehaviour
{
    [Header("Car Prefabs")]
    public GameObject[] carPrefabs;

    [Header("Traffic Lanes")]
    public TrafficLane[] lanes;

    [Header("Timing")]
    public bool autoLoop = true;
    public bool startWithRandomDelay = true;
    public float minInterval = 6f;
    public float maxInterval = 14f;

    [Header("Audio Link")]
    public ConvenienceStoreAutoDoor door;

    private void Start()
    {
        if (autoLoop)
        {
            StartCoroutine(SpawnLoop());
        }
    }

    private IEnumerator SpawnLoop()
    {
        if (startWithRandomDelay)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }

        while (autoLoop)
        {
            SpawnRandomCar();

            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    public void SpawnRandomCar()
    {
        if (carPrefabs == null || carPrefabs.Length == 0)
        {
            Debug.LogWarning("CarTrafficSpawner: 没有设置 carPrefabs。");
            return;
        }

        if (lanes == null || lanes.Length == 0)
        {
            Debug.LogWarning("CarTrafficSpawner: 没有设置 lanes。");
            return;
        }

        GameObject selectedPrefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
        TrafficLane selectedLane = lanes[Random.Range(0, lanes.Length)];

        if (selectedLane.startPoint == null || selectedLane.endPoint == null)
        {
            Debug.LogWarning($"CarTrafficSpawner: 车道 {selectedLane.laneName} 的起点或终点未设置。");
            return;
        }

        Vector3 spawnPos = selectedLane.startPoint.position;
        Quaternion spawnRot = selectedLane.startPoint.rotation;

        GameObject newCar = Instantiate(selectedPrefab, spawnPos, spawnRot);

        CarTrafficUnit unit = newCar.GetComponent<CarTrafficUnit>();
        if (unit != null)
        {
            unit.Initialize(selectedLane.endPoint);
        }
        else
        {
            Debug.LogWarning("CarTrafficSpawner: 生成的车 prefab 没有 CarTrafficUnit。");
        }

        DoorReactiveCarAudio carAudio = newCar.GetComponentInChildren<DoorReactiveCarAudio>();
        if (carAudio != null)
        {
            carAudio.door = door;
        }
    }
}