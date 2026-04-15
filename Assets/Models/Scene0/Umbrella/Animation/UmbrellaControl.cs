using UnityEngine;

public class UmbrellaToggle : MonoBehaviour
{
    private Animator animator;
    private bool isOpen = false;
    private bool isPlaying = false;

    [SerializeField] private float animationDuration = 1.0f;
    [SerializeField] private GameObject rainObject;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isPlaying)
        {
            animator.ResetTrigger("Open");
            animator.ResetTrigger("Close");

            if (isOpen)
            {
                animator.SetTrigger("Close");

                if (rainObject != null)
                {
                    rainObject.SetActive(false);
                }
            }
            else
            {
                animator.SetTrigger("Open");

                if (rainObject != null)
                {
                    rainObject.SetActive(true);
                }
            }

            isOpen = !isOpen;
            isPlaying = true;
            Invoke(nameof(UnlockInput), animationDuration);
        }
    }

    void UnlockInput()
    {
        isPlaying = false;
    }
}