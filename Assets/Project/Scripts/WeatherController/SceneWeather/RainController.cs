using UnityEngine;

public class RainController : MonoBehaviour
{
    public GameObject rainLight;
    public GameObject rainMedium;
    public GameObject rainHeavy;
    public GameObject rainVeryHeavy;

    void Start()
    {
        SetRainOff();
    }

    void DisableAll()
    {
        rainLight.SetActive(false);
        rainMedium.SetActive(false);
        rainHeavy.SetActive(false);
        rainVeryHeavy.SetActive(false);
    }

    public void SetRainOff()
    {
        DisableAll();
        Debug.Log("Rain OFF");
    }

    public void SetRainLight()
    {
        DisableAll();
        rainLight.SetActive(true);
        Debug.Log("Rain Light");
    }

    public void SetRainMedium()
    {
        DisableAll();
        rainMedium.SetActive(true);
        Debug.Log("Rain Medium");
    }

    public void SetRainHeavy()
    {
        DisableAll();
        rainHeavy.SetActive(true);
        Debug.Log("Rain Heavy");
    }

    public void SetRainVeryHeavy()
    {
        DisableAll();
        rainVeryHeavy.SetActive(true);
        Debug.Log("Rain Very Heavy");
    }
}