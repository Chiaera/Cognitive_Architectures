using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallContactWeightedSqueezeDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read finger pressure, direction and active contacts")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Squeeze detector used to read fingertip positions and global squeeze")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Ball Settings")]
    [Tooltip("Visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.045f;

    [Tooltip("Project contact points onto the sphere surface")]
    public bool projectContactsToSurface = true;

    [Tooltip("Small outward offset for projected contact points")]
    public float surfaceOffsetMeters = 0.001f;

    [Header("Activation")]
    [Tooltip("Minimum active fingers required to deform the ball")]
    public int minimumActiveFingers = 2;

    [Tooltip("Minimum average pressure required to start deformation")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.04f;

    [Tooltip("Minimum squeeze required to amplify deformation")]
    [Range(0f, 1f)]
    public float squeezeActivationThreshold = 0.20f;

    [Header("Contact-Weighted Deformation")]
    [Tooltip("World-space radius of the main deformation area")]
    public float deformationRadiusMeters = 0.045f;

    [Tooltip("World-space maximum indentation depth")]
    public float maxIndentationDepthMeters = 0.022f;

    [Tooltip("World-space secondary compression depth")]
    public float globalCompressionDepthMeters = 0.008f;

    [Tooltip("How much deformation increases with the number of active fingers")]
    [Range(0f, 1f)]
    public float activeFingerInfluence = 0.35f;

    [Tooltip("Overall deformation gain")]
    public float deformationGain = 1.0f;

    [Tooltip("Softness curve. Lower values react earlier, higher values resist more at the beginning")]
    [Range(0.2f, 2f)]
    public float responseCurve = 0.9f;

    [Header("Top Anchor Protection")]
    [Tooltip("Reduce deformation near the upper attachment area")]
    public bool protectTopArea = true;

    [Tooltip("Local Y value above which deformation starts being reduced. For a radius 0.5 mesh, 0.20 protects the upper cap")]
    public float topProtectionStartLocalY = 0.18f;

    [Tooltip("Local Y value where deformation is almost completely blocked")]
    public float topProtectionFullLocalY = 0.45f;

    [Tooltip("Minimum deformation multiplier in the protected top area")]
    [Range(0f, 1f)]
    public float topAreaMinimumMultiplier = 0.10f;

    [Header("Saturation")]
    [Tooltip("Maximum effective pressure")]
    [Range(0f, 1f)]
    public float maxEffectivePressure = 0.70f;

    [Tooltip("Maximum effective squeeze")]
    [Range(0f, 1f)]
    public float maxEffectiveSqueeze = 0.85f;

    [Header("Elastic Motion")]
    [Tooltip("How fast the mesh deforms")]
    public float deformationSpeed = 16f;

    [Tooltip("How fast the mesh returns to the original shape")]
    public float returnSpeed = 10f;

    [Header("Debug")]
    public int activeFingerCount = 0;
    public float averagePressure = 0f;
    public float squeezeAmount = 0f;
    public float currentDeformationAmount = 0f;
    public Vector3 weightedContactWorldPosition = Vector3.zero;
    public Vector3 weightedPressureWorldDirection = Vector3.forward;
    public float localDeformationRadius = 0f;
    public float localIndentationDepth = 0f;
    public float localGlobalCompressionDepth = 0f;
    public int affectedVertices = 0;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private Vector3[] targetVertices;

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
            Debug.LogWarning("Contact weighted squeeze deformer missing MeshFilter");
            enabled = false;
            return;
        }

        InitializeMesh();

        Debug.Log("Stress ball contact weighted squeeze deformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || squeezeDetector == null || deformingMesh == null) {
            return;
        }

        UpdateLocalScaleValues();
        BuildTargetDeformation();
        ApplyElasticMeshUpdate();
    }

    void InitializeMesh() {
        deformingMesh = Instantiate(ballMeshFilter.mesh);
        ballMeshFilter.mesh = deformingMesh;

        originalVertices = deformingMesh.vertices;
        currentVertices = new Vector3[originalVertices.Length];
        targetVertices = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++) {
            originalVertices[i] = deformingMesh.vertices[i];
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
        }
    }

    void UpdateLocalScaleValues() {
        Transform visualTransform = ballMeshFilter.transform;

        float averageScale = (
            visualTransform.lossyScale.x +
            visualTransform.lossyScale.y +
            visualTransform.lossyScale.z
        ) / 3f;

        if (averageScale <= 0.0001f) {
            averageScale = 1f;
        }

        localDeformationRadius = deformationRadiusMeters / averageScale;
        localIndentationDepth = maxIndentationDepthMeters / averageScale;
        localGlobalCompressionDepth = globalCompressionDepthMeters / averageScale;
    }

    void BuildTargetDeformation() {
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
        }

        affectedVertices = 0;

        if (!TryBuildWeightedContactData(out Vector3 contactWorldPosition, out Vector3 pressureWorldDirection, out float deformationAmount)) {
            currentDeformationAmount = 0f;
            return;
        }

        currentDeformationAmount = deformationAmount;
        weightedContactWorldPosition = contactWorldPosition;
        weightedPressureWorldDirection = pressureWorldDirection;

        ApplyMainContactDeformation(contactWorldPosition, pressureWorldDirection, deformationAmount);
        ApplySecondaryCompression(pressureWorldDirection, deformationAmount);
    }

    bool TryBuildWeightedContactData(out Vector3 contactWorldPosition, out Vector3 pressureWorldDirection, out float deformationAmount) {
        contactWorldPosition = transform.position;
        pressureWorldDirection = Vector3.forward;
        deformationAmount = 0f;

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();
        Vector3[] fingerDirections = pressureAnalyzer.GetAllFingerPressureDirections();

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return false;
        }

        Vector3 weightedContact = Vector3.zero;
        Vector3 weightedDirection = Vector3.zero;
        float totalWeight = 0f;
        int activeCount = 0;

        for (int i = 0; i < fingerPressures.Length; i++) {
            float pressure = fingerPressures[i];

            if (pressure < pressureActivationThreshold) {
                continue;
            }

            Vector3 contactPoint = GetSurfaceContactPoint(fingerPositions[i]);
            Vector3 direction = fingerDirections[i];

            if (direction.sqrMagnitude < 0.0001f) {
                direction = (transform.position - fingerPositions[i]).normalized;
            }

            float effectivePressure = GetEffectivePressure(pressure);
            weightedContact += contactPoint * effectivePressure;
            weightedDirection += direction.normalized * effectivePressure;
            totalWeight += effectivePressure;
            activeCount++;
        }

        activeFingerCount = activeCount;
        averagePressure = pressureAnalyzer.averagePressure;
        squeezeAmount = squeezeDetector.squeezeNormalized;

        if (activeCount < minimumActiveFingers || totalWeight <= 0.0001f) {
            return false;
        }

        contactWorldPosition = weightedContact / totalWeight;

        if (weightedDirection.sqrMagnitude > 0.0001f) {
            pressureWorldDirection = weightedDirection.normalized;
        } else {
            pressureWorldDirection = (transform.position - contactWorldPosition).normalized;
        }

        float pressureAmount = Mathf.InverseLerp(
            pressureActivationThreshold,
            maxEffectivePressure,
            averagePressure
        );

        float squeezeContribution = Mathf.InverseLerp(
            squeezeActivationThreshold,
            maxEffectiveSqueeze,
            squeezeAmount
        );

        float activeFingerAmount = Mathf.InverseLerp(
            minimumActiveFingers,
            5,
            activeCount
        );

        pressureAmount = Mathf.Clamp01(pressureAmount);
        squeezeContribution = Mathf.Clamp01(squeezeContribution);
        activeFingerAmount = Mathf.Clamp01(activeFingerAmount);

        float combinedAmount =
            pressureAmount * 0.50f +
            squeezeContribution * 0.35f +
            activeFingerAmount * activeFingerInfluence;

        combinedAmount = Mathf.Clamp01(combinedAmount);

        deformationAmount = Mathf.Pow(combinedAmount, responseCurve) * deformationGain;
        deformationAmount = Mathf.Clamp01(deformationAmount);

        return deformationAmount > 0.001f;
    }

    float GetEffectivePressure(float rawPressure) {
        float clampedPressure = Mathf.Clamp(rawPressure, 0f, maxEffectivePressure);
        float normalizedPressure = clampedPressure / Mathf.Max(maxEffectivePressure, 0.0001f);

        return Mathf.Clamp01(Mathf.Pow(normalizedPressure, responseCurve) * maxEffectivePressure);
    }

    Vector3 GetSurfaceContactPoint(Vector3 fingerWorldPosition) {
        if (!projectContactsToSurface) {
            return fingerWorldPosition;
        }

        Vector3 center = transform.position;
        Vector3 centerToFinger = fingerWorldPosition - center;

        if (centerToFinger.sqrMagnitude < 0.0001f) {
            return center + Vector3.forward * (ballRadiusMeters + surfaceOffsetMeters);
        }

        return center + centerToFinger.normalized * (ballRadiusMeters + surfaceOffsetMeters);
    }

    void ApplyMainContactDeformation(Vector3 contactWorldPosition, Vector3 pressureWorldDirection, float amount) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 localContact = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localPressureDirection = visualTransform.InverseTransformDirection(pressureWorldDirection).normalized;

        float appliedDepth = localIndentationDepth * amount;

        for (int i = 0; i < targetVertices.Length; i++) {
            float distance = Vector3.Distance(originalVertices[i], localContact);

            if (distance > localDeformationRadius) {
                continue;
            }

            float falloff = 1f - Mathf.Clamp01(distance / localDeformationRadius);
            falloff = SmoothFalloff(falloff);

            float topMultiplier = GetTopProtectionMultiplier(originalVertices[i]);

            Vector3 offset = localPressureDirection * appliedDepth * falloff * topMultiplier;

            targetVertices[i] += offset;
            affectedVertices++;
        }
    }

    void ApplySecondaryCompression(Vector3 pressureWorldDirection, float amount) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 localAxis = visualTransform.InverseTransformDirection(pressureWorldDirection).normalized;

        if (localAxis.sqrMagnitude < 0.0001f) {
            return;
        }

        float appliedDepth = localGlobalCompressionDepth * amount;

        for (int i = 0; i < targetVertices.Length; i++) {
            Vector3 vertex = originalVertices[i];

            float axisCoordinate = Vector3.Dot(vertex, localAxis);

            if (axisCoordinate <= 0f) {
                continue;
            }

            float axisWeight = Mathf.Clamp01(axisCoordinate / 0.5f);
            axisWeight = axisWeight * axisWeight;

            float topMultiplier = GetTopProtectionMultiplier(vertex);

            targetVertices[i] += localAxis * appliedDepth * axisWeight * topMultiplier;
            affectedVertices++;
        }
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
        bool hasDeformation = currentDeformationAmount > 0f;
        float speed = hasDeformation ? deformationSpeed : returnSpeed;

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
        currentDeformationAmount = 0f;

        if (deformingMesh == null || originalVertices == null) {
            return;
        }

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
        }

        deformingMesh.vertices = currentVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Debug.Log("Stress ball contact weighted deformation reset");
    }
}