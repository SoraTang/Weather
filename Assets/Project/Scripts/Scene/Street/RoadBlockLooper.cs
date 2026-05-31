using UnityEngine;

public class RoadBlockLooper : MonoBehaviour
{
    [Header("Road Blocks")]
    [Tooltip("按从车近处到远处的顺序放入：Block 1、Block 2、Block 3")]
    public Transform[] roadBlocks = new Transform[3];

    [Header("Reference")]
    [Tooltip("汽车或玩家视角。通常填出租车/PlayerRig/Camera 所在物体")]
    public Transform carReference;

    [Header("Movement")]
    [Tooltip("道路前方方向。一般使用 carReference.forward")]
    public bool useCarForward = true;

    [Tooltip("如果不使用 carReference.forward，则使用这个方向作为道路前方")]
    public Vector3 customForward = Vector3.forward;

    [Tooltip("路面移动速度")]
    public float moveSpeed = 12f;

    [Tooltip("每个路面区块的长度")]
    public float blockLength = 40f;

    [Header("Debug")]
    public bool showDebugLog = false;

    private float movedDistance = 0f;

    private void Update()
    {
        if (roadBlocks == null || roadBlocks.Length == 0)
            return;

        Vector3 forwardDir = GetForwardDirection();

        // 从汽车视角看，道路向后移动
        Vector3 moveDir = -forwardDir;

        float moveDelta = moveSpeed * Time.deltaTime;

        for (int i = 0; i < roadBlocks.Length; i++)
        {
            if (roadBlocks[i] != null)
            {
                roadBlocks[i].position += moveDir * moveDelta;
            }
        }

        movedDistance += moveDelta;

        // 每移动一个区块长度，就把最后方的区块放到最前方
        while (movedDistance >= blockLength)
        {
            movedDistance -= blockLength;
            MoveBackBlockToFront(forwardDir);
        }
    }

    private Vector3 GetForwardDirection()
    {
        Vector3 forwardDir;

        if (useCarForward && carReference != null)
        {
            forwardDir = carReference.forward;
        }
        else
        {
            forwardDir = customForward;
        }

        forwardDir.y = 0f;

        if (forwardDir.sqrMagnitude < 0.0001f)
        {
            forwardDir = Vector3.forward;
        }

        return forwardDir.normalized;
    }

    private void MoveBackBlockToFront(Vector3 forwardDir)
    {
        Transform backBlock = null;
        Transform frontBlock = null;

        float minProjection = float.MaxValue;
        float maxProjection = float.MinValue;

        // 用 forwardDir 投影判断谁在最后方，谁在最前方
        for (int i = 0; i < roadBlocks.Length; i++)
        {
            Transform block = roadBlocks[i];
            if (block == null) continue;

            float projection = Vector3.Dot(block.position, forwardDir);

            if (projection < minProjection)
            {
                minProjection = projection;
                backBlock = block;
            }

            if (projection > maxProjection)
            {
                maxProjection = projection;
                frontBlock = block;
            }
        }

        if (backBlock == null || frontBlock == null)
            return;

        // 把最后方区块移动到最前方区块的前面
        backBlock.position = frontBlock.position + forwardDir * blockLength;

        if (showDebugLog)
        {
            Debug.Log($"Moved {backBlock.name} to front of {frontBlock.name}");
        }
    }
}