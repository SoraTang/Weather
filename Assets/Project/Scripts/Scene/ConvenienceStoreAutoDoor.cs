using System.Collections;
using UnityEngine;

public class ConvenienceStoreAutoDoor : MonoBehaviour
{
    [Header("Door Objects")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Movement Settings")]
    public float openDistance = 1.2f;
    public float openDuration = 2.0f;
    public float closeDuration = 2.0f;
    public float stayOpenTime = 2.0f;

    [Header("Random Interval Settings")]
    public bool autoLoop = true;
    public float minInterval = 5.0f;
    public float maxInterval = 12.0f;
    public bool startWithRandomDelay = true;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public bool playSoundOnlyWhenOpening = true;

    [Header("Direction Settings")]
    public Vector3 leftOpenDirection = Vector3.left;
    public Vector3 rightOpenDirection = Vector3.right;

    [Header("Outdoor Audio Link")]
    [Range(0f, 1f)]
    public float DoorExposure01 = 0f;   // 0 = 门完全关，1 = 门完全开

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenedPos;
    private Vector3 rightOpenedPos;

    private bool isMoving = false;

    private void Start()
    {
        if (leftDoor == null || rightDoor == null)
        {
            Debug.LogError("ConvenienceStoreAutoDoor: 请在 Inspector 中指定 leftDoor 和 rightDoor。");
            enabled = false;
            return;
        }

        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;

        leftOpenedPos = leftClosedPos + leftOpenDirection.normalized * openDistance;
        rightOpenedPos = rightClosedPos + rightOpenDirection.normalized * openDistance;

        DoorExposure01 = 0f;

        if (autoLoop)
        {
            StartCoroutine(AutoDoorLoop());
        }
    }

    private IEnumerator AutoDoorLoop()
    {
        if (startWithRandomDelay)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }

        while (autoLoop)
        {
            yield return StartCoroutine(OpenAndCloseOnce());

            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    public IEnumerator OpenAndCloseOnce()
    {
        if (isMoving)
            yield break;

        isMoving = true;

        PlayDoorCycleSound();

        // 开门：0 -> 1
        yield return StartCoroutine(MoveDoors(
            leftClosedPos,
            leftOpenedPos,
            rightClosedPos,
            rightOpenedPos,
            openDuration,
            0f,
            1f
        ));

        // 保持开启：1
        DoorExposure01 = 1f;
        yield return new WaitForSeconds(stayOpenTime);

        // 关门：1 -> 0
        yield return StartCoroutine(MoveDoors(
            leftOpenedPos,
            leftClosedPos,
            rightOpenedPos,
            rightClosedPos,
            closeDuration,
            1f,
            0f
        ));

        DoorExposure01 = 0f;
        isMoving = false;
    }

    private IEnumerator MoveDoors(
        Vector3 leftFrom,
        Vector3 leftTo,
        Vector3 rightFrom,
        Vector3 rightTo,
        float duration,
        float exposureFrom,
        float exposureTo
    )
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            leftDoor.localPosition = Vector3.Lerp(leftFrom, leftTo, smoothT);
            rightDoor.localPosition = Vector3.Lerp(rightFrom, rightTo, smoothT);

            DoorExposure01 = Mathf.Lerp(exposureFrom, exposureTo, smoothT);

            yield return null;
        }

        leftDoor.localPosition = leftTo;
        rightDoor.localPosition = rightTo;
        DoorExposure01 = exposureTo;
    }

    private void PlayDoorCycleSound()
    {
        if (audioSource == null || openSound == null)
            return;

        if (playSoundOnlyWhenOpening)
        {
            audioSource.PlayOneShot(openSound);
        }
    }

    public void TriggerDoorOnce()
    {
        if (!isMoving)
        {
            StartCoroutine(OpenAndCloseOnce());
        }
    }
}