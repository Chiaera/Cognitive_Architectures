using System.Collections;
using UnityEngine;
using TMPro;

public class CalibrationSqueeze : MonoBehaviour {
    [System.Serializable]
    public class FingerCalibrationSnapshot {
        public float averageDistance;
        public float indexDistance;
        public float middleDistance;
        public float ringDistance;
        public float littleDistance;
    }

    [Header("References")]
    public HandSqueezeDetector detector;
    public TextMeshProUGUI instructionText;

    [Header("Experiment Flow")]
    public SoftBallRatingStaircaseController staircaseController;

    [Header("Experiment UI Trigger")]
    [Tooltip("The GameObject containing the buttons for the experiment")]
    public GameObject buttonsGroup;

    [Tooltip("The virtual stress ball shown only after calibration")]
    public GameObject stressBall;

    [Header("Settings")]
    public float stabilityDuration = 3f;
    public bool calibrationDone = false;

    [Header("Validation")]
    [Tooltip("Minimum required difference between open and closed hand distances")]
    public float minimumRange = 0.02f;

    [Tooltip("Closed hand must be smaller than this percentage of the open hand distance")]
    [Range(0.4f, 0.95f)]
    public float closedRelativeThreshold = 0.75f;

    [Tooltip("Maximum allowed distance variation during open or closed calibration")]
    public float maxDistanceVariation = 0.015f;

    [Header("Empty Squeeze Gesture Calibration")]
    [Tooltip("Enable an additional empty squeeze gesture calibration")]
    public bool calibrateSqueezeGesture = true;

    [Tooltip("Number of valid squeeze attempts required")]
    public int squeezeGestureAttemptsRequired = 3;

    [Tooltip("Minimum squeeze value required to accept a gesture attempt")]
    [Range(0f, 1f)]
    public float squeezeAttemptThreshold = 0.55f;

    [Tooltip("The squeeze value must go below this value before the next attempt")]
    [Range(0f, 1f)]
    public float squeezeReleaseThreshold = 0.30f;

    [Header("Calibrated Empty Squeeze Data")]
    public bool squeezeGestureCalibrated = false;
    public Vector3 calibratedThumbOffsetFromPalm = Vector3.zero;
    public Vector3 calibratedLittleOffsetFromPalm = Vector3.zero;
    public Vector3 calibratedFingerBandOffsetFromPalm = Vector3.zero;
    public float calibratedThumbPalmDistance = 0f;
    public float calibratedLittlePalmDistance = 0f;
    public float calibratedThumbLittleDistance = 0f;

    [Header("Legacy Optional Surface Data")]
    [Tooltip("Optional ball center. Not used for empty squeeze calibration")]
    public Transform ballCenter;

    [Tooltip("Optional ball radius. Not used for empty squeeze calibration")]
    public float ballRadiusMeters = 0.065f;

    IEnumerator Start() {
        if (buttonsGroup != null) {
            buttonsGroup.SetActive(false);
        }

        if (stressBall != null) {
            stressBall.SetActive(false);
        }

        yield return RunCalibration();
    }

