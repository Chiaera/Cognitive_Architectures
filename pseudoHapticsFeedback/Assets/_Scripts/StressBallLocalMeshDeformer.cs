using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallLocalMeshDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read per-finger pressure and pressure direction")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Header("Visual Target")]
    [Tooltip("The mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Surface Projection")]
    [Tooltip("Keep contact deformation anchored to the virtual ball surface")]
    public bool useSurfaceProjection = true;

    [Tooltip("Manual visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.045f;

    [Tooltip("Small offset from the surface to keep indentations visually stable")]
    public float surfaceOffsetMeters = 0.001f;

    [Header("Local Deformation")]
    [Tooltip("World-space radius around each contact point affected by the indentation")]
    public float indentationRadiusMeters = 0.030f;

    [Tooltip("World-space maximum indentation depth applied by each finger")]
    public float indentationDepthMeters = 0.020f;

    [Tooltip("Global mismatch gain applied to local deformation")]
    public float deformationGain = 1.8f;

    [Tooltip("Minimum finger pressure required to create a local indentation")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.04f;

    [Header("Pressure Clamp")]
    [Tooltip("Maximum effective pressure used for mesh deformation")]
    [Range(0f, 1f)]
    public float maxEffectivePressure = 0.70f;

    [Tooltip("Softens high pressure values to avoid excessive squishy deformation")]
    [Range(0.1f, 2f)]
    public float pressureResponseCurve = 0.75f;

    [Header("Elastic Return")]
    [Tooltip("How fast the mesh moves toward the target deformation")]
    public float deformationSpeed = 18f;

    [Tooltip("How fast the mesh returns to the original shape")]
    public float returnSpeed = 10f;

    [Header("Debug")]
    public int affectedVertices = 0;
    public float averageAppliedPressure = 0f;
    public float localIndentationRadius = 0f;
    public float localIndentationDepth = 0f;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private Vector3[] targetVertices;

    void Start() {
        // Get references if they were not assigned manually
        if (pressureAnalyzer == null) {
            pressureAnalyzer = GetComponent<StressBallFingerPressureAnalyzer>();
        }

        if (ballMeshFilter == null) {
            ballMeshFilter = GetComponentInChildren<MeshFilter>();
        }

        if (ballMeshFilter == null) {
            Debug.LogWarning("Stress ball local mesh deformer missing MeshFilter");
            enabled = false;
            return;
        }

        // Duplicate the mesh so the original asset is not modified
        deformingMesh = Instantiate(ballMeshFilter.mesh);
        ballMeshFilter.mesh = deformingMesh;

        originalVertices = deformingMesh.vertices;
        currentVertices = new Vector3[originalVertices.Length];
        targetVertices = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
        }

        Debug.Log("Stress ball local mesh deformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || deformingMesh == null) {
            return;
        }

        UpdateLocalDeformationValues();
        BuildTargetDeformation();
        ApplyElasticMeshUpdate();
    }

    void UpdateLocalDeformationValues() {
        // Convert world-space deformation values into local mesh-space values
        Transform visualTransform = ballMeshFilter.transform;

        float averageScale = (
            visualTransform.lossyScale.x +
            visualTransform.lossyScale.y +
            visualTransform.lossyScale.z
        ) / 3f;

        if (averageScale <= 0.0001f) {
            averageScale = 1f;
        }

        localIndentationRadius = indentationRadiusMeters / averageScale;
        localIndentationDepth = indentationDepthMeters / averageScale;
    }

    void BuildTargetDeformation() {
        // Reset target vertices to the original shape before adding finger indentations
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
        }

        affectedVertices = 0;
        averageAppliedPressure = 0f;

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();
        Vector3[] fingerDirections = pressureAnalyzer.GetAllFingerPressureDirections();

        if (!pressureAnalyzer.squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        float totalPressure = 0f;
        int activeFingerCount = 0;

        for (int fingerIndex = 0; fingerIndex < fingerPressures.Length; fingerIndex++) {
            float pressure = fingerPressures[fingerIndex];

            if (pressure < pressureActivationThreshold) {
                continue;
            }

            float effectivePressure = GetEffectivePressure(pressure);

            ApplyFingerIndentation(
                fingerPositions[fingerIndex],
                fingerDirections[fingerIndex],
                effectivePressure
            );

            totalPressure += effectivePressure;
            activeFingerCount++;
        }

        if (activeFingerCount > 0) {
            averageAppliedPressure = totalPressure / activeFingerCount;
        }
    }

    float GetEffectivePressure(float rawPressure) {
        // Clamp and soften pressure to avoid excessive squishy deformation
        float clampedPressure = Mathf.Clamp(rawPressure, 0f, maxEffectivePressure);
        float normalizedPressure = clampedPressure / Mathf.Max(maxEffectivePressure, 0.0001f);

        float curvedPressure = Mathf.Pow(normalizedPressure, pressureResponseCurve);

        return Mathf.Clamp01(curvedPressure * maxEffectivePressure);
    }

    void ApplyFingerIndentation(Vector3 fingerWorldPosition, Vector3 pressureWorldDirection, float pressure) {
        // Convert contact point and pressure direction into the local space of the visual mesh
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 contactWorldPosition = GetContactWorldPosition(fingerWorldPosition);

        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localPressureDirection = visualTransform.InverseTransformDirection(pressureWorldDirection).normalized;

        float appliedDepth = localIndentationDepth * pressure * deformationGain;

        for (int i = 0; i < targetVertices.Length; i++) {
            float distance = Vector3.Distance(originalVertices[i], localContactPosition);

            if (distance > localIndentationRadius) {
                continue;
            }

            float falloff = 1f - Mathf.Clamp01(distance / localIndentationRadius);
            falloff = falloff * falloff;

            Vector3 indentationOffset = localPressureDirection * appliedDepth * falloff;

            targetVertices[i] += indentationOffset;
            affectedVertices++;
        }
    }

    Vector3 GetContactWorldPosition(Vector3 fingerWorldPosition) {
        // Project the fingertip onto the virtual ball surface to prevent internal dragging
        if (!useSurfaceProjection) {
            return fingerWorldPosition;
        }

        Vector3 ballCenter = transform.position;
        Vector3 centerToFinger = fingerWorldPosition - ballCenter;

        if (centerToFinger.sqrMagnitude < 0.0001f) {
            return fingerWorldPosition;
        }

        Vector3 surfaceDirection = centerToFinger.normalized;
        float projectedRadius = ballRadiusMeters + surfaceOffsetMeters;

        return ballCenter + surfaceDirection * projectedRadius;
    }

    void ApplyElasticMeshUpdate() {
        // Move current vertices toward the target deformation, then update the mesh
        float speed = pressureAnalyzer.averagePressure > pressureActivationThreshold
            ? deformationSpeed
            : returnSpeed;

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

    public void ResetMesh() {
        // Restore the original mesh shape
        if (deformingMesh == null) {
            return;
        }

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
        }

        deformingMesh.vertices = currentVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Debug.Log("Stress ball mesh deformation reset");
    }
}