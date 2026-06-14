using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallAggregateSqueezeDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Reads per-finger pressure values")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Reads fingertip and palm positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Pivot used to orient the squeeze axis")]
    public Transform deformationPivot;

    [Tooltip("Visual sphere that gets scaled")]
    public Transform ballVisual;

    [Header("Finger Selection")]
    [Tooltip("Include the thumb in the squeeze computation")]
    public bool useThumb = true;

    [Tooltip("Minimum number of active fingers required")]
    [Range(1, 5)]
    public int minimumActiveFingers = 2;

    [Tooltip("Minimum pressure for a finger to count as active")]
    [Range(0f, 1f)]
    public float fingerPressureThreshold = 0.04f;

    [Header("Squeeze Activation")]
    [Tooltip("Minimum global squeeze required")]
    [Range(0f, 1f)]
    public float squeezeActivationThreshold = 0.16f;

    [Tooltip("Maximum pressure considered for deformation")]
    [Range(0f, 1f)]
    public float maxEffectivePressure = 0.75f;

    [Tooltip("Maximum squeeze considered for deformation")]
    [Range(0f, 1f)]
    public float maxEffectiveSqueeze = 0.90f;

    [Header("Shape Response")]
    [Tooltip("Maximum compression along the squeeze axis")]
    [Range(0f, 0.5f)]
    public float maxAxisCompression = 0.22f;

    [Tooltip("Maximum bulge on the two side axes")]
    [Range(0f, 0.3f)]
    public float maxSideBulge = 0.09f;

    [Tooltip("How much the sphere tries to preserve volume")]
    [Range(0f, 1f)]
    public float volumePreservation = 0.75f;

    [Tooltip("Overall deformation gain")]
    public float deformationGain = 1.0f;

    [Tooltip("Higher values make the ball feel more resistant at low force")]
    [Range(0.2f, 2f)]
    public float responseCurve = 1.15f;

    [Header("Motion")]
    [Tooltip("How fast the ball deforms")]
    public float deformationSpeed = 12f;

    [Tooltip("How fast the ball returns to the original shape")]
    public float returnSpeed = 14f;

    [Tooltip("How fast the pivot aligns with the squeeze axis")]
    public float axisFollowSpeed = 10f;

    [Header("Top Anchor Compensation")]
    [Tooltip("Keep the upper part visually more stable near the support")]
    public bool keepTopAnchored = true;

    [Tooltip("Fraction of top-anchor compensation")]
    [Range(0f, 1f)]
    public float topAnchorCompensation = 0.75f;

    [Tooltip("Approximate original sphere radius in local units")]
    public float visualRadiusLocal = 0.5f;

    [Header("Debug")]
    public int activeFingerCount = 0;
    public float averageFingerPressure = 0f;
    public float squeezeAmount = 0f;
    public float pressureAmount = 0f;
    public float activeFingerAmount = 0f;
    public float targetDeformation = 0f;
    public float currentDeformation = 0f;
    public Vector3 palmWorldPosition = Vector3.zero;
    public Vector3 fingerCentroidWorldPosition = Vector3.zero;
    public Vector3 squeezeAxisWorld = Vector3.up;

    private Vector3 originalBallScale;
    private Vector3 originalBallLocalPosition;
    private Quaternion originalPivotRotation;
    private Vector3 stableAxisWorld = Vector3.up;

    void Start() {
        if (pressureAnalyzer == null) {
            pressureAnalyzer = GetComponent<StressBallFingerPressureAnalyzer>();
        }

        if (squeezeDetector == null && pressureAnalyzer != null) {
            squeezeDetector = pressureAnalyzer.squeezeDetector;
        }

        if (deformationPivot == null) {
            Transform foundPivot = transform.Find("BallDeformationPivot");

            if (foundPivot != null) {
                deformationPivot = foundPivot;
            } else {
                deformationPivot = transform;
            }
        }

        if (ballVisual == null) {
            Transform foundVisual = transform.Find("BallDeformationPivot/StressBallVisual");

            if (foundVisual != null) {
                ballVisual = foundVisual;
            } else {
                Transform directVisual = transform.Find("StressBallVisual");

                if (directVisual != null) {
                    ballVisual = directVisual;
                }
            }
        }

        if (ballVisual == null) {
            Debug.LogWarning("Aggregate squeeze deformer missing ballVisual reference");
            enabled = false;
            return;
        }

        originalBallScale = ballVisual.localScale;
        originalBallLocalPosition = ballVisual.localPosition;
        originalPivotRotation = deformationPivot.rotation;
        stableAxisWorld = deformationPivot.up;

        Debug.Log("StressBallAggregateSqueezeDeformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || squeezeDetector == null || ballVisual == null || deformationPivot == null) {
            return;
        }

        bool hasValidInput = TryBuildSqueezeState(
            out Vector3 palmPosition,
            out Vector3 fingerCentroid,
            out Vector3 axis,
            out float avgPressure,
            out int activeCount
        );

        palmWorldPosition = palmPosition;
        fingerCentroidWorldPosition = fingerCentroid;
        averageFingerPressure = avgPressure;
        activeFingerCount = activeCount;
        squeezeAmount = squeezeDetector != null ? squeezeDetector.squeezeNormalized : 0f;

        if (hasValidInput) {
            squeezeAxisWorld = axis;
            stableAxisWorld = Vector3.Slerp(
                stableAxisWorld,
                axis,
                Time.deltaTime * axisFollowSpeed
            );
        }

        UpdateTargetDeformation(hasValidInput);
        UpdatePivotRotation();
        ApplyAggregateDeformation();
    }

    bool TryBuildSqueezeState(
        out Vector3 palmPosition,
        out Vector3 fingerCentroid,
        out Vector3 axis,
        out float avgPressure,
        out int activeCount
    ) {
        palmPosition = transform.position;
        fingerCentroid = transform.position;
        axis = stableAxisWorld;
        avgPressure = 0f;
        activeCount = 0;

        if (!squeezeDetector.TryGetPalmPosition(out palmPosition)) {
            return false;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerTipPositions)) {
            return false;
        }

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        Vector3 weightedCentroid = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < fingerPressures.Length; i++) {
            if (!useThumb && i == 0) {
                continue;
            }

            float rawPressure = fingerPressures[i];

            if (rawPressure < fingerPressureThreshold) {
                continue;
            }

            float effectivePressure = Mathf.InverseLerp(
                fingerPressureThreshold,
                maxEffectivePressure,
                rawPressure
            );

            effectivePressure = Mathf.Clamp01(effectivePressure);

            weightedCentroid += fingerTipPositions[i] * effectivePressure;
            totalWeight += effectivePressure;
            avgPressure += rawPressure;
            activeCount++;
        }

        if (activeCount < minimumActiveFingers || totalWeight <= 0.0001f) {
            avgPressure = 0f;
            return false;
        }

        fingerCentroid = weightedCentroid / totalWeight;
        avgPressure /= activeCount;

        Vector3 palmToFingers = fingerCentroid - palmPosition;

        if (palmToFingers.sqrMagnitude < 0.0001f) {
            return false;
        }

        axis = palmToFingers.normalized;
        return true;
    }

    void UpdateTargetDeformation(bool hasValidInput) {
        if (!hasValidInput) {
            targetDeformation = 0f;
        } else {
            pressureAmount = Mathf.InverseLerp(
                fingerPressureThreshold,
                maxEffectivePressure,
                averageFingerPressure
            );

            pressureAmount = Mathf.Clamp01(pressureAmount);

            squeezeAmount = Mathf.InverseLerp(
                squeezeActivationThreshold,
                maxEffectiveSqueeze,
                squeezeAmount
            );

            squeezeAmount = Mathf.Clamp01(squeezeAmount);

            activeFingerAmount = Mathf.InverseLerp(
                minimumActiveFingers,
                5f,
                activeFingerCount
            );

            activeFingerAmount = Mathf.Clamp01(activeFingerAmount);

            float combinedAmount =
                pressureAmount * 0.55f +
                squeezeAmount * 0.30f +
                activeFingerAmount * 0.15f;

            combinedAmount = Mathf.Clamp01(combinedAmount);

            targetDeformation = Mathf.Pow(combinedAmount, responseCurve) * deformationGain;
            targetDeformation = Mathf.Clamp01(targetDeformation);
        }

        float speed = targetDeformation > currentDeformation ? deformationSpeed : returnSpeed;

        currentDeformation = Mathf.Lerp(
            currentDeformation,
            targetDeformation,
            Time.deltaTime * speed
        );
    }

    void UpdatePivotRotation() {
        if (stableAxisWorld.sqrMagnitude < 0.0001f) {
            return;
        }

        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, stableAxisWorld);
        deformationPivot.rotation = Quaternion.Slerp(
            deformationPivot.rotation,
            targetRotation,
            Time.deltaTime * axisFollowSpeed
        );
    }

    void ApplyAggregateDeformation() {
        float axisCompression = maxAxisCompression * currentDeformation;
        float axisScale = 1f - axisCompression;
        axisScale = Mathf.Max(axisScale, 0.1f);

        float simpleSideScale = 1f + currentDeformation * maxSideBulge * 0.6f;
        float preservedSideScale = 1f / Mathf.Sqrt(axisScale);

        float sideScale = Mathf.Lerp(
            simpleSideScale,
            preservedSideScale,
            volumePreservation
        );

        float maxAllowedSideScale = 1f + maxSideBulge;
        sideScale = Mathf.Min(sideScale, maxAllowedSideScale);

        ballVisual.localScale = new Vector3(
            originalBallScale.x * sideScale,
            originalBallScale.y * axisScale,
            originalBallScale.z * sideScale
        );

        if (keepTopAnchored) {
            float originalHeight = originalBallScale.y * visualRadiusLocal * 2f;
            float compressedHeight = originalHeight * axisScale;
            float heightLoss = originalHeight - compressedHeight;

            Vector3 compensatedPosition = originalBallLocalPosition;
            compensatedPosition.y += heightLoss * 0.5f * topAnchorCompensation;

            ballVisual.localPosition = compensatedPosition;
        } else {
            ballVisual.localPosition = originalBallLocalPosition;
        }
    }

    public void ResetDeformation() {
        currentDeformation = 0f;
        targetDeformation = 0f;

        if (ballVisual != null) {
            ballVisual.localScale = originalBallScale;
            ballVisual.localPosition = originalBallLocalPosition;
        }

        if (deformationPivot != null) {
            deformationPivot.rotation = originalPivotRotation;
        }

        stableAxisWorld = Vector3.up;

        Debug.Log("Aggregate squeeze deformation reset");
    }
}