    IEnumerator RunCalibration() {
        if (detector == null || instructionText == null) {
            Debug.LogWarning("Calibration setup missing");
            yield break;
        }

        calibrationDone = false;
        squeezeGestureCalibrated = false;

        if (buttonsGroup != null) {
            buttonsGroup.SetActive(false);
        }

        if (stressBall != null) {
            stressBall.SetActive(false);
        }

        yield return new WaitForSeconds(1.5f);

        instructionText.text = "Please raise your LEFT hand into view";

        while (!detector.IsHandTracked) {
            yield return null;
        }

        yield return new WaitForSeconds(1.5f);

        instructionText.text = "Open your hand fully with palm facing down";

        bool handIsOpenWide = false;

        while (!handIsOpenWide) {
            if (detector.IsHandTracked) {
                float currentDistance = detector.GetAverageFingerToPalmDistance();

                if (currentDistance > 0.085f) {
                    handIsOpenWide = true;
                }
            }

            yield return null;
        }

        instructionText.text = "Hold your hand open (palm facing down)";

        FingerCalibrationSnapshot capturedOpen = null;

        yield return WaitForHandStabilityAndAverage(
            stabilityDuration,
            true,
            0f,
            result => capturedOpen = result
        );

        if (capturedOpen == null) {
            instructionText.text = "Calibration failed. Restarting";
            yield return new WaitForSeconds(3f);
            yield return RunCalibration();
            yield break;
        }

        detector.ApplyOpenCalibration(
            capturedOpen.averageDistance,
            capturedOpen.indexDistance,
            capturedOpen.middleDistance,
            capturedOpen.ringDistance,
            capturedOpen.littleDistance
        );

        float capturedOpenAverage = capturedOpen.averageDistance;

        Debug.Log("Open calibration average set to " + capturedOpenAverage.ToString("F3"));
        Debug.Log("Open index distance set to " + capturedOpen.indexDistance.ToString("F3"));
        Debug.Log("Open middle distance set to " + capturedOpen.middleDistance.ToString("F3"));
        Debug.Log("Open ring distance set to " + capturedOpen.ringDistance.ToString("F3"));
        Debug.Log("Open little distance set to " + capturedOpen.littleDistance.ToString("F3"));

        yield return new WaitForSeconds(1.5f);

        instructionText.text = "Close your hand into a tight fist";

        bool motionDetected = false;

        while (!motionDetected) {
            if (detector.IsHandTracked) {
                float currentDistance = detector.GetAverageFingerToPalmDistance();

                if (currentDistance > 0f && currentDistance < capturedOpenAverage * closedRelativeThreshold) {
                    motionDetected = true;
                }
            }

            yield return null;
        }

        instructionText.text = "Hold your fist closed";

        FingerCalibrationSnapshot capturedClosed = null;

        yield return WaitForHandStabilityAndAverage(
            stabilityDuration,
            false,
            capturedOpenAverage,
            result => capturedClosed = result
        );

        if (capturedClosed == null) {
            instructionText.text = "Calibration failed. Restarting";
            yield return new WaitForSeconds(3f);
            yield return RunCalibration();
            yield break;
        }

        detector.ApplyClosedCalibration(
            capturedClosed.averageDistance,
            capturedClosed.indexDistance,
            capturedClosed.middleDistance,
            capturedClosed.ringDistance,
            capturedClosed.littleDistance
        );

        Debug.Log("Closed calibration average set to " + capturedClosed.averageDistance.ToString("F3"));
        Debug.Log("Closed index distance set to " + capturedClosed.indexDistance.ToString("F3"));
        Debug.Log("Closed middle distance set to " + capturedClosed.middleDistance.ToString("F3"));
        Debug.Log("Closed ring distance set to " + capturedClosed.ringDistance.ToString("F3"));
        Debug.Log("Closed little distance set to " + capturedClosed.littleDistance.ToString("F3"));

        yield return new WaitForSeconds(1.5f);

        float delta = detector.openDistance - detector.closedDistance;

        if (delta < minimumRange) {
            instructionText.text = "Calibration failed: insufficient movement range. Restarting";
            Debug.LogWarning("Calibration failed because movement range was too small");

            yield return new WaitForSeconds(3f);
            yield return RunCalibration();
            yield break;
        }

        if (calibrateSqueezeGesture) {
            if (stressBall != null) {
                stressBall.SetActive(false);
            }

            if (buttonsGroup != null) {
                buttonsGroup.SetActive(false);
            }

            instructionText.text = "Open your hand again before the squeeze gestures and turn your palm up";

            yield return WaitForSqueezeReleaseBeforeGesture();

            yield return new WaitForSeconds(0.8f);

            instructionText.text = "Perform the squeeze gesture in the air";

            yield return new WaitForSeconds(1f);

            bool squeezeGestureCaptured = false;

            yield return WaitForSqueezeGestureAttemptsAndAverage(
                squeezeGestureAttemptsRequired,
                result => squeezeGestureCaptured = result
            );

            if (!squeezeGestureCaptured) {
                instructionText.text = "Squeeze gesture calibration failed. Restarting";
                Debug.LogWarning("Squeeze gesture calibration failed");

                yield return new WaitForSeconds(3f);
                yield return RunCalibration();
                yield break;
            }

            Debug.Log("Squeeze gesture calibration completed");
        }

        instructionText.text = "Calibration completed successfully";
        calibrationDone = true;

        Debug.Log("Calibration completed successfully");
        Debug.Log("Open distance " + detector.openDistance.ToString("F3"));
        Debug.Log("Closed distance " + detector.closedDistance.ToString("F3"));
        Debug.Log("Calibration range " + delta.ToString("F3"));

        yield return new WaitForSeconds(2.5f);

        if (staircaseController != null) {
            staircaseController.BeginExperimentAfterCalibration();
        }
        else {
            Debug.LogWarning("Staircase controller is not assigned in CalibrationSqueeze");
        }
    }

