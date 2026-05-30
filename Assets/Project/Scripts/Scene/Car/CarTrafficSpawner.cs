using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarTrafficSpawner : MonoBehaviour
{
    [Header("Car Spawn Entries")]
    public CarSpawnEntry[] carEntries;

    [Header("Traffic Lanes")]
    public TrafficLane[] lanes;

    [Header("Global Settings")]
    public bool autoLoop = true;
    public bool startWithRandomDelay = true;

    [Header("Audio Link")]
    public ConvenienceStoreAutoDoor door;

    // 记录每条车道当前是否有车
    private Dictionary<int, GameObject> laneOccupiedCars = new Dictionary<int, GameObject>();

    private void Start()
    {
        if (!autoLoop) return;

        // 初始化每条车道占用表
        for (int i = 0; i < lanes.Length; i++)
        {
            laneOccupiedCars[i] = null;
        }

        // 每种车各自启动一个独立循环
        for (int i = 0; i < carEntries.Length; i++)
        {
            if (carEntries[i] != null && carEntries[i].prefab != null)
            {
                StartCoroutine(SpawnLoopForCar(carEntries[i]));
            }
        }
    }

    private IEnumerator SpawnLoopForCar(CarSpawnEntry entry)
    {
        if (entry == null || entry.prefab == null)
            yield break;

        entry.isRunning = true;

        if (startWithRandomDelay)
        {
            yield return new WaitForSeconds(Random.Range(entry.minInterval, entry.maxInterval));
        }

        while (autoLoop)
        {
            TrySpawnSpecificCar(entry);

            float waitTime = Random.Range(entry.minInterval, entry.maxInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void TrySpawnSpecificCar(CarSpawnEntry entry)
    {
        if (lanes == null || lanes.Length == 0)
        {
            Debug.LogWarning("CarTrafficSpawner: 没有设置 lanes。");
            return;
        }

        // 收集当前空闲车道
        List<int> availableLaneIndices = new List<int>();

        for (int i = 0; i < lanes.Length; i++)
        {
            if (lanes[i] == null || lanes[i].startPoint == null || lanes[i].endPoint == null)
                continue;

            if (laneOccupiedCars[i] == null)
            {
                availableLaneIndices.Add(i);
            }
        }

        if (availableLaneIndices.Count == 0)
        {
            // 所有车道都占用时，本轮跳过
            return;
        }

        int selectedLaneIndex = availableLaneIndices[Random.Range(0, availableLaneIndices.Count)];
        TrafficLane selectedLane = lanes[selectedLaneIndex];

        Vector3 spawnPos = selectedLane.startPoint.position;
        Quaternion spawnRot = selectedLane.startPoint.rotation;

        // 如果是反向车道，模型额外绕Y旋转180度
        if (selectedLane.reverseModelY180)
        {
            spawnRot = spawnRot * Quaternion.Euler(0f, 180f, 0f);
        }

        GameObject newCar = Instantiate(entry.prefab, spawnPos, spawnRot);

        // 标记该车道已占用
        laneOccupiedCars[selectedLaneIndex] = newCar;

        // 初始化移动车辆
        CarTrafficUnit unit = newCar.GetComponent<CarTrafficUnit>();
        if (unit != null)
        {
            unit.Initialize(selectedLane.endPoint);
            unit.onArrived += () => OnCarArrived(selectedLaneIndex, newCar);
        }
        else
        {
            Debug.LogWarning($"CarTrafficSpawner: {entry.carName} prefab 没有 CarTrafficUnit。");
        }

        // 绑定门音量联动
        DoorReactiveCarAudio carAudio = newCar.GetComponentInChildren<DoorReactiveCarAudio>();
        if (carAudio != null)
        {
            carAudio.door = door;
        }
    }

    private void OnCarArrived(int laneIndex, GameObject arrivedCar)
    {
        if (laneOccupiedCars.ContainsKey(laneIndex) && laneOccupiedCars[laneIndex] == arrivedCar)
        {
            laneOccupiedCars[laneIndex] = null;
        }
    }
}