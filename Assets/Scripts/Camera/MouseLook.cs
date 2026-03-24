using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform playerBody;   // 控制左右转（通常是父物体）

    float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;  // 锁定鼠标
        Cursor.visible = false;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 上下视角（限制角度）
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 左右视角（转身体）
        playerBody.Rotate(Vector3.up * mouseX);
    }
}