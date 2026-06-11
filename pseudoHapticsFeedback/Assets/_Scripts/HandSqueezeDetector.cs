using UnityEngine;
using UnityEngine.XR.Hands;
using TMPro;

public class HandSqueezeDetector : MonoBehaviour {
    public enum SqueezeMode {
        AverageAllFingers,
        StrongestTwoFingers,
        StrongestFinger
    }

    public XRHandTrackingEvents handTrackingEvents;

    [Header("Squeeze Mode")]
    [Tooltip("Controls how the final squeeze value is computed")]
    public SqueezeMode squeezeMode = SqueezeMode.StrongestTwoFingers;

    [Header("Finger Positions Debug")]
    public Vector3 palmPosition;
    public Vector3 thumbTipPosition;
    public Vector3 indexTipPosition;
    public Vector3 middleTipPosition;
    public Vector3 ringTipPosition;
    public Vector3 littleTipPosition;

    [Header("Per-Finger Squeeze Debug")]
    [Range(0f, 1f)] public float indexSqueeze = 0f;
    [Range(0f, 1f)] public float middleSqueeze = 0f;
    [Range(0f, 1f)] public float ringSqueeze = 0f;
    [Range(0f, 1f)] public float littleSqueeze = 0f;

    [Header("Debug Settings")]
    public bool enableFrameLogs = false;

    [Header("Runtime Debug UI")]
    public TextMeshProUGUI debugText;
    public bool showRuntimeDebug = false;

    [Header("Output")]
    public float rawAverageDistance;
    [Range(0f, 1f)] public float squeezeAmount = 0f;
    [Range(0f, 1f)] public float squeezeNormalized = 0f;

    [Header("Smoothing")]
    [Range(0f, 20f)]
    public float smoothingSpeed = 8f;

    [Header("Global Calibration")]
    public float openDistance = 0.12f;
    public float closedDistance = 0.04f;

    [Header("Per-Finger Calibration")]
    public float indexOpenDistance = 0.12f;
    public float middleOpenDistance = 0.12f;
    public float ringOpenDistance = 0.12f;
    public float littleOpenDistance = 0.12f;

    public float indexClosedDistance = 0.04f;
    public float middleClosedDistance = 0.04f;
    public float ringClosedDistance = 0.04f;
    public float littleClosedDistance = 0.04f;

    private XRHand currentHand;
    private bool isHandTracked = false;
    private float smoothedSqueeze = 0f;

    public bool IsHandTracked => isHandTracked && currentHand.isTracked;

    public bool CalibrateOpen() {
        // Store global and per-finger open distances
        float d = GetAverageFingerToPalmDistance();

        if (d <= 0f) {
            return false;
        }

        openDistance = d;

        TryGetFingerToPalmDistance(XRHandJointID.IndexTip, out indexOpenDistance);
        TryGetFingerToPalmDistance(XRHandJointID.MiddleTip, out middleOpenDistance);
        TryGetFingerToPalmDistance(XRHandJointID.RingTip, out ringOpenDistance);
        TryGetFingerToPalmDistance(XRHandJointID.LittleTip, out littleOpenDistance);

        Debug.Log("Open calibration value set to " + d.ToString("F3"));

        return true;
    }

    public bool CalibrateClosed() {
        // Store global and per-finger closed distances
        float d = GetAverageFingerToPalmDistance();

        if (d <= 0f) {
            return false;
        }

        closedDistance = d;

        TryGetFingerToPalmDistance(XRHandJointID.IndexTip, out indexClosedDistance);
        TryGetFingerToPalmDistance(XRHandJointID.MiddleTip, out middleClosedDistance);
        TryGetFingerToPalmDistance(XRHandJointID.RingTip, out ringClosedDistance);
        TryGetFingerToPalmDistance(XRHandJointID.LittleTip, out littleClosedDistance);

        Debug.Log("Closed calibration value set to " + d.ToString("F3"));

        return true;
    }

    public bool TryGetFingerToPalmDistances(out float indexDistance, out float middleDistance, out float ringDistance, out float littleDistance) {
        // Return the current distance from each fingertip to the palm
        indexDistance = -1f;
        middleDistance = -1f;
        ringDistance = -1f;
        littleDistance = -1f;

        bool indexFound = TryGetFingerToPalmDistance(XRHandJointID.IndexTip, out indexDistance);
        bool middleFound = TryGetFingerToPalmDistance(XRHandJointID.MiddleTip, out middleDistance);
        bool ringFound = TryGetFingerToPalmDistance(XRHandJointID.RingTip, out ringDistance);
        bool littleFound = TryGetFingerToPalmDistance(XRHandJointID.LittleTip, out littleDistance);

        return indexFound && middleFound && ringFound && littleFound;
    }

