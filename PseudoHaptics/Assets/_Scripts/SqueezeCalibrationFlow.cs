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
        if (detector == null || instructionText == null) {
            Debug.LogError("[CalibrationFlow] Missing references in the Inspector!");
            yield break;
        }

        calibrationDone = false;
        yield return new WaitForSeconds(1f); 

        // raise hand
        instructionText.text = "Raise your hand to start";
        while (!detector.IsHandTracked) {
            yield return null;
        }

        yield return new WaitForSeconds(2f);

        // OPEN hand
        instructionText.text = "Open your hand with your palm facing down";
        
        // Blocks progression until the hand is genuinely open
        bool handIsOpenWide = false;
        while (!handIsOpenWide) {
            if (detector.IsHandTracked) {
                float currentDist = detector.GetAverageFingerToPalmDistance();
                if (currentDist > 0.095f) {
                    handIsOpenWide = true;
                } else {
                    instructionText.text = "Open your hand wider (palm down)";
                }
            }
            yield return null;
        }

        instructionText.text = "Open your hand with your palm facing down";
        yield return WaitForHandStability(stabilityDuration, checkOpen: true, 0f);
        
        detector.CalibrateOpen();
        float capturedOpen = detector.openDistance;
        Debug.Log($"[Calibration] Captured Open Distance: {capturedOpen}m");

        yield return new WaitForSeconds(2f);

        // CLOSE hand
        instructionText.text = "Close your hand, always keep your palm facing down";

        // Blocks progression until the user actively shrinks their hand
        bool motionDetected = false;
        while (!motionDetected) {
            if (detector.IsHandTracked) {
                float currentDist = detector.GetAverageFingerToPalmDistance();
                if (currentDist > 0 && currentDist < (capturedOpen - 0.025f)) {
                    motionDetected = true;
                }
            }
            yield return null; 
        }

        instructionText.text = "Hold your fist closed still";
        yield return WaitForHandStability(stabilityDuration, checkOpen: false, capturedOpen);
        
        detector.CalibrateClosed();
        Debug.Log($"[Calibration] Captured Closed Distance: {detector.closedDistance}m");

        yield return new WaitForSeconds(2f);

        // FINAL CHECK
        float delta = detector.openDistance - detector.closedDistance;
        if (delta < 0.025f) { 
            instructionText.text = "Calibration error: insufficient movement! Restarting";
            yield return new WaitForSeconds(3f);
            StartCoroutine(Start()); 
            yield break;
        }

        instructionText.text = "Calibration completed successfully";
        calibrationDone = true;

        yield return new WaitForSeconds(2f);
        instructionText.text = "";
    }

    // Check that the hand remains in correct position
    IEnumerator WaitForHandStability(float duration, bool checkOpen, float referenceOpenDistance) {
        float elapsed = 0f;
        string baseText = instructionText.text;

        while (elapsed < duration) {
            if (!detector.IsHandTracked) { //tracking hand
                instructionText.text = "Hand lost! Please reposition your hand";
                elapsed = 0f; 
                yield return null;
                continue;
            }

            // position correct
            float currentDist = detector.GetAverageFingerToPalmDistance();
            
            if (checkOpen) {
                if (currentDist < 0.095f) {
                    instructionText.text = "Do not close your hand! Keep it open";
                    elapsed = 0f;
                    yield return null;
                    continue;
                }
            } else {
                if (currentDist > (referenceOpenDistance - 0.02f)) {
                    instructionText.text = "Do not open your hand! Keep it closed";
                    elapsed = 0f;
                    yield return null;
                    continue;
                }
            }

            // position correct, update timer
            instructionText.text = baseText;
            elapsed += Time.deltaTime;
            int secondsLeft = Mathf.CeilToInt(duration - elapsed);
            instructionText.text = instructionText.text + $" ({secondsLeft})";
            yield return null;
        }
    }
}