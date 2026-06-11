using UnityEngine;

public class LazyFollowCanvas : MonoBehaviour {
    [Header("Target")]
    [Tooltip("Camera transform used as the reference for repositioning the canvas")]
    public Transform cameraTransform;

    [Header("Position")]
    [Tooltip("Distance of the canvas from the camera after recentering")]
    public float distanceFromCamera = 1.2f;

    [Tooltip("Vertical offset relative to the camera height")]
    public float heightOffset = -0.05f;

    [Header("Recenter Rules")]
    [Tooltip("Maximum angle allowed before the canvas starts recentering")]
    public float maxAngleFromView = 25f;

    [Tooltip("Maximum distance allowed from the ideal target position")]
    public float maxDistanceFromTarget = 0.45f;

    [Tooltip("Time before the canvas starts recentering")]
    public float recenterDelay = 1.2f;

    [Header("Smoothing")]
    [Tooltip("Movement speed during recentering")]
    public float moveSpeed = 3f;

    [Tooltip("Rotation speed during recentering")]
    public float rotationSpeed = 4f;

    private float outOfViewTimer = 0f;
    private bool isRecentering = false;

    void Start() {
        // Place the canvas in front of the user at the beginning
        SnapToTargetPosition();
    }

    void Update() {
        if (cameraTransform == null) {
            return;
        }

        Vector3 targetPosition = GetTargetPosition();
        Quaternion targetRotation = GetTargetRotation(targetPosition);

        bool shouldRecenter = ShouldRecenter(targetPosition);

        // Start counting only when the canvas is too far from the user's view
        if (shouldRecenter) {
            outOfViewTimer += Time.deltaTime;
        } else {
            outOfViewTimer = 0f;
            isRecentering = false;
        }

        // Enable smooth recentering after the delay
        if (outOfViewTimer >= recenterDelay) {
            isRecentering = true;
        }

        if (isRecentering) {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.deltaTime * moveSpeed
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );

            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            if (distanceToTarget < 0.02f) {
                isRecentering = false;
                outOfViewTimer = 0f;
            }
        }
    }

    Vector3 GetTargetPosition() {
        // Use only the horizontal forward direction to avoid placing the canvas too high or too low
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 targetPosition = cameraTransform.position + forward * distanceFromCamera;
        targetPosition.y = cameraTransform.position.y + heightOffset;

        return targetPosition;
    }

    Quaternion GetTargetRotation(Vector3 targetPosition) {
        // Make the canvas face the camera horizontally
        Vector3 directionToCamera = targetPosition - cameraTransform.position;
        directionToCamera.y = 0f;

        if (directionToCamera.sqrMagnitude < 0.001f) {
            directionToCamera = cameraTransform.forward;
            directionToCamera.y = 0f;
        }

        return Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
    }

    bool ShouldRecenter(Vector3 targetPosition) {
        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 directionToCanvas = transform.position - cameraTransform.position;
        directionToCanvas.y = 0f;
        directionToCanvas.Normalize();

        float angle = Vector3.Angle(cameraForward, directionToCanvas);
        float distance = Vector3.Distance(transform.position, targetPosition);

        if (angle > maxAngleFromView) {
            return true;
        }

        if (distance > maxDistanceFromTarget) {
            return true;
        }

        return false;
    }

    void SnapToTargetPosition() {
        if (cameraTransform == null) {
            return;
        }

        Vector3 targetPosition = GetTargetPosition();

        transform.position = targetPosition;
        transform.rotation = GetTargetRotation(targetPosition);

        Debug.Log("Canvas snapped to target position");
    }
}