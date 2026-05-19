using UnityEngine;
using Unity.XR.CoreUtils;

public class SeatedVROriginAligner : MonoBehaviour
{
    public XROrigin xrOrigin;
    public Transform seatAnchor;

    [Header("Options")]
    public bool alignOnStart = true;
    public bool matchYawOnly = true;

    private void Start()
    {
        if (alignOnStart)
        {
            AlignToSeat();
        }
    }

    [ContextMenu("Align To Seat")]
    public void AlignToSeat()
    {
        if (xrOrigin == null || seatAnchor == null)
            return;

        Transform cameraTransform = xrOrigin.Camera.transform;

        float cameraYaw = cameraTransform.eulerAngles.y;
        float targetYaw = seatAnchor.eulerAngles.y;
        float yawDelta = targetYaw - cameraYaw;

        xrOrigin.transform.RotateAround(
            cameraTransform.position,
            Vector3.up,
            yawDelta
        );

        Vector3 cameraOffsetFromOrigin = cameraTransform.position - xrOrigin.transform.position;

        Vector3 targetCameraPosition = seatAnchor.position;

        xrOrigin.transform.position = targetCameraPosition - cameraOffsetFromOrigin;

        if (!matchYawOnly)
        {
            xrOrigin.transform.rotation = seatAnchor.rotation;
        }
    }
}