using UnityEngine;

public class StressBallContactShadowController : MonoBehaviour {
    [Header("References")]
    [Tooltip("Center transform of the stress ball")]
    public Transform ballCenter;

    [Tooltip("Contact volume controller used for thumb and little contact segments")]
    public HandContactVolumeController contactVolumeController;

    [Tooltip("Palm contact proxy")]
    public Transform palmProxy;

    [Tooltip("Palm contact shadow quad")]
    public Transform palmShadow;

    [Tooltip("Thumb contact shadow quad")]
    public Transform thumbShadow;

    [Tooltip("Little contact shadow quad")]
    public Transform littleShadow;

    [Header("Ball Settings")]
    [Tooltip("Radius of the visual stress ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Tooltip("Small offset to avoid z-fighting with the ball surface")]
    public float surfaceOffsetMeters = 0.0015f;

    [Header("Palm Shadow")]
    public bool usePalmShadow = true;
    public float palmShadowWidthMeters = 0.060f;
    public float palmShadowHeightMeters = 0.040f;
    public float palmShadowMinDistance = 0.080f;

    [Header("Thumb Shadow")]
    public bool useThumbShadow = true;
    public float thumbShadowWidthMeters = 0.050f;
    public float thumbShadowHeightMeters = 0.018f;

    [Header("Little Shadow")]
    public bool useLittleShadow = true;
    public float littleShadowWidthMeters = 0.040f;
    public float littleShadowHeightMeters = 0.015f;

    [Header("Visibility")]
    [Tooltip("Show shadows only when their related proxy or volume is active")]
    public bool hideWhenInactive = true;

    [Tooltip("Global shadow alpha multiplier")]
    [Range(0f, 1f)]
    public float alphaMultiplier = 0.35f;

    [Header("Smoothing")]
    public float followSpeed = 18f;
    public float alphaSpeed = 12f;

    private Renderer palmRenderer;
    private Renderer thumbRenderer;
    private Renderer littleRenderer;

    private Material palmMaterialInstance;
    private Material thumbMaterialInstance;
    private Material littleMaterialInstance;

    private float palmAlpha = 0f;
    private float thumbAlpha = 0f;
    private float littleAlpha = 0f;

    void Start() {
        palmRenderer = GetRenderer(palmShadow);
        thumbRenderer = GetRenderer(thumbShadow);
        littleRenderer = GetRenderer(littleShadow);

        palmMaterialInstance = CreateMaterialInstance(palmRenderer);
        thumbMaterialInstance = CreateMaterialInstance(thumbRenderer);
        littleMaterialInstance = CreateMaterialInstance(littleRenderer);

        SetShadowAlpha(palmMaterialInstance, 0f);
        SetShadowAlpha(thumbMaterialInstance, 0f);
        SetShadowAlpha(littleMaterialInstance, 0f);

        Debug.Log("Stress ball contact shadow controller initialized");
    }

    void Update() {
        if (ballCenter == null) {
            return;
        }

        UpdatePalmShadow();
        UpdateThumbShadow();
        UpdateLittleShadow();
    }

    void UpdatePalmShadow() {
        bool active = false;

        if (
            usePalmShadow &&
            palmProxy != null &&
            palmShadow != null &&
            (!hideWhenInactive || palmProxy.gameObject.activeSelf)
        ) {
            Vector3 targetPosition = GetSurfacePoint(palmProxy.position);
            Vector3 normal = GetSurfaceNormal(targetPosition);

            UpdateShadowTransform(
                palmShadow,
                targetPosition,
                normal,
                palmShadowWidthMeters,
                palmShadowHeightMeters
            );

            active = true;
        }

        palmAlpha = UpdateAlpha(palmAlpha, active ? alphaMultiplier : 0f);
        SetShadowAlpha(palmMaterialInstance, palmAlpha);
    }

    void UpdateThumbShadow() {
        bool active = false;

        if (
            useThumbShadow &&
            contactVolumeController != null &&
            thumbShadow != null &&
            contactVolumeController.TryGetThumbSegment(out Vector3 startPoint, out Vector3 endPoint, out float radius)
        ) {
            Vector3 midpoint = (startPoint + endPoint) * 0.5f;
            Vector3 targetPosition = GetSurfacePoint(midpoint);
            Vector3 normal = GetSurfaceNormal(targetPosition);

            UpdateShadowTransform(
                thumbShadow,
                targetPosition,
                normal,
                thumbShadowWidthMeters,
                thumbShadowHeightMeters
            );

            active = true;
        }

        thumbAlpha = UpdateAlpha(thumbAlpha, active ? alphaMultiplier : 0f);
        SetShadowAlpha(thumbMaterialInstance, thumbAlpha);
    }

    void UpdateLittleShadow() {
        bool active = false;

        if (
            useLittleShadow &&
            contactVolumeController != null &&
            littleShadow != null &&
            contactVolumeController.TryGetLittleSegment(out Vector3 startPoint, out Vector3 endPoint, out float radius)
        ) {
            Vector3 midpoint = (startPoint + endPoint) * 0.5f;
            Vector3 targetPosition = GetSurfacePoint(midpoint);
            Vector3 normal = GetSurfaceNormal(targetPosition);

            UpdateShadowTransform(
                littleShadow,
                targetPosition,
                normal,
                littleShadowWidthMeters,
                littleShadowHeightMeters
            );

            active = true;
        }

        littleAlpha = UpdateAlpha(littleAlpha, active ? alphaMultiplier : 0f);
        SetShadowAlpha(littleMaterialInstance, littleAlpha);
    }

    void UpdateShadowTransform(
        Transform shadow,
        Vector3 targetPosition,
        Vector3 surfaceNormal,
        float widthMeters,
        float heightMeters
    ) {
        shadow.position = Vector3.Lerp(
            shadow.position,
            targetPosition,
            Time.deltaTime * followSpeed
        );

        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.forward, surfaceNormal);

        shadow.rotation = Quaternion.Slerp(
            shadow.rotation,
            targetRotation,
            Time.deltaTime * followSpeed
        );

        Vector3 targetScale = new Vector3(widthMeters, heightMeters, 1f);

        shadow.localScale = Vector3.Lerp(
            shadow.localScale,
            targetScale,
            Time.deltaTime * followSpeed
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

    Renderer GetRenderer(Transform target) {
        if (target == null) {
            return null;
        }

        return target.GetComponentInChildren<Renderer>();
    }

    Material CreateMaterialInstance(Renderer renderer) {
        if (renderer == null) {
            return null;
        }

        Material instance = new Material(renderer.material);
        renderer.material = instance;
        return instance;
    }

    float UpdateAlpha(float currentAlpha, float targetAlpha) {
        return Mathf.Lerp(
            currentAlpha,
            targetAlpha,
            Time.deltaTime * alphaSpeed
        );
    }

    void SetShadowAlpha(Material material, float alpha) {
        if (material == null) {
            return;
        }

        Color color = material.color;
        color.a = alpha;
        material.color = color;
    }
}