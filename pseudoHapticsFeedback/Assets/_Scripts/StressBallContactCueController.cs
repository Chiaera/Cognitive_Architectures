using UnityEngine;

public class StressBallContactCueController : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Tooltip("Analyzer used to read per-finger pressure values")]
    public StressBallFingerPressureAnalyzer pressureAnalyzer;

    [Header("Visual Target")]
    [Tooltip("The visual mesh object of the stress ball")]
    public Transform ballVisual;

    [Tooltip("Material used for the contact cue marks")]
    public Material contactCueMaterial;

    [Header("Ball Settings")]
    [Tooltip("Manual radius of the ball in meters")]
    public float ballRadius = 0.045f;

    [Tooltip("Small offset used to avoid z-fighting with the ball surface")]
    public float surfaceOffset = 0.002f;

    [Header("Cue Settings")]
    [Tooltip("Minimum finger pressure required to show a contact cue")]
    [Range(0f, 1f)]
    public float pressureActivationThreshold = 0.03f;

    [Tooltip("Minimum cue size")]
    public float minCueSize = 0.012f;

    [Tooltip("Maximum cue size")]
    public float maxCueSize = 0.035f;

    [Tooltip("How flat the cue marks should be")]
    public float cueThickness = 0.0015f;

    [Tooltip("How fast contact cues appear")]
    public float appearSpeed = 18f;

    [Tooltip("How fast contact cues disappear")]
    public float disappearSpeed = 12f;

    [Header("Debug")]
    public GameObject[] cueObjects = new GameObject[5];

    private readonly string[] cueNames = {
        "ThumbContactCue",
        "IndexContactCue",
        "MiddleContactCue",
        "RingContactCue",
        "LittleContactCue"
    };

    private Vector3[] currentCueScales = new Vector3[5];

    void Start() {
        // Try to find required references if they were not assigned manually
        if (pressureAnalyzer == null) {
            pressureAnalyzer = GetComponent<StressBallFingerPressureAnalyzer>();
        }

        if (ballVisual == null && transform.childCount > 0) {
            ballVisual = transform.GetChild(0);
        }

        CreateContactCues();

        Debug.Log("Stress ball contact cue controller initialized");
    }

    void Update() {
        UpdateContactCues();
    }

    void CreateContactCues() {
        // Create one small flattened sphere for each fingertip contact cue
        for (int i = 0; i < cueObjects.Length; i++) {
            GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            cue.name = cueNames[i];
            cue.transform.SetParent(ballVisual != null ? ballVisual : transform);

            SphereCollider collider = cue.GetComponent<SphereCollider>();

            if (collider != null) {
                Destroy(collider);
            }

            Renderer renderer = cue.GetComponent<Renderer>();

            if (renderer != null && contactCueMaterial != null) {
                renderer.material = contactCueMaterial;
            }

            cue.transform.localScale = Vector3.zero;
            cueObjects[i] = cue;
            currentCueScales[i] = Vector3.zero;
        }
    }

    void UpdateContactCues() {
        if (squeezeDetector == null || pressureAnalyzer == null || ballVisual == null) {
            HideAllCues();
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            HideAllCues();
            return;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            HideAllCues();
            return;
        }

        float[] fingerPressures = pressureAnalyzer.GetAllFingerPressures();

        for (int i = 0; i < cueObjects.Length; i++) {
            UpdateSingleCue(i, fingerPositions[i], fingerPressures[i]);
        }
    }

    void UpdateSingleCue(int index, Vector3 fingerWorldPosition, float pressure) {
        if (cueObjects[index] == null) {
            return;
        }

        bool cueShouldBeVisible = pressure >= pressureActivationThreshold;

        if (!cueShouldBeVisible) {
            SmoothCueScale(index, Vector3.zero, disappearSpeed);
            return;
        }

        Vector3 ballCenter = transform.position;
        Vector3 centerToFinger = fingerWorldPosition - ballCenter;

        if (centerToFinger.sqrMagnitude < 0.0001f) {
            SmoothCueScale(index, Vector3.zero, disappearSpeed);
            return;
        }

        Vector3 surfaceNormal = centerToFinger.normalized;
        Vector3 cueWorldPosition = ballCenter + surfaceNormal * (ballRadius + surfaceOffset);

        cueObjects[index].transform.position = cueWorldPosition;
        cueObjects[index].transform.rotation = Quaternion.LookRotation(surfaceNormal, Vector3.up);

        float cueSize = Mathf.Lerp(minCueSize, maxCueSize, pressure);

        Vector3 targetScale = new Vector3(
            cueSize,
            cueSize,
            cueThickness
        );

        SmoothCueScale(index, targetScale, appearSpeed);
    }

    void SmoothCueScale(int index, Vector3 targetScale, float speed) {
        // Smoothly update each cue scale to avoid flickering
        currentCueScales[index] = Vector3.Lerp(
            currentCueScales[index],
            targetScale,
            Time.deltaTime * speed
        );

        cueObjects[index].transform.localScale = currentCueScales[index];
    }

    void HideAllCues() {
        // Hide all contact cues when tracking or references are missing
        for (int i = 0; i < cueObjects.Length; i++) {
            if (cueObjects[i] == null) {
                continue;
            }

            SmoothCueScale(i, Vector3.zero, disappearSpeed);
        }
    }
}