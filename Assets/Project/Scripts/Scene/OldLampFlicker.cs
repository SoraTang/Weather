using UnityEngine;

public class OldTubeLampFlicker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("把这一根灯对应的 3 个 Spot Light 都拖进来")]
    public Light[] spotLights;

    [Tooltip("灯管模型的 Renderer（带 Emission 材质的那个）")]
    public Renderer emissiveRenderer;

    [Header("Base Light Settings")]
    [Tooltip("3 个 Spot Light 各自的基础强度")]
    public float[] baseLightIntensities = new float[] { 2.2f, 2.8f, 2.2f };

    [Tooltip("每个灯的小幅差异，建议接近 1")]
    public float[] perLightVariation = new float[] { 0.96f, 1.00f, 0.98f };

    [Header("Emission Settings")]
    [ColorUsage(true, true)]
    public Color emissionColor = new Color(1.0f, 0.92f, 0.78f, 1.0f);

    [Tooltip("灯管自发光基础强度")]
    public float baseEmissionIntensity = 3.0f;

    [Header("Subtle Instability")]
    [Tooltip("平时轻微不稳定的幅度，建议很小")]
    [Range(0f, 0.3f)]
    public float subtleAmount = 0.04f;

    [Tooltip("平时轻微波动速度")]
    public float subtleSpeed = 3.0f;

    [Header("Jump Flicker")]
    [Tooltip("每秒触发一次短促跳闪的概率")]
    public float burstChancePerSecond = 0.12f;

    [Tooltip("跳闪时掉到的亮度范围，最小值不要太低")]
    public Vector2 burstDropRange = new Vector2(0.82f, 0.92f);

    [Tooltip("从跳闪状态恢复到正常亮度的速度")]
    public float recoverSpeed = 22f;

    [Header("Response")]
    [Tooltip("Spot Light 对闪烁的响应强度，越小越稳")]
    [Range(0f, 1f)]
    public float lightResponse = 0.55f;

    [Tooltip("Emission 对闪烁的响应强度，通常可以略高一点")]
    [Range(0f, 1f)]
    public float emissionResponse = 0.85f;

    [Header("Optional Startup Flicker")]
    [Tooltip("开始时是否先轻微闪两下再稳定")]
    public bool playStartupFlicker = true;

    [Tooltip("启动阶段持续时间")]
    public float startupDuration = 0.45f;

    [Tooltip("启动阶段额外跳闪次数")]
    public int startupFlickerCount = 2;

    private Material _runtimeMat;
    private float _noiseSeed;
    private float _flickerValue = 1f;

    private float _startupTimer = 0f;
    private int _startupBurstsLeft = 0;
    private bool _startupActive = false;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        // 检查数组长度
        if (spotLights != null)
        {
            if (baseLightIntensities == null || baseLightIntensities.Length != spotLights.Length)
            {
                float[] newBase = new float[spotLights.Length];
                for (int i = 0; i < newBase.Length; i++)
                {
                    newBase[i] = (baseLightIntensities != null && i < baseLightIntensities.Length)
                        ? baseLightIntensities[i]
                        : 2.5f;
                }
                baseLightIntensities = newBase;
            }

            if (perLightVariation == null || perLightVariation.Length != spotLights.Length)
            {
                float[] newVar = new float[spotLights.Length];
                for (int i = 0; i < newVar.Length; i++)
                {
                    newVar[i] = (perLightVariation != null && i < perLightVariation.Length)
                        ? perLightVariation[i]
                        : 1f;
                }
                perLightVariation = newVar;
            }
        }

        // 运行时实例化材质，避免改到原始材质
        if (emissiveRenderer != null)
        {
            _runtimeMat = emissiveRenderer.material;
            _runtimeMat.EnableKeyword("_EMISSION");
        }

        _noiseSeed = Random.Range(0f, 1000f);
        _flickerValue = 1f;

        if (playStartupFlicker)
        {
            _startupActive = true;
            _startupTimer = startupDuration;
            _startupBurstsLeft = startupFlickerCount;
        }

        ApplyLighting(1f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1. 平时极轻微波动：只提供一点“老旧不稳定感”
        float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * subtleSpeed);
        float subtle = Mathf.Lerp(1f - subtleAmount, 1f, noise);

        // 2. 启动阶段：可选，刚开灯时轻微闪两下
        if (_startupActive)
        {
            _startupTimer -= dt;

            if (_startupBurstsLeft > 0 && Random.value < 8f * dt)
            {
                _flickerValue = Random.Range(0.72f, 0.88f);
                _startupBurstsLeft--;
            }

            if (_startupTimer <= 0f)
            {
                _startupActive = false;
            }
        }
        else
        {
            // 3. 正常运行时，偶尔短促掉一下
            if (Random.value < burstChancePerSecond * dt)
            {
                _flickerValue = Random.Range(burstDropRange.x, burstDropRange.y);
            }
        }

        // 4. 快速恢复：关键，避免变成平滑呼吸灯
        _flickerValue = Mathf.MoveTowards(_flickerValue, 1f, recoverSpeed * dt);

        // 5. 最终乘数
        float finalMul = subtle * _flickerValue;

        // 6. 应用到灯光和材质
        ApplyLighting(finalMul);
    }

    private void ApplyLighting(float finalMul)
    {
        // Spot Light：变化更克制
        if (spotLights != null)
        {
            for (int i = 0; i < spotLights.Length; i++)
            {
                if (spotLights[i] == null) continue;

                float lightMul = Mathf.Lerp(1f, finalMul, lightResponse);
                lightMul *= perLightVariation[i];

                spotLights[i].intensity = baseLightIntensities[i] * lightMul;
            }
        }

        // Emission：变化略明显一点
        if (_runtimeMat != null)
        {
            float emissionMul = Mathf.Lerp(1f, finalMul, emissionResponse);
            Color finalEmission = emissionColor * (baseEmissionIntensity * emissionMul);
            _runtimeMat.SetColor(EmissionColorID, finalEmission);
        }
    }

#if UNITY_EDITOR
    // 在 Inspector 改参数时也能立刻预览一点基础状态
    private void OnValidate()
    {
        if (recoverSpeed < 0f) recoverSpeed = 0f;
        if (burstChancePerSecond < 0f) burstChancePerSecond = 0f;
        if (baseEmissionIntensity < 0f) baseEmissionIntensity = 0f;
        if (startupDuration < 0f) startupDuration = 0f;
        if (startupFlickerCount < 0) startupFlickerCount = 0;
    }
#endif
}