using UnityEngine;
using UnityEngine.XR.Hands;

public class HandSqueezeDetector : MonoBehaviour
{
    public XRHandTrackingEvents handTrackingEvents;

    [Header("Debug")]
    public float rawAverageDistance;
    [Range(0f, 1f)] public float squeezeAmount = 0f;
    public float squeezeNormalized = 0f;

    public float openDistance = 0.12f;
    public float closedDistance = 0.04f;

    private XRHand currentHand;
    private bool isHandTracked = false; 

    public bool IsHandTracked => isHandTracked && currentHand.isTracked; //if currently the hand is visible

    public bool CalibrateOpen() {
        float d = GetAverageFingerToPalmDistance();
        if (d > 0) {
            openDistance = d;
            Debug.Log($"Open calibrato: {d:F3}m");
            return true;
        }
        return false;
    }

    public bool CalibrateClosed() {
        float d = GetAverageFingerToPalmDistance();
        if (d > 0) {
            closedDistance = d;
            Debug.Log($"Closed calibrato: {d:F3}m");
            return true;
        }
        return false;
    }

    void OnEnable() {
        if (handTrackingEvents != null) {
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
            handTrackingEvents.trackingLost.AddListener(OnTrackingLost);
        }
    }

    void OnDisable() {
        if (handTrackingEvents != null) {
            handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
            handTrackingEvents.trackingLost.RemoveListener(OnTrackingLost);
        }
    }

    void OnJointsUpdated(XRHandJointsUpdatedEventArgs args) {
        currentHand = args.hand;
        isHandTracked = true;
    }

    void OnTrackingLost() {
        isHandTracked = false;
    }

    void Update() {
        if (!IsHandTracked) return;

        float avg = GetAverageFingerToPalmDistance();
        if (avg < 0) return; 

        rawAverageDistance = avg;

        squeezeAmount = 1f - Mathf.InverseLerp(closedDistance, openDistance, avg); 
        squeezeAmount = Mathf.Clamp01(squeezeAmount);

        squeezeNormalized = Mathf.Clamp01(Mathf.InverseLerp(0.1f, 0.9f, squeezeAmount));
    }

    public float GetAverageFingerToPalmDistance() {
        if (!IsHandTracked) return -1f;

        if (!currentHand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose))
            return -1f;

        XRHandJointID[] tips = {
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

        float total = 0f;
        int count = 0;

        foreach (var tipID in tips) {
            if (currentHand.GetJoint(tipID).TryGetPose(out Pose tipPose)) {
                total += Vector3.Distance(tipPose.position, palmPose.position);
                count++;
            }
        }

        return count > 0 ? total / count : -1f;
    }
}