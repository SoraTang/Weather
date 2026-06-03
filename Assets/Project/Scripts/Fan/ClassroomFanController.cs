using UnityEngine;

public class ClassroomFanController : MonoBehaviour
{
    [Header("Fan Controller")]
    public ThreeFanSerialController fanController;

    [Header("Classroom Fan Settings")]
    [Range(0, 255)] public int leftMin = 120;
    [Range(0, 255)] public int leftMax = 255;
    [Range(0, 255)] public int centerValue = 255;
    [Range(0, 255)] public int rightValue = 0;

    [Header("Natural Wind")]
    [Tooltip("左风机自然浮动速度，数值越大变化越快")]
    public float windChangeSpeed = 0.25f;

    [Tooltip("发送间隔，避免每帧都向ESP32发送数据")]
    public float sendInterval = 0.15f;

    [Tooltip("离开场景或脚本关闭时是否关闭风机")]
    public bool stopFansOnDisable = true;

    private float noiseSeed;
    private float sendTimer;
    private int lastLeft = -1;
    private int lastCenter = -1;
    private int lastRight = -1;

    private void Start()
    {
        if (fanController == null)
        {
            fanController = FindObjectOfType<ThreeFanSerialController>();
        }

        noiseSeed = Random.Range(0f, 1000f);

        SendFans(leftMin, centerValue, rightValue);
    }

    private void Update()
    {
        if (fanController == null) return;

        sendTimer += Time.deltaTime;
        if (sendTimer < sendInterval) return;
        sendTimer = 0f;

        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * windChangeSpeed);
        int leftValue = Mathf.RoundToInt(Mathf.Lerp(leftMin, leftMax, noise));

        SendFans(leftValue, centerValue, rightValue);
    }

    private void SendFans(int left, int center, int right)
    {
        left = Mathf.Clamp(left, 0, 255);
        center = Mathf.Clamp(center, 0, 255);
        right = Mathf.Clamp(right, 0, 255);

        if (left == lastLeft && center == lastCenter && right == lastRight)
        {
            return;
        }

        fanController.SetFans(left, center, right);

        lastLeft = left;
        lastCenter = center;
        lastRight = right;
    }

    private void OnDisable()
    {
        if (!stopFansOnDisable) return;

        if (fanController != null)
        {
            fanController.SetFans(0, 0, 0);
        }
    }
}