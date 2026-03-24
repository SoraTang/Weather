using UnityEngine;

public class CeilingFanController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("整体晃动的支点。通常就是挂这个脚本的物体；可留空，默认用当前 transform")]
    public Transform swayPivot;

    [Tooltip("风扇整体模型根节点。若留空，则默认不额外处理位置")]
    public Transform fanBody;

    [Tooltip("扇叶旋转中心")]
    public Transform bladesPivot;

    [Header("Blade Rotation")]
    [Tooltip("扇叶绕本地 Z 轴旋转速度（度/秒）")]
    public float bladeSpeed = 300f;

    [Tooltip("是否随机一个初始角度")]
    public bool randomStartAngle = true;

    [Tooltip("是否反向旋转")]
    public bool reverseBladeDirection = false;

    [Header("Whole Fan Sway")]
    [Tooltip("是否启用整体轻微晃动")]
    public bool enableSway = true;

    [Tooltip("X方向最大晃动角度（度）")]
    public float swayAngleX = 0.8f;

    [Tooltip("Z方向最大晃动角度（度）")]
    public float swayAngleZ = 1.0f;

    [Tooltip("晃动速度")]
    public float swaySpeed = 0.8f;

    [Tooltip("X/Z晃动不同步的相位偏移")]
    public float swayPhaseOffset = 1.37f;

    [Header("Randomness")]
    [Tooltip("给每个风扇一个随机相位，避免多个风扇一模一样")]
    public bool randomizeSeed = true;

    private Quaternion _basePivotLocalRotation;
    private float _seed;

    void Start()
    {
        if (swayPivot == null)
            swayPivot = transform;

        _basePivotLocalRotation = swayPivot.localRotation;

        _seed = randomizeSeed ? Random.Range(0f, 100f) : 0f;

        if (bladesPivot != null && randomStartAngle)
        {
            Vector3 euler = bladesPivot.localEulerAngles;
            euler.z = Random.Range(0f, 360f);
            bladesPivot.localEulerAngles = euler;
        }
    }

    void Update()
    {
        RotateBlades();
        ApplySway();
    }

    private void RotateBlades()
    {
        if (bladesPivot == null) return;

        float direction = reverseBladeDirection ? -1f : 1f;

        // 绕本地 Z 轴旋转
        bladesPivot.Rotate(0f, 0f, direction * bladeSpeed * Time.deltaTime, Space.Self);
    }

    private void ApplySway()
    {
        if (!enableSway || swayPivot == null) return;

        float t = Time.time * swaySpeed + _seed;

        // 两组不同频率的波叠加，让晃动别太机械
        float swayX =
            Mathf.Sin(t) * swayAngleX +
            Mathf.Sin(t * 0.53f + swayPhaseOffset) * swayAngleX * 0.35f;

        float swayZ =
            Mathf.Cos(t * 0.91f + 0.7f) * swayAngleZ +
            Mathf.Sin(t * 0.47f + swayPhaseOffset * 0.8f) * swayAngleZ * 0.25f;

        swayPivot.localRotation = _basePivotLocalRotation * Quaternion.Euler(swayX, 0f, swayZ);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bladeSpeed < 0f) bladeSpeed = 0f;
        if (swaySpeed < 0f) swaySpeed = 0f;
        if (swayAngleX < 0f) swayAngleX = 0f;
        if (swayAngleZ < 0f) swayAngleZ = 0f;
    }
#endif
}