using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallUnifiedDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read per-finger pressure and direction")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Squeeze detector used to read global hand squeeze")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("The mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Ball Settings")]
    [Tooltip("Manual visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.045f;

    [Tooltip("Keep all contact points anchored to the virtual ball surface")]
    public bool useSurfaceProjection = true;

    [Tooltip("Small surface offset used to stabilize contact deformation")]
    public float surfaceOffsetMeters = 0.001f;

    [Header("Global Squeeze")]
    [Tooltip("How much the full-hand squeeze compresses the ball")]
    public float globalCompressionDepthMeters = 0.018f;

    [Tooltip("How much the ball expands sideways when compressed")]
    public float globalBulgeAmountMeters = 0.008f;

    [Tooltip("Minimum squeeze value required to activate global deformation")]
    [Range(0f, 1f)]
    public float globalSqueezeActivationThreshold = 0.35f;

    [Tooltip("Maximum squeeze value used for global deformation")]
    [Range(0f, 1f)]
    public float maxEffectiveSqueeze = 0.85f;

    [Header("Local Finger Detail")]
    [Tooltip("World-space radius around each contact point affected by local indentation")]
    public float localIndentationRadiusMeters = 0.028f;

    [Tooltip("World-space maximum local indentation depth")]
    public float localIndentationDepthMeters = 0.014f;

    [Tooltip("Minimum finger pressure required to create local indentation")]
    [Range(0f, 1f)]
    public float localPressureActivationThreshold = 0.04f;

    [Tooltip("Maximum pressure used for local indentation")]
    [Range(0f, 1f)]
    public float maxEffectiveFingerPressure = 0.60f;

    [Tooltip("Maximum number of local contacts used at the same time")]
    [Range(1, 5)]
    public int maxActiveLocalContacts = 3;

    [Header("Shape Response")]
    [Tooltip("Softens high pressure values")]
    [Range(0.1f, 2f)]
    public float pressureResponseCurve = 0.85f;

    [Tooltip("Global deformation strength")]
    public float globalDeformationGain = 1.0f;

    [Tooltip("Local deformation strength")]
    public float localDeformationGain = 1.0f;

    [Header("Elastic Return")]
    [Tooltip("How fast the mesh moves toward the target deformation")]
    public float deformationSpeed = 18f;

    [Tooltip("How fast the mesh returns to the original shape")]
    public float returnSpeed = 10f;

    [Header("Debug")]
    public int activeFingerCount = 0;
    public float currentSqueeze = 0f;
    public float currentGlobalAmount = 0f;
    public int affectedVertices = 0;
    public float localIndentationRadius = 0f;
    public float localIndentationDepth = 0f;
    public float globalCompressionDepth = 0f;
    public float globalBulgeAmount = 0f;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private Vector3[] targetVertices;

    private struct FingerContact {
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
            Debug.LogWarning("Stress ball unified deformer missing MeshFilter");
            enabled = false;
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

        Debug.Log("Stress ball unified deformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || squeezeDetector == null || deformingMesh == null) {
            return;
        }

        UpdateLocalSpaceValues();
        BuildTargetDeformation();
        ApplyElasticMeshUpdate();
    }

    void UpdateLocalSpaceValues() {
        Transform visualTransform = ballMeshFilter.transform;

        float averageScale = (
            visualTransform.lossyScale.x +
            visualTransform.lossyScale.y +
            visualTransform.lossyScale.z
        ) / 3f;

        if (averageScale <= 0.0001f) {
            averageScale = 1f;
        }

        localIndentationRadius = localIndentationRadiusMeters / averageScale;
        localIndentationDepth = localIndentationDepthMeters / averageScale;
        globalCompressionDepth = globalCompressionDepthMeters / averageScale;
        globalBulgeAmount = globalBulgeAmountMeters / averageScale;
    }

    void BuildTargetDeformation() {
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
        }

        affectedVertices = 0;
        activeFingerCount = 0;

        FingerContact[] selectedContacts = GetSelectedFingerContacts();

        currentSqueeze = squeezeDetector.squeezeNormalized;

        bool hasEnoughContactForGlobalSqueeze = activeFingerCount >= 3;

        if (hasEnoughContactForGlobalSqueeze) {
            currentGlobalAmount = GetEffectiveSqueezeAmount(currentSqueeze);
        } else {
            currentGlobalAmount = 0f;
        }

        Vector3 globalCompressionAxis = GetGlobalCompressionAxis(selectedContacts);

        if (currentGlobalAmount > 0f) {
            ApplyGlobalSqueeze(globalCompressionAxis, currentGlobalAmount);
        }

        ApplyLocalFingerDetails(selectedContacts);
    }

    float GetEffectiveSqueezeAmount(float rawSqueeze) {
        if (rawSqueeze < globalSqueezeActivationThreshold) {
            return 0f;
        }

        float normalized = Mathf.InverseLerp(
            globalSqueezeActivationThreshold,
            maxEffectiveSqueeze,
            rawSqueeze
        );

        normalized = Mathf.Clamp01(normalized);

        return Mathf.Pow(normalized, pressureResponseCurve);
    }

    FingerContact[] GetSelectedFingerContacts() {
        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();
        Vector3[] fingerDirections = pressureAnalyzer.GetAllFingerPressureDirections();

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return new FingerContact[0];
        }

        FingerContact[] contacts = new FingerContact[5];
        int count = 0;

        for (int i = 0; i < fingerPressures.Length; i++) {
            float pressure = fingerPressures[i];

            if (pressure < localPressureActivationThreshold) {
                continue;
            }

            FingerContact contact = new FingerContact();
            contact.index = i;
            contact.pressure = GetEffectiveFingerPressure(pressure);
            contact.position = fingerPositions[i];
            contact.direction = fingerDirections[i];

            contacts[count] = contact;
            count++;
        }

        activeFingerCount = count;

        SortContactsByPressure(contacts, count);

        int selectedCount = Mathf.Min(count, maxActiveLocalContacts);
        FingerContact[] selectedContacts = new FingerContact[selectedCount];

        for (int i = 0; i < selectedCount; i++) {
            selectedContacts[i] = contacts[i];
        }

        return selectedContacts;
    }

    float GetEffectiveFingerPressure(float rawPressure) {
        float clampedPressure = Mathf.Clamp(rawPressure, 0f, maxEffectiveFingerPressure);
        float normalizedPressure = clampedPressure / Mathf.Max(maxEffectiveFingerPressure, 0.0001f);

        float curvedPressure = Mathf.Pow(normalizedPressure, pressureResponseCurve);

        return Mathf.Clamp01(curvedPressure * maxEffectiveFingerPressure);
    }

    void SortContactsByPressure(FingerContact[] contacts, int count) {
        for (int i = 0; i < count - 1; i++) {
            for (int j = i + 1; j < count; j++) {
                if (contacts[j].pressure > contacts[i].pressure) {
                    FingerContact temp = contacts[i];
                    contacts[i] = contacts[j];
                    contacts[j] = temp;
                }
            }
        }
    }

    Vector3 GetGlobalCompressionAxis(FingerContact[] contacts) {
        if (contacts == null || contacts.Length == 0) {
            return Vector3.forward;
        }

        Vector3 weightedDirection = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < contacts.Length; i++) {
            weightedDirection += contacts[i].direction * contacts[i].pressure;
            totalWeight += contacts[i].pressure;
        }

        if (totalWeight <= 0.0001f || weightedDirection.sqrMagnitude <= 0.0001f) {
            return Vector3.forward;
        }

        return weightedDirection.normalized;
    }

    void ApplyGlobalSqueeze(Vector3 compressionAxisWorld, float squeezeAmount) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 localAxis = visualTransform.InverseTransformDirection(compressionAxisWorld).normalized;

        if (localAxis.sqrMagnitude <= 0.0001f) {
            return;
        }

        for (int i = 0; i < targetVertices.Length; i++) {
            Vector3 vertex = originalVertices[i];

            float axisCoordinate = Vector3.Dot(vertex, localAxis);
            Vector3 axisComponent = localAxis * axisCoordinate;
            Vector3 perpendicularComponent = vertex - axisComponent;

            float normalizedAxisDistance = Mathf.Clamp01(Mathf.Abs(axisCoordinate) / 0.5f);
            float centerWeight = 1f - normalizedAxisDistance;

            float compression = globalCompressionDepth * squeezeAmount * globalDeformationGain;
            float bulge = globalBulgeAmount * squeezeAmount * globalDeformationGain;

            Vector3 compressionOffset = -localAxis * Mathf.Sign(axisCoordinate) * compression * normalizedAxisDistance;
            Vector3 bulgeOffset = Vector3.zero;

            if (perpendicularComponent.sqrMagnitude > 0.0001f) {
                bulgeOffset = perpendicularComponent.normalized * bulge * centerWeight;
            }

            targetVertices[i] += compressionOffset + bulgeOffset;
            affectedVertices++;
        }
    }

    void ApplyLocalFingerDetails(FingerContact[] contacts) {
        for (int i = 0; i < contacts.Length; i++) {
            ApplySingleLocalIndentation(contacts[i]);
        }
    }

    void ApplySingleLocalIndentation(FingerContact contact) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 contactWorldPosition = GetContactWorldPosition(contact.position);
        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localPressureDirection = visualTransform.InverseTransformDirection(contact.direction).normalized;

        float appliedDepth = localIndentationDepth * contact.pressure * localDeformationGain;

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
        bool hasActiveDeformation = currentGlobalAmount > 0f || activeFingerCount > 0;

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

    public void ResetMesh() {
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

        Debug.Log("Stress ball unified deformation reset");
    }
}