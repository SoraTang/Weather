using UnityEngine;
using SerialPortUtility;

public class ThreeFanSerialController : MonoBehaviour
{
    [Header("Serial")]
    public SerialPortUtilityPro serialPort;

    [Header("PWM Values 0-255")]
    [Range(0, 255)] public int leftFan = 0;
    [Range(0, 255)] public int centerFan = 0;
    [Range(0, 255)] public int rightFan = 0;

    [Header("Options")]
    public bool sendOnStart = true;
    public bool enableDebugLog = false;

    private int lastLeft = -1;
    private int lastCenter = -1;
    private int lastRight = -1;

    private void Start()
    {
        if (serialPort == null)
        {
            serialPort = GetComponent<SerialPortUtilityPro>();
        }

        if (sendOnStart)
        {
            SendFanValues();
        }
    }

    private void OnValidate()
    {
        leftFan = Mathf.Clamp(leftFan, 0, 255);
        centerFan = Mathf.Clamp(centerFan, 0, 255);
        rightFan = Mathf.Clamp(rightFan, 0, 255);
    }

    public void SetFans(int left, int center, int right)
    {
        leftFan = Mathf.Clamp(left, 0, 255);
        centerFan = Mathf.Clamp(center, 0, 255);
        rightFan = Mathf.Clamp(right, 0, 255);

        SendFanValues();
    }

    public void SendFanValues()
    {
        if (serialPort == null)
        {
            Debug.LogWarning("SerialPortUtilityPro is not assigned.");
            return;
        }

        if (!serialPort.IsOpened())
        {
            Debug.LogWarning("Serial port is not opened.");
            return;
        }

        if (leftFan == lastLeft && centerFan == lastCenter && rightFan == lastRight)
        {
            return;
        }

        string message = $"{leftFan},{centerFan},{rightFan}";
        bool success = serialPort.WriteCRLF(message);

        if (success)
        {
            lastLeft = leftFan;
            lastCenter = centerFan;
            lastRight = rightFan;
            DebugLog("Send Fans: " + message);
        }
        else
        {
            Debug.LogWarning("Failed to send fan values.");
        }
    }

    public void StopAllFans()
    {
        SetFans(0, 0, 0);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void DebugLog(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log(message);
        }
    }
}