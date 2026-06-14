using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallCompensatedSqueezeDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read finger pressure and active contact count")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Squeeze detector used to read global hand squeeze")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("The visual object of the stress ball")]
    public Transform ballVisual;

    [Tooltip("The mesh filter of the stress ball visual")]
    public MeshFilter ballMeshFilter;

    [Header("Activation")]
    [Tooltip("Minimum number of active fingers required for full-hand squeeze")]
    public int minimumActiveFingers = 3;

    [Tooltip("Minimum squeeze amount required to start deformation")]
    [Range(0f, 1f)]
    public float squeezeActivationThreshold = 0.35f;

    [Tooltip("Minimum average pressure required to start deformation")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.08f;

    [Header("Compensated Global Deformation")]
    [Tooltip("Maximum compression along the main pressure direction")]
    [Range(0f, 0.5f)]
    public float maxCompression = 0.18f;

    [Tooltip("Maximum sideways expansion used to preserve a soft-ball feeling")]
    [Range(0f, 0.5f)]
    public float maxBulge = 0.08f;

    [Tooltip("Overall deformation gain")]
    public float deformationGain = 1.0f;

    [Tooltip("Softens high deformation values")]
    [Range(0.1f, 2f)]
    public float responseCurve = 0.8f;

    [Header("Saturation")]
    [Tooltip("Maximum effective squeeze value")]
    [Range(0f, 1f)]
    public float maxEffectiveSqueeze = 0.85f;

    [Tooltip("Maximum effective pressure value")]
    [Range(0f, 1f)]
    public float maxEffectivePressure = 0.65f;

    [Header("Elastic Return")]
    [Tooltip("How fast the ball deforms")]
    public float deformationSpeed = 14f;

    [Tooltip("How fast the ball returns to its original shape")]
    public float returnSpeed = 8f;

    [Header("Optional Mesh Detail")]
    [Tooltip("Add a small mesh indentation based on the average pressure direction")]
    public bool useSubtleMeshIndentation = true;

    [Tooltip("World-space indentation depth used as a secondary detail")]
    public float subtleIndentationDepthMeters = 0.006f;

    [Tooltip("World-space indentation radius used as a secondary detail")]
    public float subtleIndentationRadiusMeters = 0.035f;

    [Header("Debug")]
    public float currentSqueeze = 0f;
    public float currentPressure = 0f;
    public int currentActiveFingers = 0;
    public float targetDeformation = 0f;
    public Vector3 currentCompressionAxis = Vector3.forward;

    private Vector3 originalVisualScale;
    private Vector3 currentVisualScale;

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

        if (ballVisual == null && transform.childCount > 0) {
            ballVisual = transform.GetChild(0);
        }

        if (ballMeshFilter == null && ballVisual != null) {
            ballMeshFilter = ballVisual.GetComponent<MeshFilter>();
        }

        if (ballVisual == null) {
            Debug.LogWarning("Compensated squeeze deformer missing ball visual");
            enabled = false;
            return;
        }

        originalVisualScale = ballVisual.localScale;
        currentVisualScale = originalVisualScale;

        InitializeMesh();

        Debug.Log("Stress ball compensated squeeze deformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || squeezeDetector == null || ballVisual == null) {
            return;
        }

        UpdateInputValues();
        UpdateCompensatedDeformation();
        ApplyVisualScale();

        if (useSubtleMeshIndentation) {
            ApplySubtleMeshIndentation();
        }
    }

    void InitializeMesh() {
        if (ballMeshFilter == null) {
            return;
        }

        deformingMesh = Instantiate(ballMeshFilter.mesh);
        ballMeshFilter.mesh = deformingMesh;

        originalVertices = deformingMesh.vertices;
        currentVertices = new Vector3[originalVertices.Length];
        targetVertices = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
        }
    }

    void UpdateInputValues() {
        currentSqueeze = squeezeDetector.squeezeNormalized;
        currentPressure = pressureAnalyzer.averagePressure;
        currentActiveFingers = pressureAnalyzer.activeFingerCount;

        if (pressureAnalyzer.averagePressureDirection.sqrMagnitude > 0.0001f) {
            currentCompressionAxis = pressureAnalyzer.averagePressureDirection.normalized;
        } else {
            currentCompressionAxis = Vector3.forward;
        }
    }

    void UpdateCompensatedDeformation() {
        bool hasEnoughContact = currentActiveFingers >= minimumActiveFingers;
        bool hasEnoughSqueeze = currentSqueeze >= squeezeActivationThreshold;
        bool hasEnoughPressure = currentPressure >= pressureActivationThreshold;

        if (!hasEnoughContact || !hasEnoughSqueeze || !hasEnoughPressure) {
            targetDeformation = 0f;
            return;
        }

        float squeezeAmount = Mathf.InverseLerp(
            squeezeActivationThreshold,
            maxEffectiveSqueeze,
            currentSqueeze
        );

        float pressureAmount = Mathf.InverseLerp(
            pressureActivationThreshold,
            maxEffectivePressure,
            currentPressure
        );

        squeezeAmount = Mathf.Clamp01(squeezeAmount);
        pressureAmount = Mathf.Clamp01(pressureAmount);

        float combinedAmount = squeezeAmount * 0.6f + pressureAmount * 0.4f;
        targetDeformation = Mathf.Pow(combinedAmount, responseCurve) * deformationGain;
        targetDeformation = Mathf.Clamp01(targetDeformation);
    }

    void ApplyVisualScale() {
        float speed = targetDeformation > 0f ? deformationSpeed : returnSpeed;

        float compression = maxCompression * targetDeformation;
        float bulge = maxBulge * targetDeformation;

        Vector3 targetScale = new Vector3(
            originalVisualScale.x * (1f + bulge),
            originalVisualScale.y * (1f - compression),
            originalVisualScale.z * (1f + bulge)
        );

        currentVisualScale = Vector3.Lerp(
            currentVisualScale,
            targetScale,
            Time.deltaTime * speed
        );

        ballVisual.localScale = currentVisualScale;
    }

    void ApplySubtleMeshIndentation() {
        if (deformingMesh == null || originalVertices == null) {
            return;
        }

        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
        }

        if (targetDeformation > 0f) {
            ApplyAverageIndentation();
        }

        float speed = targetDeformation > 0f ? deformationSpeed : returnSpeed;

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

    void ApplyAverageIndentation() {
        Transform visualTransform = ballMeshFilter.transform;

        float averageScale = (
            visualTransform.lossyScale.x +
            visualTransform.lossyScale.y +
            visualTransform.lossyScale.z
        ) / 3f;

        if (averageScale <= 0.0001f) {
            averageScale = 1f;
        }

        float localRadius = subtleIndentationRadiusMeters / averageScale;
        float localDepth = subtleIndentationDepthMeters / averageScale;

        Vector3 worldContactPoint = transform.position - currentCompressionAxis * 0.045f;
        Vector3 localContactPoint = visualTransform.InverseTransformPoint(worldContactPoint);
        Vector3 localIndentDirection = visualTransform.InverseTransformDirection(currentCompressionAxis).normalized;

        float appliedDepth = localDepth * targetDeformation;

        for (int i = 0; i < targetVertices.Length; i++) {
            float distance = Vector3.Distance(originalVertices[i], localContactPoint);

            if (distance > localRadius) {
                continue;
            }

            float falloff = 1f - Mathf.Clamp01(distance / localRadius);
            falloff = falloff * falloff;

            targetVertices[i] += localIndentDirection * appliedDepth * falloff;
        }
    }

    public void ResetDeformation() {
        targetDeformation = 0f;
        currentVisualScale = originalVisualScale;
        ballVisual.localScale = originalVisualScale;

        if (deformingMesh != null && originalVertices != null) {
            for (int i = 0; i < originalVertices.Length; i++) {
                currentVertices[i] = originalVertices[i];
                targetVertices[i] = originalVertices[i];
            }

            deformingMesh.vertices = currentVertices;
            deformingMesh.RecalculateNormals();
            deformingMesh.RecalculateBounds();
        }

        Debug.Log("Stress ball compensated squeeze deformation reset");
    }
}