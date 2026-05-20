using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Manus.Interaction;
using Manus;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class test01_weightCube : MonoBehaviour, IGrabbable {

    public float weightFactor = 0.5f;

    private Transform handTransform = null;
    private bool usingManus = false;
    private bool isGrabbedByML2 = false;

    void Start() {
        //check if Manus is connected
        usingManus = ManusManager.communicationHub != null && ManusManager.communicationHub.currentState == CommunicationHub.State.Connected;

        //check collider setup
        if (!usingManus) {
            GetComponent<Collider>().isTrigger = true;
        }
    }

    //ML2 MODE
    void OnTriggerEnter(Collider other) {
        if (usingManus) return; 
        
        //check 'hand' collider
        if (other.CompareTag("Hand") || other.name.Contains("Hand")) {
            handTransform = other.transform;
            isGrabbedByML2 = true;
        }
    }
    void OnTriggerExit(Collider other) {
        if (usingManus) return;
        
        if (other.transform == handTransform) {
            handTransform = null;
            isGrabbedByML2 = false;
        }
    }
    void FixedUpdate() {
        if (isGrabbedByML2 && handTransform != null) {
            AddWeight();
        }
    }


    //MANUS MODE
    public void OnGrabbedStart(GrabbedObject p_Object) {
        if (p_Object != null) handTransform = p_Object.transform;
    }

    public void OnGrabbedEnd(GrabbedObject p_Object) {
        handTransform = null;
    }

    public void OnGrabbedFixedUpdate(GrabbedObject p_Object) {
        if (handTransform != null) AddWeight();
    }

    public void OnGrabbedHandPose(InteractionHand p_Hand, GrabbedObject.Info p_Info) { }
    public void OnAddedInteractingInfo(GrabbedObject p_Object, GrabbedObject.Info p_Info) { }
    public void OnRemovedInteractingInfo(GrabbedObject p_Object, GrabbedObject.Info p_Info) { }

    
    //WEIGHT calculation
    void AddWeight() {
        transform.position = handTransform.position + (handTransform.position - transform.position) * (weightFactor - 1);
    }
}