    void OnEnable() {
        if (handTrackingEvents != null) {
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
            handTrackingEvents.trackingLost.AddListener(OnTrackingLost);
        }
    }

    void OnDisable() {
        if (handTrackingEvents != null) {
            handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
            handTrackingEvents.trackingLost.RemoveListener(OnTrackingLost);
        }
    }

    void OnJointsUpdated(XRHandJointsUpdatedEventArgs args) {
        currentHand = args.hand;
        isHandTracked = true;
    }

    void OnTrackingLost() {
        isHandTracked = false;
    }

    void Update() {
        if (!IsHandTracked) {
            return;
        }

        UpdateDebugJointPositions();
        UpdateSqueezeValues();
        UpdateRuntimeDebugUI();
    }

    void UpdateSqueezeValues() {
        // Update global average distance for calibration and debug
        float avg = GetAverageFingerToPalmDistance();

        if (avg < 0f) {
            return;
        }

        rawAverageDistance = avg;

        // Compute per-finger squeeze values independently
        indexSqueeze = GetNormalizedFingerSqueeze(
            XRHandJointID.IndexTip,
            indexOpenDistance,
            indexClosedDistance
        );

        middleSqueeze = GetNormalizedFingerSqueeze(
            XRHandJointID.MiddleTip,
            middleOpenDistance,
            middleClosedDistance
        );

        ringSqueeze = GetNormalizedFingerSqueeze(
            XRHandJointID.RingTip,
            ringOpenDistance,
            ringClosedDistance
        );

        littleSqueeze = GetNormalizedFingerSqueeze(
            XRHandJointID.LittleTip,
            littleOpenDistance,
            littleClosedDistance
        );

        float targetSqueeze = GetSelectedSqueezeAmount();

        smoothedSqueeze = Mathf.Lerp(
            smoothedSqueeze,
            targetSqueeze,
            Time.deltaTime * smoothingSpeed
        );

        squeezeAmount = Mathf.Clamp01(smoothedSqueeze);

        // Keep the same normalized output used by the other scripts
        squeezeNormalized = Mathf.Clamp01(
            Mathf.InverseLerp(0.15f, 1.0f, squeezeAmount)
        );

        if (enableFrameLogs) {
            Debug.Log(
                "RAW=" + rawAverageDistance.ToString("F3") + "  " +
                "INDEX=" + indexSqueeze.ToString("F2") + "  " +
                "MIDDLE=" + middleSqueeze.ToString("F2") + "  " +
                "RING=" + ringSqueeze.ToString("F2") + "  " +
                "LITTLE=" + littleSqueeze.ToString("F2") + "  " +
                "SQUEEZE=" + squeezeNormalized.ToString("F2")
            );
        }
    }

    public void ApplyOpenCalibration(float averageDistance, float indexDistance, float middleDistance, float ringDistance, float littleDistance) {
        // Store the open hand calibration values
        openDistance = averageDistance;

        indexOpenDistance = indexDistance;
        middleOpenDistance = middleDistance;
        ringOpenDistance = ringDistance;
        littleOpenDistance = littleDistance;

        Debug.Log("Open calibration values applied");
    }

    public void ApplyClosedCalibration(float averageDistance, float indexDistance, float middleDistance, float ringDistance, float littleDistance) {
        // Store the closed hand calibration values
        closedDistance = averageDistance;

        indexClosedDistance = indexDistance;
        middleClosedDistance = middleDistance;
        ringClosedDistance = ringDistance;
        littleClosedDistance = littleDistance;

        Debug.Log("Closed calibration values applied");
    }


    float GetSelectedSqueezeAmount() {
        // Select how the final squeeze value should be computed
        if (squeezeMode == SqueezeMode.AverageAllFingers) {
            return (indexSqueeze + middleSqueeze + ringSqueeze + littleSqueeze) / 4f;
        }

        if (squeezeMode == SqueezeMode.StrongestFinger) {
            return Mathf.Max(indexSqueeze, middleSqueeze, ringSqueeze, littleSqueeze);
        }

        return GetStrongestTwoFingerAverage();
    }

    float GetStrongestTwoFingerAverage() {
        // Compute the average of the two strongest finger squeeze values
        float first = 0f;
        float second = 0f;

        UpdateTopTwo(indexSqueeze, ref first, ref second);
        UpdateTopTwo(middleSqueeze, ref first, ref second);
        UpdateTopTwo(ringSqueeze, ref first, ref second);
        UpdateTopTwo(littleSqueeze, ref first, ref second);

        return (first + second) / 2f;
    }

