using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class HandSqueezeDetector : MonoBehaviour {
    [Header("Hand Selection")]
    [Tooltip("Use the left hand for the squeeze interaction")]
    public bool useLeftHand = true;

    [Header("Calibration")]
    [Tooltip("Average open hand distance from fingertips to palm")]
    public float openDistance = 0.12f;

    [Tooltip("Average closed hand distance from fingertips to palm")]
    public float closedDistance = 0.04f;

    [Header("Per-Finger Open Calibration")]
    public float thumbOpenDistance = 0.10f;
    public float indexOpenDistance = 0.12f;
    public float middleOpenDistance = 0.12f;
    public float ringOpenDistance = 0.11f;
    public float littleOpenDistance = 0.10f;

    [Header("Per-Finger Closed Calibration")]
    public float thumbClosedDistance = 0.04f;
    public float indexClosedDistance = 0.04f;
    public float middleClosedDistance = 0.04f;
    public float ringClosedDistance = 0.04f;
    public float littleClosedDistance = 0.04f;

    [Header("Smoothing")]
    [Tooltip("Smoothing speed for the global squeeze value")]
    public float smoothingSpeed = 8f;

    [Tooltip("Smoothing speed for per-finger pressure values")]
    public float fingerSmoothingSpeed = 10f;

    [Header("Output")]
    [Range(0f, 1f)]
    public float squeezeNormalized = 0f;

    [Range(0f, 1f)]
    public float thumbPressure = 0f;

    [Range(0f, 1f)]
    public float indexPressure = 0f;

    [Range(0f, 1f)]
    public float middlePressure = 0f;

    [Range(0f, 1f)]
    public float ringPressure = 0f;

    [Range(0f, 1f)]
    public float littlePressure = 0f;

    [Header("Debug")]
    public bool isHandTracked = false;
    public float currentAverageDistance = 0f;
    public float currentThumbDistance = 0f;
    public float currentIndexDistance = 0f;
    public float currentMiddleDistance = 0f;
    public float currentRingDistance = 0f;
    public float currentLittleDistance = 0f;

    private XRHandSubsystem handSubsystem;
    private XRHand currentHand;

    private Vector3 palmPosition;
    private Vector3[] fingertipPositions = new Vector3[5];

    private bool hasPalmPosition = false;
    private bool hasFingerPositions = false;

    public bool IsHandTracked {
        get { return isHandTracked; }
    }

    void Start() {
        TryInitializeHandSubsystem();

        Debug.Log("Hand squeeze detector initialized");
    }

    void Update() {
        if (handSubsystem == null) {
            TryInitializeHandSubsystem();
        }

        UpdateHandData();
    }

    void TryInitializeHandSubsystem() {
        if (XRGeneralSettings.Instance == null) {
            return;
        }

        if (XRGeneralSettings.Instance.Manager == null) {
            return;
        }

        XRLoader loader = XRGeneralSettings.Instance.Manager.activeLoader;

        if (loader == null) {
            return;
        }

        handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
    }

    void UpdateHandData() {
        isHandTracked = false;
        hasPalmPosition = false;
        hasFingerPositions = false;

        if (handSubsystem == null || !handSubsystem.running) {
            ResetOutputs();
            return;
        }

        currentHand = useLeftHand ? handSubsystem.leftHand : handSubsystem.rightHand;

        if (!currentHand.isTracked) {
            ResetOutputs();
            return;
        }

        isHandTracked = true;

        bool palmFound = TryReadPalmPosition(currentHand, out palmPosition);
        bool fingersFound = TryReadFingertipPositions(currentHand, fingertipPositions);

        hasPalmPosition = palmFound;
        hasFingerPositions = fingersFound;

        if (!palmFound || !fingersFound) {
            ResetOutputs();
            return;
        }

        UpdateDistances();
        UpdateSqueezeValues();
    }

    bool TryReadPalmPosition(XRHand hand, out Vector3 position) {
        position = Vector3.zero;

        XRHandJoint palmJoint = hand.GetJoint(XRHandJointID.Palm);

        if (!palmJoint.TryGetPose(out Pose palmPose)) {
            return false;
        }

        position = palmPose.position;
        return true;
    }

    bool TryReadFingertipPositions(XRHand hand, Vector3[] positions) {
        if (positions == null || positions.Length < 5) {
            return false;
        }

        bool thumbFound = TryReadJointPosition(hand, XRHandJointID.ThumbTip, out positions[0]);
        bool indexFound = TryReadJointPosition(hand, XRHandJointID.IndexTip, out positions[1]);
        bool middleFound = TryReadJointPosition(hand, XRHandJointID.MiddleTip, out positions[2]);
        bool ringFound = TryReadJointPosition(hand, XRHandJointID.RingTip, out positions[3]);
        bool littleFound = TryReadJointPosition(hand, XRHandJointID.LittleTip, out positions[4]);

        return thumbFound && indexFound && middleFound && ringFound && littleFound;
    }

    bool TryReadJointPosition(XRHand hand, XRHandJointID jointId, out Vector3 position) {
        position = Vector3.zero;

        XRHandJoint joint = hand.GetJoint(jointId);

        if (!joint.TryGetPose(out Pose pose)) {
            return false;
        }

        position = pose.position;
        return true;
    }

    void UpdateDistances() {
        currentThumbDistance = Vector3.Distance(fingertipPositions[0], palmPosition);
        currentIndexDistance = Vector3.Distance(fingertipPositions[1], palmPosition);
        currentMiddleDistance = Vector3.Distance(fingertipPositions[2], palmPosition);
        currentRingDistance = Vector3.Distance(fingertipPositions[3], palmPosition);
        currentLittleDistance = Vector3.Distance(fingertipPositions[4], palmPosition);

        currentAverageDistance = (
            currentThumbDistance +
            currentIndexDistance +
            currentMiddleDistance +
            currentRingDistance +
            currentLittleDistance
        ) / 5f;
    }

    void UpdateSqueezeValues() {
        float targetSqueeze = DistanceToPressure(
            currentAverageDistance,
            openDistance,
            closedDistance
        );

        squeezeNormalized = Mathf.Lerp(
            squeezeNormalized,
            targetSqueeze,
            Time.deltaTime * smoothingSpeed
        );

        thumbPressure = SmoothFingerPressure(
            thumbPressure,
            DistanceToPressure(currentThumbDistance, thumbOpenDistance, thumbClosedDistance)
        );

        indexPressure = SmoothFingerPressure(
            indexPressure,
            DistanceToPressure(currentIndexDistance, indexOpenDistance, indexClosedDistance)
        );

        middlePressure = SmoothFingerPressure(
            middlePressure,
            DistanceToPressure(currentMiddleDistance, middleOpenDistance, middleClosedDistance)
        );

        ringPressure = SmoothFingerPressure(
            ringPressure,
            DistanceToPressure(currentRingDistance, ringOpenDistance, ringClosedDistance)
        );

        littlePressure = SmoothFingerPressure(
            littlePressure,
            DistanceToPressure(currentLittleDistance, littleOpenDistance, littleClosedDistance)
        );
    }

    float SmoothFingerPressure(float currentValue, float targetValue) {
        return Mathf.Lerp(
            currentValue,
            targetValue,
            Time.deltaTime * fingerSmoothingSpeed
        );
    }

    float DistanceToPressure(float currentDistance, float openValue, float closedValue) {
        float range = openValue - closedValue;

        if (Mathf.Abs(range) < 0.0001f) {
            return 0f;
        }

        float pressure = Mathf.InverseLerp(openValue, closedValue, currentDistance);
        return Mathf.Clamp01(pressure);
    }

    void ResetOutputs() {
        squeezeNormalized = Mathf.Lerp(
            squeezeNormalized,
            0f,
            Time.deltaTime * smoothingSpeed
        );

        thumbPressure = SmoothFingerPressure(thumbPressure, 0f);
        indexPressure = SmoothFingerPressure(indexPressure, 0f);
        middlePressure = SmoothFingerPressure(middlePressure, 0f);
        ringPressure = SmoothFingerPressure(ringPressure, 0f);
        littlePressure = SmoothFingerPressure(littlePressure, 0f);

        currentAverageDistance = 0f;
        currentThumbDistance = 0f;
        currentIndexDistance = 0f;
        currentMiddleDistance = 0f;
        currentRingDistance = 0f;
        currentLittleDistance = 0f;
    }

    public bool TryGetPalmPosition(out Vector3 position) {
        position = Vector3.zero;

        if (!isHandTracked || !hasPalmPosition) {
            return false;
        }

        position = palmPosition;
        return true;
    }

    public bool TryGetFingerTipPositions(out Vector3[] positions) {
        positions = null;

        if (!isHandTracked || !hasFingerPositions) {
            return false;
        }

        positions = new Vector3[5];

        for (int i = 0; i < fingertipPositions.Length; i++) {
            positions[i] = fingertipPositions[i];
        }

        return true;
    }

    public float GetAverageFingerToPalmDistance() {
        if (!isHandTracked || !hasFingerPositions || !hasPalmPosition) {
            return 0f;
        }

        return currentAverageDistance;
    }

    public float GetFingerToPalmDistance(int fingerIndex) {
        if (!isHandTracked || !hasFingerPositions || !hasPalmPosition) {
            return 0f;
        }

        switch (fingerIndex) {
            case 0:
                return currentThumbDistance;

            case 1:
                return currentIndexDistance;

            case 2:
                return currentMiddleDistance;

            case 3:
                return currentRingDistance;

            case 4:
                return currentLittleDistance;

            default:
                return 0f;
        }
    }

    public float[] GetAllFingerPressures() {
        return new float[] {
            thumbPressure,
            indexPressure,
            middlePressure,
            ringPressure,
            littlePressure
        };
    }

    public void ApplyOpenCalibration(
        float average,
        float index,
        float middle,
        float ring,
        float little
    ) {
        openDistance = Mathf.Max(average, 0.001f);

        indexOpenDistance = Mathf.Max(index, 0.001f);
        middleOpenDistance = Mathf.Max(middle, 0.001f);
        ringOpenDistance = Mathf.Max(ring, 0.001f);
        littleOpenDistance = Mathf.Max(little, 0.001f);

        thumbOpenDistance = openDistance;

        Debug.Log("Open hand calibration applied");
    }

    public void ApplyClosedCalibration(
        float average,
        float index,
        float middle,
        float ring,
        float little
    ) {
        closedDistance = Mathf.Max(average, 0.001f);

        indexClosedDistance = Mathf.Max(index, 0.001f);
        middleClosedDistance = Mathf.Max(middle, 0.001f);
        ringClosedDistance = Mathf.Max(ring, 0.001f);
        littleClosedDistance = Mathf.Max(little, 0.001f);

        thumbClosedDistance = closedDistance;

        Debug.Log("Closed hand calibration applied");
    }
}