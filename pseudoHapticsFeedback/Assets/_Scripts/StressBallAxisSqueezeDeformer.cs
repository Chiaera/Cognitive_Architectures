using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallAxisSqueezeDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read hand pressure information")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Squeeze detector used to read global hand squeeze")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Pivot used to rotate the deformation axis toward the hand")]
    public Transform deformationPivot;

    [Tooltip("Visual object of the stress ball")]
    public Transform ballVisual;

    [Header("Activation")]
    [Tooltip("Minimum number of active fingers required to start the squeeze deformation")]
    public int minimumActiveFingers = 3;

    [Tooltip("Minimum squeeze amount required to start deformation")]
    [Range(0f, 1f)]
    public float squeezeActivationThreshold = 0.30f;

    [Tooltip("Minimum average pressure required to start deformation")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.06f;

    [Header("Axis Deformation")]
    [Tooltip("Maximum compression along the hand pressure axis")]
    [Range(0f, 0.5f)]
    public float maxAxisCompression = 0.16f;

    [Tooltip("Maximum expansion on the two perpendicular axes")]
    [Range(0f, 0.5f)]
    public float maxSideBulge = 0.055f;

    [Tooltip("Overall deformation strength")]
    public float deformationGain = 1.0f;

    [Tooltip("Softens the response at high values")]
    [Range(0.1f, 2f)]
    public float responseCurve = 0.85f;

    [Header("Saturation")]
    [Tooltip("Maximum effective squeeze used for deformation")]
    [Range(0f, 1f)]
    public float maxEffectiveSqueeze = 0.85f;

    [Tooltip("Maximum effective pressure used for deformation")]
    [Range(0f, 1f)]
    public float maxEffectivePressure = 0.65f;

    [Header("Smoothing")]
    [Tooltip("How fast the deformation appears")]
    public float deformationSpeed = 14f;

    [Tooltip("How fast the ball returns to its original shape")]
    public float returnSpeed = 8f;

    [Tooltip("How fast the deformation axis follows the hand pressure direction")]
    public float axisFollowSpeed = 10f;

    [Header("Debug")]
    public float currentSqueeze = 0f;
    public float currentPressure = 0f;
    public int currentActiveFingers = 0;
    public float currentDeformation = 0f;
    public float targetDeformation = 0f;
    public Vector3 currentAxis = Vector3.forward;
    public Vector3 targetAxis = Vector3.forward;

    private Vector3 originalVisualScale;
    private Quaternion originalPivotRotation;
    private Quaternion currentPivotRotation;

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
            }
        }

        if (ballVisual == null && deformationPivot != null && deformationPivot.childCount > 0) {
            ballVisual = deformationPivot.GetChild(0);
        }

        if (deformationPivot == null || ballVisual == null) {
            Debug.LogWarning("Axis squeeze deformer missing deformation pivot or ball visual");
            enabled = false;
            return;
        }

        originalVisualScale = ballVisual.localScale;
        originalPivotRotation = deformationPivot.rotation;
        currentPivotRotation = originalPivotRotation;

        Debug.Log("Stress ball axis squeeze deformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || squeezeDetector == null || deformationPivot == null || ballVisual == null) {
            return;
        }

        ReadInputValues();
        UpdateTargetDeformation();
        UpdateDeformationAxis();
        ApplyAxisScale();
    }

    void ReadInputValues() {
        currentSqueeze = squeezeDetector.squeezeNormalized;
        currentPressure = pressureAnalyzer.averagePressure;
        currentActiveFingers = pressureAnalyzer.activeFingerCount;

        if (pressureAnalyzer.averagePressureDirection.sqrMagnitude > 0.0001f) {
            targetAxis = pressureAnalyzer.averagePressureDirection.normalized;
        } else {
            targetAxis = currentAxis;
        }
    }

    void UpdateTargetDeformation() {
        bool hasEnoughContact = currentActiveFingers >= minimumActiveFingers;
        bool hasEnoughSqueeze = currentSqueeze >= squeezeActivationThreshold;
        bool hasEnoughPressure = currentPressure >= pressureActivationThreshold;

        if (!hasEnoughContact || !hasEnoughSqueeze || !hasEnoughPressure) {
            targetDeformation = 0f;
        } else {
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

            float combinedAmount = squeezeAmount * 0.45f + pressureAmount * 0.55f;

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

    void UpdateDeformationAxis() {
        if (targetAxis.sqrMagnitude < 0.0001f) {
            return;
        }

        currentAxis = Vector3.Slerp(
            currentAxis,
            targetAxis,
            Time.deltaTime * axisFollowSpeed
        ).normalized;

        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.forward, currentAxis);
        currentPivotRotation = Quaternion.Slerp(
            currentPivotRotation,
            targetRotation,
            Time.deltaTime * axisFollowSpeed
        );

        deformationPivot.rotation = currentPivotRotation;
    }

    void ApplyAxisScale() {
        float compression = maxAxisCompression * currentDeformation;
        float bulge = maxSideBulge * currentDeformation;

        Vector3 targetScale = new Vector3(
            originalVisualScale.x * (1f + bulge),
            originalVisualScale.y * (1f + bulge),
            originalVisualScale.z * (1f - compression)
        );

        ballVisual.localScale = Vector3.Lerp(
            ballVisual.localScale,
            targetScale,
            Time.deltaTime * deformationSpeed
        );
    }

    public void ResetDeformation() {
        currentDeformation = 0f;
        targetDeformation = 0f;
        currentAxis = Vector3.forward;
        targetAxis = Vector3.forward;

        if (ballVisual != null) {
            ballVisual.localScale = originalVisualScale;
        }

        if (deformationPivot != null) {
            deformationPivot.rotation = originalPivotRotation;
        }

        Debug.Log("Stress ball axis squeeze deformation reset");
    }
}