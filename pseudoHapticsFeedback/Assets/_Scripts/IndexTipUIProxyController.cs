using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class IndexTipUIProxyController : MonoBehaviour {
    [Header("References")]
    [SerializeField]
    private HandSqueezeDetector squeezeDetector;

    [Header("Settings")]
    [SerializeField]
    private float followSpeed = 35f;

    [SerializeField]
    private float proxyRadiusMeters = 0.014f;

    [SerializeField]
    private bool showProxyVisual = true;

    [Header("Debug")]
    [SerializeField]
    private bool indexTracked = false;

    [SerializeField]
    private Vector3 currentIndexPosition;

    private SphereCollider sphereCollider;
    private Rigidbody proxyRigidbody;
    private Renderer proxyRenderer;

    void Awake() {
        sphereCollider = GetComponent<SphereCollider>();
        proxyRigidbody = GetComponent<Rigidbody>();
        proxyRenderer = GetComponentInChildren<Renderer>();

        sphereCollider.isTrigger = true;
        sphereCollider.radius = 0.5f;

        proxyRigidbody.useGravity = false;
        proxyRigidbody.isKinematic = true;
    }

    void Start() {
        if (squeezeDetector == null) {
            squeezeDetector = FindObjectOfType<HandSqueezeDetector>();
        }

        ApplyVisualState();

        Debug.Log("Index tip UI proxy initialized");
    }

    void Update() {
        ApplyVisualState();
        UpdateProxyPosition();
    }

    void UpdateProxyPosition() {
        indexTracked = false;

        if (squeezeDetector == null) {
            return;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            return;
        }

        if (fingerPositions == null || fingerPositions.Length < 2) {
            return;
        }

        currentIndexPosition = fingerPositions[1];
        indexTracked = true;

        transform.position = Vector3.Lerp(
            transform.position,
            currentIndexPosition,
            Time.deltaTime * followSpeed
        );

        transform.localScale = Vector3.one * proxyRadiusMeters;
    }

    void ApplyVisualState() {
        if (proxyRenderer == null) {
            return;
        }

        proxyRenderer.enabled = showProxyVisual;
    }

    public bool IsIndexTracked() {
        return indexTracked;
    }
}