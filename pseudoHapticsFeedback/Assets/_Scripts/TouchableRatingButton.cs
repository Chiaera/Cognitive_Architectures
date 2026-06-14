using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class TouchableRatingButton : MonoBehaviour {
    [Header("References")]
    [SerializeField]
    private Button targetButton;

    [Header("Touch Settings")]
    [SerializeField]
    private float touchCooldownSeconds = 0.60f;

    [SerializeField]
    private bool requireIndexProxyComponent = true;

    [Header("Visual Feedback")]
    [SerializeField]
    private bool useVisualFeedback = true;

    [SerializeField]
    private float feedbackDurationSeconds = 0.12f;

    [Header("Debug")]
    [SerializeField]
    private bool canTouch = true;

    private BoxCollider boxCollider;
    private Image buttonImage;
    private Color originalColor;

    void Awake() {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        if (targetButton == null) {
            targetButton = GetComponent<Button>();
        }

        buttonImage = GetComponent<Image>();

        if (buttonImage != null) {
            originalColor = buttonImage.color;
        }
    }

    void Start() {
        Debug.Log("Touchable rating button initialized: " + gameObject.name);
    }

    void OnTriggerEnter(Collider other) {
        TryTriggerButton(other);
    }

    void OnTriggerStay(Collider other) {
        TryTriggerButton(other);
    }

    void TryTriggerButton(Collider other) {
        if (!canTouch) {
            return;
        }

        if (requireIndexProxyComponent && other.GetComponent<IndexTipUIProxyController>() == null) {
            return;
        }

        if (targetButton == null) {
            return;
        }

        if (!targetButton.interactable) {
            return;
        }

        canTouch = false;
        targetButton.onClick.Invoke();

        if (useVisualFeedback) {
            StartCoroutine(PlayFeedback());
        }
        else {
            StartCoroutine(ResetCooldown());
        }

        Debug.Log("Touched rating button: " + gameObject.name);
    }

    IEnumerator PlayFeedback() {
        if (buttonImage != null) {
            buttonImage.color = Color.gray;
        }

        yield return new WaitForSeconds(feedbackDurationSeconds);

        if (buttonImage != null) {
            buttonImage.color = originalColor;
        }

        yield return ResetCooldown();
    }

    IEnumerator ResetCooldown() {
        yield return new WaitForSeconds(touchCooldownSeconds);
        canTouch = true;
    }
}