    void UpdateTopTwo(float value, ref float first, ref float second) {
        // Keep track of the two highest values
        if (value > first) {
            second = first;
            first = value;
            return;
        }

        if (value > second) {
            second = value;
        }
    }

    float GetNormalizedFingerSqueeze(
        XRHandJointID fingerTip,
        float openValue,
        float closedValue
    ) {
        // Convert one finger-to-palm distance into a normalized squeeze value
        if (!TryGetFingerToPalmDistance(fingerTip, out float currentDistance)) {
            return 0f;
        }

        if (Mathf.Abs(openValue - closedValue) < 0.001f) {
            return 0f;
        }

        float value = 1f - Mathf.InverseLerp(closedValue, openValue, currentDistance);

        return Mathf.Clamp01(value);
    }

    bool TryGetFingerToPalmDistance(XRHandJointID fingerTip, out float distance) {
        // Measure the distance between a fingertip and the palm
        distance = -1f;

        if (!TryGetJointPosition(XRHandJointID.Palm, out Vector3 palm)) {
            return false;
        }

        if (!TryGetJointPosition(fingerTip, out Vector3 tip)) {
            return false;
        }

        distance = Vector3.Distance(tip, palm);

        return true;
    }

    void UpdateDebugJointPositions() {
        // Update joint positions shown in the Inspector for debugging
        TryGetJointPosition(XRHandJointID.Palm, out palmPosition);
        TryGetJointPosition(XRHandJointID.ThumbTip, out thumbTipPosition);
        TryGetJointPosition(XRHandJointID.IndexTip, out indexTipPosition);
        TryGetJointPosition(XRHandJointID.MiddleTip, out middleTipPosition);
        TryGetJointPosition(XRHandJointID.RingTip, out ringTipPosition);
        TryGetJointPosition(XRHandJointID.LittleTip, out littleTipPosition);
    }

    void UpdateRuntimeDebugUI() {
        // Show tracking and squeeze values directly inside the headset
        if (!showRuntimeDebug || debugText == null) {
            return;
        }

        debugText.text =
            "Tracking: " + IsHandTracked + "\n" +
            "Mode: " + squeezeMode + "\n" +
            "Index squeeze: " + indexSqueeze.ToString("F2") + "\n" +
            "Middle squeeze: " + middleSqueeze.ToString("F2") + "\n" +
            "Ring squeeze: " + ringSqueeze.ToString("F2") + "\n" +
            "Little squeeze: " + littleSqueeze.ToString("F2") + "\n" +
            "Final squeeze: " + squeezeNormalized.ToString("F2");
    }

    public float GetAverageFingerToPalmDistance() {
        // Compute the average distance between the four main fingertips and the palm
        if (!IsHandTracked) {
            return -1f;
        }

        if (!currentHand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose)) {
            return -1f;
        }

        XRHandJointID[] tips = {
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

        float total = 0f;
        int count = 0;

        foreach (var tipID in tips) {
            if (currentHand.GetJoint(tipID).TryGetPose(out Pose tipPose)) {
                total += Vector3.Distance(tipPose.position, palmPose.position);
                count++;
            }
        }

        return count > 0 ? total / count : -1f;
    }

    bool TryGetJointPosition(XRHandJointID jointID, out Vector3 position) {
        // Try to read a tracked hand joint position
        position = Vector3.zero;

        if (!IsHandTracked) {
            return false;
        }

        if (!currentHand.GetJoint(jointID).TryGetPose(out Pose jointPose)) {
            return false;
        }

        position = jointPose.position;

        return true;
    }

    public bool TryGetFingerTipPositions(out Vector3[] fingerTipPositions) {
        // Return all fingertip positions used for ball contact detection
        fingerTipPositions = new Vector3[5];

        if (!IsHandTracked) {
            return false;
        }

        bool thumbFound = TryGetJointPosition(XRHandJointID.ThumbTip, out fingerTipPositions[0]);
        bool indexFound = TryGetJointPosition(XRHandJointID.IndexTip, out fingerTipPositions[1]);
        bool middleFound = TryGetJointPosition(XRHandJointID.MiddleTip, out fingerTipPositions[2]);
        bool ringFound = TryGetJointPosition(XRHandJointID.RingTip, out fingerTipPositions[3]);
        bool littleFound = TryGetJointPosition(XRHandJointID.LittleTip, out fingerTipPositions[4]);

        return thumbFound || indexFound || middleFound || ringFound || littleFound;
    }

    public bool TryGetPalmPosition(out Vector3 position) {
        // Return the palm position used for global hand-ball interaction
        return TryGetJointPosition(XRHandJointID.Palm, out position);
    }
}