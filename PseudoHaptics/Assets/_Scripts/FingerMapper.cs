using UnityEngine;

public class FingerMapper : MonoBehaviour {

    [Header("ML2 source joints")]
    public Transform ml2IndexProximal;
    public Transform ml2IndexIntermediate;
    public Transform ml2IndexDistal;

    [Header("MANUS target bones")]
    public Transform manusIndex01;
    public Transform manusIndex02;
    public Transform manusIndex03;

    [Header("Settings")]
    public float rotationMultiplier = 1f;
    public Vector3 flexionAxis = Vector3.forward;

    private Quaternion manus01Initial;
    private Quaternion manus02Initial;
    private Quaternion manus03Initial;

    private Quaternion ml2ProxInitial;
    private Quaternion ml2InterInitial;
    private Quaternion ml2DistInitial;

    public bool calibrated = false;

    void OnDrawGizmos() {
        DrawJointAxes(ml2IndexProximal, 0.03f);
        DrawJointAxes(manusIndex01, 0.02f);
    }

    void DrawJointAxes(Transform t, float size) {
        if (t == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(t.position, t.position + t.right * size);    // X
        Gizmos.color = Color.green;
        Gizmos.DrawLine(t.position, t.position + t.up * size);       // Y
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(t.position, t.position + t.forward * size);  // Z
    }

    public void Calibrate() {
        if (manusIndex01 != null) manus01Initial = manusIndex01.localRotation;
        if (manusIndex02 != null) manus02Initial = manusIndex02.localRotation;
        if (manusIndex03 != null) manus03Initial = manusIndex03.localRotation;

        if (ml2IndexProximal != null) ml2ProxInitial = ml2IndexProximal.localRotation;
        if (ml2IndexIntermediate != null) ml2InterInitial = ml2IndexIntermediate.localRotation;
        if (ml2IndexDistal != null) ml2DistInitial = ml2IndexDistal.localRotation;

        calibrated = true;
    }

    void Start() {
        Invoke(nameof(Calibrate), 2f);
    }

    void LateUpdate() {
        if (!calibrated) return;

        MapJoint(ml2IndexProximal, ml2ProxInitial, manusIndex01, manus01Initial);
        MapJoint(ml2IndexIntermediate, ml2InterInitial, manusIndex02, manus02Initial);
        MapJoint(ml2IndexDistal, ml2DistInitial, manusIndex03, manus03Initial);
    }

   void MapJoint(Transform source, Quaternion sourceInitial, Transform target, Quaternion targetInitial) {
    if (source == null || target == null) return;

    Quaternion delta = Quaternion.Inverse(sourceInitial) * source.localRotation;
    Vector3 euler = delta.eulerAngles;

    // Normalizza tutti gli angoli in -180/+180
    float x = NormalizeAngle(euler.x);
    float y = NormalizeAngle(euler.y);
    float z = NormalizeAngle(euler.z);

    // Log per capire quale asse si muove davvero
    Debug.Log($"[{source.name}] x:{x:F1} y:{y:F1} z:{z:F1}");

    // Per ora mappa solo Z
    float mappedAngle = z * rotationMultiplier;
    target.localRotation = targetInitial * Quaternion.Euler(0, 0, mappedAngle);
    }

    float NormalizeAngle(float a) {
        return a > 180f ? a - 360f : a;
    }
}