    IEnumerator WaitForHandStabilityAndAverage(
        float duration,
        bool checkOpen,
        float referenceOpenDistance,
        System.Action<FingerCalibrationSnapshot> onAverageComputed
    ) {
        float elapsed = 0f;

        float averageSum = 0f;
        float indexSum = 0f;
        float middleSum = 0f;
        float ringSum = 0f;
        float littleSum = 0f;

        int samples = 0;

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        string currentActionText = instructionText.text;
        int lastSecondsLeft = -1;

        while (elapsed < duration) {
            if (!detector.IsHandTracked) {
                instructionText.text = "Hand lost. Please look at your hand";

                ResetSamplingState(
                    ref elapsed,
                    ref averageSum,
                    ref indexSum,
                    ref middleSum,
                    ref ringSum,
                    ref littleSum,
                    ref samples,
                    ref minValue,
                    ref maxValue
                );

                yield return null;
                continue;
            }

            float averageDistance = detector.GetAverageFingerToPalmDistance();

            if (averageDistance <= 0f) {
                yield return null;
                continue;
            }

            if (checkOpen && averageDistance < 0.075f) {
                instructionText.text = "Open your hand more";

                ResetSamplingState(
                    ref elapsed,
                    ref averageSum,
                    ref indexSum,
                    ref middleSum,
                    ref ringSum,
                    ref littleSum,
                    ref samples,
                    ref minValue,
                    ref maxValue
                );

                yield return null;
                continue;
            }

            if (!checkOpen && referenceOpenDistance > 0f && averageDistance > referenceOpenDistance * closedRelativeThreshold) {
                instructionText.text = "Close your hand more";

                ResetSamplingState(
                    ref elapsed,
                    ref averageSum,
                    ref indexSum,
                    ref middleSum,
                    ref ringSum,
                    ref littleSum,
                    ref samples,
                    ref minValue,
                    ref maxValue
                );

                yield return null;
                continue;
            }

            float indexDistance = detector.GetFingerToPalmDistance(1);
            float middleDistance = detector.GetFingerToPalmDistance(2);
            float ringDistance = detector.GetFingerToPalmDistance(3);
            float littleDistance = detector.GetFingerToPalmDistance(4);

            averageSum += averageDistance;
            indexSum += indexDistance;
            middleSum += middleDistance;
            ringSum += ringDistance;
            littleSum += littleDistance;

            samples++;

            minValue = Mathf.Min(minValue, averageDistance);
            maxValue = Mathf.Max(maxValue, averageDistance);

            float variation = maxValue - minValue;

            if (variation > maxDistanceVariation) {
                instructionText.text = "Hold your hand still";

                ResetSamplingState(
                    ref elapsed,
                    ref averageSum,
                    ref indexSum,
                    ref middleSum,
                    ref ringSum,
                    ref littleSum,
                    ref samples,
                    ref minValue,
                    ref maxValue
                );

                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;

            int secondsLeft = Mathf.CeilToInt(duration - elapsed);

            if (secondsLeft != lastSecondsLeft) {
                instructionText.text = currentActionText + " (" + secondsLeft + ")";
                lastSecondsLeft = secondsLeft;
            }

            yield return null;
        }

        if (samples <= 0) {
            onAverageComputed?.Invoke(null);
            yield break;
        }

        FingerCalibrationSnapshot snapshot = new FingerCalibrationSnapshot {
            averageDistance = averageSum / samples,
            indexDistance = indexSum / samples,
            middleDistance = middleSum / samples,
            ringDistance = ringSum / samples,
            littleDistance = littleSum / samples
        };

        onAverageComputed?.Invoke(snapshot);
    }

    IEnumerator WaitForSqueezeReleaseBeforeGesture() {
        while (true) {
            if (!detector.IsHandTracked) {
                instructionText.text = "Hand lost. Please look at your hand";
                yield return null;
                continue;
            }

            instructionText.text = "Open your hand again before the squeeze gestures";

            if (detector.squeezeNormalized < squeezeReleaseThreshold) {
                break;
            }

            yield return null;
        }

        Debug.Log("Hand released before squeeze gesture calibration");
    }

    IEnumerator WaitForSqueezeGestureAttemptsAndAverage(
        int requiredAttempts,
        System.Action<bool> onGestureCaptured
    ) {
        Vector3 thumbOffsetSum = Vector3.zero;
        Vector3 littleOffsetSum = Vector3.zero;
        Vector3 fingerBandOffsetSum = Vector3.zero;

        float thumbPalmDistanceSum = 0f;
        float littlePalmDistanceSum = 0f;
        float thumbLittleDistanceSum = 0f;

        int validAttempts = 0;
        bool waitingForRelease = false;

        squeezeGestureCalibrated = false;

        while (validAttempts < requiredAttempts) {
            if (!detector.IsHandTracked) {
                instructionText.text = "Hand lost. Please look at your hand";
                yield return null;
                continue;
            }

            if (waitingForRelease) {
                instructionText.text = "Release your hand before the next squeeze";

                if (detector.squeezeNormalized < squeezeReleaseThreshold) {
                    waitingForRelease = false;
                }

                yield return null;
                continue;
            }

            instructionText.text = "Perform squeeze " + (validAttempts + 1) + " of " + requiredAttempts;

            if (detector.squeezeNormalized < squeezeAttemptThreshold) {
                yield return null;
                continue;
            }

            if (!detector.TryGetPalmPosition(out Vector3 palmPosition)) {
                yield return null;
                continue;
            }

            if (!detector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
                yield return null;
                continue;
            }

            if (fingerPositions == null || fingerPositions.Length < 5) {
                yield return null;
                continue;
            }

            Vector3 thumbPosition = fingerPositions[0];
            Vector3 indexPosition = fingerPositions[1];
            Vector3 middlePosition = fingerPositions[2];
            Vector3 ringPosition = fingerPositions[3];
            Vector3 littlePosition = fingerPositions[4];

            Vector3 fingerBandPosition = (
                thumbPosition +
                indexPosition +
                middlePosition +
                ringPosition +
                littlePosition
            ) / 5f;

            Vector3 thumbOffset = thumbPosition - palmPosition;
            Vector3 littleOffset = littlePosition - palmPosition;
            Vector3 fingerBandOffset = fingerBandPosition - palmPosition;

            if (
                thumbOffset.sqrMagnitude < 0.0001f ||
                littleOffset.sqrMagnitude < 0.0001f ||
                fingerBandOffset.sqrMagnitude < 0.0001f
            ) {
                yield return null;
                continue;
            }

            thumbOffsetSum += thumbOffset;
            littleOffsetSum += littleOffset;
            fingerBandOffsetSum += fingerBandOffset;

            thumbPalmDistanceSum += Vector3.Distance(thumbPosition, palmPosition);
            littlePalmDistanceSum += Vector3.Distance(littlePosition, palmPosition);
            thumbLittleDistanceSum += Vector3.Distance(thumbPosition, littlePosition);

            validAttempts++;
            waitingForRelease = true;

            instructionText.text = "Squeeze accepted";

            Debug.Log("Squeeze gesture attempt accepted " + validAttempts.ToString());

            yield return new WaitForSeconds(0.6f);
        }

        if (validAttempts <= 0) {
            onGestureCaptured?.Invoke(false);
            yield break;
        }

        calibratedThumbOffsetFromPalm = thumbOffsetSum / validAttempts;
        calibratedLittleOffsetFromPalm = littleOffsetSum / validAttempts;
        calibratedFingerBandOffsetFromPalm = fingerBandOffsetSum / validAttempts;

        calibratedThumbPalmDistance = thumbPalmDistanceSum / validAttempts;
        calibratedLittlePalmDistance = littlePalmDistanceSum / validAttempts;
        calibratedThumbLittleDistance = thumbLittleDistanceSum / validAttempts;

        squeezeGestureCalibrated = true;

        Debug.Log("Calibrated thumb offset from palm " + calibratedThumbOffsetFromPalm.ToString("F3"));
        Debug.Log("Calibrated little offset from palm " + calibratedLittleOffsetFromPalm.ToString("F3"));
        Debug.Log("Calibrated finger band offset from palm " + calibratedFingerBandOffsetFromPalm.ToString("F3"));
        Debug.Log("Calibrated thumb palm distance " + calibratedThumbPalmDistance.ToString("F3"));
        Debug.Log("Calibrated little palm distance " + calibratedLittlePalmDistance.ToString("F3"));
        Debug.Log("Calibrated thumb little distance " + calibratedThumbLittleDistance.ToString("F3"));

        onGestureCaptured?.Invoke(true);
    }

    void ResetSamplingState(
        ref float elapsed,
        ref float averageSum,
        ref float indexSum,
        ref float middleSum,
        ref float ringSum,
        ref float littleSum,
        ref int samples,
        ref float minValue,
        ref float maxValue
    ) {
        elapsed = 0f;
        averageSum = 0f;
        indexSum = 0f;
        middleSum = 0f;
        ringSum = 0f;
        littleSum = 0f;
        samples = 0;
        minValue = float.MaxValue;
        maxValue = float.MinValue;
    }

    public bool TryGetEstimatedThumbPositionFromPalm(Vector3 palmPosition, out Vector3 position) {
        position = Vector3.zero;

        if (!squeezeGestureCalibrated) {
            return false;
        }

        if (calibratedThumbOffsetFromPalm.sqrMagnitude < 0.0001f) {
            return false;
        }

        position = palmPosition + calibratedThumbOffsetFromPalm;
        return true;
    }

    public bool TryGetEstimatedLittlePositionFromPalm(Vector3 palmPosition, out Vector3 position) {
        position = Vector3.zero;

        if (!squeezeGestureCalibrated) {
            return false;
        }

        if (calibratedLittleOffsetFromPalm.sqrMagnitude < 0.0001f) {
            return false;
        }

        position = palmPosition + calibratedLittleOffsetFromPalm;
        return true;
    }

    public bool TryGetEstimatedFingerBandPositionFromPalm(Vector3 palmPosition, out Vector3 position) {
        position = Vector3.zero;

        if (!squeezeGestureCalibrated) {
            return false;
        }

        if (calibratedFingerBandOffsetFromPalm.sqrMagnitude < 0.0001f) {
            return false;
        }

        position = palmPosition + calibratedFingerBandOffsetFromPalm;
        return true;
    }

    public bool TryGetCalibratedLittleSurfacePosition(out Vector3 position) {
        position = Vector3.zero;
        return false;
    }

    public bool TryGetCalibratedThumbSurfacePosition(out Vector3 position) {
        position = Vector3.zero;
        return false;
    }

    public bool TryGetCalibratedFingerBandSurfacePosition(out Vector3 position) {
        position = Vector3.zero;
        return false;
    }
}