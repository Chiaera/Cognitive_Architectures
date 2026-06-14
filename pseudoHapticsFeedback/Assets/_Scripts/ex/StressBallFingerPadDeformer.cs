using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallFingerPadDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read per-finger pressure and direction")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Squeeze detector used to read fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Ball Settings")]
    [Tooltip("Visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Tooltip("Project fingertip contacts onto the ball surface")]
    public bool projectContactsToSurface = true;

    [Tooltip("Small offset used to keep the contact point stable on the surface")]
    public float surfaceOffsetMeters = 0.001f;

    [Header("Finger Pad Shape")]
    [Tooltip("Length of the fingertip imprint in meters")]
    public float padLengthMeters = 0.034f;

    [Tooltip("Width of the fingertip imprint in meters")]
    public float padWidthMeters = 0.018f;

    [Tooltip("Maximum indentation depth in meters")]
    public float padDepthMeters = 0.018f;

    [Tooltip("How much the finger pad indentation is amplified")]
    public float deformationGain = 1.2f;

    [Tooltip("Minimum pressure required to deform the ball")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.035f;

    [Tooltip("Maximum pressure used for deformation")]
    [Range(0f, 1f)]
    public float maxEffectivePressure = 0.70f;

    [Tooltip("Higher values make the material feel more resistant at low pressure")]
    [Range(0.2f, 2f)]
    public float pressureResponseCurve = 0.95f;

    [Header("Contact Selection")]
    [Tooltip("Maximum number of fingers that can deform the ball at the same time")]
    [Range(1, 5)]
    public int maxActiveFingerPads = 4;

    [Tooltip("Ignore the thumb for now if it creates unstable deformation")]
    public bool useThumb = true;

    [Header("Top Anchor Protection")]
    [Tooltip("Reduce deformation near the top connector")]
    public bool protectTopArea = true;

    [Tooltip("Local Y value where the top protection starts")]
    public float topProtectionStartLocalY = 0.26f;

    [Tooltip("Local Y value where the top protection is strongest")]
    public float topProtectionFullLocalY = 0.50f;

    [Tooltip("Minimum deformation multiplier near the top connector")]
    [Range(0f, 1f)]
    public float topAreaMinimumMultiplier = 0.35f;

    [Header("Elastic Motion")]
    [Tooltip("How fast the mesh deforms")]
    public float deformationSpeed = 18f;

    [Tooltip("How fast the mesh returns to its original shape")]
    public float returnSpeed = 16f;

    [Header("Debug")]
    public int activeFingerPadCount = 0;
    public int affectedVertices = 0;
    public float localPadLength = 0f;
    public float localPadWidth = 0f;
    public float localPadDepth = 0f;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private Vector3[] targetVertices;

    private struct FingerPadContact {
        public int index;
        public float pressure;
        public Vector3 position;
        public Vector3 direction;
    }

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
            Debug.LogWarning("Finger pad deformer missing MeshFilter");
            enabled = false;
            return;
        }

        InitializeMesh();

        Debug.Log("Stress ball finger pad deformer initialized");
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

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
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

        localPadLength = padLengthMeters / averageScale;
        localPadWidth = padWidthMeters / averageScale;
        localPadDepth = padDepthMeters / averageScale;
    }

    void BuildTargetDeformation() {
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
        }

        affectedVertices = 0;
        activeFingerPadCount = 0;

        FingerPadContact[] contacts = GetSelectedFingerPadContacts();

        for (int i = 0; i < contacts.Length; i++) {
            ApplyFingerPadIndentation(contacts[i]);
        }
    }

    FingerPadContact[] GetSelectedFingerPadContacts() {
        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();
        Vector3[] fingerDirections = pressureAnalyzer.GetAllFingerPressureDirections();

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return new FingerPadContact[0];
        }

        FingerPadContact[] contacts = new FingerPadContact[5];
        int count = 0;

        for (int i = 0; i < fingerPressures.Length; i++) {
            if (!useThumb && i == 0) {
                continue;
            }

            float pressure = fingerPressures[i];

            if (pressure < pressureActivationThreshold) {
                continue;
            }

            FingerPadContact contact = new FingerPadContact();
            contact.index = i;
            contact.pressure = GetEffectivePressure(pressure);
            contact.position = fingerPositions[i];
            contact.direction = fingerDirections[i];

            if (contact.direction.sqrMagnitude < 0.0001f) {
                contact.direction = (transform.position - fingerPositions[i]).normalized;
            }

            contacts[count] = contact;
            count++;
        }

        SortContactsByPressure(contacts, count);

        int selectedCount = Mathf.Min(count, maxActiveFingerPads);
        FingerPadContact[] selectedContacts = new FingerPadContact[selectedCount];

        for (int i = 0; i < selectedCount; i++) {
            selectedContacts[i] = contacts[i];
        }

        activeFingerPadCount = selectedCount;

        return selectedContacts;
    }

    float GetEffectivePressure(float rawPressure) {
        float clampedPressure = Mathf.Clamp(rawPressure, 0f, maxEffectivePressure);
        float normalizedPressure = clampedPressure / Mathf.Max(maxEffectivePressure, 0.0001f);
        float curvedPressure = Mathf.Pow(normalizedPressure, pressureResponseCurve);

        return Mathf.Clamp01(curvedPressure * maxEffectivePressure);
    }

    void SortContactsByPressure(FingerPadContact[] contacts, int count) {
        for (int i = 0; i < count - 1; i++) {
            for (int j = i + 1; j < count; j++) {
                if (contacts[j].pressure > contacts[i].pressure) {
                    FingerPadContact temp = contacts[i];
                    contacts[i] = contacts[j];
                    contacts[j] = temp;
                }
            }
        }
    }

    void ApplyFingerPadIndentation(FingerPadContact contact) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 contactWorldPosition = GetSurfaceContactPoint(contact.position);

        // Always push the mesh inward, from the surface contact point toward the ball center.
        // This avoids outward bumps when the tracked finger direction is unstable or inverted.
        Vector3 pressureWorldDirection = (transform.position - contactWorldPosition).normalized;

        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localPressureDirection = visualTransform.InverseTransformDirection(pressureWorldDirection).normalized;

        Vector3 localNormal = -localPressureDirection;

        if (localNormal.sqrMagnitude < 0.0001f) {
            return;
        }

        Vector3 tangentA = Vector3.Cross(localNormal, Vector3.up);

        if (tangentA.sqrMagnitude < 0.0001f) {
            tangentA = Vector3.Cross(localNormal, Vector3.right);
        }

        tangentA.Normalize();

        Vector3 tangentB = Vector3.Cross(localNormal, tangentA).normalized;

        float appliedDepth = localPadDepth * contact.pressure * deformationGain;

        for (int i = 0; i < targetVertices.Length; i++) {
            Vector3 vertexOffset = originalVertices[i] - localContactPosition;

            float x = Vector3.Dot(vertexOffset, tangentA);
            float y = Vector3.Dot(vertexOffset, tangentB);

            float normalizedDistance = Mathf.Sqrt(
                (x * x) / Mathf.Max(localPadWidth * localPadWidth, 0.0001f) +
                (y * y) / Mathf.Max(localPadLength * localPadLength, 0.0001f)
            );

            if (normalizedDistance > 1f) {
                continue;
            }

            float falloff = 1f - Mathf.Clamp01(normalizedDistance);
            falloff = SmoothFalloff(falloff);

            float topMultiplier = GetTopProtectionMultiplier(originalVertices[i]);

            Vector3 indentationOffset = localPressureDirection * appliedDepth * falloff * topMultiplier;

            targetVertices[i] += indentationOffset;
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
            return center + Vector3.forward * (ballRadiusMeters + surfaceOffsetMeters);
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
        bool hasActiveDeformation = activeFingerPadCount > 0;
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
        }

        deformingMesh.vertices = currentVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Debug.Log("Stress ball finger pad deformation reset");
    }
}