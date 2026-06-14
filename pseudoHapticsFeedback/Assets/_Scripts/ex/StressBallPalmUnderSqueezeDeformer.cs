using UnityEngine;

[RequireComponent(typeof(StressBallFingerPressureAnalyzer))]
public class StressBallPalmUnderSqueezeDeformer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Analyzer used to read finger pressure and active contact count")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Tooltip("Squeeze detector used to read global hand squeeze")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Visual Target")]
    [Tooltip("Visual object of the stress ball")]
    public Transform ballVisual;

    [Header("Activation")]
    [Tooltip("Minimum number of active fingers required to start deformation")]
    public int minimumActiveFingers = 3;

    [Tooltip("Minimum squeeze amount required to start deformation")]
    [Range(0f, 1f)]
    public float squeezeActivationThreshold = 0.25f;

    [Tooltip("Minimum average pressure required to start deformation")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.05f;

    [Header("Palm-Under Deformation")]
    [Tooltip("Maximum vertical compression of the ball")]
    [Range(0f, 0.6f)]
    public float maxVerticalCompression = 0.22f;

    [Tooltip("Maximum horizontal expansion of the ball")]
    [Range(0f, 0.4f)]
    public float maxHorizontalBulge = 0.055f;

    [Tooltip("Overall deformation gain")]
    public float deformationGain = 1.0f;

    [Tooltip("Softness curve. Lower values react earlier, higher values feel more resistant")]
    [Range(0.2f, 2f)]
    public float responseCurve = 0.85f;

    [Header("Saturation")]
    [Tooltip("Maximum effective squeeze value")]
    [Range(0f, 1f)]
    public float maxEffectiveSqueeze = 0.85f;

    [Tooltip("Maximum effective pressure value")]
    [Range(0f, 1f)]
    public float maxEffectivePressure = 0.65f;

    [Header("Elastic Motion")]
    [Tooltip("How fast the ball deforms")]
    public float deformationSpeed = 14f;

    [Tooltip("How fast the ball returns to its original shape")]
    public float returnSpeed = 10f;

    [Header("Anchor Compensation")]
    [Tooltip("Keep the top connector visually stable while the ball compresses")]
    public bool keepTopAnchored = true;

    [Tooltip("Approximate original visual radius of the ball in local units")]
    public float visualRadiusLocal = 0.5f;

    [Header("Debug")]
    public float currentSqueeze = 0f;
    public float currentPressure = 0f;
    public int currentActiveFingers = 0;
    public float targetDeformation = 0f;
    public float currentDeformation = 0f;

    private Vector3 originalScale;
    private Vector3 originalLocalPosition;

    void Start() {
        if (pressureAnalyzer == null) {
            pressureAnalyzer = GetComponent<StressBallFingerPressureAnalyzer>();
        }

        if (squeezeDetector == null && pressureAnalyzer != null) {
            squeezeDetector = pressureAnalyzer.squeezeDetector;
        }

        if (ballVisual == null && transform.childCount > 0) {
            ballVisual = FindBallVisual();
        }

        if (ballVisual == null) {
            Debug.LogWarning("Palm-under squeeze deformer missing ball visual");
            enabled = false;
            return;
        }

        originalScale = ballVisual.localScale;
        originalLocalPosition = ballVisual.localPosition;

        Debug.Log("Stress ball palm-under squeeze deformer initialized");
    }

    void Update() {
        if (pressureAnalyzer == null || squeezeDetector == null || ballVisual == null) {
            return;
        }

        ReadInputs();
        UpdateTargetDeformation();
        ApplyDeformation();
    }

    Transform FindBallVisual() {
        Transform directVisual = transform.Find("StressBallVisual");

        if (directVisual != null) {
            return directVisual;
        }

        Transform pivot = transform.Find("BallDeformationPivot");

        if (pivot != null) {
            Transform nestedVisual = pivot.Find("StressBallVisual");

            if (nestedVisual != null) {
                return nestedVisual;
            }
        }

        return GetComponentInChildren<MeshRenderer>() != null
            ? GetComponentInChildren<MeshRenderer>().transform
            : null;
    }

    void ReadInputs() {
        currentSqueeze = squeezeDetector.squeezeNormalized;
        currentPressure = pressureAnalyzer.averagePressure;
        currentActiveFingers = pressureAnalyzer.activeFingerCount;
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

            float combinedAmount = squeezeAmount * 0.55f + pressureAmount * 0.45f;

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

    void ApplyDeformation() {
        float verticalCompression = maxVerticalCompression * currentDeformation;
        float horizontalBulge = maxHorizontalBulge * currentDeformation;

        Vector3 targetScale = new Vector3(
            originalScale.x * (1f + horizontalBulge),
            originalScale.y * (1f - verticalCompression),
            originalScale.z * (1f + horizontalBulge)
        );

        ballVisual.localScale = targetScale;

        if (keepTopAnchored) {
            ApplyTopAnchorCompensation(verticalCompression);
        } else {
            ballVisual.localPosition = originalLocalPosition;
        }
    }

    void ApplyTopAnchorCompensation(float verticalCompression) {
        // When the sphere compresses on Y, its top would move downward.
        // This offset keeps the upper attachment visually more stable.
        float originalHeight = originalScale.y * visualRadiusLocal * 2f;
        float compressedHeight = originalHeight * (1f - verticalCompression);
        float heightLoss = originalHeight - compressedHeight;

        Vector3 compensatedPosition = originalLocalPosition;
        compensatedPosition.y = originalLocalPosition.y - heightLoss * 0.5f;

        ballVisual.localPosition = compensatedPosition;
    }

    public void ResetDeformation() {
        currentDeformation = 0f;
        targetDeformation = 0f;

        if (ballVisual != null) {
            ballVisual.localScale = originalScale;
            ballVisual.localPosition = originalLocalPosition;
        }

        Debug.Log("Stress ball palm-under squeeze deformation reset");
    }
}