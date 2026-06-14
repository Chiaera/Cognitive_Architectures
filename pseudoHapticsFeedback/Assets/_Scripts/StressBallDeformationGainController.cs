using UnityEngine;

public class StressBallDeformationGainController : MonoBehaviour {
    [Header("References")]
    [Tooltip("Deformer controlled by the experimental gain")]
    public StressBallPalmBulgeFingerBandDeformer deformer;

    [Header("Current Gain")]
    [Tooltip("Current multiplier applied to the visual deformation")]
    [Range(0.25f, 2.0f)]
    public float visualDeformationGain = 1.0f;

    [Header("Baseline Deformation Values")]
    public float basePalmMaxDepthMeters = 0.024f;
    public float baseThumbPatchMaxDepthMeters = 0.016f;
    public float baseThumbSegmentMaxDepthMeters = 0.024f;
    public float baseFingerBandMaxDepthMeters = 0.000f;
    public float baseLittleSegmentMaxDepthMeters = 0.000f;

    [Header("Runtime Applied Values")]
    public float appliedPalmMaxDepthMeters = 0f;
    public float appliedThumbPatchMaxDepthMeters = 0f;
    public float appliedThumbSegmentMaxDepthMeters = 0f;
    public float appliedFingerBandMaxDepthMeters = 0f;
    public float appliedLittleSegmentMaxDepthMeters = 0f;

    void Start() {
        if (deformer == null) {
            deformer = GetComponent<StressBallPalmBulgeFingerBandDeformer>();
        }

        ApplyGain();

        Debug.Log("Stress ball deformation gain controller initialized");
    }

    void Update() {
        ApplyGain();
    }

    public void SetVisualDeformationGain(float newGain) {
        visualDeformationGain = Mathf.Clamp(newGain, 0.25f, 2.0f);
        ApplyGain();

        Debug.Log("Visual deformation gain set to " + visualDeformationGain);
    }

    public float GetVisualDeformationGain() {
        return visualDeformationGain;
    }

    public void SetLevel(int levelIndex) {
        float gain = 1.0f;

        if (levelIndex == 1) {
            gain = 0.50f;
        }
        else if (levelIndex == 2) {
            gain = 0.75f;
        }
        else if (levelIndex == 3) {
            gain = 1.00f;
        }
        else if (levelIndex == 4) {
            gain = 1.25f;
        }
        else if (levelIndex == 5) {
            gain = 1.50f;
        }

        SetVisualDeformationGain(gain);
    }

    void ApplyGain() {
        if (deformer == null) {
            return;
        }

        appliedPalmMaxDepthMeters = basePalmMaxDepthMeters * visualDeformationGain;
        appliedThumbPatchMaxDepthMeters = baseThumbPatchMaxDepthMeters * visualDeformationGain;
        appliedThumbSegmentMaxDepthMeters = baseThumbSegmentMaxDepthMeters * visualDeformationGain;
        appliedFingerBandMaxDepthMeters = baseFingerBandMaxDepthMeters * visualDeformationGain;
        appliedLittleSegmentMaxDepthMeters = baseLittleSegmentMaxDepthMeters * visualDeformationGain;

        deformer.palmMaxDepthMeters = appliedPalmMaxDepthMeters;
        deformer.thumbPatchMaxDepthMeters = appliedThumbPatchMaxDepthMeters;
        deformer.thumbSegmentMaxDepthMeters = appliedThumbSegmentMaxDepthMeters;
        deformer.fingerBandMaxDepthMeters = appliedFingerBandMaxDepthMeters;
        deformer.littleSegmentMaxDepthMeters = appliedLittleSegmentMaxDepthMeters;
    }
}