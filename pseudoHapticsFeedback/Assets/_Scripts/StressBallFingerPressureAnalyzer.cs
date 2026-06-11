using UnityEngine;
using TMPro;

public class StressBallFingerPressureAnalyzer : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read fingertip positions and squeeze amount")]
    public HandSqueezeDetector squeezeDetector;

    [Tooltip("Visual renderer used to estimate the current ball radius")]
    public Renderer ballVisualRenderer;

    [Header("Ball Settings")]
    [Tooltip("Use the renderer bounds to calculate the current ball radius automatically")]
    public bool useAutomaticRadius = true;

    [Tooltip("Manual ball radius used only when automatic radius is disabled")]
    public float manualBallRadius = 0.06f;

    [Tooltip("Extra interaction zone outside the ball surface")]
    public float outerContactTolerance = 0.012f;

    [Tooltip("Maximum expected fingertip penetration inside the ball")]
    public float maxPenetrationDepth = 0.04f;

    [Header("Pressure Settings")]
    [Tooltip("Global gain applied to all finger pressures")]
    public float deformationGain = 1f;

    [Tooltip("How much the global squeeze value amplifies local finger pressure")]
    [Range(0f, 1f)]
    public float squeezeAmplification = 0.3f;

    [Header("Runtime Debug UI")]
    [Tooltip("Optional text used to show pressure debug values inside the headset")]
    public TextMeshProUGUI debugText;

    [Tooltip("Show pressure debug values inside the headset")]
    public bool showRuntimeDebug = true;

    [Header("Debug - Ball")]
    public float currentBallRadius = 0f;
    public float interactionLimit = 0f;
    public int activeFingerCount = 0;
    public float averagePressure = 0f;
    public Vector3 averagePressureDirection = Vector3.zero;

    [Header("Debug - Finger Pressures")]
    public float thumbPressure = 0f;
    public float indexPressure = 0f;
    public float middlePressure = 0f;
    public float ringPressure = 0f;
    public float littlePressure = 0f;

    [Header("Debug - Finger Distances")]
    public float thumbDistance = 0f;
    public float indexDistance = 0f;
    public float middleDistance = 0f;
    public float ringDistance = 0f;
    public float littleDistance = 0f;

    private readonly string[] fingerNames = {
        "Thumb",
        "Index",
        "Middle",
        "Ring",
        "Little"
    };

    private float[] fingerPressures = new float[5];
    private float[] fingerDistances = new float[5];
    private Vector3[] fingerPressureDirections = new Vector3[5];

    void Start() {
        // Use the assigned visual renderer, or try to find one in the children
        if (ballVisualRenderer == null) {
            ballVisualRenderer = GetComponentInChildren<Renderer>();
        }

        UpdateBallRadius();

        Debug.Log("Stress ball finger pressure analyzer initialized");
    }

    void Update() {
        UpdateBallRadius();
        AnalyzeFingerPressures();
        UpdateDebugValues();
        UpdateRuntimeDebugUI();
    }

    void UpdateBallRadius() {
        // Estimate the visual radius from the assigned visual renderer bounds
        if (useAutomaticRadius && ballVisualRenderer != null) {
            Vector3 extents = ballVisualRenderer.bounds.extents;
            currentBallRadius = Mathf.Max(extents.x, extents.y, extents.z);
        } else {
            currentBallRadius = manualBallRadius;
        }

        interactionLimit = currentBallRadius + outerContactTolerance;
    }

    void AnalyzeFingerPressures() {
        // Reset all pressure values before analyzing the current frame
        activeFingerCount = 0;
        averagePressure = 0f;
        averagePressureDirection = Vector3.zero;

        ResetFingerArrays();

        if (squeezeDetector == null) {
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            return;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        float totalPressure = 0f;
        Vector3 totalDirection = Vector3.zero;

        for (int i = 0; i < fingerPositions.Length; i++) {
            Vector3 fingerPosition = fingerPositions[i];

            float distanceFromCenter = Vector3.Distance(fingerPosition, transform.position);
            fingerDistances[i] = distanceFromCenter;

            if (distanceFromCenter > interactionLimit) {
                continue;
            }

            Vector3 directionFromFingerToCenter = transform.position - fingerPosition;

            if (directionFromFingerToCenter.sqrMagnitude < 0.0001f) {
                continue;
            }

            directionFromFingerToCenter.Normalize();

            float penetrationDepth = interactionLimit - distanceFromCenter;
            float normalizedPenetration = Mathf.Clamp01(penetrationDepth / maxPenetrationDepth);

            float squeezeMultiplier = 1f + squeezeDetector.squeezeNormalized * squeezeAmplification;

            float fingerPressure = Mathf.Clamp01(
                normalizedPenetration * deformationGain * squeezeMultiplier
            );

            fingerPressures[i] = fingerPressure;
            fingerPressureDirections[i] = directionFromFingerToCenter;

            if (fingerPressure > 0f) {
                activeFingerCount++;
                totalPressure += fingerPressure;
                totalDirection += directionFromFingerToCenter * fingerPressure;
            }
        }

        if (activeFingerCount > 0) {
            averagePressure = Mathf.Clamp01(totalPressure / activeFingerCount);

            if (totalDirection.sqrMagnitude > 0.0001f) {
                averagePressureDirection = totalDirection.normalized;
            }
        }
    }

    void ResetFingerArrays() {
        // Clear per-finger values
        for (int i = 0; i < fingerPressures.Length; i++) {
            fingerPressures[i] = 0f;
            fingerDistances[i] = 0f;
            fingerPressureDirections[i] = Vector3.zero;
        }
    }

    void UpdateDebugValues() {
        // Copy array values into Inspector-friendly fields
        thumbPressure = fingerPressures[0];
        indexPressure = fingerPressures[1];
        middlePressure = fingerPressures[2];
        ringPressure = fingerPressures[3];
        littlePressure = fingerPressures[4];

        thumbDistance = fingerDistances[0];
        indexDistance = fingerDistances[1];
        middleDistance = fingerDistances[2];
        ringDistance = fingerDistances[3];
        littleDistance = fingerDistances[4];
    }

    void UpdateRuntimeDebugUI() {
        // Show per-finger pressure information directly inside the headset
        if (!showRuntimeDebug || debugText == null) {
            return;
        }

        debugText.text =
            "Active fingers: " + activeFingerCount + "\n" +
            "Average pressure: " + averagePressure.ToString("F2") + "\n" +
            "Direction: " + averagePressureDirection.ToString("F2") + "\n" +
            "Limit: " + interactionLimit.ToString("F3") + "\n" +
            fingerNames[0] + ": " + thumbPressure.ToString("F2") + " | " + thumbDistance.ToString("F3") + "\n" +
            fingerNames[1] + ": " + indexPressure.ToString("F2") + " | " + indexDistance.ToString("F3") + "\n" +
            fingerNames[2] + ": " + middlePressure.ToString("F2") + " | " + middleDistance.ToString("F3") + "\n" +
            fingerNames[3] + ": " + ringPressure.ToString("F2") + " | " + ringDistance.ToString("F3") + "\n" +
            fingerNames[4] + ": " + littlePressure.ToString("F2") + " | " + littleDistance.ToString("F3") + "\n" +
            "Squeeze: " + squeezeDetector.squeezeNormalized.ToString("F2");
    }

    public float GetFingerPressure(int fingerIndex) {
        // Return the pressure value for a specific finger
        if (fingerIndex < 0 || fingerIndex >= fingerPressures.Length) {
            return 0f;
        }

        return fingerPressures[fingerIndex];
    }

    public Vector3 GetFingerPressureDirection(int fingerIndex) {
        // Return the pressure direction for a specific finger
        if (fingerIndex < 0 || fingerIndex >= fingerPressureDirections.Length) {
            return Vector3.zero;
        }

        return fingerPressureDirections[fingerIndex];
    }

    public float[] GetAllFingerPressures() {
        // Return all finger pressures
        return fingerPressures;
    }

    public Vector3[] GetAllFingerPressureDirections() {
        // Return all finger pressure directions
        return fingerPressureDirections;
    }
}