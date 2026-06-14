using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoftBallRatingStaircaseController : MonoBehaviour {
    [Header("Toolkit References")]
    [Tooltip("Staircase Procedure component from Andre Zenner's toolkit")]
    public StaircaseProcedure staircaseProcedure;

    [Tooltip("Controller that applies the current gain to the ball deformer")]
    public StressBallDeformationGainController gainController;

    [Header("Experiment Objects")]
    [Tooltip("Root object of the stress ball")]
    public GameObject stressBallRoot;

    [Tooltip("Calibration panel shown during hand calibration")]
    public GameObject calibrationPanel;

    [Tooltip("Optional start panel shown after calibration and before the first trial")]
    public GameObject startPanel;

    [Tooltip("Experiment panel used for trial instructions and rating")]
    public GameObject experimentPanel;

    [Tooltip("Group containing the 1 to 7 rating buttons")]
    public GameObject ratingButtonsGroup;

    [Header("Start UI")]
    [Tooltip("Existing START button")]
    public Button startButton;

    [Header("Experiment UI References")]
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI statusText;

    [Tooltip("Rating buttons from 1 to 7, in order")]
    public Button[] ratingButtons = new Button[7];

    [Header("Canvas Follow Control")]
    [Tooltip("LazyFollowCanvas script attached to the Canvas GameObject")]
    public LazyFollowCanvas lazyFollowCanvas;

    [Tooltip("Enable Lazy Follow briefly at the beginning of each trial to recenter the panel")]
    public bool recenterCanvasAtTrialStart = true;

    [Tooltip("How long Lazy Follow is enabled before freezing the panel")]
    public float canvasRecenterDurationSeconds = 0.7f;

    [Tooltip("Disable Lazy Follow while the participant is answering")]
    public bool freezeCanvasDuringRating = true;

    [Tooltip("Disable Lazy Follow during the interaction phase as well")]
    public bool freezeCanvasDuringInteraction = true;

    [Header("Trial Timing")]
    [Tooltip("Time given to interact with the ball before the rating buttons appear")]
    public float interactionDurationSeconds = 6f;

    [Header("Start Mode")]
    [Tooltip("If true, the experiment waits until BeginExperimentAfterCalibration is called")]
    public bool waitForExternalCalibration = true;

    [Header("Staircase Settings")]
    public float minimumGain = 0.50f;
    public float maximumGain = 1.50f;
    public int numberOfSteps = 10;

    [Tooltip("Low starting sequence")]
    public int startStepSequence1 = 2;

    [Tooltip("High starting sequence")]
    public int startStepSequence2 = 10;

    [Tooltip("Number of reversals per sequence")]
    public int stopAmount = 4;

    public int numberThresholdPoints = 3;
    public int participantNumber = 1;

    public string experimentName = "SoftBallPseudoHaptics";
    public string conditionName = "SiliconeVisualDeformation";

    [Header("Rating Mapping")]
    [Tooltip("Ratings equal or above this value are sent to the toolkit as noticed")]
    [Range(1, 7)]
    public int noticedRatingThreshold = 4;

    [Header("Runtime State")]
    public bool calibrationCompleted = false;
    public bool staircaseInitialized = false;
    public bool staircaseFinished = false;
    public bool ratingEnabled = false;

    public int currentTrial = 0;
    public float currentGain = 1.0f;
    public int lastRating = 0;
    public bool lastToolkitAnswer = false;

    private Coroutine trialRoutine;
    private Coroutine canvasRoutine;

    private List<string> ratingLogRows = new List<string>();
    private string ratingLogPath = "";

    void Start() {
        SetupStartButton();
        SetupRatingButtons();
        PrepareLocalLog();
        EnsureToolkitIsReady();

        if (waitForExternalCalibration) {
            ShowCalibrationState();
        }
        else {
            BeginExperimentAfterCalibration();
        }

        Debug.Log("Soft ball rating staircase controller initialized");
    }

    void SetupStartButton() {
        if (startButton != null) {
            startButton.onClick.RemoveListener(StartExperimentFromButton);
            startButton.onClick.AddListener(StartExperimentFromButton);
        }
    }

    void SetupRatingButtons() {
        for (int i = 0; i < ratingButtons.Length; i++) {
            int ratingValue = i + 1;

            if (ratingButtons[i] != null) {
                ratingButtons[i].onClick.RemoveAllListeners();
                ratingButtons[i].onClick.AddListener(() => SubmitRating(ratingValue));
            }
        }
    }

    void PrepareLocalLog() {
        ratingLogRows.Clear();
        ratingLogRows.Add("trial_index,gain,rating,toolkit_answer,timestamp");

        string fileName = "soft_ball_rating_staircase_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        ratingLogPath = Path.Combine(Application.persistentDataPath, fileName);
    }

    void EnsureToolkitIsReady() {
        if (staircaseProcedure == null) {
            staircaseProcedure = FindObjectOfType<StaircaseProcedure>();
        }

        if (staircaseProcedure == null) {
            Debug.LogWarning("Staircase Procedure component not found in scene");
            return;
        }

        if (StaircaseProcedure.SP == null) {
            staircaseProcedure.Create(
                Application.persistentDataPath,
                "python_disabled",
                false,
                false,
                false
            );

            staircaseProcedure.Awake();

            Debug.Log("Staircase Procedure toolkit initialized without Python live plotter");
        }
    }

    void ShowCalibrationState() {
        calibrationCompleted = false;
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;

        SetPanelStates(
            calibration: true,
            start: false,
            experiment: false,
            ratingButtons: false,
            ball: false
        );

        SetLazyFollow(true);

        Debug.Log("Calibration state shown");
    }

    public void BeginExperimentAfterCalibration() {
        StartCoroutine(BeginExperimentAfterCalibrationRoutine());
    }

    IEnumerator BeginExperimentAfterCalibrationRoutine() {
        yield return null;

        calibrationCompleted = true;
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;

        SetPanelStates(
            calibration: false,
            start: true,
            experiment: false,
            ratingButtons: false,
            ball: false
        );

        SetLazyFollow(false);

        Debug.Log("Calibration completed and start panel shown");
    }

    public void StartExperimentFromButton() {
        calibrationCompleted = true;
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;

        SetPanelStates(
            calibration: false,
            start: false,
            experiment: true,
            ratingButtons: false,
            ball: true
        );

        if (instructionText != null) {
            instructionText.text =
                "Ready to start." +
                "\n\nPress START again or wait for the first trial.";
        }

        if (statusText != null) {
            statusText.text =
                "The rating scale will appear after each interaction.";
        }

        SetLazyFollow(false);

        StartStaircase();

        Debug.Log("Experiment started from START button");
    }

    public void StartStaircase() {
        if (staircaseInitialized) {
            return;
        }

        EnsureToolkitIsReady();

        if (StaircaseProcedure.SP == null) {
            Debug.LogWarning("StaircaseProcedure.SP is null");
            return;
        }

        StaircaseProcedure.SP.Init(
            minimumValue: minimumGain,
            maximumValue: maximumGain,
            numberOfSteps: numberOfSteps,
            startStepSequ1: startStepSequence1,
            startStepSequ2: startStepSequence2,
            stopAmount: stopAmount,
            numberThresholdPoints: numberThresholdPoints,
            experimentName: experimentName,
            conditionName: conditionName,
            numberParticipant: participantNumber,
            stepsUp: 1,
            stepsDown: 1,
            stopCriterionReversals: true,
            strictLimits: true,
            plotTitle: "Soft Ball Pseudo-Haptic Staircase"
        );

        staircaseInitialized = true;
        staircaseFinished = false;
        ratingEnabled = false;
        currentTrial = 0;

        StartNextTrial();

        Debug.Log("Rating staircase started");
    }

    void StartNextTrial() {
        if (!staircaseInitialized || staircaseFinished) {
            return;
        }

        if (StaircaseProcedure.SP.IsFinished()) {
            FinishStaircase();
            return;
        }

        currentTrial++;
        currentGain = StaircaseProcedure.SP.GetNextStimulus();

        if (gainController != null) {
            gainController.SetVisualDeformationGain(currentGain);
        }

        ratingEnabled = false;

        SetPanelStates(
            calibration: false,
            start: false,
            experiment: true,
            ratingButtons: false,
            ball: true
        );

        if (instructionText != null) {
            instructionText.text =
                "Trial " + currentTrial +
                "\n\nInteract with the virtual silicone ball." +
                "\nPlace your palm under the ball and press gently." +
                "\nThen add your thumb to increase the pressure.";
        }

        if (statusText != null) {
            statusText.text = "";
        }

        if (trialRoutine != null) {
            StopCoroutine(trialRoutine);
        }

        if (canvasRoutine != null) {
            StopCoroutine(canvasRoutine);
        }

        canvasRoutine = StartCoroutine(HandleCanvasForInteractionPhase());
        trialRoutine = StartCoroutine(ShowRatingAfterInteraction());

        Debug.Log("Started trial " + currentTrial + " with gain " + currentGain);
    }

    IEnumerator HandleCanvasForInteractionPhase() {
        if (lazyFollowCanvas == null) {
            yield break;
        }

        if (recenterCanvasAtTrialStart) {
            SetLazyFollow(true);
            yield return new WaitForSeconds(canvasRecenterDurationSeconds);
        }

        if (freezeCanvasDuringInteraction) {
            SetLazyFollow(false);
        }
    }

    IEnumerator ShowRatingAfterInteraction() {
        yield return new WaitForSeconds(interactionDurationSeconds);

        ratingEnabled = true;

        if (freezeCanvasDuringRating) {
            SetLazyFollow(false);
        }

        if (ratingButtonsGroup != null) {
            ratingButtonsGroup.SetActive(true);
        }

        if (instructionText != null) {
            instructionText.text =
                "How clearly did you perceive the deformation of the ball?";
        }

        if (statusText != null) {
            statusText.text =
                "1 = not clear | 7 = very clear";
        }

        Debug.Log("Rating phase started for trial " + currentTrial);
    }

    public void SubmitRating(int rating) {
        if (!staircaseInitialized || staircaseFinished || !ratingEnabled) {
            Debug.Log("Rating ignored because this is not an active rating phase");
            return;
        }

        lastRating = Mathf.Clamp(rating, 1, 7);
        lastToolkitAnswer = lastRating >= noticedRatingThreshold;

        SaveRatingRow(lastRating, lastToolkitAnswer);

        StaircaseProcedure.SP.TrialFinished(lastToolkitAnswer);

        Debug.Log("Submitted rating " + lastRating + " as toolkit answer " + lastToolkitAnswer);

        if (StaircaseProcedure.SP.IsFinished()) {
            FinishStaircase();
        }
        else {
            StartNextTrial();
        }
    }

    void SaveRatingRow(int rating, bool toolkitAnswer) {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");

        string row =
            currentTrial.ToString() + "," +
            currentGain.ToString("F3") + "," +
            rating.ToString() + "," +
            toolkitAnswer.ToString() + "," +
            timestamp;

        ratingLogRows.Add(row);
        File.WriteAllLines(ratingLogPath, ratingLogRows);
    }

    void FinishStaircase() {
        staircaseFinished = true;
        ratingEnabled = false;

        if (trialRoutine != null) {
            StopCoroutine(trialRoutine);
        }

        if (canvasRoutine != null) {
            StopCoroutine(canvasRoutine);
        }

        SetLazyFollow(false);

        float threshold = StaircaseProcedure.SP.GetThreshold();

        File.WriteAllLines(ratingLogPath, ratingLogRows);

        SetPanelStates(
            calibration: false,
            start: false,
            experiment: true,
            ratingButtons: false,
            ball: false
        );

        if (instructionText != null) {
            instructionText.text =
                "Experiment completed." +
                "\n\nEstimated noticeability threshold: " + threshold.ToString("F2");
        }

        if (statusText != null) {
            statusText.text =
                "Rating CSV saved to:" +
                "\n" + ratingLogPath;
        }

        Debug.Log("Rating staircase completed with threshold " + threshold);
        Debug.Log("Rating data saved to " + ratingLogPath);
    }

    public void ResetExperiment() {
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;
        currentTrial = 0;
        currentGain = 1.0f;
        lastRating = 0;
        lastToolkitAnswer = false;

        if (trialRoutine != null) {
            StopCoroutine(trialRoutine);
        }

        if (canvasRoutine != null) {
            StopCoroutine(canvasRoutine);
        }

        PrepareLocalLog();

        if (waitForExternalCalibration) {
            ShowCalibrationState();
        }
        else {
            BeginExperimentAfterCalibration();
        }

        Debug.Log("Soft ball rating staircase controller reset");
    }

    void SetPanelStates(bool calibration, bool start, bool experiment, bool ratingButtons, bool ball) {
        if (calibrationPanel != null) {
            calibrationPanel.SetActive(calibration);
        }

        if (startPanel != null) {
            startPanel.SetActive(start);
        }

        if (startButton != null) {
            startButton.gameObject.SetActive(start);
        }

        if (experimentPanel != null) {
            experimentPanel.SetActive(experiment);
        }

        if (ratingButtonsGroup != null) {
            ratingButtonsGroup.SetActive(ratingButtons);
        }

        if (stressBallRoot != null) {
            stressBallRoot.SetActive(ball);
        }
    }

    void SetLazyFollow(bool enabled) {
        if (lazyFollowCanvas != null) {
            lazyFollowCanvas.enabled = enabled;
        }
    }
}