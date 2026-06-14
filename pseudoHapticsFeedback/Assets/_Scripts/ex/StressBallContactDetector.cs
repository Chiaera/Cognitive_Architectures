using UnityEngine;
using TMPro;

public class StressBallContactDetector : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Contact Settings")]
    [Tooltip("Extra tolerance around the ball surface for contact detection")]
    public float contactTolerance = 0.05f;

    [Tooltip("Use the renderer bounds to calculate the current ball radius automatically")]
    public bool useAutomaticRadius = true;

    [Tooltip("Manual radius used only when automatic radius is disabled")]
    public float manualBallRadius = 0.06f;

    [Header("Runtime Debug UI")]
    [Tooltip("Optional text used to show contact debug values inside the headset")]
    public TextMeshProUGUI debugText;

    [Tooltip("Show contact debug values inside the headset")]
    public bool showRuntimeDebug = true;

    [Header("Debug")]
    public float currentBallRadius = 0f;
    public int touchingFingerCount = 0;

    public bool thumbTouching = false;
    public bool indexTouching = false;
    public bool middleTouching = false;
    public bool ringTouching = false;
    public bool littleTouching = false;

    public float thumbDistance = 0f;
    public float indexDistance = 0f;
    public float middleDistance = 0f;
    public float ringDistance = 0f;
    public float littleDistance = 0f;

    private Renderer ballRenderer;

    private readonly string[] fingerNames = {
        "Thumb",
        "Index",
        "Middle",
        "Ring",
        "Little"
    };

    void Start() {
        // Cache the renderer used to estimate the visual size of the ball
        ballRenderer = GetComponent<Renderer>();
        UpdateBallRadius();

        Debug.Log("Stress ball contact detector initialized");
    }

    void Update() {
        UpdateBallRadius();
        UpdateContactDetection();
        UpdateRuntimeDebugUI();
    }

    void UpdateBallRadius() {
        // Estimate the ball radius from the renderer bounds
        if (useAutomaticRadius && ballRenderer != null) {
            Vector3 extents = ballRenderer.bounds.extents;
            currentBallRadius = Mathf.Max(extents.x, extents.y, extents.z);
            return;
        }

        currentBallRadius = manualBallRadius;
    }

    void UpdateContactDetection() {
        // Reset contact state before checking the current frame
        touchingFingerCount = 0;

        thumbTouching = false;
        indexTouching = false;
        middleTouching = false;
        ringTouching = false;
        littleTouching = false;

        thumbDistance = 0f;
        indexDistance = 0f;
        middleDistance = 0f;
        ringDistance = 0f;
        littleDistance = 0f;

        if (squeezeDetector == null) {
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            return;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        bool[] touchingStates = new bool[5];
        float[] distances = new float[5];

        for (int i = 0; i < fingerPositions.Length; i++) {
            float distanceFromCenter = Vector3.Distance(fingerPositions[i], transform.position);
            distances[i] = distanceFromCenter;

            // A finger is considered touching when it is inside or close to the visual ball surface
            bool isTouching = distanceFromCenter <= currentBallRadius + contactTolerance;

            touchingStates[i] = isTouching;

            if (isTouching) {
                touchingFingerCount++;
            }
        }

        thumbTouching = touchingStates[0];
        indexTouching = touchingStates[1];
        middleTouching = touchingStates[2];
        ringTouching = touchingStates[3];
        littleTouching = touchingStates[4];

        thumbDistance = distances[0];
        indexDistance = distances[1];
        middleDistance = distances[2];
        ringDistance = distances[3];
        littleDistance = distances[4];
    }

    void UpdateRuntimeDebugUI() {
        // Show contact information directly inside the headset during testing
        if (!showRuntimeDebug || debugText == null) {
            return;
        }

        float contactLimit = currentBallRadius + contactTolerance;

        debugText.text =
            "Touching fingers: " + touchingFingerCount + "\n" +
            "Ball radius: " + currentBallRadius.ToString("F3") + "\n" +
            "Contact limit: " + contactLimit.ToString("F3") + "\n" +
            "Thumb: " + thumbDistance.ToString("F3") + " / " + thumbTouching + "\n" +
            "Index: " + indexDistance.ToString("F3") + " / " + indexTouching + "\n" +
            "Middle: " + middleDistance.ToString("F3") + " / " + middleTouching + "\n" +
            "Ring: " + ringDistance.ToString("F3") + " / " + ringTouching + "\n" +
            "Little: " + littleDistance.ToString("F3") + " / " + littleTouching + "\n" +
            "Squeeze: " + squeezeDetector.squeezeNormalized.ToString("F2");
    }

    public bool IsBallTouched() {
        // Return true when at least one finger is touching the ball
        return touchingFingerCount > 0;
    }

    public bool IsBallGrasped() {
        // Return true when enough fingers are touching the ball to simulate a grasp
        return touchingFingerCount >= 2;
    }
}