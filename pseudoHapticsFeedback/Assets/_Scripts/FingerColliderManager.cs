using UnityEngine;

public class FingerColliderManager : MonoBehaviour {
    [Header("Input")]
    [Tooltip("Detector used to read fingertip positions")]
    public HandSqueezeDetector squeezeDetector;

    [Header("Finger Colliders")]
    [Tooltip("Radius of each fingertip collider")]
    public float fingertipColliderRadius = 0.015f;

    [Tooltip("Show fingertip collider spheres for debugging")]
    public bool showDebugSpheres = true;

    [Header("Debug")]
    public GameObject[] fingertipObjects = new GameObject[5];

    private readonly string[] fingerNames = {
        "ThumbTipCollider",
        "IndexTipCollider",
        "MiddleTipCollider",
        "RingTipCollider",
        "LittleTipCollider"
    };

    void Start() {
        // Create one collider proxy for each fingertip
        CreateFingerColliders();
    }

    void Update() {
        if (squeezeDetector == null) {
            SetFingerCollidersActive(false);
            return;
        }

        if (!squeezeDetector.IsHandTracked) {
            SetFingerCollidersActive(false);
            return;
        }

        if (!squeezeDetector.TryGetFingerTipPositions(out Vector3[] fingerPositions)) {
            SetFingerCollidersActive(false);
            return;
        }

        SetFingerCollidersActive(true);

        for (int i = 0; i < fingertipObjects.Length; i++) {
            if (fingertipObjects[i] != null) {
                fingertipObjects[i].transform.position = fingerPositions[i];
            }
        }
    }

    void CreateFingerColliders() {
        for (int i = 0; i < fingertipObjects.Length; i++) {
            GameObject fingerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            fingerObject.name = fingerNames[i];
            fingerObject.transform.SetParent(transform);
            fingerObject.transform.localScale = Vector3.one * fingertipColliderRadius * 2f;

            SphereCollider sphereCollider = fingerObject.GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = 0.5f;

            Rigidbody rigidbody = fingerObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            Renderer renderer = fingerObject.GetComponent<Renderer>();

            if (renderer != null) {
                renderer.enabled = showDebugSpheres;
            }

            fingertipObjects[i] = fingerObject;
        }

        Debug.Log("Finger colliders created");
    }

    void SetFingerCollidersActive(bool active) {
        for (int i = 0; i < fingertipObjects.Length; i++) {
            if (fingertipObjects[i] != null && fingertipObjects[i].activeSelf != active) {
                fingertipObjects[i].SetActive(active);
            }
        }
    }

    public void SetDebugSpheresVisible(bool visible) {
        // Enable or disable fingertip collider visualization
        showDebugSpheres = visible;

        for (int i = 0; i < fingertipObjects.Length; i++) {
            if (fingertipObjects[i] == null) {
                continue;
            }

            Renderer renderer = fingertipObjects[i].GetComponent<Renderer>();

            if (renderer != null) {
                renderer.enabled = showDebugSpheres;
            }
        }
    }
}