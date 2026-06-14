using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallFiveFingerCappedDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read per-finger pressures")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Detector used to read fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Ball Settings")]
    [Tooltip("Visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Tooltip("Project each fingertip contact onto the sphere surface")]
    public bool projectContactsToSurface = true;

    [Tooltip("Surface offset in meters. Usually keep this at 0")]
    public float surfaceOffsetMeters = 0f;

    [Header("Finger Local Pressure")]
    [Tooltip("Use thumb pressure")]
    public bool useThumb = true;

    [Tooltip("Minimum pressure required to activate a finger indentation")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.035f;

    [Tooltip("Pressure value that corresponds to maximum indentation")]
    [Range(0f, 1f)]
    public float pressureForMaxDepth = 0.70f;

    [Tooltip("Maximum indentation depth in meters for each finger")]
    public float maxFingerIndentationDepthMeters = 0.015f;

    [Tooltip("Radius of each local finger indentation area in meters")]
    public float fingerIndentationRadiusMeters = 0.026f;

    [Tooltip("Higher values make the material resist more at low pressure")]
    [Range(0.2f, 2f)]
    public float pressureResponseCurve = 1.0f;

    [Header("Safety Clamp")]
    [Tooltip("Maximum total displacement allowed for each mesh vertex in meters")]
    public float maxTotalVertexDisplacementMeters = 0.022f;

    [Tooltip("If true, multiple fingers cannot pull one vertex beyond the total cap")]
    public bool clampTotalVertexDisplacement = true;

    [Header("Top Anchor Protection")]
    [Tooltip("Reduce deformation close to the upper connector")]
    public bool protectTopArea = true;

    [Tooltip("Local Y value where top protection starts")]
    public float topProtectionStartLocalY = 0.30f;

    [Tooltip("Local Y value where top protection is strongest")]
    public float topProtectionFullLocalY = 0.52f;

    [Tooltip("Minimum deformation multiplier near the top connector")]
    [Range(0f, 1f)]
    public float topAreaMinimumMultiplier = 0.45f;

    [Header("Elastic Motion")]
    [Tooltip("How fast the mesh deforms")]
    public float deformationSpeed = 18f;

    [Tooltip("How fast the mesh returns to the original shape")]
    public float returnSpeed = 18f;

    [Header("Debug")]
    public int activeFingerCount = 0;
    public int affectedVertices = 0;
    public float localIndentationRadius = 0f;
    public float localMaxFingerDepth = 0f;
    public float localMaxTotalDisplacement = 0f;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private Vector3[] targetVertices;
    private Vector3[] accumulatedOffsets;

    void Start() {
        if (pressureAnalyzer == null) {
            pressureAnalyzer = GetComponent<StressBallFingerPressureAnalyzer>();
        }

        if (squeezeDetector == null && pressureAnalyzer != null) {
            squeezeDetector = pressureAnalyzer.squeezeDetector;
        }

        if (ballMeshFilter == null) {
            ballMeshFilter = GetComponentInChildren<MeshFilter>();
        }

        if (ballMeshFilter == null) {
            Debug.LogWarning("Five finger capped deformer missing MeshFilter");
            enabled = false;
            return;
        }

        InitializeMesh();

        Debug.Log("Stress ball five finger capped deformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || squeezeDetector == null || deformingMesh == null) {
            return;
        }

        UpdateLocalValues();
        BuildTargetDeformation();
        ApplyElasticMeshUpdate();
    }

    void InitializeMesh() {
        deformingMesh = Instantiate(ballMeshFilter.mesh);
        ballMeshFilter.mesh = deformingMesh;

        originalVertices = deformingMesh.vertices;
        currentVertices = new Vector3[originalVertices.Length];
        targetVertices = new Vector3[originalVertices.Length];
        accumulatedOffsets = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }
    }

    void UpdateLocalValues() {
        Transform visualTransform = ballMeshFilter.transform;

        float averageScale = (
            visualTransform.lossyScale.x +
            visualTransform.lossyScale.y +
            visualTransform.lossyScale.z
        ) / 3f;

        if (averageScale <= 0.0001f) {
            averageScale = 1f;
        }

        localIndentationRadius = fingerIndentationRadiusMeters / averageScale;
        localMaxFingerDepth = maxFingerIndentationDepthMeters / averageScale;
        localMaxTotalDisplacement = maxTotalVertexDisplacementMeters / averageScale;
    }

    void BuildTargetDeformation() {
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }

        activeFingerCount = 0;
        affectedVertices = 0;

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        for (int fingerIndex = 0; fingerIndex < fingerPressures.Length; fingerIndex++) {
            if (!useThumb && fingerIndex == 0) {
                continue;
            }

            float pressure = fingerPressures[fingerIndex];

            if (pressure < pressureActivationThreshold) {
                continue;
            }

            float normalizedPressure = Mathf.InverseLerp(
                pressureActivationThreshold,
                pressureForMaxDepth,
                pressure
            );

            normalizedPressure = Mathf.Clamp01(normalizedPressure);
            normalizedPressure = Mathf.Pow(normalizedPressure, pressureResponseCurve);

            ApplySingleFingerIndentation(
                fingerPositions[fingerIndex],
                normalizedPressure
            );

            activeFingerCount++;
        }

        for (int i = 0; i < targetVertices.Length; i++) {
            Vector3 finalOffset = accumulatedOffsets[i];

            if (clampTotalVertexDisplacement && finalOffset.magnitude > localMaxTotalDisplacement) {
                finalOffset = finalOffset.normalized * localMaxTotalDisplacement;
            }

            targetVertices[i] = originalVertices[i] + finalOffset;
        }
    }

    void ApplySingleFingerIndentation(Vector3 fingerWorldPosition, float normalizedPressure) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 contactWorldPosition = GetSurfaceContactPoint(fingerWorldPosition);

        // Always indent from the contact point toward the center of the sphere.
        // This prevents outward bumps and gives a clear material resistance direction.
        Vector3 inwardWorldDirection = (transform.position - contactWorldPosition).normalized;

        if (inwardWorldDirection.sqrMagnitude < 0.0001f) {
            return;
        }

        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localInwardDirection = visualTransform.InverseTransformDirection(inwardWorldDirection).normalized;

        float appliedDepth = localMaxFingerDepth * normalizedPressure;

        for (int i = 0; i < originalVertices.Length; i++) {
            float distance = Vector3.Distance(originalVertices[i], localContactPosition);

            if (distance > localIndentationRadius) {
                continue;
            }

            float falloff = 1f - Mathf.Clamp01(distance / localIndentationRadius);
            falloff = SmoothFalloff(falloff);

            float topMultiplier = GetTopProtectionMultiplier(originalVertices[i]);

            Vector3 offset = localInwardDirection * appliedDepth * falloff * topMultiplier;

            accumulatedOffsets[i] += offset;
            affectedVertices++;
        }
    }

    Vector3 GetSurfaceContactPoint(Vector3 fingerWorldPosition) {
        if (!projectContactsToSurface) {
            return fingerWorldPosition;
        }

        Vector3 center = transform.position;
        Vector3 centerToFinger = fingerWorldPosition - center;

        if (centerToFinger.sqrMagnitude < 0.0001f) {
            return center + Vector3.forward * ballRadiusMeters;
        }

        return center + centerToFinger.normalized * (ballRadiusMeters + surfaceOffsetMeters);
    }

    float SmoothFalloff(float value) {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    float GetTopProtectionMultiplier(Vector3 localVertex) {
        if (!protectTopArea) {
            return 1f;
        }

        float topAmount = Mathf.InverseLerp(
            topProtectionStartLocalY,
            topProtectionFullLocalY,
            localVertex.y
        );

        topAmount = Mathf.Clamp01(topAmount);

        return Mathf.Lerp(1f, topAreaMinimumMultiplier, topAmount);
    }

    void ApplyElasticMeshUpdate() {
        bool hasActiveDeformation = activeFingerCount > 0;
        float speed = hasActiveDeformation ? deformationSpeed : returnSpeed;

        for (int i = 0; i < currentVertices.Length; i++) {
            currentVertices[i] = Vector3.Lerp(
                currentVertices[i],
                targetVertices[i],
                Time.deltaTime * speed
            );
        }

        deformingMesh.vertices = currentVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();
    }

    public void ResetDeformation() {
        if (deformingMesh == null || originalVertices == null) {
            return;
        }

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }

        deformingMesh.vertices = currentVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Debug.Log("Stress ball five finger capped deformation reset");
    }
}