using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Staircase;

public class ExperimentManager : MonoBehaviour {
    [System.Serializable]
    public class TrialData {
        public int trialID;
        public float cdRatio;
        public string userResponse;
        public bool stimulusNoticed;
        public float responseTime;
        public float peakSqueeze;
    }

    [Header("References")]
    public HandSqueezeDetector handDetector;
    public SqueezeCalibrationFlow calibrationFlow;
    public SqueezeBall squeezeBall;

    [Header("Settings")]
    public bool requireCalibration = false;
    public int participantNumber = 1;

    private List<TrialData> experimentLog = new List<TrialData>();

    private int currentTrialID = 0;
    private float currentCDRatio;
    private float trialStartTime;
    private float peakSqueeze;
    private string participantID;

    void Start() {
        // Generate unique participant ID based on current timestamp
        participantID = "SUBJ_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Debug.Log($"[ExperimentManager] Ready for device testing: {participantID}");
    }

    void Update() {
        if (handDetector != null) {
            peakSqueeze = Mathf.Max(peakSqueeze, handDetector.squeezeNormalized);
        }
    }

    public void StartNextTrial() {
        if (requireCalibration && calibrationFlow != null && !calibrationFlow.calibrationDone) {
            Debug.LogWarning("Calibration not completed");
            return;
        }

        currentTrialID++;
        peakSqueeze = 0f;
        trialStartTime = Time.time;

        // Check if the Staircase Procedure singleton instance is valid
        if (StaircaseProcedure.SP != null) {
            currentCDRatio = StaircaseProcedure.SP.GetNextStimulus();
        } else {
            // Fallback values for testing environment when toolkit is offline
            currentCDRatio = (currentTrialID % 2 == 0) ? 1.5f : 0.8f; 
        }

        if (squeezeBall != null) {
            // SqueezeBall interaction logic will be placed here
        }

        Debug.Log($"[Trial {currentTrialID}] Current C/D Ratio value: {currentCDRatio:F3}");
    }

    public void SubmitResponse(string response) {
        float responseTime = Time.time - trialStartTime;
        bool stimulusNoticed = response == "Different" || response == "Noticed" || response == "Soft" || response == "Rigid";

        // Send the trial result to the Staircase Procedure backend if valid
        if (StaircaseProcedure.SP != null) {
            Staircase.TrialData staircaseTrial = StaircaseProcedure.SP.TrialFinished(stimulusNoticed);
        }

        TrialData data = new TrialData {
            trialID = currentTrialID,
            cdRatio = currentCDRatio,
            userResponse = response,
            stimulusNoticed = stimulusNoticed,
            responseTime = responseTime,
            peakSqueeze = peakSqueeze
        };

        experimentLog.Add(data);
        Debug.Log($"[Trial {currentTrialID}] User Response is: {response}, Stimulus Noticed state is: {stimulusNoticed}");
        
        // Auto save data locally every 5 trials for verification
        if (currentTrialID % 5 == 0) {
            SaveDataToCSV();
        }
    }

    private void SaveDataToCSV() {
        string filePath = Path.Combine(Application.persistentDataPath, $"{participantID}_experiment_results.csv");

        using (StreamWriter writer = new StreamWriter(filePath)) {
            writer.WriteLine("ParticipantID,TrialID,CDRatio,UserResponse,StimulusNoticed,ResponseTime,PeakSqueeze");

            foreach (TrialData trial in experimentLog) {
                writer.WriteLine($"{participantID},{trial.trialID},{trial.cdRatio:F3},{trial.userResponse},{trial.stimulusNoticed},{trial.responseTime:F3},{trial.peakSqueeze:F3}");
            }
        }

        Debug.Log($"CSV file successfully saved at path: {filePath}");
    }
}