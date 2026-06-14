using UnityEngine;

public class StressBallVisualController : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read the normalized squeeze value")]
    public HandSqueezeDetector squeezeDetector;

    [Tooltip("Contact detector used to understand whether the hand is touching the ball")]
    public StressBallContactDetector contactDetector;

    [Header("Interaction")]
    [Tooltip("Minimum number of fingers required to start deforming the ball")]
    public int minimumTouchingFingers = 2;

    [Tooltip("Minimum squeeze required before the ball starts deforming")]
    [Range(0f, 1f)]
    public float squeezeActivationThreshold = 0.15f;

    [Header("Visual Deformation")]
    [Tooltip("Maximum visual deformation applied to the stress ball")]
    [Range(0f, 1f)]
    public float deformationIntensity = 0.35f;

    [Header("Response")]
    [Tooltip("How fast the ball reacts when the user squeezes")]
    public float compressionSpeed = 12f;

    [Tooltip("How fast the ball returns to its original shape")]
    public float releaseSpeed = 7f;

    [Header("Debug")]
    [Tooltip("True when the ball can be visually deformed")]
    public bool canDeform = false;

    [Tooltip("Current visual squeeze applied to the ball")]
    [Range(0f, 1f)]
    public float currentVisualSqueeze = 0f;

    [Tooltip("Target squeeze currently requested by the interaction logic")]
    [Range(0f, 1f)]
    public float targetVisualSqueeze = 0f;

    private Vector3 originalScale;

    void Start() {
        // Store the initial scale of the ball
        originalScale = transform.localScale;
    }

    void Update() {
        UpdateDeformationState();
        UpdateVisualSqueeze();
        ApplyVisualDeformation(currentVisualSqueeze);
    }

    void UpdateDeformationState() {
        // Reset the deformation target by default
        canDeform = false;
        targetVisualSqueeze = 0f;

        if (squeezeDetector == null) {
            return;
        }

        if (contactDetector == null) {
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            return;
        }

        bool enoughFingersTouching = contactDetector.touchingFingerCount >= minimumTouchingFingers;
        bool enoughSqueeze = squeezeDetector.squeezeNormalized >= squeezeActivationThreshold;

        if (enoughFingersTouching && enoughSqueeze) {
            canDeform = true;
            targetVisualSqueeze = squeezeDetector.squeezeNormalized;
        }
    }

    void UpdateVisualSqueeze() {
        // Use different speeds for compression and release
        float speed = targetVisualSqueeze > currentVisualSqueeze
            ? compressionSpeed
            : releaseSpeed;

        currentVisualSqueeze = Mathf.Lerp(
            currentVisualSqueeze,
            targetVisualSqueeze,
            Time.deltaTime * speed
        );
    }

    void ApplyVisualDeformation(float squeeze) {
        // The ball becomes flatter vertically and expands slightly on the horizontal axes
        float horizontalExpansion = 1f + squeeze * deformationIntensity * 0.5f;
        float verticalCompression = 1f - squeeze * deformationIntensity;

        transform.localScale = new Vector3(
            originalScale.x * horizontalExpansion,
            originalScale.y * verticalCompression,
            originalScale.z * horizontalExpansion
        );
    }

    public void ResetShape() {
        // Restore the original shape
        currentVisualSqueeze = 0f;
        targetVisualSqueeze = 0f;
        transform.localScale = originalScale;

        Debug.Log("Stress ball shape reset");
    }
}