using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallPalmBulgeFingerBandDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read finger pressure values")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Detector used to read palm and fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Contact Proxy References")]
    [Tooltip("Use stopped visual proxies as deformation anchors")]
    public bool useContactProxiesForFingerAccents = true;

    [Tooltip("Controller that provides thumb and little contact volume segments")]
    public HandContactVolumeController contactVolumeController;

    [Tooltip("Stopped proxy for the palm contact")]
    public Transform palmContactProxy;

    [Tooltip("Stopped proxy for the thumb contact")]
    public Transform thumbContactProxy;

    [Tooltip("Stopped proxy for the little finger contact")]
    public Transform littleContactProxy;

    [Header("Visual Target")]
    [Tooltip("Mesh filter of the visual stress ball")]
    public MeshFilter ballMeshFilter;

    [Header("Ball Settings")]
    [Tooltip("Visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.065f;

    [Tooltip("Project contact points onto the ideal sphere surface")]
    public bool projectContactsToSurface = true;

    [Tooltip("Surface offset in meters")]
    public float surfaceOffsetMeters = 0f;

    [Header("Palm Compression")]
    public bool usePalmCompression = true;
    public bool usePalmSurfaceProxy = true;
    public float palmSurfaceOffsetMeters = 0.026f;
    public float palmContactToleranceMeters = 0.010f;
    public float palmMaxPenetrationMeters = 0.045f;
    public float palmCompressionRadiusMeters = 0.090f;
    public float palmMaxDepthMeters = 0.026f;

    [Range(0f, 1f)]
    public float squeezeInfluenceOnPalm = 0.03f;

    [Header("Palm Material Bulge")]
    public bool usePalmSideBulge = true;
    public float palmBulgeRadiusMeters = 0.070f;
    public float palmBulgeInnerRadiusMeters = 0.055f;
    public float palmBulgeDepthMeters = 0.0005f;

    [Range(0.5f, 4f)]
    public float palmBulgeFalloffPower = 3.0f;

    [Header("Finger Band")]
    public bool useFingerBand = false;
    public bool useThumbInFingerBand = false;

    [Range(0f, 1f)]
    public float fingerPressureThreshold = 0.040f;

    [Range(0f, 1f)]
    public float fingerPressureForMaxDepth = 0.75f;

    public float fingerBandRadiusMeters = 0.025f;
    public float fingerBandMaxDepthMeters = 0.001f;

    [Range(0f, 1f)]
    public float squeezeInfluenceOnFingerBand = 0.02f;

    [Range(0.2f, 2f)]
    public float fingerBandResponseCurve = 1.2f;

    [Header("Local Finger Accents")]
    public bool useLocalFingerAccents = true;
    public bool useThumbAccent = true;
    public bool useLittleFingerAccent = false;

    [Range(0f, 1f)]
    public float localAccentPressureThreshold = 0.025f;

    [Range(0f, 1f)]
    public float localAccentPressureForMaxDepth = 0.50f;

    [Range(0.2f, 2f)]
    public float localAccentResponseCurve = 0.75f;

    [Header("Local Thumb Patch")]
    [Tooltip("Use a direct local thumb indentation driven by ThumbProxy")]
    public bool useLocalThumbPatch = true;

    [Tooltip("Radius of the local thumb indentation")]
    public float thumbPatchRadiusMeters = 0.026f;

    [Tooltip("Maximum depth of the local thumb indentation")]
    public float thumbPatchMaxDepthMeters = 0.034f;

    [Tooltip("Minimum visible indentation amount when ThumbProxy is active")]
    [Range(0f, 1f)]
    public float thumbPatchMinimumAmount = 0.18f;

    [Tooltip("How much the local thumb indentation follows squeeze")]
    [Range(0f, 1f)]
    public float thumbPatchSqueezeInfluence = 0.35f;

    [Tooltip("How focused the thumb indentation is")]
    [Range(0.5f, 4f)]
    public float thumbPatchFalloffPower = 1.10f;

    [Tooltip("Small surface offset used only for the thumb patch")]
    public float thumbPatchSurfaceOffsetMeters = 0.000f;

    [Header("Palm To Finger Segment Compression")]
    [Tooltip("Use segment compression from palm/contact volume to finger/contact volume")]
    public bool usePalmToFingerCompression = true;

    [Tooltip("Use the contact volume controller segments instead of raw proxy-to-proxy segments")]
    public bool useContactVolumeSegments = true;

    [Tooltip("Enable palm-to-thumb segment compression")]
    public bool usePalmToThumbCompression = true;

    [Tooltip("Enable palm-to-little segment compression")]
    public bool usePalmToLittleCompression = false;

    [Tooltip("Fallback radius of the thumb contact segment")]
    public float thumbSegmentRadiusMeters = 0.022f;

    [Tooltip("Fallback radius of the little finger contact segment")]
    public float littleSegmentRadiusMeters = 0.016f;

    [Tooltip("Maximum inward depth for the thumb segment")]
    public float thumbSegmentMaxDepthMeters = 0.010f;

    [Tooltip("Maximum inward depth for the little segment")]
    public float littleSegmentMaxDepthMeters = 0.010f;

    [Tooltip("Extra multiplier for the thumb volume deformation radius")]
    public float thumbVolumeDeformationRadiusMultiplier = 1.0f;

    [Tooltip("Extra multiplier for the little volume deformation radius")]
    public float littleVolumeDeformationRadiusMultiplier = 1.0f;

    [Tooltip("How much the segment deformation depends on squeeze")]
    [Range(0f, 1f)]
    public float segmentSqueezeInfluence = 0.30f;

    [Tooltip("How focused the segment compression is")]
    [Range(0.5f, 4f)]
    public float segmentFalloffPower = 0.85f;

    [Header("Safety Clamp")]
    public float maxTotalVertexDepthMeters = 0.065f;
    public bool clampTotalVertexDepth = true;

    [Header("Top Anchor Protection")]
    public bool protectTopArea = true;
    public float topProtectionStartLocalY = 0.30f;
    public float topProtectionFullLocalY = 0.52f;

    [Range(0f, 1f)]
    public float topAreaMinimumMultiplier = 0.55f;

    [Header("Shape Quality")]
    [Range(0.5f, 4f)]
    public float indentationFalloffPower = 1.25f;

    [Range(0f, 1f)]
    public float softnessBlend = 0.50f;

    [Header("Elastic Motion")]
    public float deformationSpeed = 10f;
    public float returnSpeed = 10f;

    [Header("Debug")]
    public bool palmContactActive = false;
    public float palmRawDistance = 0f;
    public float palmSurfaceDistance = 0f;
    public float palmAmount = 0f;
    public float fingerBandAmount = 0f;
    public float thumbPatchAmount = 0f;
    public int activeFingerCount = 0;
    public int affectedVertices = 0;
    public bool thumbSegmentActive = false;
    public bool littleSegmentActive = false;
    public bool thumbPatchActive = false;
    public float lastThumbPressure = 0f;
    public float lastThumbSegmentRadiusMeters = 0f;

    private Mesh deformingMesh;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private Vector3[] targetVertices;
    private Vector3[] accumulatedOffsets;

    private float localPalmCompressionRadius;
    private float localPalmMaxDepth;
    private float localPalmBulgeRadius;
    private float localPalmBulgeInnerRadius;
    private float localPalmBulgeDepth;
    private float localFingerBandRadius;
    private float localFingerBandMaxDepth;
    private float localMaxTotalDepth;
    private float localThumbSegmentMaxDepth;
    private float localLittleSegmentMaxDepth;
    private float localThumbPatchRadius;
    private float localThumbPatchMaxDepth;

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
            Debug.LogWarning("Palm bulge finger band deformer missing MeshFilter");
            enabled = false;
            return;
        }

        InitializeMesh();

        Debug.Log("Palm bulge finger band deformer initialized");
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

        localPalmCompressionRadius = palmCompressionRadiusMeters / averageScale;
        localPalmMaxDepth = palmMaxDepthMeters / averageScale;
        localPalmBulgeRadius = palmBulgeRadiusMeters / averageScale;
        localPalmBulgeInnerRadius = palmBulgeInnerRadiusMeters / averageScale;
        localPalmBulgeDepth = palmBulgeDepthMeters / averageScale;
        localFingerBandRadius = fingerBandRadiusMeters / averageScale;
        localFingerBandMaxDepth = fingerBandMaxDepthMeters / averageScale;
        localMaxTotalDepth = maxTotalVertexDepthMeters / averageScale;
        localThumbSegmentMaxDepth = thumbSegmentMaxDepthMeters / averageScale;
        localLittleSegmentMaxDepth = littleSegmentMaxDepthMeters / averageScale;
        localThumbPatchRadius = thumbPatchRadiusMeters / averageScale;
        localThumbPatchMaxDepth = thumbPatchMaxDepthMeters / averageScale;
    }

    void BuildTargetDeformation() {
        for (int i = 0; i < originalVertices.Length; i++) {
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }

        affectedVertices = 0;
        palmAmount = 0f;
        fingerBandAmount = 0f;
        thumbPatchAmount = 0f;
        activeFingerCount = 0;
        thumbSegmentActive = false;
        littleSegmentActive = false;
        thumbPatchActive = false;
        lastThumbPressure = 0f;
        lastThumbSegmentRadiusMeters = 0f;

        if (usePalmCompression) {
            ApplyPalmCompressionAndBulge();
        }

        if (useFingerBand) {
            ApplyFingerBandDeformation();
        }

        if (usePalmToFingerCompression) {
            ApplyPalmToFingerSegmentCompressions();
        }

        if (useLocalFingerAccents && useThumbAccent && useLocalThumbPatch) {
            ApplyLocalThumbPatch();
        }

        for (int i = 0; i < targetVertices.Length; i++) {
            Vector3 finalOffset = accumulatedOffsets[i];

            if (clampTotalVertexDepth && finalOffset.magnitude > localMaxTotalDepth) {
                finalOffset = finalOffset.normalized * localMaxTotalDepth;
            }

            targetVertices[i] = originalVertices[i] + finalOffset;
        }
    }

    void ApplyPalmCompressionAndBulge() {
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
            return;
        }

        palmContactActive = true;

        float penetrationMeters = contactStartDistance - palmSurfaceDistance;

        float contactAmount = Mathf.Clamp01(
            penetrationMeters / Mathf.Max(palmMaxPenetrationMeters, 0.0001f)
        );

        float squeezeAmount = Mathf.Clamp01(squeezeDetector.squeezeNormalized);

        palmAmount = Mathf.Clamp01(
            contactAmount * (1f - squeezeInfluenceOnPalm) +
            squeezeAmount * squeezeInfluenceOnPalm
        );

        if (palmAmount <= 0.001f) {
            return;
        }

        Vector3 palmContactWorldPosition = GetSurfaceContactPoint(palmSurfaceWorldPosition);

        ApplyInwardPatch(
            palmContactWorldPosition,
            localPalmCompressionRadius,
            localPalmMaxDepth,
            palmAmount,
            true
        );

        if (usePalmSideBulge) {
            ApplyBulgeRing(
                palmContactWorldPosition,
                localPalmBulgeInnerRadius,
                localPalmBulgeRadius,
                localPalmBulgeDepth,
                palmAmount
            );
        }
    }

    void ApplyFingerBandDeformation() {
        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        Vector3 weightedPosition = Vector3.zero;
        float totalWeight = 0f;
        float pressureSum = 0f;

        for (int fingerIndex = 0; fingerIndex < fingerPressures.Length; fingerIndex++) {
            if (!useThumbInFingerBand && fingerIndex == 0) {
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

            weightedPosition += fingerPositions[fingerIndex] * pressure01;
            totalWeight += pressure01;
            pressureSum += pressure;
            activeFingerCount++;
        }

        if (activeFingerCount == 0 || totalWeight <= 0.0001f) {
            return;
        }

        Vector3 averageFingerPosition = weightedPosition / totalWeight;
        Vector3 fingerBandWorldPosition = GetSurfaceContactPoint(averageFingerPosition);

        float averagePressure = pressureSum / activeFingerCount;

        float pressureAmount = Mathf.InverseLerp(
            fingerPressureThreshold,
            fingerPressureForMaxDepth,
            averagePressure
        );

        pressureAmount = Mathf.Clamp01(pressureAmount);
        pressureAmount = Mathf.Pow(pressureAmount, fingerBandResponseCurve);

        float squeezeAmount = Mathf.Clamp01(squeezeDetector.squeezeNormalized);

        fingerBandAmount = Mathf.Clamp01(
            pressureAmount * (1f - squeezeInfluenceOnFingerBand) +
            squeezeAmount * squeezeInfluenceOnFingerBand
        );

        ApplyInwardPatch(
            fingerBandWorldPosition,
            localFingerBandRadius,
            localFingerBandMaxDepth,
            fingerBandAmount,
            false
        );
    }

    void ApplyPalmToFingerSegmentCompressions() {
        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();
        float squeezeAmount = Mathf.Clamp01(squeezeDetector.squeezeNormalized);

        if (
            usePalmToThumbCompression &&
            TryGetThumbCompressionSegment(out Vector3 thumbStart, out Vector3 thumbEnd, out float thumbRadiusMeters)
        ) {
            float thumbPressure = GetProxySupportedFingerPressure(0, fingerPressures);

            ApplySinglePalmToFingerSegment(
                thumbStart,
                thumbEnd,
                thumbRadiusMeters,
                localThumbSegmentMaxDepth,
                thumbPressure,
                squeezeAmount
            );

            thumbSegmentActive = true;
            lastThumbSegmentRadiusMeters = thumbRadiusMeters;
        }

        if (
            usePalmToLittleCompression &&
            TryGetLittleCompressionSegment(out Vector3 littleStart, out Vector3 littleEnd, out float littleRadiusMeters)
        ) {
            float littlePressure = GetProxySupportedFingerPressure(4, fingerPressures);

            ApplySinglePalmToFingerSegment(
                littleStart,
                littleEnd,
                littleRadiusMeters,
                localLittleSegmentMaxDepth,
                littlePressure,
                squeezeAmount
            );

            littleSegmentActive = true;
        }
    }

    void ApplyLocalThumbPatch() {
        if (thumbContactProxy == null || !thumbContactProxy.gameObject.activeSelf) {
            return;
        }

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        float thumbPressure = 0f;

        if (fingerPressures != null && fingerPressures.Length > 0) {
            thumbPressure = fingerPressures[0];
        }

        lastThumbPressure = thumbPressure;

        float pressure01 = Mathf.InverseLerp(
            localAccentPressureThreshold,
            localAccentPressureForMaxDepth,
            thumbPressure
        );

        pressure01 = Mathf.Clamp01(pressure01);
        pressure01 = Mathf.Pow(pressure01, localAccentResponseCurve);

        float squeezeAmount = Mathf.Clamp01(squeezeDetector.squeezeNormalized);

        float combinedAmount = Mathf.Clamp01(
            pressure01 * (1f - thumbPatchSqueezeInfluence) +
            squeezeAmount * thumbPatchSqueezeInfluence
        );

        if (combinedAmount < thumbPatchMinimumAmount) {
            combinedAmount = thumbPatchMinimumAmount;
        }

        thumbPatchAmount = combinedAmount;
        thumbPatchActive = true;

        Vector3 thumbPatchWorldPosition = GetSurfaceContactPointWithOffset(
            thumbContactProxy.position,
            thumbPatchSurfaceOffsetMeters
        );

        ApplyLocalInwardPatch(
            thumbPatchWorldPosition,
            localThumbPatchRadius,
            localThumbPatchMaxDepth,
            thumbPatchAmount,
            thumbPatchFalloffPower
        );
    }

    bool TryGetThumbCompressionSegment(out Vector3 startPoint, out Vector3 endPoint, out float radiusMeters) {
        startPoint = Vector3.zero;
        endPoint = Vector3.zero;
        radiusMeters = thumbSegmentRadiusMeters;

        if (
            useContactVolumeSegments &&
            contactVolumeController != null &&
            contactVolumeController.TryGetThumbSegment(out startPoint, out endPoint, out float volumeRadius)
        ) {
            radiusMeters = volumeRadius * thumbVolumeDeformationRadiusMultiplier;
            return true;
        }

        if (
            palmContactProxy == null ||
            thumbContactProxy == null ||
            !palmContactProxy.gameObject.activeSelf ||
            !thumbContactProxy.gameObject.activeSelf
        ) {
            return false;
        }

        startPoint = palmContactProxy.position;
        endPoint = thumbContactProxy.position;
        radiusMeters = thumbSegmentRadiusMeters;

        return true;
    }

    bool TryGetLittleCompressionSegment(out Vector3 startPoint, out Vector3 endPoint, out float radiusMeters) {
        startPoint = Vector3.zero;
        endPoint = Vector3.zero;
        radiusMeters = littleSegmentRadiusMeters;

        if (
            useContactVolumeSegments &&
            contactVolumeController != null &&
            contactVolumeController.TryGetLittleSegment(out startPoint, out endPoint, out float volumeRadius)
        ) {
            radiusMeters = volumeRadius * littleVolumeDeformationRadiusMultiplier;
            return true;
        }

        if (
            palmContactProxy == null ||
            littleContactProxy == null ||
            !palmContactProxy.gameObject.activeSelf ||
            !littleContactProxy.gameObject.activeSelf
        ) {
            return false;
        }

        startPoint = palmContactProxy.position;
        endPoint = littleContactProxy.position;
        radiusMeters = littleSegmentRadiusMeters;

        return true;
    }

    float GetProxySupportedFingerPressure(int fingerIndex, float[] fingerPressures) {
        if (fingerPressures == null || fingerIndex < 0 || fingerIndex >= fingerPressures.Length) {
            return localAccentPressureThreshold + 0.20f;
        }

        float pressure = fingerPressures[fingerIndex];

        if (pressure < localAccentPressureThreshold) {
            pressure = localAccentPressureThreshold + 0.20f;
        }

        return pressure;
    }

    void ApplySinglePalmToFingerSegment(
        Vector3 segmentStartWorld,
        Vector3 segmentEndWorld,
        float segmentRadiusMeters,
        float localSegmentDepth,
        float fingerPressure,
        float squeezeAmount
    ) {
        float pressure01 = Mathf.InverseLerp(
            localAccentPressureThreshold,
            localAccentPressureForMaxDepth,
            fingerPressure
        );

        pressure01 = Mathf.Clamp01(pressure01);
        pressure01 = Mathf.Pow(pressure01, localAccentResponseCurve);

        float amount = Mathf.Clamp01(
            pressure01 * (1f - segmentSqueezeInfluence) +
            squeezeAmount * segmentSqueezeInfluence
        );

        if (amount <= 0.001f) {
            return;
        }

        Transform visualTransform = ballMeshFilter.transform;

        float averageScale = (
            visualTransform.lossyScale.x +
            visualTransform.lossyScale.y +
            visualTransform.lossyScale.z
        ) / 3f;

        if (averageScale <= 0.0001f) {
            averageScale = 1f;
        }

        float localSegmentRadius = segmentRadiusMeters / averageScale;

        Vector3 localSegmentStart = visualTransform.InverseTransformPoint(
            GetSurfaceContactPoint(segmentStartWorld)
        );

        Vector3 localSegmentEnd = visualTransform.InverseTransformPoint(
            GetSurfaceContactPoint(segmentEndWorld)
        );

        Vector3 localSphereCenter = visualTransform.InverseTransformPoint(transform.position);

        Vector3 segment = localSegmentEnd - localSegmentStart;

        if (segment.sqrMagnitude < 0.0001f) {
            return;
        }

        Vector3 segmentDirection = segment.normalized;
        float segmentLength = segment.magnitude;

        for (int i = 0; i < originalVertices.Length; i++) {
            Vector3 vertex = originalVertices[i];

            Vector3 closestPoint = GetClosestPointOnSegment(
                vertex,
                localSegmentStart,
                localSegmentEnd
            );

            float distanceToSegment = Vector3.Distance(vertex, closestPoint);

            if (distanceToSegment > localSegmentRadius) {
                continue;
            }

            float distance01 = Mathf.Clamp01(distanceToSegment / localSegmentRadius);
            float radialFalloff = 1f - distance01;
            radialFalloff = Mathf.Pow(radialFalloff, segmentFalloffPower);
            radialFalloff = SmoothFalloff(radialFalloff);

            float alongSegment = Vector3.Dot(closestPoint - localSegmentStart, segmentDirection);
            float along01 = Mathf.Clamp01(alongSegment / segmentLength);
            float alongWeight = Mathf.Lerp(0.65f, 1f, along01);

            Vector3 inwardDirection = localSphereCenter - closestPoint;

            if (inwardDirection.sqrMagnitude < 0.0001f) {
                continue;
            }

            inwardDirection.Normalize();

            float topMultiplier = GetTopProtectionMultiplier(vertex);

            Vector3 offset =
                inwardDirection *
                localSegmentDepth *
                amount *
                radialFalloff *
                alongWeight *
                topMultiplier;

            accumulatedOffsets[i] += offset;
            affectedVertices++;
        }
    }

    void ApplyInwardPatch(Vector3 contactWorldPosition, float localRadius, float localDepth, float amount, bool broadPatch) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localSphereCenter = visualTransform.InverseTransformPoint(transform.position);

        Vector3 inwardDirection = localSphereCenter - localContactPosition;

        if (inwardDirection.sqrMagnitude < 0.0001f) {
            return;
        }

        inwardDirection.Normalize();

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
            float shapedFalloff = Mathf.Pow(falloff, indentationFalloffPower);

            float finalFalloff = broadPatch
                ? Mathf.Lerp(shapedFalloff, smoothFalloff, 0.80f)
                : Mathf.Lerp(shapedFalloff, smoothFalloff, softnessBlend);

            float topMultiplier = GetTopProtectionMultiplier(vertex);

            Vector3 offset = inwardDirection * appliedDepth * finalFalloff * topMultiplier;

            accumulatedOffsets[i] += offset;
            affectedVertices++;
        }
    }

    void ApplyLocalInwardPatch(
        Vector3 contactWorldPosition,
        float localRadius,
        float localDepth,
        float amount,
        float falloffPower
    ) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localSphereCenter = visualTransform.InverseTransformPoint(transform.position);

        Vector3 inwardDirection = localSphereCenter - localContactPosition;

        if (inwardDirection.sqrMagnitude < 0.0001f) {
            return;
        }

        inwardDirection.Normalize();

        float appliedDepth = localDepth * amount;

        for (int i = 0; i < originalVertices.Length; i++) {
            Vector3 vertex = originalVertices[i];

            float distance = Vector3.Distance(vertex, localContactPosition);

            if (distance > localRadius) {
                continue;
            }

            float distance01 = Mathf.Clamp01(distance / localRadius);
            float falloff = 1f - distance01;
            falloff = Mathf.Pow(falloff, falloffPower);
            falloff = SmoothFalloff(falloff);

            float topMultiplier = GetTopProtectionMultiplier(vertex);

            Vector3 offset = inwardDirection * appliedDepth * falloff * topMultiplier;

            accumulatedOffsets[i] += offset;
            affectedVertices++;
        }
    }

    void ApplyBulgeRing(
        Vector3 contactWorldPosition,
        float localInnerRadius,
        float localOuterRadius,
        float localDepth,
        float amount
    ) {
        Transform visualTransform = ballMeshFilter.transform;

        Vector3 localContactPosition = visualTransform.InverseTransformPoint(contactWorldPosition);
        Vector3 localSphereCenter = visualTransform.InverseTransformPoint(transform.position);

        for (int i = 0; i < originalVertices.Length; i++) {
            Vector3 vertex = originalVertices[i];

            float distance = Vector3.Distance(vertex, localContactPosition);

            if (distance < localInnerRadius || distance > localOuterRadius) {
                continue;
            }

            float ringT = Mathf.InverseLerp(localInnerRadius, localOuterRadius, distance);
            float ringFalloff = Mathf.Sin(ringT * Mathf.PI);
            ringFalloff = Mathf.Pow(ringFalloff, palmBulgeFalloffPower);

            float topMultiplier = GetTopProtectionMultiplier(vertex);

            Vector3 outwardDirection = vertex - localSphereCenter;

            if (outwardDirection.sqrMagnitude < 0.0001f) {
                continue;
            }

            outwardDirection.Normalize();

            Vector3 offset = outwardDirection * localDepth * amount * ringFalloff * topMultiplier;

            accumulatedOffsets[i] += offset;
            affectedVertices++;
        }
    }

    Vector3 GetSurfaceContactPoint(Vector3 worldPosition) {
        return GetSurfaceContactPointWithOffset(worldPosition, surfaceOffsetMeters);
    }

    Vector3 GetSurfaceContactPointWithOffset(Vector3 worldPosition, float extraSurfaceOffsetMeters) {
        if (!projectContactsToSurface) {
            return worldPosition;
        }

        Vector3 center = transform.position;
        Vector3 centerToPoint = worldPosition - center;

        if (centerToPoint.sqrMagnitude < 0.0001f) {
            return center + Vector3.forward * (ballRadiusMeters + extraSurfaceOffsetMeters);
        }

        return center + centerToPoint.normalized * (ballRadiusMeters + extraSurfaceOffsetMeters);
    }

    Vector3 GetClosestPointOnSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd) {
        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;

        if (segmentLengthSquared < 0.0001f) {
            return segmentStart;
        }

        float t = Vector3.Dot(point - segmentStart, segment) / segmentLengthSquared;
        t = Mathf.Clamp01(t);

        return segmentStart + segment * t;
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
        bool hasActiveDeformation =
            palmAmount > 0.001f ||
            fingerBandAmount > 0.001f ||
            thumbPatchAmount > 0.001f ||
            affectedVertices > 0;

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
        fingerBandAmount = 0f;
        thumbPatchAmount = 0f;
        activeFingerCount = 0;
        affectedVertices = 0;
        thumbPatchActive = false;

        for (int i = 0; i < originalVertices.Length; i++) {
            currentVertices[i] = originalVertices[i];
            targetVertices[i] = originalVertices[i];
            accumulatedOffsets[i] = Vector3.zero;
        }

        deformingMesh.vertices = currentVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Debug.Log("Palm bulge finger band deformation reset");
    }
}