using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoftBallRatingStaircaseController : MonoBehaviour {
    [Header("Tracking Metrics")]
    [Tooltip("Detector used to log tracking-derived interaction intensity")]
    public HandSqueezeDetector squeezeDetector;

    public float maxSqueezeAmount = 0f;
    public float meanSqueezeAmount = 0f;
    public float maxThumbPressure = 0f;
    public float meanThumbPressure = 0f;

    private float squeezeSum = 0f;
    private float thumbPressureSum = 0f;
    private int trackingSampleCount = 0;

    [Header("Question Panels")]
    public GameObject introPanel;
    public GameObject preQuestionPanel;
    public GameObject postQuestionsPanel;
    public GameObject endPanel;

    [Header("Pre Question Containers")]
    public GameObject handChoiceContainer;
    public GameObject arExperienceContainer;

    [Header("Post Question UI")]
    public TextMeshProUGUI postQuestionText;
    public TextMeshProUGUI postQuestionStatusText;

    [Header("Intro UI")]
    public GameObject introTextObject;
    public GameObject introPostTextObject;
    private bool waitingForPostQuestionIntro = false;

    [Header("Post Question Buttons Group")]
    [Tooltip("Main 1 to 7 button group used for standard post questions")]
    public GameObject postRatingButtonsGroup;

    [Header("Post Question Disconnection UI")]
    [Tooltip("Container shown for Q8 Yes/No step")]
    public GameObject disconnectionYesNoContainer;

    [Tooltip("Container shown for Q8 timing step, only if participant answered Yes")]
    public GameObject disconnectionTimingContainer;

    [Header("Question Runtime")]
    public int currentPostQuestionIndex = 0;
    public string currentPostQuestionId = "";
    private bool disconnectionAnswerWasYes = false;

    [Header("Fallback Trial Sequence")]
    [Tooltip("Force a fixed sequence of trials instead of using the adaptive staircase toolkit")]
    public bool forceFallbackSequence = true;

    [Tooltip("Use fallback if the staircase toolkit is not ready")]
    public bool useFallbackIfToolkitFails = true;

    [Tooltip("Fixed gain sequence used when fallback mode is active")]
    public float[] fallbackGains = new float[] {
        1.00f,
        0.60f,
        1.40f,
        0.60f,
        1.40f,
        1.00f
    };

    private bool usingFallbackSequence = false;

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

    [Tooltip("Start panel shown after pre-questions and before the first trial")]
    public GameObject startPanel;

    [Tooltip("Info panel shown during the ball interaction phase")]
    public GameObject infoPanel;

    [Tooltip("Experiment panel used for trial rating")]
    public GameObject experimentPanel;

    [Tooltip("Group containing the 1 to 7 rating buttons for trial ratings")]
    public GameObject ratingButtonsGroup;

    [Header("Start UI")]
    [Tooltip("START button shown on the start panel")]
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
    public int participantNumber = 1;
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

        if (waitForExternalCalibration) {
            ShowCalibrationState();
        } else {
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
        ratingLogRows.Add("participant_id;phase;question_id;trial_index;gain;gain_label;rating;binary_answer;max_squeeze;mean_squeeze;max_thumb_pressure;mean_thumb_pressure;timestamp");

        string folderPath = Path.Combine(Application.persistentDataPath, "CogAR_test");

        if (!Directory.Exists(folderPath)) {
            Directory.CreateDirectory(folderPath);
        }

        string fileName =
            "CogAR_test_P" +
            participantNumber.ToString("D2") +
            "_" +
            System.DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".csv";

        ratingLogPath = Path.Combine(folderPath, fileName);

        File.WriteAllLines(ratingLogPath, ratingLogRows);

        Debug.Log("CSV log prepared at " + ratingLogPath);
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
            Debug.LogWarning("StaircaseProcedure.SP is null — do not manually call Create or Awake on Magic Leap");
            return;
        }

        Debug.Log("Staircase Procedure found and ready");
    }

    // ─── CALIBRATION ────────────────────────────────────────────────────────────

    void ShowCalibrationState() {
        calibrationCompleted = false;
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;

        SetAllPanelsOff();

        if (calibrationPanel != null) {
            calibrationPanel.SetActive(true);
        }

        SetLazyFollow(true);

        Debug.Log("Calibration state shown");
    }

    public void BeginExperimentAfterCalibration() {
        StartCoroutine(ShowIntroPanelAfterCalibration());
    }

    IEnumerator ShowIntroPanelAfterCalibration() {
        yield return null;

        calibrationCompleted = true;
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;
        waitingForPostQuestionIntro = false;

        SetAllPanelsOff();

        if (introPanel != null) {
            introPanel.SetActive(true);
        }

        if (introTextObject != null) {
            introTextObject.SetActive(true);
        }

        if (introPostTextObject != null) {
            introPostTextObject.SetActive(false);
        }

        SetLazyFollow(true);
        yield return new WaitForSeconds(0.5f);
        SetLazyFollow(false);

        Debug.Log("Intro panel shown after calibration");
    }

    // ─── INTRO ──────────────────────────────────────────────────────────────────

    public void HandleContinuePressed() {
        if (waitingForPostQuestionIntro) {
            waitingForPostQuestionIntro = false;

            if (introPanel != null) {
                introPanel.SetActive(false);
            }

            if (introPostTextObject != null) {
                introPostTextObject.SetActive(false);
            }

            StartPostQuestions();

            Debug.Log("Post-questionnaire started from intro panel");
            return;
        }

        SetAllPanelsOff();

        if (preQuestionPanel != null) {
            preQuestionPanel.SetActive(true);
        }

        if (handChoiceContainer != null) {
            handChoiceContainer.SetActive(true);
        }

        if (arExperienceContainer != null) {
            arExperienceContainer.SetActive(false);
        }

        Debug.Log("Pre-questionnaire started");
    }

    // ─── PRE QUESTIONS ──────────────────────────────────────────────────────────

    public void HandleHandednessAnswer(int value) {
        if (value < 1 || value > 3) {
            Debug.Log("Handedness answer ignored — only 1, 2, or 3 are valid");
            return;
        }

        SaveQuestionRow("pre", "handedness", 0, 0f, "", value, "");

        if (handChoiceContainer != null) {
            handChoiceContainer.SetActive(false);
        }

        if (arExperienceContainer != null) {
            arExperienceContainer.SetActive(true);
        }

        Debug.Log("Handedness answer saved: " + value);
    }

    public void HandleARExperienceAnswer(int value) {
        SaveQuestionRow("pre", "ar_vr_familiarity", 0, 0f, "", value, "");

        SetAllPanelsOff();

        if (startPanel != null) {
            startPanel.SetActive(true);
        }

        if (startButton != null) {
            startButton.gameObject.SetActive(true);
        }

        Debug.Log("AR experience answer saved: " + value);
    }

    // ─── EXPERIMENT START ───────────────────────────────────────────────────────

    public void StartExperimentFromButton() {
        calibrationCompleted = true;
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;

        SetAllPanelsOff();

        if (stressBallRoot != null) {
            stressBallRoot.SetActive(true);
        }

        if (infoPanel != null) {
            infoPanel.SetActive(true);
        }

        SetLazyFollow(false);
        StartStaircase();

        Debug.Log("Experiment started from START button");
    }

    // ─── STAIRCASE / FIXED SEQUENCE ─────────────────────────────────────────────

    public void StartStaircase() {
        if (staircaseInitialized) {
            return;
        }

        usingFallbackSequence = forceFallbackSequence;

        if (usingFallbackSequence) {
            Debug.Log("Starting fixed fallback trial sequence");
        } else {
            EnsureToolkitIsReady();

            if (StaircaseProcedure.SP == null) {
                if (!useFallbackIfToolkitFails) {
                    Debug.LogWarning("StaircaseProcedure.SP is null — staircase cannot start");
                    return;
                }

                usingFallbackSequence = true;
                Debug.LogWarning("StaircaseProcedure.SP is null — switching to fallback trial sequence");
            }
        }

        if (!usingFallbackSequence && StaircaseProcedure.SP != null) {
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
        }

        staircaseInitialized = true;
        staircaseFinished = false;
        ratingEnabled = false;
        currentTrial = 0;

        StartNextTrial();

        Debug.Log("Rating experiment started");
    }

    void StartNextTrial() {
        if (!staircaseInitialized || staircaseFinished) {
            return;
        }

        if (usingFallbackSequence) {
            if (currentTrial >= fallbackGains.Length) {
                FinishStaircase();
                return;
            }

            currentGain = fallbackGains[currentTrial];
        } else {
            if (StaircaseProcedure.SP.IsFinished()) {
                FinishStaircase();
                return;
            }

            currentGain = StaircaseProcedure.SP.GetNextStimulus();
        }

        currentTrial++;

        if (gainController != null) {
            gainController.SetVisualDeformationGain(currentGain);
        }

        ratingEnabled = false;

        ResetTrackingMetrics();

        SetAllPanelsOff();

        if (stressBallRoot != null) {
            stressBallRoot.SetActive(true);
        }

        if (infoPanel != null) {
            infoPanel.SetActive(true);
        }

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

        Debug.Log("Started trial " + currentTrial + " with gain " + currentGain + " (" + GetGainLabel(currentGain) + ")");
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
        float elapsed = 0f;

        while (elapsed < interactionDurationSeconds) {
            UpdateTrackingMetrics();

            elapsed += Time.deltaTime;
            yield return null;
        }

        ratingEnabled = true;

        if (freezeCanvasDuringRating) {
            SetLazyFollow(false);
        }

        if (infoPanel != null) {
            infoPanel.SetActive(false);
        }

        if (stressBallRoot != null) {
            stressBallRoot.SetActive(false);
        }

        if (experimentPanel != null) {
            experimentPanel.SetActive(true);
        }

        if (ratingButtonsGroup != null) {
            ratingButtonsGroup.SetActive(true);
        }

        if (instructionText != null) {
            instructionText.text = "How clearly did you perceive the deformation of the ball?";
        }

        if (statusText != null) {
            statusText.text = "1 = not clear at all | 7 = very clear";
        }

        Debug.Log("Rating phase started for trial " + currentTrial);
    }

    void ResetTrackingMetrics() {
        maxSqueezeAmount = 0f;
        meanSqueezeAmount = 0f;
        maxThumbPressure = 0f;
        meanThumbPressure = 0f;

        squeezeSum = 0f;
        thumbPressureSum = 0f;
        trackingSampleCount = 0;
    }

    void UpdateTrackingMetrics() {
        if (squeezeDetector == null) {
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            return;
        }

        float squeeze = Mathf.Clamp01(squeezeDetector.squeezeNormalized);
        float thumb = Mathf.Clamp01(squeezeDetector.thumbPressure);

        maxSqueezeAmount = Mathf.Max(maxSqueezeAmount, squeeze);
        maxThumbPressure = Mathf.Max(maxThumbPressure, thumb);

        squeezeSum += squeeze;
        thumbPressureSum += thumb;
        trackingSampleCount++;

        meanSqueezeAmount = squeezeSum / Mathf.Max(trackingSampleCount, 1);
        meanThumbPressure = thumbPressureSum / Mathf.Max(trackingSampleCount, 1);
    }

    public void HandleTrialRatingAnswer(int value) {
        SubmitRating(value);
    }

    public void SubmitRating(int rating) {
        if (!staircaseInitialized || staircaseFinished || !ratingEnabled) {
            Debug.Log("Rating ignored — not in active rating phase");
            return;
        }

        lastRating = Mathf.Clamp(rating, 1, 7);
        lastToolkitAnswer = lastRating >= noticedRatingThreshold;

        SaveTrialRatingRow(lastRating, lastToolkitAnswer);

        if (!usingFallbackSequence && StaircaseProcedure.SP != null) {
            StaircaseProcedure.SP.TrialFinished(lastToolkitAnswer);
        }

        Debug.Log("Submitted rating " + lastRating + " — toolkit answer: " + lastToolkitAnswer);

        bool isFinished = usingFallbackSequence
            ? currentTrial >= fallbackGains.Length
            : StaircaseProcedure.SP.IsFinished();

        if (isFinished) {
            FinishStaircase();
        } else {
            StartNextTrial();
        }
    }

    void SaveTrialRatingRow(int rating, bool toolkitAnswer) {
        SaveQuestionRow(
            "trial",
            "deformation_clarity",
            currentTrial,
            currentGain,
            GetGainLabel(currentGain),
            rating,
            toolkitAnswer.ToString(),
            maxSqueezeAmount,
            meanSqueezeAmount,
            maxThumbPressure,
            meanThumbPressure
        );
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

        SetAllPanelsOff();

        waitingForPostQuestionIntro = true;

        if (introPanel != null) {
            introPanel.SetActive(true);
        }

        if (introTextObject != null) {
            introTextObject.SetActive(false);
        }

        if (introPostTextObject != null) {
            introPostTextObject.SetActive(true);
        }

        Debug.Log("Trial sequence completed — showing post-questionnaire intro");
    }

    // ─── POST QUESTIONS ─────────────────────────────────────────────────────────

    void StartPostQuestions() {
        currentPostQuestionIndex = 0;
        disconnectionAnswerWasYes = false;

        SetAllPanelsOff();

        if (postQuestionsPanel != null) {
            postQuestionsPanel.SetActive(true);
        }

        ShowPostQuestion();

        Debug.Log("Post-questionnaire started");
    }

    void ShowPostQuestion() {
        if (disconnectionYesNoContainer != null) {
            disconnectionYesNoContainer.SetActive(false);
        }

        if (disconnectionTimingContainer != null) {
            disconnectionTimingContainer.SetActive(false);
        }

        if (postRatingButtonsGroup != null) {
            postRatingButtonsGroup.SetActive(true);
        }

        string question = "";
        string status = "1 = not at all | 7 = very much";

        if (currentPostQuestionIndex == 0) {
            currentPostQuestionId = "softness";
            question = "How soft did the virtual ball feel?";
            status = "1 = not soft at all | 7 = very soft";
        } else if (currentPostQuestionIndex == 1) {
            currentPostQuestionId = "elasticity";
            question = "How elastic did the virtual ball feel?";
            status = "1 = not elastic at all | 7 = very elastic";
        } else if (currentPostQuestionIndex == 2) {
            currentPostQuestionId = "contact_believability";
            question = "How believable was the contact with the ball?";
            status = "1 = not believable at all | 7 = very believable";
        } else if (currentPostQuestionIndex == 3) {
            currentPostQuestionId = "thumb_contribution";
            question = "Did adding the thumb make the interaction feel more convincing?";
            status = "1 = not at all | 7 = very much";
        } else if (currentPostQuestionIndex == 4) {
            currentPostQuestionId = "visual_touch_influence";
            question = "Did the visual deformation create a sense of physical contact?";
            status = "1 = not at all | 7 = very strongly";
        } else if (currentPostQuestionIndex == 5) {
            currentPostQuestionId = "object_vs_animation";
            question = "Did the interaction feel more like touching an object than watching an animation?";
            status = "1 = only watching an animation | 7 = touching an object";
        } else if (currentPostQuestionIndex == 6) {
            currentPostQuestionId = "movement_match";
            question = "How well did the visual deformation match your hand movement?";
            status = "1 = not well at all | 7 = very well";
        } else if (currentPostQuestionIndex == 7) {
            currentPostQuestionId = "disconnection_presence";
            question = "At any point, did the visual deformation feel disconnected from your hand movement?";
            status = "";

            if (postQuestionText != null) {
                postQuestionText.text = question;
            }

            if (postQuestionStatusText != null) {
                postQuestionStatusText.text = status;
            }

            if (postRatingButtonsGroup != null) {
                postRatingButtonsGroup.SetActive(false);
            }

            if (disconnectionYesNoContainer != null) {
                disconnectionYesNoContainer.SetActive(true);
            }

            Debug.Log("Showing post question: disconnection_presence");
            return;
        } else {
            FinishPostQuestions();
            return;
        }

        if (postQuestionText != null) {
            postQuestionText.text = question;
        }

        if (postQuestionStatusText != null) {
            postQuestionStatusText.text = status;
        }

        Debug.Log("Showing post question: " + currentPostQuestionId);
    }

    public void HandlePostQuestionAnswer(int value) {
        if (currentPostQuestionId == "disconnection_presence") {
            Debug.Log("Post rating ignored because Q8 expects Yes or No");
            return;
        }

        SaveQuestionRow("post", currentPostQuestionId, 0, 0f, "", value, "");

        currentPostQuestionIndex++;
        ShowPostQuestion();
    }

    public void HandleDisconnectionYesNo(bool answeredYes) {
        disconnectionAnswerWasYes = answeredYes;

        SaveQuestionRow("post", "disconnection_presence", 0, 0f, "", 0, answeredYes ? "yes" : "no");

        if (answeredYes) {
            if (postRatingButtonsGroup != null) {
                postRatingButtonsGroup.SetActive(false);
            }

            if (disconnectionYesNoContainer != null) {
                disconnectionYesNoContainer.SetActive(false);
            }

            if (disconnectionTimingContainer != null) {
                disconnectionTimingContainer.SetActive(true);
            }

            if (postQuestionText != null) {
                postQuestionText.text = "At which point did you first notice the disconnection?";
            }

            if (postQuestionStatusText != null) {
                postQuestionStatusText.text =
                    "1 = from the beginning\n" +
                    "2 = when first touching the ball\n" +
                    "3 = when adding the thumb\n" +
                    "4 = toward the end of the interaction";
            }

            Debug.Log("Disconnection confirmed — showing timing question");
        } else {
            currentPostQuestionIndex++;
            ShowPostQuestion();

            Debug.Log("No disconnection reported — skipping timing question");
        }
    }

    public void HandleDisconnectionTiming(int value) {
        if (value < 1 || value > 4) {
            Debug.Log("Disconnection timing answer ignored — only 1, 2, 3, or 4 are valid");
            return;
        }

        SaveQuestionRow("post", "disconnection_timing", 0, 0f, "", value, "");

        currentPostQuestionIndex++;
        ShowPostQuestion();

        Debug.Log("Disconnection timing answer saved: " + value);
    }

    void FinishPostQuestions() {
        if (!usingFallbackSequence && StaircaseProcedure.SP != null) {
            float threshold = StaircaseProcedure.SP.GetThreshold();
            SaveQuestionRow("result", "staircase_threshold", 0, threshold, "threshold", 0, "");
            Debug.Log("Staircase threshold saved: " + threshold);
        }

        File.WriteAllLines(ratingLogPath, ratingLogRows);

        SetAllPanelsOff();

        if (endPanel != null) {
            endPanel.SetActive(true);
        }

        Debug.Log("Post-questionnaire completed — experiment finished");
    }

    // ─── RESET ──────────────────────────────────────────────────────────────────

    public void ResetExperiment() {
        staircaseInitialized = false;
        staircaseFinished = false;
        ratingEnabled = false;
        currentTrial = 0;
        currentGain = 1.0f;
        lastRating = 0;
        lastToolkitAnswer = false;
        currentPostQuestionIndex = 0;
        disconnectionAnswerWasYes = false;

        if (trialRoutine != null) {
            StopCoroutine(trialRoutine);
        }

        if (canvasRoutine != null) {
            StopCoroutine(canvasRoutine);
        }

        PrepareLocalLog();

        if (waitForExternalCalibration) {
            ShowCalibrationState();
        } else {
            BeginExperimentAfterCalibration();
        }

        Debug.Log("Soft ball rating staircase controller reset");
    }

    // ─── HELPERS ────────────────────────────────────────────────────────────────

    void SetAllPanelsOff() {
        if (calibrationPanel != null) {
            calibrationPanel.SetActive(false);
        }

        if (introPanel != null) {
            introPanel.SetActive(false);
        }

        if (preQuestionPanel != null) {
            preQuestionPanel.SetActive(false);
        }

        if (startPanel != null) {
            startPanel.SetActive(false);
        }

        if (startButton != null) {
            startButton.gameObject.SetActive(false);
        }

        if (infoPanel != null) {
            infoPanel.SetActive(false);
        }

        if (experimentPanel != null) {
            experimentPanel.SetActive(false);
        }

        if (ratingButtonsGroup != null) {
            ratingButtonsGroup.SetActive(false);
        }

        if (postQuestionsPanel != null) {
            postQuestionsPanel.SetActive(false);
        }

        if (postRatingButtonsGroup != null) {
            postRatingButtonsGroup.SetActive(false);
        }

        if (endPanel != null) {
            endPanel.SetActive(false);
        }

        if (stressBallRoot != null) {
            stressBallRoot.SetActive(false);
        }

        if (disconnectionYesNoContainer != null) {
            disconnectionYesNoContainer.SetActive(false);
        }

        if (disconnectionTimingContainer != null) {
            disconnectionTimingContainer.SetActive(false);
        }
    }

    void SetLazyFollow(bool enabled) {
        if (lazyFollowCanvas != null) {
            lazyFollowCanvas.enabled = enabled;
        }
    }

    string GetGainLabel(float gain) {
        if (gain < 0.80f) {
            return "low";
        }

        if (gain > 1.20f) {
            return "high";
        }

        return "medium";
    }

    void SaveQuestionRow(
        string phase,
        string questionId,
        int trialIndex,
        float gain,
        string gainLabel,
        int rating,
        string binaryAnswer,
        float maxSqueeze,
        float meanSqueeze,
        float maxThumb,
        float meanThumb
    ) {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");

        string row =
            participantNumber.ToString() + ";" +
            phase + ";" +
            questionId + ";" +
            trialIndex.ToString() + ";" +
            gain.ToString("F3") + ";" +
            gainLabel + ";" +
            rating.ToString() + ";" +
            binaryAnswer + ";" +
            maxSqueeze.ToString("F3") + ";" +
            meanSqueeze.ToString("F3") + ";" +
            maxThumb.ToString("F3") + ";" +
            meanThumb.ToString("F3") + ";" +
            timestamp;

        ratingLogRows.Add(row);
        File.WriteAllLines(ratingLogPath, ratingLogRows);

        Debug.Log("Saved row: " + row);
    }

    void SaveQuestionRow(
        string phase,
        string questionId,
        int trialIndex,
        float gain,
        string gainLabel,
        int rating,
        string binaryAnswer
    ) {
        SaveQuestionRow(
            phase,
            questionId,
            trialIndex,
            gain,
            gainLabel,
            rating,
            binaryAnswer,
            0f,
            0f,
            0f,
            0f
        );
    }
}