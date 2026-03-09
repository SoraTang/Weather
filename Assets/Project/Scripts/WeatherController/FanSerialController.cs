using UnityEngine;
using SerialPortUtility;

public class FanSerialController : MonoBehaviour
{
    [Header("Serial")]
    public SerialPortUtilityPro serialPort;

    [Header("PWM Settings")]
    public int minPwm = 0;
    public int maxPwm = 255;
    public int minSendDelta = 2;

    [Header("Safety")]
    public bool stopFanWhenDisable = true;
    public bool closePortWhenDisable = true;

    private int lastValue = -999;
    private bool isShuttingDown = false;

    void Start()
    {
        if (serialPort == null)
        {
            serialPort = GetComponent<SerialPortUtilityPro>();
        }

        if (serialPort == null)
        {
            Debug.LogError("FanSerialController: SerialPortUtilityPro not found.");
        }
    }

    public void SetPwm(int pwm)
    {
        if (isShuttingDown) return;

        pwm = Mathf.Clamp(pwm, minPwm, maxPwm);

        if (Mathf.Abs(pwm - lastValue) < minSendDelta)
            return;

        lastValue = pwm;

        SendPwmInternal(pwm);
    }

    private void SendPwmInternal(int pwm)
    {
        if (serialPort == null)
        {
            Debug.LogWarning("Serial port component missing.");
            return;
        }

        if (!serialPort.IsOpened())
        {
            Debug.LogWarning("Serial port is not opened.");
            return;
        }

        try
        {
            serialPort.WriteCRLF(pwm.ToString());
            Debug.Log("Send PWM: " + pwm);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Serial write failed: " + e.Message);
        }
    }

    private void SafeShutdown()
    {
        if (isShuttingDown) return;
        isShuttingDown = true;

        if (serialPort != null && serialPort.IsOpened())
        {
            try
            {
                if (stopFanWhenDisable)
                {
                    serialPort.WriteCRLF("0");
                    Debug.Log("Send PWM: 0 (shutdown)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Shutdown PWM failed: " + e.Message);
            }

            try
            {
                if (closePortWhenDisable)
                {
                    serialPort.Close();
                    Debug.Log("Serial port closed safely.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Serial close failed: " + e.Message);
            }
        }
    }

    void OnDisable()
    {
        SafeShutdown();
    }

    void OnDestroy()
    {
        SafeShutdown();
    }

    void OnApplicationQuit()
    {
        SafeShutdown();
    }
}