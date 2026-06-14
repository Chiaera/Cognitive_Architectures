using UnityEngine;

public class StressBallVisualBlockerController : MonoBehaviour {
    [Header("References")]
    [Tooltip("Center transform of the stress ball")]
    public Transform ballCenter;

    [Tooltip("Hand squeeze detector")]
    public HandSqueezeDetector squeezeDetector;

    [Tooltip("Contact volume controller for thumb and little segments")]
    public HandContactVolumeController contactVolumeController;

    [Tooltip("Palm proxy transform")]
    public Transform palmProxy;

    [Tooltip("Palm visual blocker")]
    public Transform palmVisualBlocker;

    [Tooltip("Thumb visual blocker")]
    public Transform thumbVisualBlocker;

    [Tooltip("Little visual blocker")]
    public Transform littleVisualBlocker;

    [Header("Ball Settings")]
    [Tooltip("Visual radius of the stress ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Tooltip("Surface offset to keep blockers slightly outside the ball")]
    public float surfaceOffsetMeters = 0.003f;

    [Header("Palm Blocker")]
    public bool usePalmBlocker = true;
    public float palmWidthMeters = 0.070f;
    public float palmHeightMeters = 0.045f;
    public float palmThicknessMeters = 0.010f;

    [Header("Thumb Blocker")]
    public bool useThumbBlocker = true;
    public float thumbWidthMeters = 0.052f;
    public float thumbHeightMeters = 0.022f;
    public float thumbThicknessMeters = 0.008f;

    [Header("Little Blocker")]
    public bool useLittleBlocker = true;
    public float littleWidthMeters = 0.045f;
    public float littleHeightMeters = 0.020f;
    public float littleThicknessMeters = 0.008f;

    [Header("Activation")]
    [Tooltip("Minimum squeeze needed to enlarge blockers")]
    [Range(0f, 1f)]
    public float squeezeActivationThreshold = 0.12f;

    [Tooltip("Minimum scale when contact is active")]
    [Range(0f, 1f)]
    public float minimumVisibleScale = 0.35f;

    [Tooltip("Maximum additional scale driven by squeeze")]
    [Range(0f, 1f)]
    public float squeezeScaleContribution = 0.65f;

    [Header("Smoothing")]
    public float followSpeed = 22f;
    public float scaleSpeed = 14f;

    [Header("Debug")]
    public bool palmBlockerActive = false;
    public bool thumbBlockerActive = false;
    public bool littleBlockerActive = false;

    private Vector3 palmCurrentScale = Vector3.zero;
    private Vector3 thumbCurrentScale = Vector3.zero;
    private Vector3 littleCurrentScale = Vector3.zero;

    void Start() {
        ForceInitialState(palmVisualBlocker);
        ForceInitialState(thumbVisualBlocker);
        ForceInitialState(littleVisualBlocker);

        Debug.Log("Stress ball visual blocker controller initialized");
    }

    void Update() {
        if (ballCenter == null) {
            return;
        }

        UpdatePalmBlocker();
        UpdateThumbBlocker();
        UpdateLittleBlocker();
    }

    void UpdatePalmBlocker() {
        palmBlockerActive = false;

        if (
            !usePalmBlocker ||
            palmProxy == null ||
            palmVisualBlocker == null ||
            !palmProxy.gameObject.activeSelf
        ) {
            palmCurrentScale = UpdateBlockerScale(palmVisualBlocker, palmCurrentScale, Vector3.zero);
            return;
        }

        Vector3 sourcePosition = palmProxy.position;
        Vector3 surfacePoint = GetSurfacePoint(sourcePosition);
        Vector3 surfaceNormal = GetSurfaceNormal(surfacePoint);

        float squeezeAmount = GetSqueezeAmount();
        float visibility = GetVisibilityAmount(squeezeAmount);

        Vector3 targetScale = new Vector3(
            palmWidthMeters,
            palmHeightMeters,
            palmThicknessMeters
        ) * visibility;

        UpdateBlockerTransform(palmVisualBlocker, surfacePoint, surfaceNormal);
        palmCurrentScale = UpdateBlockerScale(palmVisualBlocker, palmCurrentScale, targetScale);

        palmBlockerActive = visibility > 0.01f;
    }

    void UpdateThumbBlocker() {
        thumbBlockerActive = false;

        if (
            !useThumbBlocker ||
            contactVolumeController == null ||
            thumbVisualBlocker == null ||
            !contactVolumeController.TryGetThumbSegment(out Vector3 startPoint, out Vector3 endPoint, out float radius)
        ) {
            thumbCurrentScale = UpdateBlockerScale(thumbVisualBlocker, thumbCurrentScale, Vector3.zero);
            return;
        }

        Vector3 sourcePosition = (startPoint + endPoint) * 0.5f;
        Vector3 surfacePoint = GetSurfacePoint(sourcePosition);
        Vector3 surfaceNormal = GetSurfaceNormal(surfacePoint);

        float squeezeAmount = GetSqueezeAmount();
        float visibility = GetVisibilityAmount(squeezeAmount);

        Vector3 targetScale = new Vector3(
            thumbWidthMeters,
            thumbHeightMeters,
            thumbThicknessMeters
        ) * visibility;

        UpdateBlockerTransform(thumbVisualBlocker, surfacePoint, surfaceNormal);
        thumbCurrentScale = UpdateBlockerScale(thumbVisualBlocker, thumbCurrentScale, targetScale);

        thumbBlockerActive = visibility > 0.01f;
    }

    void UpdateLittleBlocker() {
        littleBlockerActive = false;

        if (
            !useLittleBlocker ||
            contactVolumeController == null ||
            littleVisualBlocker == null ||
            !contactVolumeController.TryGetLittleSegment(out Vector3 startPoint, out Vector3 endPoint, out float radius)
        ) {
            littleCurrentScale = UpdateBlockerScale(littleVisualBlocker, littleCurrentScale, Vector3.zero);
            return;
        }

        Vector3 sourcePosition = (startPoint + endPoint) * 0.5f;
        Vector3 surfacePoint = GetSurfacePoint(sourcePosition);
        Vector3 surfaceNormal = GetSurfaceNormal(surfacePoint);

        float squeezeAmount = GetSqueezeAmount();
        float visibility = GetVisibilityAmount(squeezeAmount);

        Vector3 targetScale = new Vector3(
            littleWidthMeters,
            littleHeightMeters,
            littleThicknessMeters
        ) * visibility;

        UpdateBlockerTransform(littleVisualBlocker, surfacePoint, surfaceNormal);
        littleCurrentScale = UpdateBlockerScale(littleVisualBlocker, littleCurrentScale, targetScale);

        littleBlockerActive = visibility > 0.01f;
    }

    void UpdateBlockerTransform(Transform blocker, Vector3 surfacePoint, Vector3 surfaceNormal) {
        if (blocker == null) {
            return;
        }

        blocker.position = Vector3.Lerp(
            blocker.position,
            surfacePoint,
            Time.deltaTime * followSpeed
        );

        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.forward, surfaceNormal);

        blocker.rotation = Quaternion.Slerp(
            blocker.rotation,
            targetRotation,
            Time.deltaTime * followSpeed
        );
    }

    Vector3 UpdateBlockerScale(Transform blocker, Vector3 currentScale, Vector3 targetScale) {
        if (blocker == null) {
            return currentScale;
        }

        Vector3 updatedScale = Vector3.Lerp(
            currentScale,
            targetScale,
            Time.deltaTime * scaleSpeed
        );

        blocker.localScale = updatedScale;

        return updatedScale;
    }

    float GetSqueezeAmount() {
        if (squeezeDetector == null) {
            return 0f;
        }

        return Mathf.Clamp01(squeezeDetector.squeezeNormalized);
    }

    float GetVisibilityAmount(float squeezeAmount) {
        if (squeezeAmount < squeezeActivationThreshold) {
            return minimumVisibleScale;
        }

        float squeeze01 = Mathf.InverseLerp(
            squeezeActivationThreshold,
            1f,
            squeezeAmount
        );

        return Mathf.Clamp01(
            minimumVisibleScale + squeeze01 * squeezeScaleContribution
        );
    }

    Vector3 GetSurfacePoint(Vector3 sourceWorldPosition) {
        Vector3 center = ballCenter.position;
        Vector3 direction = sourceWorldPosition - center;

        if (direction.sqrMagnitude < 0.0001f) {
            direction = Vector3.forward;
        }

        direction.Normalize();

        return center + direction * (ballRadiusMeters + surfaceOffsetMeters);
    }

    Vector3 GetSurfaceNormal(Vector3 surfaceWorldPosition) {
        Vector3 normal = surfaceWorldPosition - ballCenter.position;

        if (normal.sqrMagnitude < 0.0001f) {
            return Vector3.forward;
        }

        return normal.normalized;
    }

    void ForceInitialState(Transform blocker) {
        if (blocker == null) {
            return;
        }

        blocker.localScale = Vector3.zero;
    }
}