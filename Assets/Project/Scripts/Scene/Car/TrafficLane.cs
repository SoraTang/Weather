using UnityEngine;

[System.Serializable]
public class TrafficLane
{
    public string laneName;
    public Transform startPoint;
    public Transform endPoint;
    public bool reverseModelY180 = false;
}