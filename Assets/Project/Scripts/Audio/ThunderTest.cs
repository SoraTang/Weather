using UnityEngine;

public class ThunderTest : MonoBehaviour
{
    public ExternalThunderPlayer externalThunderPlayer;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            externalThunderPlayer.PlayThunder();
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            externalThunderPlayer.StopThunder();
        }
    }
}