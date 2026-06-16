using UnityEngine;
using UnityEngine.UI;

public class IndexTipUIButtonClicker : MonoBehaviour {
    [Header("References")]
    public IndexTipUIProxyController proxyController;
    public SoftBallRatingStaircaseController experimentController;

    [Header("Special Buttons")]
    public Button continueButton;
    public Button startButton;

    [Header("Pre Question Buttons")]
    public Button[] handednessButtons = new Button[3];
    public Button[] arExperienceButtons = new Button[7];

    [Header("Experiment Buttons")]
    public Button[] trialRatingButtons = new Button[7];

    [Header("Post Question Buttons")]
    public Button[] postQuestionButtons = new Button[7];

    [Header("Q8 Disconnection Buttons")]
    [Tooltip("Yes button for Q8 disconnection presence")]
    public Button disconnectionYesButton;

    [Tooltip("No button for Q8 disconnection presence")]
    public Button disconnectionNoButton;

    [Tooltip("Timing buttons for Q8 follow-up, in order from 1 to 4")]
    public Button[] disconnectionTimingButtons = new Button[4];

    [Header("Special Button Touch Settings")]
    public float specialButtonTouchDepthMeters = 0.09f;
    public float specialButtonPaddingUnits = 18f;

    [Header("Rating Button Touch Settings")]
    public float ratingTouchDepthMeters = 0.055f;
    public float ratingPaddingUnits = 8f;

    [Header("Activation Settings")]
    public float activationHoldSeconds = 0.25f;
    public float clickCooldownSeconds = 0.80f;
    public bool ignoreInactiveButtons = true;
    
    [Tooltip("Require the finger to leave all buttons before another click can be accepted")]
    public bool requireFingerReleaseBetweenClicks = true;

    private bool waitingForFingerRelease = false;

    [Header("Debug")]
    public bool showDebug = true;
    public string currentHoverButton = "";
    public string lastActivatedButton = "";
    public float hoverProgress = 0f;
    public float lastButtonDistanceMeters = 0f;
    public Vector2 lastButtonLocalMeters;
    public bool lastInsideRect = false;

    private Button candidateButton;
    private float candidateStartTime = 0f;
    private float nextAllowedClickTime = 0f;

    void Start() {
        if (proxyController == null) {
            proxyController = GetComponent<IndexTipUIProxyController>();
        }

        Debug.Log("Index tip UI button clicker initialized");
    }

