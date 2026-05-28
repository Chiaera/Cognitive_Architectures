using UnityEngine;
using UnityEngine.XR.Hands;
using Manus.Interaction;
using Manus;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class test01_weightCube : MonoBehaviour, IGrabbable {

    [Tooltip("0 = light, 1 = real weight, >1 = very heavy")]
    public float weightFactor = 1f;

    [Tooltip("Force with which the hand 'drags' the cube towards itself")]
    public float grabForce = 50f;

    private Rigidbody rb;
    private Transform handTransform = null;
    private bool usingManus = false;
    private bool isGrabbed = false;

    // hand offset during grab: where the cube is relative to the hand when grabbed
    private Vector3 grabLocalOffset;

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }

    void Start() {
        usingManus = ManusManager.communicationHub != null
                     && ManusManager.communicationHub.currentState == CommunicationHub.State.Connected;

        //ML2: trigger collider | MANUS: normal collision
        GetComponent<Collider>().isTrigger = !usingManus;

        //mass wrt weight factor
        rb.mass = Mathf.Max(0.1f, weightFactor);
        rb.useGravity = true;
    }

    // ML2 -----------------------------------------------------------------------
    void OnTriggerEnter(Collider other) {
        if (usingManus || isGrabbed) return;

        if (other.CompareTag("Hand") || other.name.Contains("Hand")) {
            StartGrab(other.transform);
        }
    }

    void OnTriggerExit(Collider other) {
        if (usingManus) return;

        if (handTransform != null && other.transform == handTransform) {
            EndGrab();
        }
    }

    void FixedUpdate() {
        if (!isGrabbed || handTransform == null || usingManus) return;
        ApplyWeightedFollow();
    }

    // MANUS -----------------------------------------------------------------------
    public void OnGrabbedStart(GrabbedObject p_Object) {
        if (p_Object != null) StartGrab(p_Object.transform);
    }

    public void OnGrabbedEnd(GrabbedObject p_Object) {
        EndGrab();
    }

    public void OnGrabbedFixedUpdate(GrabbedObject p_Object) {
        if (!isGrabbed || handTransform == null) return;
        ApplyWeightedFollow();
    }

    public void OnGrabbedHandPose(InteractionHand p_Hand, GrabbedObject.Info p_Info) { }
    public void OnAddedInteractingInfo(GrabbedObject p_Object, GrabbedObject.Info p_Info) { }
    public void OnRemovedInteractingInfo(GrabbedObject p_Object, GrabbedObject.Info p_Info) { }


    // COMMON FUNCTIONS -----------------------------------------------------------------------
    void StartGrab(Transform hand) {
        handTransform = hand;
        isGrabbed = true;
        rb.useGravity = false; // disattiva gravità durante il grab
        // Salva l'offset locale rispetto alla mano
        grabLocalOffset = handTransform.InverseTransformPoint(transform.position);
    }

    void EndGrab() {
        isGrabbed = false;
        handTransform = null;
        rb.useGravity = true;
        // Trasferisci la velocità della mano al rigidbody al rilascio
        // (opzionale: puoi campionare la velocità della mano nei frame precedenti)
    }

    //force push towards hand, scaled by weight factor
    void ApplyWeightedFollow() {
        // target position if the cube were to follow the hand's grab offset
        Vector3 targetPosition = handTransform.TransformPoint(grabLocalOffset);

        // direction and distance to the target position
        Vector3 delta = targetPosition - transform.position;

        // grabForce / weightFactor --> ligheter feels more responsive, heavier feels more sluggish
        float effectiveForce = grabForce / Mathf.Max(0.1f, weightFactor);

        rb.AddForce(delta * effectiveForce, ForceMode.Force);

        // avoid oscillations by damping velocity (more for heavier objects)
        rb.velocity *= 0.85f;
    }
}