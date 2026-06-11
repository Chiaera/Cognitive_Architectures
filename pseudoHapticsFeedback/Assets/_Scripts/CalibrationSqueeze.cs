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

    public HandSqueezeDetector detector;
    public TextMeshProUGUI instructionText;

    [Header("Experiment UI Trigger")]
    [Tooltip("The GameObject containing the buttons for the experiment")]
    public GameObject buttonsGroup;

    [Tooltip("The virtual stress ball shown after calibration")]
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

    [Tooltip("Maximum allowed distance variation during calibration")]
    public float maxDistanceVariation = 0.015f;

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

        if (buttonsGroup != null) {
            buttonsGroup.SetActive(false);
        }

        if (stressBall != null) {
            stressBall.SetActive(false);
        }

        yield return new WaitForSeconds(1.5f);

        instructionText.text = "Please raise your hand into view";

        while (!detector.IsHandTracked) {
            yield return null;
        }

        yield return new WaitForSeconds(1.5f);

        instructionText.text = "Open your hand fully with palm facing down";

        bool handIsOpenWide = false;

        while (!handIsOpenWide) {
            if (detector.IsHandTracked) {
                float currentDist = detector.GetAverageFingerToPalmDistance();

                if (currentDist > 0.085f) {
                    handIsOpenWide = true;
                }
            }

            yield return null;
        }

        instructionText.text = "Hold your hand open";

        FingerCalibrationSnapshot capturedOpen = null;

        yield return WaitForHandStabilityAndAverage(
            stabilityDuration,
            checkOpen: true,
            referenceOpenDistance: 0f,
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
                float currentDist = detector.GetAverageFingerToPalmDistance();

                if (currentDist > 0f && currentDist < capturedOpenAverage * closedRelativeThreshold) {
                    motionDetected = true;
                }
            }

            yield return null;
        }

        instructionText.text = "Hold your fist closed";

        FingerCalibrationSnapshot capturedClosed = null;

        yield return WaitForHandStabilityAndAverage(
            stabilityDuration,
            checkOpen: false,
            referenceOpenDistance: capturedOpenAverage,
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

        instructionText.text = "Calibration completed successfully";
        calibrationDone = true;

        Debug.Log("Calibration completed successfully");
        Debug.Log("Open distance " + detector.openDistance.ToString("F3"));
        Debug.Log("Closed distance " + detector.closedDistance.ToString("F3"));
        Debug.Log("Calibration range " + delta.ToString("F3"));

        yield return new WaitForSeconds(2.5f);

        instructionText.text = "";

        if (stressBall != null) {
            stressBall.SetActive(true);
        }

        if (buttonsGroup != null) {
            buttonsGroup.SetActive(true);
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

            float currentDist = detector.GetAverageFingerToPalmDistance();

            if (currentDist <= 0f) {
                yield return null;
                continue;
            }

            if (!detector.TryGetFingerToPalmDistances(
                out float indexDistance,
                out float middleDistance,
                out float ringDistance,
                out float littleDistance
            )) {
                yield return null;
                continue;
            }

            bool validPose = true;

            if (checkOpen) {
                if (currentDist < 0.08f) {
                    instructionText.text = "Do not close your hand. Keep it open";
                    validPose = false;
                }
            } else {
                if (currentDist > referenceOpenDistance * closedRelativeThreshold) {
                    instructionText.text = "Do not open your hand. Keep a tight fist";
                    validPose = false;
                }
            }

            if (!validPose) {
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

            averageSum += currentDist;
            indexSum += indexDistance;
            middleSum += middleDistance;
            ringSum += ringDistance;
            littleSum += littleDistance;
            samples++;

            minValue = Mathf.Min(minValue, currentDist);
            maxValue = Mathf.Max(maxValue, currentDist);

            float variation = maxValue - minValue;

            if (variation > maxDistanceVariation) {
                instructionText.text = "Keep your hand position steady";

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

        if (samples > 0) {
            FingerCalibrationSnapshot snapshot = new FingerCalibrationSnapshot();

            snapshot.averageDistance = averageSum / samples;
            snapshot.indexDistance = indexSum / samples;
            snapshot.middleDistance = middleSum / samples;
            snapshot.ringDistance = ringSum / samples;
            snapshot.littleDistance = littleSum / samples;

            onAverageComputed?.Invoke(snapshot);
        }
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
        // Reset all temporary calibration sampling values
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
}