    void Update() {
        if (Time.time < nextAllowedClickTime) {
            return;
        }

        if (proxyController != null && !proxyController.IsIndexTracked()) {
            ClearCandidate();
            return;
        }

        if (waitingForFingerRelease) {
            if (IsAnyButtonCurrentlyTouched()) {
                return;
            }

            waitingForFingerRelease = false;
            ClearCandidate();
        }

        Button hoveredButton = null;
        int value = 0;
        UIButtonKind buttonKind = UIButtonKind.None;

        if (IsFingerInsideButton(continueButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            hoveredButton = continueButton;
            buttonKind = UIButtonKind.Continue;
        } else if (IsFingerInsideButton(startButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            hoveredButton = startButton;
            buttonKind = UIButtonKind.Start;
        } else if (IsFingerInsideButton(disconnectionYesButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            hoveredButton = disconnectionYesButton;
            buttonKind = UIButtonKind.DisconnectionYes;
        } else if (IsFingerInsideButton(disconnectionNoButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            hoveredButton = disconnectionNoButton;
            buttonKind = UIButtonKind.DisconnectionNo;
        } else if (TryFindButton(disconnectionTimingButtons, out hoveredButton, out value)) {
            buttonKind = UIButtonKind.DisconnectionTiming;
        } else if (TryFindButton(handednessButtons, out hoveredButton, out value)) {
            buttonKind = UIButtonKind.HandednessAnswer;
        } else if (TryFindButton(arExperienceButtons, out hoveredButton, out value)) {
            buttonKind = UIButtonKind.ARExperienceAnswer;
        } else if (TryFindButton(trialRatingButtons, out hoveredButton, out value)) {
            buttonKind = UIButtonKind.TrialRatingAnswer;
        } else if (TryFindButton(postQuestionButtons, out hoveredButton, out value)) {
            buttonKind = UIButtonKind.PostQuestionAnswer;
        }

        if (hoveredButton == null) {
            ClearCandidate();
            return;
        }

        UpdateCandidate(hoveredButton);

        if (hoverProgress < 1f) {
            return;
        }

        ActivateButton(hoveredButton, buttonKind, value);
    }

    bool TryFindButton(Button[] buttons, out Button hoveredButton, out int value) {
        hoveredButton = null;
        value = 0;

        if (buttons == null) {
            return false;
        }

        for (int i = 0; i < buttons.Length; i++) {
            if (IsFingerInsideButton(buttons[i], ratingTouchDepthMeters, ratingPaddingUnits)) {
                hoveredButton = buttons[i];
                value = i + 1;
                return true;
            }
        }

        return false;
    }

    void UpdateCandidate(Button hoveredButton) {
        if (candidateButton != hoveredButton) {
            candidateButton = hoveredButton;
            candidateStartTime = Time.time;
        }

        currentHoverButton = hoveredButton.gameObject.name;
        hoverProgress = Mathf.Clamp01((Time.time - candidateStartTime) / activationHoldSeconds);
    }

    void ActivateButton(Button button, UIButtonKind buttonKind, int value) {
        nextAllowedClickTime = Time.time + clickCooldownSeconds;
        lastActivatedButton = button.gameObject.name;

        ClearCandidate();

        if (requireFingerReleaseBetweenClicks) {
            waitingForFingerRelease = true;
        }

        if (experimentController == null) {
            Debug.LogWarning("Experiment controller is not assigned in IndexTipUIButtonClicker");
            return;
        }

        if (buttonKind == UIButtonKind.Continue) {
            experimentController.HandleContinuePressed();

            if (showDebug) {
                Debug.Log("Index proxy activated CONTINUE");
            }

            return;
        }

        if (buttonKind == UIButtonKind.Start) {
            experimentController.StartExperimentFromButton();

            if (showDebug) {
                Debug.Log("Index proxy activated START");
            }

            return;
        }

        if (buttonKind == UIButtonKind.DisconnectionYes) {
            experimentController.HandleDisconnectionYesNo(true);

            if (showDebug) {
                Debug.Log("Index proxy activated DISCONNECTION YES");
            }

            return;
        }

        if (buttonKind == UIButtonKind.DisconnectionNo) {
            experimentController.HandleDisconnectionYesNo(false);

            if (showDebug) {
                Debug.Log("Index proxy activated DISCONNECTION NO");
            }

            return;
        }

        if (buttonKind == UIButtonKind.DisconnectionTiming) {
            experimentController.HandleDisconnectionTiming(value);

            if (showDebug) {
                Debug.Log("Index proxy submitted disconnection timing: " + value);
            }

            return;
        }

        if (buttonKind == UIButtonKind.HandednessAnswer) {
            experimentController.HandleHandednessAnswer(value);

            if (showDebug) {
                Debug.Log("Index proxy submitted handedness: " + value);
            }

            return;
        }

        if (buttonKind == UIButtonKind.ARExperienceAnswer) {
            experimentController.HandleARExperienceAnswer(value);

            if (showDebug) {
                Debug.Log("Index proxy submitted AR experience: " + value);
            }

            return;
        }

        if (buttonKind == UIButtonKind.TrialRatingAnswer) {
            experimentController.HandleTrialRatingAnswer(value);

            if (showDebug) {
                Debug.Log("Index proxy submitted trial rating: " + value);
            }

            return;
        }

        if (buttonKind == UIButtonKind.PostQuestionAnswer) {
            experimentController.HandlePostQuestionAnswer(value);

            if (showDebug) {
                Debug.Log("Index proxy submitted post question answer: " + value);
            }
        }
    }

    void ClearCandidate() {
        candidateButton = null;
        candidateStartTime = 0f;
        currentHoverButton = "";
        hoverProgress = 0f;
    }

    bool IsFingerInsideButton(Button button, float touchDepthMeters, float paddingUnits) {
        if (button == null) {
            return false;
        }

        if (ignoreInactiveButtons && !button.gameObject.activeInHierarchy) {
            return false;
        }

        if (!button.interactable) {
            return false;
        }

        RectTransform rectTransform = button.transform as RectTransform;

        if (rectTransform == null) {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector3 bottomLeft = corners[0];
        Vector3 topLeft = corners[1];
        Vector3 topRight = corners[2];
        Vector3 bottomRight = corners[3];

        Vector3 center = (bottomLeft + topLeft + topRight + bottomRight) * 0.25f;

        Vector3 xAxis = (bottomRight - bottomLeft).normalized;
        Vector3 yAxis = (topLeft - bottomLeft).normalized;
        Vector3 normal = Vector3.Cross(xAxis, yAxis).normalized;

        float worldWidth = Vector3.Distance(bottomLeft, bottomRight);
        float worldHeight = Vector3.Distance(bottomLeft, topLeft);

        float uiWidth = Mathf.Max(rectTransform.rect.width, 0.001f);
        float uiHeight = Mathf.Max(rectTransform.rect.height, 0.001f);

        float paddingWorldX = paddingUnits * (worldWidth / uiWidth);
        float paddingWorldY = paddingUnits * (worldHeight / uiHeight);

        float halfWidth = worldWidth * 0.5f + paddingWorldX;
        float halfHeight = worldHeight * 0.5f + paddingWorldY;

        Vector3 proxyWorldPosition = transform.position;

        float signedDistance = Vector3.Dot(proxyWorldPosition - center, normal);
        float absoluteDistance = Mathf.Abs(signedDistance);

        lastButtonDistanceMeters = absoluteDistance;

        if (absoluteDistance > touchDepthMeters) {
            lastInsideRect = false;
            return false;
        }

        Vector3 projectedPoint = proxyWorldPosition - normal * signedDistance;
        Vector3 delta = projectedPoint - center;

        float localX = Vector3.Dot(delta, xAxis);
        float localY = Vector3.Dot(delta, yAxis);

        bool insideRect =
            Mathf.Abs(localX) <= halfWidth &&
            Mathf.Abs(localY) <= halfHeight;

        lastButtonLocalMeters = new Vector2(localX, localY);
        lastInsideRect = insideRect;

        return insideRect;
    }

    bool IsAnyButtonCurrentlyTouched() {
        Button ignoredButton;
        int ignoredValue;

        if (IsFingerInsideButton(continueButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            return true;
        }

        if (IsFingerInsideButton(startButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            return true;
        }

        if (IsFingerInsideButton(disconnectionYesButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            return true;
        }

        if (IsFingerInsideButton(disconnectionNoButton, specialButtonTouchDepthMeters, specialButtonPaddingUnits)) {
            return true;
        }

        if (TryFindButton(disconnectionTimingButtons, out ignoredButton, out ignoredValue)) {
            return true;
        }

        if (TryFindButton(handednessButtons, out ignoredButton, out ignoredValue)) {
            return true;
        }

        if (TryFindButton(arExperienceButtons, out ignoredButton, out ignoredValue)) {
            return true;
        }

        if (TryFindButton(trialRatingButtons, out ignoredButton, out ignoredValue)) {
            return true;
        }

        if (TryFindButton(postQuestionButtons, out ignoredButton, out ignoredValue)) {
            return true;
        }

        return false;
    }

    enum UIButtonKind {
        None,
        Continue,
        Start,
        HandednessAnswer,
        ARExperienceAnswer,
        TrialRatingAnswer,
        PostQuestionAnswer,
        DisconnectionYes,
        DisconnectionNo,
        DisconnectionTiming
    }
}