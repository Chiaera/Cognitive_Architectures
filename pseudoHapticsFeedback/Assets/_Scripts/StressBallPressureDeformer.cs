using UnityEngine;
using TMPro;

public class StressBallPressureDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read fingertip positions and squeeze amount")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Ball Settings")]
    [Tooltip("Use the renderer bounds to calculate the current ball radius automatically")]
    public bool useAutomaticRadius = true;

    [Tooltip("Manual ball radius used only when automatic radius is disabled")]
    public float manualBallRadius = 0.06f;

    [Tooltip("Extra interaction zone outside the ball surface")]
    public float outerContactTolerance = 0.015f;

    [Tooltip("Maximum expected fingertip penetration inside the ball")]
    public float maxPenetrationDepth = 0.04f;

    [Header("Pressure Settings")]
    [Tooltip("Minimum number of fingers required to allow deformation")]
    public int minimumActiveFingers = 2;

    [Tooltip("How much the global squeeze value contributes to the pressure")]
    [Range(0f, 1f)]
    public float squeezeContribution = 0.35f;

    [Tooltip("How much fingertip penetration contributes to the pressure")]
    [Range(0f, 1f)]
    public float penetrationContribution = 0.65f;

    [Header("Visual Deformation")]
    [Tooltip("Maximum visual deformation applied to the stress ball")]
    [Range(0f, 1f)]
    public float deformationIntensity = 0.35f;

    [Tooltip("How fast the ball reacts when pressure increases")]
    public float compressionSpeed = 12f;

    [Tooltip("How fast the ball returns to its original shape")]
    public float releaseSpeed = 7f;

    [Header("Debug")]
    public float currentBallRadius = 0f;
    public int activeFingerCount = 0;
    public float penetrationPressure = 0f;
    public float squeezePressure = 0f;
    public float targetPressure = 0f;
    public float currentPressure = 0f;

    private Renderer ballRenderer;
    private Vector3 originalScale;

    [Header("Runtime Debug UI")]
    [Tooltip("Optional text used to show pressure debug values inside the headset")]
    public TextMeshProUGUI debugText;

    [Tooltip("Show pressure debug values inside the headset")]
    public bool showRuntimeDebug = true;

    void Start() {
        // Store the initial scale and renderer reference
        originalScale = transform.localScale;
        ballRenderer = GetComponent<Renderer>();

        UpdateBallRadius();

        Debug.Log("Stress ball pressure deformer initialized");
    }

    void Update() {
        UpdateBallRadius();
        UpdatePressure();
        UpdateVisualPressure();
        ApplyGlobalDeformation(currentPressure);
        UpdateRuntimeDebugUI();
    }

    void UpdateBallRadius() {
        // Estimate the visual radius from the renderer bounds
        if (useAutomaticRadius && ballRenderer != null) {
            Vector3 extents = ballRenderer.bounds.extents;
            currentBallRadius = Mathf.Max(extents.x, extents.y, extents.z);
            return;
        }

        currentBallRadius = manualBallRadius;
    }

    void UpdatePressure() {
        // Reset target pressure by default
        activeFingerCount = 0;
        penetrationPressure = 0f;
        squeezePressure = 0f;
        targetPressure = 0f;

        if (squeezeDetector == null) {
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            return;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        float totalPenetrationAmount = 0f;
        float interactionLimit = currentBallRadius + outerContactTolerance;

        for (int i = 0; i < fingerPositions.Length; i++) {
            float distanceFromCenter = Vector3.Distance(fingerPositions[i], transform.position);

            if (distanceFromCenter <= interactionLimit) {
                activeFingerCount++;

                float penetrationDepth = interactionLimit - distanceFromCenter;
                float normalizedPenetration = Mathf.Clamp01(penetrationDepth / maxPenetrationDepth);

                totalPenetrationAmount += normalizedPenetration;
            }
        }

        if (activeFingerCount < minimumActiveFingers) {
            return;
        }

        penetrationPressure = Mathf.Clamp01(totalPenetrationAmount / activeFingerCount);
        squeezePressure = Mathf.Clamp01(squeezeDetector.squeezeNormalized);

        targetPressure = Mathf.Clamp01(
            penetrationPressure * penetrationContribution +
            squeezePressure * squeezeContribution
        );
    }

    void UpdateVisualPressure() {
        // Use different speeds for compression and release
        float speed = targetPressure > currentPressure
            ? compressionSpeed
            : releaseSpeed;

        currentPressure = Mathf.Lerp(
            currentPressure,
            targetPressure,
            Time.deltaTime * speed
        );
    }

    void ApplyGlobalDeformation(float pressure) {
        // Compress the ball vertically and expand it slightly on the horizontal axes
        float horizontalExpansion = 1f + pressure * deformationIntensity * 0.5f;
        float verticalCompression = 1f - pressure * deformationIntensity;

        transform.localScale = new Vector3(
            originalScale.x * horizontalExpansion,
            originalScale.y * verticalCompression,
            originalScale.z * horizontalExpansion
        );
    }

    void UpdateRuntimeDebugUI() {
        // Show pressure information directly inside the headset during testing
        if (!showRuntimeDebug || debugText == null) {
            return;
        }

        debugText.text =
            "Active fingers: " + activeFingerCount + "\n" +
            "Ball radius: " + currentBallRadius.ToString("F3") + "\n" +
            "Penetration pressure: " + penetrationPressure.ToString("F2") + "\n" +
            "Squeeze pressure: " + squeezePressure.ToString("F2") + "\n" +
            "Target pressure: " + targetPressure.ToString("F2") + "\n" +
            "Current pressure: " + currentPressure.ToString("F2");
}

    public void ResetShape() {
        // Restore the original ball shape
        currentPressure = 0f;
        targetPressure = 0f;
        transform.localScale = originalScale;

        Debug.Log("Stress ball shape reset");
    }
}