using UnityEngine;

public class StressBallPalmVisualConstraint : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read the palm position")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("The visual mesh object that will be visually constrained")]
    public Transform ballVisual;

    [Header("Ball Settings")]
    [Tooltip("Manual radius of the visual ball in meters")]
    public float ballRadius = 0.06f;

    [Tooltip("Minimum allowed distance between palm and ball center before visual correction starts")]
    public float palmMinimumDistance = 0.055f;

    [Header("Visual Constraint")]
    [Tooltip("Maximum visual offset applied to prevent the palm from entering too deeply")]
    public float maxVisualOffset = 0.035f;

    [Tooltip("How strongly the ball reacts to palm penetration")]
    public float constraintGain = 1.2f;

    [Tooltip("How fast the visual ball moves toward the constrained position")]
    public float constraintSpeed = 14f;

    [Tooltip("How fast the visual ball returns to the original position")]
    public float returnSpeed = 8f;

    [Header("Debug")]
    public float palmDistanceFromBall = 0f;
    public float palmPenetration = 0f;
    public Vector3 visualOffsetWorld = Vector3.zero;

    private Vector3 originalLocalPosition;

    void Start() {
        // Use the first child as fallback if no visual target is assigned
        if (ballVisual == null && transform.childCount > 0) {
            ballVisual = transform.GetChild(0);
        }

        if (ballVisual == null) {
            Debug.LogWarning("Palm visual constraint missing ball visual");
            enabled = false;
            return;
        }

        originalLocalPosition = ballVisual.localPosition;

        Debug.Log("Stress ball palm visual constraint initialized");
    }

    void Update() {
        if (ballVisual == null) {
            return;
        }

        UpdatePalmConstraint();
    }

    void UpdatePalmConstraint() {
        // Return to the original visual position by default
        Vector3 targetLocalPosition = originalLocalPosition;

        palmDistanceFromBall = 0f;
        palmPenetration = 0f;
        visualOffsetWorld = Vector3.zero;

        if (squeezeDetector == null) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        if (!squeezeDetector.TryGetPalmPosition(out Vector3 palmPosition)) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        Vector3 ballCenter = transform.position;
        Vector3 palmToBall = ballCenter - palmPosition;

        palmDistanceFromBall = palmToBall.magnitude;

        if (palmDistanceFromBall <= 0.0001f) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        if (palmDistanceFromBall >= palmMinimumDistance) {
            MoveVisualToTarget(targetLocalPosition, returnSpeed);
            return;
        }

        palmPenetration = palmMinimumDistance - palmDistanceFromBall;

        Vector3 correctionDirection = palmToBall.normalized;

        float offsetAmount = Mathf.Clamp(
            palmPenetration * constraintGain,
            0f,
            maxVisualOffset
        );

        visualOffsetWorld = correctionDirection * offsetAmount;

        Vector3 targetWorldPosition = ballCenter + visualOffsetWorld;
        targetLocalPosition = transform.InverseTransformPoint(targetWorldPosition);

        MoveVisualToTarget(targetLocalPosition, constraintSpeed);
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

        Debug.Log("Stress ball palm visual constraint reset");
    }
}