using UnityEngine;

public class HandContactProxyStop : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Reads palm and fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Calibration Fallback")]
    [Tooltip("Calibration script used to recover user-specific squeeze gesture anchors")]
    public CalibrationSqueeze calibrationSqueeze;

    [Tooltip("Prefer live thumb tracking when available")]
    public bool preferLiveThumb = true;

    [Tooltip("Prefer live little finger tracking when available")]
    public bool preferLiveLittle = true;

    [Tooltip("Use calibrated thumb position when live thumb tracking is unreliable")]
    public bool useCalibratedThumbFallback = false;

    [Tooltip("Use calibrated little position when live little finger tracking is unreliable")]
    public bool useCalibratedLittleFallback = false;

    [Header("Ball Target")]
    [Tooltip("Center transform of the stress ball")]
    public Transform ballCenter;

    [Tooltip("Radius of the ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Header("Proxy References")]
    [Tooltip("Visual proxy for the palm contact")]
    public Transform palmProxy;

    [Tooltip("Visual proxy for the thumb contact")]
    public Transform thumbProxy;

    [Tooltip("Visual proxy for the little finger contact")]
    public Transform littleProxy;

    [Header("Activation")]
    [Tooltip("Extra shell outside the sphere where the proxy becomes active")]
    public float activationDistanceMeters = 0.025f;

    [Tooltip("Distance outside the sphere required to release the locked proxy")]
    public float releaseDistanceMeters = 0.018f;

    [Tooltip("Smooth follow speed of the proxies")]
    public float followSpeed = 8f;

    [Tooltip("Lock the proxy when it reaches the maximum visual indentation")]
    public bool lockAtMaxIndent = true;

    [Tooltip("Distance from max indentation where the proxy becomes locked")]
    public float lockThresholdMeters = 0.0015f;

    [Header("Palm Settings")]
    [Tooltip("Use a palm surface proxy instead of the raw palm joint")]
    public bool usePalmSurfaceOffset = true;

    [Tooltip("Offset from the palm joint toward the sphere center")]
    public float palmSurfaceOffsetMeters = 0.020f;

    [Tooltip("Maximum visual indentation depth for the palm")]
    public float palmMaxVisualIndentMeters = 0.006f;

    [Header("Finger Settings")]
    [Tooltip("Maximum visual indentation depth for the thumb")]
    public float thumbMaxVisualIndentMeters = 0.006f;

    [Tooltip("Maximum visual indentation depth for the little finger")]
    public float littleMaxVisualIndentMeters = 0.006f;

    [Header("Visibility")]
    [Tooltip("Show palm proxy")]
    public bool usePalmProxy = true;

    [Tooltip("Show thumb proxy")]
    public bool useThumbProxy = true;

    [Tooltip("Show little finger proxy")]
    public bool useLittleProxy = true;

    [Header("Debug")]
    public bool palmActive = false;
    public bool thumbActive = false;
    public bool littleActive = false;

    public bool palmLocked = false;
    public bool thumbLocked = false;
    public bool littleLocked = false;

    public bool thumbUsesCalibratedFallback = false;
    public bool littleUsesCalibratedFallback = false;

    public float palmDistance = 0f;
    public float thumbDistance = 0f;
    public float littleDistance = 0f;

    private Vector3 palmLockedPosition = Vector3.zero;
    private Vector3 thumbLockedPosition = Vector3.zero;
    private Vector3 littleLockedPosition = Vector3.zero;

    void Start() {
        HideProxy(palmProxy);
        HideProxy(thumbProxy);
        HideProxy(littleProxy);

        Debug.Log("Hand contact proxy stop initialized");
    }

    void Update() {
        if (squeezeDetector == null || ballCenter == null) {
            return;
        }

        UpdatePalmProxy();
        UpdateThumbProxy();
        UpdateLittleProxy();
    }

    void UpdatePalmProxy() {
        palmActive = false;

        if (!usePalmProxy || palmProxy == null) {
            return;
        }

        if (!squeezeDetector.TryGetPalmPosition(out Vector3 palmJointPosition)) {
            HideProxy(palmProxy);
            palmLocked = false;
            return;
        }

        Vector3 center = ballCenter.position;
        Vector3 directionToCenter = center - palmJointPosition;

        if (directionToCenter.sqrMagnitude < 0.0001f) {
            HideProxy(palmProxy);
            palmLocked = false;
            return;
        }

        Vector3 palmSurfacePosition = palmJointPosition;

        if (usePalmSurfaceOffset) {
            palmSurfacePosition = palmJointPosition + directionToCenter.normalized * palmSurfaceOffsetMeters;
        }

        bool shouldShow = TryGetStoppedProxyPosition(
            palmSurfacePosition,
            palmMaxVisualIndentMeters,
            ref palmLocked,
            ref palmLockedPosition,
            out Vector3 targetPosition,
            out float distanceToCenter
        );

        palmDistance = distanceToCenter;

        if (!shouldShow) {
            HideProxy(palmProxy);
            return;
        }

        palmActive = true;
        ShowProxy(palmProxy);
        MoveProxy(palmProxy, targetPosition);
    }

    void UpdateThumbProxy() {
        thumbActive = false;
        thumbUsesCalibratedFallback = false;

        if (!useThumbProxy || thumbProxy == null) {
            return;
        }

        if (!TryGetPalmForFallback(out Vector3 palmPosition)) {
            HideProxy(thumbProxy);
            thumbLocked = false;
            return;
        }

        Vector3 thumbPosition = Vector3.zero;
        bool hasThumbPosition = false;

        if (preferLiveThumb && TryGetThumbAndLittlePositions(out Vector3 liveThumbPosition, out Vector3 liveLittlePosition)) {
            thumbPosition = liveThumbPosition;
            hasThumbPosition = true;
        }

        if (!hasThumbPosition && useCalibratedThumbFallback) {
            if (
                calibrationSqueeze != null &&
                calibrationSqueeze.TryGetEstimatedThumbPositionFromPalm(palmPosition, out Vector3 estimatedThumbPosition)
            ) {
                thumbPosition = estimatedThumbPosition;
                hasThumbPosition = true;
                thumbUsesCalibratedFallback = true;
            }
        }

        if (!hasThumbPosition) {
            HideProxy(thumbProxy);
            thumbLocked = false;
            return;
        }

        bool shouldShow = TryGetStoppedProxyPosition(
            thumbPosition,
            thumbMaxVisualIndentMeters,
            ref thumbLocked,
            ref thumbLockedPosition,
            out Vector3 targetPosition,
            out float distanceToCenter
        );

        thumbDistance = distanceToCenter;

        if (!shouldShow) {
            HideProxy(thumbProxy);
            return;
        }

        thumbActive = true;
        ShowProxy(thumbProxy);
        MoveProxy(thumbProxy, targetPosition);
    }

    void UpdateLittleProxy() {
        littleActive = false;
        littleUsesCalibratedFallback = false;

        if (!useLittleProxy || littleProxy == null) {
            return;
        }

        if (!TryGetPalmForFallback(out Vector3 palmPosition)) {
            HideProxy(littleProxy);
            littleLocked = false;
            return;
        }

        Vector3 littlePosition = Vector3.zero;
        bool hasLittlePosition = false;

        if (preferLiveLittle && TryGetThumbAndLittlePositions(out Vector3 liveThumbPosition, out Vector3 liveLittlePosition)) {
            littlePosition = liveLittlePosition;
            hasLittlePosition = true;
        }

        if (!hasLittlePosition && useCalibratedLittleFallback) {
            if (
                calibrationSqueeze != null &&
                calibrationSqueeze.TryGetEstimatedLittlePositionFromPalm(palmPosition, out Vector3 estimatedLittlePosition)
            ) {
                littlePosition = estimatedLittlePosition;
                hasLittlePosition = true;
                littleUsesCalibratedFallback = true;
            }
        }

        if (!hasLittlePosition) {
            HideProxy(littleProxy);
            littleLocked = false;
            return;
        }

        bool shouldShow = TryGetStoppedProxyPosition(
            littlePosition,
            littleMaxVisualIndentMeters,
            ref littleLocked,
            ref littleLockedPosition,
            out Vector3 targetPosition,
            out float distanceToCenter
        );

        littleDistance = distanceToCenter;

        if (!shouldShow) {
            HideProxy(littleProxy);
            return;
        }

        littleActive = true;
        ShowProxy(littleProxy);
        MoveProxy(littleProxy, targetPosition);
    }

    bool TryGetPalmForFallback(out Vector3 palmPosition) {
        palmPosition = Vector3.zero;

        if (!squeezeDetector.TryGetPalmPosition(out palmPosition)) {
            return false;
        }

        return true;
    }

    bool TryGetThumbAndLittlePositions(out Vector3 thumbPosition, out Vector3 littlePosition) {
        thumbPosition = Vector3.zero;
        littlePosition = Vector3.zero;

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerTipPositions)) {
            return false;
        }

        if (fingerTipPositions == null || fingerTipPositions.Length < 5) {
            return false;
        }

        thumbPosition = fingerTipPositions[0];
        littlePosition = fingerTipPositions[4];

        return true;
    }

    bool TryGetStoppedProxyPosition(
        Vector3 sourceWorldPosition,
        float maxVisualIndentMeters,
        ref bool isLocked,
        ref Vector3 lockedPosition,
        out Vector3 targetWorldPosition,
        out float distanceToCenter
    ) {
        targetWorldPosition = sourceWorldPosition;
        distanceToCenter = 0f;

        Vector3 center = ballCenter.position;
        Vector3 centerToPoint = sourceWorldPosition - center;

        if (centerToPoint.sqrMagnitude < 0.0001f) {
            isLocked = false;
            return false;
        }

        distanceToCenter = centerToPoint.magnitude;

        float activationRadius = ballRadiusMeters + activationDistanceMeters;
        float releaseRadius = ballRadiusMeters + releaseDistanceMeters;

        if (isLocked) {
            if (distanceToCenter > releaseRadius) {
                isLocked = false;
            } else {
                targetWorldPosition = lockedPosition;
                return true;
            }
        }

        if (distanceToCenter > activationRadius) {
            return false;
        }

        Vector3 direction = centerToPoint.normalized;

        float rawIndent = Mathf.Max(ballRadiusMeters - distanceToCenter, 0f);
        float clampedIndent = Mathf.Clamp(rawIndent, 0f, maxVisualIndentMeters);

        float stoppedRadius = ballRadiusMeters - clampedIndent;
        stoppedRadius = Mathf.Max(stoppedRadius, 0.001f);

        targetWorldPosition = center + direction * stoppedRadius;

        if (lockAtMaxIndent && rawIndent >= maxVisualIndentMeters - lockThresholdMeters) {
            isLocked = true;
            lockedPosition = targetWorldPosition;
        }

        return true;
    }

    void MoveProxy(Transform proxy, Vector3 targetPosition) {
        proxy.position = Vector3.Lerp(
            proxy.position,
            targetPosition,
            Time.deltaTime * followSpeed
        );
    }

    void ShowProxy(Transform proxy) {
        if (proxy != null && !proxy.gameObject.activeSelf) {
            proxy.gameObject.SetActive(true);
        }
    }

    void HideProxy(Transform proxy) {
        if (proxy != null && proxy.gameObject.activeSelf) {
            proxy.gameObject.SetActive(false);
        }
    }
}