using UnityEngine;
using UnityEngine.UI;
using SerialPortUtility;

public class FanSerialController : MonoBehaviour
{
    public SerialPortUtilityPro serialPort;
    public Slider pwmSlider;

    private int lastValue = -999;

    void Start()
    {
        if (serialPort == null)
        {
            serialPort = GetComponent<SerialPortUtilityPro>();
        }

        if (pwmSlider != null)
        {
            pwmSlider.minValue = 0;
            pwmSlider.maxValue = 255;
            pwmSlider.wholeNumbers = true;
            pwmSlider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    void OnSliderChanged(float value)
    {
        SetPwm(Mathf.RoundToInt(value));
    }

    public void SetPwm(int pwm)
    {
        pwm = Mathf.Clamp(pwm, 0, 255);

        if (Mathf.Abs(pwm - lastValue) < 2) return;
        lastValue = pwm;

        if (pwmSlider != null && Mathf.RoundToInt(pwmSlider.value) != pwm)
        {
            pwmSlider.SetValueWithoutNotify(pwm);
        }

        if (serialPort != null && serialPort.IsOpened())
        {
            serialPort.WriteCRLF(pwm.ToString());
            Debug.Log("Send PWM: " + pwm);
        }
        else
        {
            Debug.LogWarning("Serial port is not opened.");
        }
    }
}