using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallSurfaceNormalCappedDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read per-finger pressure values")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Detector used to read fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Ball Settings")]
    [Tooltip("Visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Tooltip("Project fingertip positions onto the ideal sphere surface")]
    public bool projectContactsToSurface = true;

    [Tooltip("Surface offset in meters. Keep at 0 for stable normal indentation")]
    public float surfaceOffsetMeters = 0f;

    [Header("Per-Finger Local Deformation")]
    [Tooltip("Use thumb deformation")]
    public bool useThumb = true;

    [Tooltip("Minimum pressure required to activate a finger deformation")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.035f;

    [Tooltip("Pressure value that maps to maximum indentation depth")]
    [Range(0f, 1f)]
    public float pressureForMaxDepth = 0.70f;

    [Tooltip("Maximum indentation depth per finger in meters")]
    public float maxFingerDepthMeters = 0.014f;

    [Tooltip("Radius of the local deformation area in meters")]
    public float fingerContactRadiusMeters = 0.030f;

    [Tooltip("Higher values make the material more resistant at low pressure")]
    [Range(0.2f, 2f)]
    public float pressureResponseCurve = 1.0f;

    [Header("Surface Normal Behavior")]
    [Tooltip("If true, every affected vertex moves toward the sphere center along its own radial normal")]
    public bool useVertexRadialNormal = true;

    [Tooltip("If true, deformation is limited so vertices cannot pass too far inside the sphere")]
    public bool clampDepthPerVertex = true;

    [Tooltip("Maximum total displacement allowed per vertex in meters")]
    public float maxTotalVertexDepthMeters = 0.020f;

    [Header("Shape Quality")]
    [Tooltip("Makes the center of the indentation flatter and the border smoother")]
    [Range(0.5f, 4f)]
    public float falloffPower = 1.6f;

    [Tooltip("Blend between a sharp indentation and a softer wide pad")]
    [Range(0f, 1f)]
    public float softnessBlend = 0.35f;

    [Header("Top Anchor Protection")]
    [Tooltip("Reduce deformation close to the upper support")]
    public bool protectTopArea = true;

    [Tooltip("Local Y value where top protection begins")]
    public float topProtectionStartLocalY = 0.30f;

    [Tooltip("Local Y value where top protection is strongest")]
    public float topProtectionFullLocalY = 0.52f;

    [Tooltip("Minimum deformation multiplier close to the upper support")]
    [Range(0f, 1f)]
    public float topAreaMinimumMultiplier = 0.45f;

    [Header("Elastic Motion")]
    [Tooltip("How fast the mesh reaches the target deformation")]
    public float deformationSpeed = 18f;

    [Tooltip("How fast the mesh returns to the original shape")]
    public float returnSpeed = 18f;

    [Header("Debug")]
    public int activeFingerCount = 0;
    public int affectedVertices = 0;
    public float localContactRadius = 0f;
    public float localMaxFingerDepth = 0f;
    public float localMaxTotalVertexDepth = 0f;

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
            Debug.LogWarning("Surface normal capped deformer missing MeshFilter");
            enabled = false;
            return;
        }

        InitializeMesh();

        Debug.Log("Stress ball surface normal capped deformer initialized");
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

        localContactRadius = fingerContactRadiusMeters / averageScale;
        localMaxFingerDepth = maxFingerDepthMeters / averageScale;
        localMaxTotalVertexDepth = maxTotalVertexDepthMeters / averageScale;
    }

    void BuildTargetDeformation() {
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }

        activeFingerCount = 0;
        affectedVertices = 0;

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        for (int fingerIndex = 0; fingerIndex < fingerPressures.Length; fingerIndex++) {
            if (!useThumb && fingerIndex == 0) {
                continue;
            }

            float pressure = fingerPressures[fingerIndex];

            if (pressure < pressureActivationThreshold) {
                continue;
            }

            float pressure01 = Mathf.InverseLerp(
                pressureActivationThreshold,
                pressureForMaxDepth,
                pressure
            );

            pressure01 = Mathf.Clamp01(pressure01);
            pressure01 = Mathf.Pow(pressure01, pressureResponseCurve);

            ApplyFingerNormalIndentation(
                fingerPositions[fingerIndex],
                pressure01
            );

            activeFingerCount++;
        }

        for (int i = 0; i < targetVertices.Length; i++) {
            Vector3 finalOffset = accumulatedOffsets[i];

            if (clampDepthPerVertex && finalOffset.magnitude > localMaxTotalVertexDepth) {
                finalOffset = finalOffset.normalized * localMaxTotalVertexDepth;
            }

            targetVertices[i] = originalVertices[i] + finalOffset;
        }
    }

    void ApplyFingerNormalIndentation(Vector3 fingerWorldPosition, float pressure01) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 contactWorldPosition = GetSurfaceContactPoint(fingerWorldPosition);
        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localSphereCenter = visualTransform.InverseTransformPoint(transform.position);

        float appliedDepth = localMaxFingerDepth * pressure01;

        for (int i = 0; i < originalVertices.Length; i++) {
            Vector3 vertex = originalVertices[i];

            float distance = Vector3.Distance(vertex, localContactPosition);

            if (distance > localContactRadius) {
                continue;
            }

            float normalizedDistance = Mathf.Clamp01(distance / localContactRadius);
            float falloff = 1f - normalizedDistance;

            float softFalloff = SmoothFalloff(falloff);
            float sharperFalloff = Mathf.Pow(falloff, falloffPower);

            falloff = Mathf.Lerp(sharperFalloff, softFalloff, softnessBlend);

            float topMultiplier = GetTopProtectionMultiplier(vertex);

            Vector3 inwardDirection = GetInwardDirection(vertex, localContactPosition, localSphereCenter);

            Vector3 offset = inwardDirection * appliedDepth * falloff * topMultiplier;

            accumulatedOffsets[i] += offset;
            affectedVertices++;
        }
    }

    Vector3 GetInwardDirection(Vector3 localVertex, Vector3 localContactPosition, Vector3 localSphereCenter) {
        if (useVertexRadialNormal) {
            Vector3 radialDirection = localSphereCenter - localVertex;

            if (radialDirection.sqrMagnitude > 0.0001f) {
                return radialDirection.normalized;
            }
        }

        Vector3 contactDirection = localSphereCenter - localContactPosition;

        if (contactDirection.sqrMagnitude > 0.0001f) {
            return contactDirection.normalized;
        }

        return Vector3.down;
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

        Debug.Log("Stress ball surface normal capped deformation reset");
    }
}