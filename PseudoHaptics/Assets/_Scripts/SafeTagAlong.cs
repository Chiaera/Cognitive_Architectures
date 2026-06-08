using UnityEngine;

public class SnapTagAlong : MonoBehaviour {
    [Header("References")]
    public Transform cameraTransform;

    [Header("Space Configuration")]
    public float distanceFromCamera = 1.4f;
    public float snapAngleThreshold = 35f;   
    public float snapDelay = 1.5f;           

    private float timeOutsideThreshold = 0f;

    void Start() {
        if (cameraTransform == null) {
            cameraTransform = Camera.main.transform;
        }
        SnapNow();
    }

    void LateUpdate() {
        if (cameraTransform == null) {
            return;
        }

        Vector3 directionToCanvas = (transform.position - cameraTransform.position).normalized;
        float currentAngle = Vector3.Angle(cameraTransform.forward, directionToCanvas);

        if (currentAngle > snapAngleThreshold) {
            timeOutsideThreshold += Time.deltaTime;
            if (timeOutsideThreshold >= snapDelay) {
                SnapNow();
            }
        } else {
            timeOutsideThreshold = 0f; 
        }
    }

    private void SnapNow() {
        Vector3 cameraForward = cameraTransform.forward;
        transform.position = cameraTransform.position + (cameraForward * distanceFromCamera);

        Vector3 lookDirection = cameraForward;
        lookDirection.y = 0;
        
        if (lookDirection.sqrMagnitude > 0.001f) {
            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        timeOutsideThreshold = 0f;
    }
}