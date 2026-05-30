using System;
using UnityEngine;

public class CarTrafficUnit : MonoBehaviour
{
    [Header("Car Settings")]
    public float travelDuration = 4.0f;
    public float arriveThreshold = 0.01f;
    public bool destroyOnArrive = true;

    private Transform endPoint;
    private Vector3 startPoint;
    private float timer = 0f;
    private bool initialized = false;
    private bool arrived = false;

    public Action onArrived;

    public void Initialize(Transform targetEndPoint)
    {
        endPoint = targetEndPoint;
        startPoint = transform.position;
        timer = 0f;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || arrived || endPoint == null) return;

        if (travelDuration <= 0.01f)
        {
            transform.position = endPoint.position;
            Arrive();
            return;
        }

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / travelDuration);

        transform.position = Vector3.Lerp(startPoint, endPoint.position, t);

        if (t >= 1f || Vector3.Distance(transform.position, endPoint.position) <= arriveThreshold)
        {
            Arrive();
        }
    }

    private void Arrive()
    {
        if (arrived) return;
        arrived = true;

        onArrived?.Invoke();

        if (destroyOnArrive)
        {
            Destroy(gameObject);
        }
    }
}