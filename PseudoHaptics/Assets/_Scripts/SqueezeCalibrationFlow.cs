using System.Collections;
using UnityEngine;
using TMPro;

public class SqueezeCalibrationFlow : MonoBehaviour {
    public HandSqueezeDetector detector;
    public TextMeshProUGUI instructionText;

    [Header("Settings")]
    public float stabilityDuration = 3f; 
    public bool calibrationDone = false;

    IEnumerator Start() {
        yield return RunCalibration();
    }

    IEnumerator RunCalibration() {
        if (detector == null || instructionText == null) {
            yield break;
        }

        calibrationDone = false;
        yield return new WaitForSeconds(1.5f); 

        // 1. Initial Hand Check
        instructionText.text = "Please raise your hand into view";
        while (!detector.IsHandTracked) {
            yield return null;
        }

        yield return new WaitForSeconds(1.5f);

        // 2. Open Hand Phase
        instructionText.text = "Open your hand fully with palm facing down";
        
        bool handIsOpenWide = false;
        while (!handIsOpenWide) {
            if (detector.IsHandTracked) {
                // Check if the hand distance reaches an acceptable open state baseline
                if (detector.GetAverageFingerToPalmDistance() > 0.085f) {
                    handIsOpenWide = true;
                }
            }
            yield return null;
        }

        // 3. Open Hand Stability Timer
        instructionText.text = "Hold your hand open and perfectly still";
        yield return WaitForHandStability(stabilityDuration, checkOpen: true, 0f);
        
        detector.CalibrateOpen();
        float capturedOpen = detector.openDistance;

        yield return new WaitForSeconds(1.5f);

        // 4. Close Hand Phase
        instructionText.text = "Close your hand into a tight fist";

        bool motionDetected = false;
        while (!motionDetected) {
            if (detector.IsHandTracked) {
                float currentDist = detector.GetAverageFingerToPalmDistance();
                // Advance only if the hand is actively closing compared to the open calibration
                if (currentDist > 0 && currentDist < (capturedOpen - 0.02f)) {
                    motionDetected = true;
                }
            }
            yield return null; 
        }

        // 5. Closed Hand Stability Timer
        instructionText.text = "Hold your fist closed and perfectly still";
        yield return WaitForHandStability(stabilityDuration, checkOpen: false, capturedOpen);
        
        detector.CalibrateClosed();

        yield return new WaitForSeconds(1.5f);

        // 6. Validation Check
        float delta = detector.openDistance - detector.closedDistance;
        if (delta < 0.02f) { 
            instructionText.text = "Calibration failed: insufficient movement range! Restarting";
            yield return new WaitForSeconds(3f);
            StartCoroutine(RunCalibration());
            yield break;
        }

        instructionText.text = "Calibration completed successfully!";
        calibrationDone = true;

        yield return new WaitForSeconds(2.5f);
        instructionText.text = "";
    }

    IEnumerator WaitForHandStability(float duration, bool checkOpen, float referenceOpenDistance) {
        float elapsed = 0f;
        string currentActionText = instructionText.text;
        int lastSecondsLeft = -1;

        while (elapsed < duration) {
            if (!detector.IsHandTracked) {
                if (instructionText.text != "Hand lost! Please look at your hand") {
                    instructionText.text = "Hand lost! Please look at your hand";
                }
                elapsed = 0f; 
                yield return null;
                continue;
            }

            float currentDist = detector.GetAverageFingerToPalmDistance();
            bool isStable = true;
            
            if (checkOpen) {
                // If checking open hand, ensure it doesn't accidentally drop back into a closed posture
                if (currentDist < 0.08f) {
                    if (instructionText.text != "Do not close your hand! Keep it extended") {
                        instructionText.text = "Do not close your hand! Keep it extended";
                    }
                    isStable = false;
                }
            } else {
                // If checking closed hand, ensure it doesn't open past a safe fraction of the captured open distance
                if (currentDist > (referenceOpenDistance - 0.03f)) {
                    if (instructionText.text != "Do not open your hand! Keep a tight fist") {
                        instructionText.text = "Do not open your hand! Keep a tight fist";
                    }
                    isStable = false;
                }
            }

            if (!isStable) {
                elapsed = 0f; // Reset the countdown timer upon breaking posture rules
                yield return null;
                continue;
            }

            // If execution reaches this point, the posture is valid -> restore description and advance timer
            if (instructionText.text != currentActionText && elapsed == 0f) {
                instructionText.text = currentActionText;
            }

            elapsed += Time.deltaTime;
            int secondsLeft = Mathf.CeilToInt(duration - elapsed);
            
            if (secondsLeft != lastSecondsLeft) {
                instructionText.text = $"{currentActionText} ({secondsLeft})";
                lastSecondsLeft = secondsLeft;
            }
            
            yield return null;
        }
    }
}