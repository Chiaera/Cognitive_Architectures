using UnityEngine;

public class SqueezeHandProxyConstraint : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read real hand joint positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Ball Constraint")]
    [Tooltip("Center of the fixed stress ball")]
    public Transform ballCenter;

    [Tooltip("Visual radius of the ball in meters")]
    public float ballRadiusMeters = 0.045f;

    [Tooltip("Small offset to keep proxy markers slightly outside the ball surface")]
    public float surfaceOffsetMeters = 0.002f;

    [Header("Proxy Visuals")]
    [Tooltip("Material used for the proxy fingertips")]
    public Material proxyMaterial;

    [Tooltip("Size of fingertip proxy markers")]
    public float fingertipProxySize = 0.014f;

    [Tooltip("Show palm proxy marker")]
    public bool showPalmProxy = false;

    [Tooltip("Size of palm proxy marker")]
    public float palmProxySize = 0.025f;

    [Header("Behavior")]
    [Tooltip("If true, proxy points cannot enter the ball")]
    public bool constrainInsideBall = true;

    [Tooltip("How fast proxy markers move toward their target position")]
    public float followSpeed = 25f;

    [Header("Debug")]
    public GameObject[] fingertipProxyObjects = new GameObject[5];
    public GameObject palmProxyObject;

    public float thumbPenetration = 0f;
    public float indexPenetration = 0f;
    public float middlePenetration = 0f;
    public float ringPenetration = 0f;
    public float littlePenetration = 0f;
    public float averagePenetration = 0f;

    private readonly string[] fingertipNames = {
        "ThumbProxy",
        "IndexProxy",
        "MiddleProxy",
        "RingProxy",
        "LittleProxy"
    };

    private float[] fingertipPenetrations = new float[5];

    void Start() {
        if (ballCenter == null) {
            ballCenter = transform;
        }

        CreateProxyObjects();

        Debug.Log("Squeeze hand proxy constraint initialized");
    }

    void Update() {
        if (squeezeDetector == null || ballCenter == null) {
            HideAllProxyObjects();
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            HideAllProxyObjects();
            return;
        }

        UpdateFingertipProxies();
        UpdatePalmProxy();
        UpdateDebugValues();
    }

    void CreateProxyObjects() {
        for (int i = 0; i < fingertipProxyObjects.Length; i++) {
            if (fingertipProxyObjects[i] != null) {
                continue;
            }

            GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxy.name = fingertipNames[i];
            proxy.transform.SetParent(transform);

            Collider collider = proxy.GetComponent<Collider>();

            if (collider != null) {
                Destroy(collider);
            }

            Renderer renderer = proxy.GetComponent<Renderer>();

            if (renderer != null && proxyMaterial != null) {
                renderer.material = proxyMaterial;
            }

            proxy.transform.localScale = Vector3.one * fingertipProxySize;
            fingertipProxyObjects[i] = proxy;
        }

        if (palmProxyObject == null) {
            palmProxyObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            palmProxyObject.name = "PalmProxy";
            palmProxyObject.transform.SetParent(transform);

            Collider collider = palmProxyObject.GetComponent<Collider>();

            if (collider != null) {
                Destroy(collider);
            }

            Renderer renderer = palmProxyObject.GetComponent<Renderer>();

            if (renderer != null && proxyMaterial != null) {
                renderer.material = proxyMaterial;
            }

            palmProxyObject.transform.localScale = Vector3.one * palmProxySize;
            palmProxyObject.SetActive(showPalmProxy);
        }
    }

    void UpdateFingertipProxies() {
        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingertipPositions)) {
            HideAllProxyObjects();
            return;
        }

        for (int i = 0; i < fingertipProxyObjects.Length; i++) {
            if (fingertipProxyObjects[i] == null) {
                continue;
            }

            fingertipProxyObjects[i].SetActive(true);

            Vector3 targetPosition = GetConstrainedPosition(
                fingertipPositions[i],
                out float penetration
            );

            fingertipPenetrations[i] = penetration;

            fingertipProxyObjects[i].transform.position = Vector3.Lerp(
                fingertipProxyObjects[i].transform.position,
                targetPosition,
                Time.deltaTime * followSpeed
            );

            fingertipProxyObjects[i].transform.localScale = Vector3.one * fingertipProxySize;
        }
    }

    void UpdatePalmProxy() {
        if (palmProxyObject == null) {
            return;
        }

        palmProxyObject.SetActive(showPalmProxy);

        if (!showPalmProxy) {
            return;
        }

        if (!squeezeDetector.TryGetPalmPosition(out Vector3 palmPosition)) {
            palmProxyObject.SetActive(false);
            return;
        }

        Vector3 targetPosition = GetConstrainedPosition(
            palmPosition,
            out float penetration
        );

        palmProxyObject.transform.position = Vector3.Lerp(
            palmProxyObject.transform.position,
            targetPosition,
            Time.deltaTime * followSpeed
        );

        palmProxyObject.transform.localScale = Vector3.one * palmProxySize;
    }

    Vector3 GetConstrainedPosition(Vector3 realWorldPosition, out float penetration) {
        penetration = 0f;

        if (!constrainInsideBall) {
            return realWorldPosition;
        }

        Vector3 center = ballCenter.position;
        Vector3 centerToPoint = realWorldPosition - center;
        float distance = centerToPoint.magnitude;

        float constraintRadius = ballRadiusMeters + surfaceOffsetMeters;

        if (distance >= constraintRadius) {
            return realWorldPosition;
        }

        penetration = constraintRadius - distance;

        if (centerToPoint.sqrMagnitude < 0.0001f) {
            return center + Vector3.forward * constraintRadius;
        }

        Vector3 surfaceDirection = centerToPoint.normalized;

        return center + surfaceDirection * constraintRadius;
    }

    void UpdateDebugValues() {
        thumbPenetration = fingertipPenetrations[0];
        indexPenetration = fingertipPenetrations[1];
        middlePenetration = fingertipPenetrations[2];
        ringPenetration = fingertipPenetrations[3];
        littlePenetration = fingertipPenetrations[4];

        float total = 0f;
        int count = 0;

        for (int i = 0; i < fingertipPenetrations.Length; i++) {
            if (fingertipPenetrations[i] <= 0f) {
                continue;
            }

            total += fingertipPenetrations[i];
            count++;
        }

        averagePenetration = count > 0 ? total / count : 0f;
    }

    void HideAllProxyObjects() {
        for (int i = 0; i < fingertipProxyObjects.Length; i++) {
            if (fingertipProxyObjects[i] != null) {
                fingertipProxyObjects[i].SetActive(false);
            }
        }

        if (palmProxyObject != null) {
            palmProxyObject.SetActive(false);
        }
    }
}