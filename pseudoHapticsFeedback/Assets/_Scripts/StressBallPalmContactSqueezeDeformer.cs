using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallPalmContactSqueezeDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read per-finger pressure values")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Detector used to read palm and fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Ball Settings")]
    [Tooltip("Visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Tooltip("Project contacts onto the ideal sphere surface")]
    public bool projectContactsToSurface = true;

    [Tooltip("Surface offset in meters")]
    public float surfaceOffsetMeters = 0f;

    [Header("Palm Contact")]
    [Tooltip("Enable broad palm-based deformation")]
    public bool usePalmContact = true;

    [Tooltip("Distance outside the sphere where palm contact starts being considered")]
    public float palmContactToleranceMeters = 0.025f;

    [Tooltip("Maximum palm penetration used to normalize palm deformation")]
    public float palmMaxPenetrationMeters = 0.055f;

    [Tooltip("Radius of the broad palm deformation area")]
    public float palmDeformationRadiusMeters = 0.060f;

    [Tooltip("Maximum palm indentation depth")]
    public float palmMaxDepthMeters = 0.030f;

    [Tooltip("How much squeeze amplifies the palm deformation")]
    [Range(0f, 1f)]
    public float squeezeInfluenceOnPalm = 0.45f;

    [Tooltip("Use an offset point to approximate the visible palm surface")]
    public bool usePalmSurfaceProxy = true;

    [Tooltip("Distance from the palm joint toward the sphere center used to approximate the palm surface")]
    public float palmSurfaceOffsetMeters = 0.045f;

    [Header("Finger Details")]
    [Tooltip("Enable local finger detail deformation")]
    public bool useFingerDetails = true;

    [Tooltip("Use thumb deformation")]
    public bool useThumb = true;

    [Tooltip("Minimum pressure required to activate a finger indentation")]
    [Range(0f, 1f)]
    public float fingerPressureThreshold = 0.040f;

    [Tooltip("Pressure value that maps to maximum finger depth")]
    [Range(0f, 1f)]
    public float fingerPressureForMaxDepth = 0.75f;

    [Tooltip("Radius of each local finger deformation area")]
    public float fingerDeformationRadiusMeters = 0.026f;

    [Tooltip("Maximum indentation depth for each finger")]
    public float fingerMaxDepthMeters = 0.010f;

    [Tooltip("Higher values make finger deformation more resistant at low pressure")]
    [Range(0.2f, 2f)]
    public float fingerResponseCurve = 1.10f;

    [Header("Global Safety")]
    [Tooltip("Maximum displacement allowed for each vertex")]
    public float maxTotalVertexDepthMeters = 0.030f;

    [Tooltip("Clamp total vertex displacement to avoid broken shapes")]
    public bool clampTotalVertexDepth = true;

    [Header("Top Anchor Protection")]
    [Tooltip("Reduce deformation near the upper support")]
    public bool protectTopArea = true;

    [Tooltip("Local Y value where top protection starts")]
    public float topProtectionStartLocalY = 0.30f;

    [Tooltip("Local Y value where top protection is strongest")]
    public float topProtectionFullLocalY = 0.52f;

    [Tooltip("Minimum deformation multiplier near the support")]
    [Range(0f, 1f)]
    public float topAreaMinimumMultiplier = 0.55f;

    [Header("Shape Quality")]
    [Tooltip("Higher values make the deformation center more focused")]
    [Range(0.5f, 4f)]
    public float falloffPower = 1.35f;

    [Tooltip("Blend between focused indentation and softer broad deformation")]
    [Range(0f, 1f)]
    public float softnessBlend = 0.55f;

    [Header("Elastic Motion")]
    [Tooltip("How fast the ball deforms")]
    public float deformationSpeed = 16f;

    [Tooltip("How fast the ball returns to its original shape")]
    public float returnSpeed = 18f;

    [Header("Debug")]
    public float palmAmount = 0f;
    public float squeezeAmount = 0f;
    public int activeFingerCount = 0;
    public int affectedVertices = 0;
    public float localPalmRadius = 0f;
    public float localPalmDepth = 0f;
    public float localFingerRadius = 0f;
    public float localFingerDepth = 0f;
    public float localMaxTotalDepth = 0f;
    public bool palmContactActive = false;
    public float palmRawDistance = 0f;
    public float palmSurfaceDistance = 0f;

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
            Debug.LogWarning("Palm contact squeeze deformer missing MeshFilter");
            enabled = false;
            return;
        }

        InitializeMesh();

        Debug.Log("Palm contact squeeze deformer initialized");
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

        localPalmRadius = palmDeformationRadiusMeters / averageScale;
        localPalmDepth = palmMaxDepthMeters / averageScale;
        localFingerRadius = fingerDeformationRadiusMeters / averageScale;
        localFingerDepth = fingerMaxDepthMeters / averageScale;
        localMaxTotalDepth = maxTotalVertexDepthMeters / averageScale;
    }

    void BuildTargetDeformation() {
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }

        activeFingerCount = 0;
        affectedVertices = 0;
        palmAmount = 0f;
        squeezeAmount = squeezeDetector.squeezeNormalized;

        if (usePalmContact) {
            ApplyPalmContactDeformation();
        }

        if (useFingerDetails) {
            ApplyFingerDetailDeformations();
        }

        for (int i = 0; i < targetVertices.Length; i++) {
            Vector3 finalOffset = accumulatedOffsets[i];

            if (clampTotalVertexDepth && finalOffset.magnitude > localMaxTotalDepth) {
                finalOffset = finalOffset.normalized * localMaxTotalDepth;
            }

            targetVertices[i] = originalVertices[i] + finalOffset;
        }
    }

    void ApplyPalmContactDeformation() {
        palmContactActive = false;

        if (!squeezeDetector.TryGetPalmPosition(out Vector3 palmWorldPosition)) {
            return;
        }

        Vector3 center = transform.position;
        Vector3 palmToCenter = center - palmWorldPosition;

        if (palmToCenter.sqrMagnitude < 0.0001f) {
            return;
        }

        palmRawDistance = Vector3.Distance(palmWorldPosition, center);

        Vector3 palmSurfaceWorldPosition = palmWorldPosition;

        if (usePalmSurfaceProxy) {
            palmSurfaceWorldPosition = palmWorldPosition + palmToCenter.normalized * palmSurfaceOffsetMeters;
        }

        palmSurfaceDistance = Vector3.Distance(palmSurfaceWorldPosition, center);

        float contactStartDistance = ballRadiusMeters + palmContactToleranceMeters;

        if (palmSurfaceDistance > contactStartDistance) {
            palmAmount = 0f;
            return;
        }

        palmContactActive = true;

        float penetrationMeters = contactStartDistance - palmSurfaceDistance;

        float palmContactAmount = Mathf.Clamp01(
            penetrationMeters / Mathf.Max(palmMaxPenetrationMeters, 0.0001f)
        );

        float squeezeContribution = Mathf.Clamp01(squeezeAmount);

        palmAmount = Mathf.Clamp01(
            palmContactAmount * (1f - squeezeInfluenceOnPalm) +
            squeezeContribution * squeezeInfluenceOnPalm
        );

        if (palmAmount <= 0.001f) {
            return;
        }

        Vector3 palmContactWorldPosition = GetSurfaceContactPoint(palmSurfaceWorldPosition);

        ApplyRadialPatch(
            palmContactWorldPosition,
            localPalmRadius,
            localPalmDepth,
            palmAmount,
            true
        );
    }

    void ApplyFingerDetailDeformations() {
        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        for (int fingerIndex = 0; fingerIndex < fingerPressures.Length; fingerIndex++) {
            if (!useThumb && fingerIndex == 0) {
                continue;
            }

            float pressure = fingerPressures[fingerIndex];

            if (pressure < fingerPressureThreshold) {
                continue;
            }

            float pressure01 = Mathf.InverseLerp(
                fingerPressureThreshold,
                fingerPressureForMaxDepth,
                pressure
            );

            pressure01 = Mathf.Clamp01(pressure01);
            pressure01 = Mathf.Pow(pressure01, fingerResponseCurve);

            Vector3 fingerContactWorldPosition = GetSurfaceContactPoint(fingerPositions[fingerIndex]);

            ApplyRadialPatch(
                fingerContactWorldPosition,
                localFingerRadius,
                localFingerDepth,
                pressure01,
                false
            );

            activeFingerCount++;
        }
    }

    void ApplyRadialPatch(Vector3 contactWorldPosition, float localRadius, float localDepth, float amount, bool broadPatch) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localSphereCenter = visualTransform.InverseTransformPoint(transform.position);

        float appliedDepth = localDepth * amount;

        for (int i = 0; i < originalVertices.Length; i++) {
            Vector3 vertex = originalVertices[i];

            float distance = Vector3.Distance(vertex, localContactPosition);

            if (distance > localRadius) {
                continue;
            }

            float normalizedDistance = Mathf.Clamp01(distance / localRadius);
            float falloff = 1f - normalizedDistance;

            float smoothFalloff = SmoothFalloff(falloff);
            float shapedFalloff = Mathf.Pow(falloff, falloffPower);

            float finalFalloff = broadPatch
                ? Mathf.Lerp(shapedFalloff, smoothFalloff, 0.75f)
                : Mathf.Lerp(shapedFalloff, smoothFalloff, softnessBlend);

            float topMultiplier = GetTopProtectionMultiplier(vertex);

            // Use one stable inward direction for the whole contact patch.
            // This creates a clearer indentation under the finger or palm.
            Vector3 inwardDirection = localSphereCenter - localContactPosition;

            if (inwardDirection.sqrMagnitude < 0.0001f) {
                continue;
            }

            inwardDirection.Normalize();

            Vector3 offset = inwardDirection * appliedDepth * finalFalloff * topMultiplier;

            accumulatedOffsets[i] += offset;
            affectedVertices++;
        }
    }

    Vector3 GetSurfaceContactPoint(Vector3 worldPosition) {
        if (!projectContactsToSurface) {
            return worldPosition;
        }

        Vector3 center = transform.position;
        Vector3 centerToPoint = worldPosition - center;

        if (centerToPoint.sqrMagnitude < 0.0001f) {
            return center + Vector3.forward * (ballRadiusMeters + surfaceOffsetMeters);
        }

        return center + centerToPoint.normalized * (ballRadiusMeters + surfaceOffsetMeters);
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
        bool hasActiveDeformation = palmAmount > 0.001f || activeFingerCount > 0;
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

        palmAmount = 0f;
        activeFingerCount = 0;

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }

        deformingMesh.vertices = currentVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Debug.Log("Palm contact squeeze deformation reset");
    }
}