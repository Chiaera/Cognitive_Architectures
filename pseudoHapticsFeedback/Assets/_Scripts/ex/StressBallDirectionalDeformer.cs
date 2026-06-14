using UnityEngine;

public class StressBallDirectionalDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read average pressure and pressure direction")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Header("Visual Target")]
    [Tooltip("The visual mesh object that will be deformed")]
    public Transform ballVisual;

    [Header("Deformation Settings")]
    [Tooltip("Minimum pressure required before deformation starts")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.05f;

    [Tooltip("Maximum compression applied along the pressure direction")]
    [Range(0f, 1f)]
    public float compressionIntensity = 0.35f;

    [Tooltip("Expansion applied on the axes perpendicular to the pressure direction")]
    [Range(0f, 1f)]
    public float perpendicularExpansion = 0.18f;

    [Tooltip("Global mismatch gain applied to the visual deformation")]
    public float deformationGain = 1f;

    [Header("Response")]
    [Tooltip("How fast the ball reacts when pressure increases")]
    public float compressionSpeed = 12f;

    [Tooltip("How fast the ball returns to its original shape")]
    public float releaseSpeed = 7f;

    [Header("Debug")]
    public float targetPressure = 0f;
    public float currentPressure = 0f;
    public Vector3 currentPressureDirection = Vector3.forward;

    private Vector3 originalScale;
    private Quaternion originalRotation;

    void Start() {
        // Use this object as fallback if no visual target is assigned
        if (ballVisual == null) {
            ballVisual = transform;
        }

        originalScale = ballVisual.localScale;
        originalRotation = ballVisual.localRotation;

        Debug.Log("Stress ball directional deformer initialized");
    }

    void Update() {
        UpdateTargetPressure();
        UpdateVisualPressure();
        ApplyDirectionalDeformation();
    }

    void UpdateTargetPressure() {
        // Reset target pressure by default
        targetPressure = 0f;

        if (pressureAnalyzer == null) {
            return;
        }

        if (pressureAnalyzer.averagePressure < pressureActivationThreshold) {
            return;
        }

        targetPressure = Mathf.Clamp01(pressureAnalyzer.averagePressure * deformationGain);

        if (pressureAnalyzer.averagePressureDirection.sqrMagnitude > 0.0001f) {
            currentPressureDirection = pressureAnalyzer.averagePressureDirection.normalized;
        }
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

    void ApplyDirectionalDeformation() {
        if (ballVisual == null) {
            return;
        }

        if (currentPressure <= 0.001f) {
            ballVisual.localScale = Vector3.Lerp(
                ballVisual.localScale,
                originalScale,
                Time.deltaTime * releaseSpeed
            );

            ballVisual.localRotation = Quaternion.Slerp(
                ballVisual.localRotation,
                originalRotation,
                Time.deltaTime * releaseSpeed
            );

            return;
        }

        Quaternion targetRotation = GetRotationFromPressureDirection(currentPressureDirection);

        float compression = 1f - currentPressure * compressionIntensity;
        float expansion = 1f + currentPressure * perpendicularExpansion;

        Vector3 targetScale = new Vector3(
            originalScale.x * expansion,
            originalScale.y * expansion,
            originalScale.z * compression
        );

        ballVisual.localRotation = Quaternion.Slerp(
            ballVisual.localRotation,
            targetRotation,
            Time.deltaTime * compressionSpeed
        );

        ballVisual.localScale = Vector3.Lerp(
            ballVisual.localScale,
            targetScale,
            Time.deltaTime * compressionSpeed
        );
    }

    Quaternion GetRotationFromPressureDirection(Vector3 pressureDirection) {
        // Align the local Z axis of the visual mesh with the pressure direction
        if (pressureDirection.sqrMagnitude < 0.0001f) {
            return originalRotation;
        }

        Vector3 localDirection = transform.InverseTransformDirection(pressureDirection.normalized);

        if (localDirection.sqrMagnitude < 0.0001f) {
            return originalRotation;
        }

        return Quaternion.LookRotation(localDirection, Vector3.up);
    }

    public void ResetShape() {
        // Restore the original visual shape
        targetPressure = 0f;
        currentPressure = 0f;

        if (ballVisual != null) {
            ballVisual.localScale = originalScale;
            ballVisual.localRotation = originalRotation;
        }

        Debug.Log("Stress ball directional deformation reset");
    }
}