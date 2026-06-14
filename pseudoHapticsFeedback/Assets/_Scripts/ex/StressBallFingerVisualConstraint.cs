using UnityEngine;

public class StressBallFingerVisualConstraint : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Tooltip("Analyzer used to read finger pressures")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Header("Visual Target")]
    [Tooltip("The visual mesh object that will be visually constrained")]
    public Transform ballVisual;

    [Header("Ball Settings")]
    [Tooltip("Manual radius of the visual ball in meters")]
    public float ballRadius = 0.06f;

    [Tooltip("Distance from the center where finger correction starts")]
    public float fingerMinimumDistance = 0.055f;

    [Header("Finger Constraint")]
    [Tooltip("Maximum visual offset caused by all fingers")]
    public float maxVisualOffset = 0.025f;

    [Tooltip("How strongly the ball reacts to finger penetration")]
    public float constraintGain = 0.8f;

    [Tooltip("Minimum finger pressure required to affect the visual constraint")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.05f;

    [Header("Response")]
    [Tooltip("How fast the visual ball moves toward the constrained position")]
    public float constraintSpeed = 14f;

    [Tooltip("How fast the visual ball returns to the original position")]
    public float returnSpeed = 8f;

    [Header("Debug")]
    public int constrainingFingerCount = 0;
    public float averageFingerPenetration = 0f;
    public Vector3 visualOffsetWorld = Vector3.zero;

    private Vector3 originalLocalPosition;

    void Start() {
        // Use the first child as fallback if no visual target is assigned
        if (ballVisual == null && transform.childCount > 0) {
            ballVisual = transform.GetChild(0);
        }

        if (ballVisual == null) {
            Debug.LogWarning("Finger visual constraint missing ball visual");
            enabled = false;
            return;
        }

        if (pressureAnalyzer == null) {
            pressureAnalyzer = GetComponent<StressBallFingerPressureAnalyzer>();
        }

        originalLocalPosition = ballVisual.localPosition;

        Debug.Log("Stress ball finger visual constraint initialized");
    }

    void Update() {
        if (ballVisual == null) {
            return;
        }

        UpdateFingerConstraint();
    }

    void UpdateFingerConstraint() {
        // Return to the original visual position by default
        Vector3 targetLocalPosition = originalLocalPosition;

        constrainingFingerCount = 0;
        averageFingerPenetration = 0f;
        visualOffsetWorld = Vector3.zero;

        if (squeezeDetector == null || pressureAnalyzer == null) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        Vector3 totalCorrection = Vector3.zero;
        float totalPenetration = 0f;

        for (int i = 0; i < fingerPositions.Length; i++) {
            float fingerPressure = fingerPressures[i];

            if (fingerPressure < pressureActivationThreshold) {
                continue;
            }

            Vector3 fingerPosition = fingerPositions[i];
            Vector3 fingerToBall = transform.position - fingerPosition;

            float distanceFromBall = fingerToBall.magnitude;

            if (distanceFromBall <= 0.0001f) {
                continue;
            }

            if (distanceFromBall >= fingerMinimumDistance) {
                continue;
            }

            float penetration = fingerMinimumDistance - distanceFromBall;
            Vector3 correctionDirection = fingerToBall.normalized;

            totalCorrection += correctionDirection * penetration * fingerPressure;
            totalPenetration += penetration;
            constrainingFingerCount++;
        }

        if (constrainingFingerCount > 0) {
            averageFingerPenetration = totalPenetration / constrainingFingerCount;

            Vector3 averageCorrection = totalCorrection / constrainingFingerCount;

            visualOffsetWorld = Vector3.ClampMagnitude(
                averageCorrection * constraintGain,
                maxVisualOffset
            );

            Vector3 targetWorldPosition = transform.position + visualOffsetWorld;
            targetLocalPosition = transform.InverseTransformPoint(targetWorldPosition);

            MoveVisualToTarget(targetLocalPosition, constraintSpeed);
            return;
        }

        MoveVisualToTarget(targetLocalPosition, returnSpeed);
    }

    void MoveVisualToTarget(Vector3 targetLocalPosition, float speed) {
        // Smoothly move the visual ball toward the target local position
        ballVisual.localPosition = Vector3.Lerp(
            ballVisual.localPosition,
            targetLocalPosition,
            Time.deltaTime * speed
        );
    }

    public void ResetConstraint() {
        // Restore the original visual position
        if (ballVisual == null) {
            return;
        }

        ballVisual.localPosition = originalLocalPosition;

        Debug.Log("Stress ball finger visual constraint reset");
    }
}