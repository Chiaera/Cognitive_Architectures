using System.Collections.Generic;
using UnityEngine;
using Manus.Interaction;

public class test01_weightCube : MonoBehaviour, IGrabbable{
    public float weightFactor = 0.5f;
    private Transform handTransform = null;

// --- IGrabbable interface ---
    public void OnGrabbedStart(GrabbedObject p_Object){ //Store the grabber's transform when grab begins
        if (p_Object != null) handTransform = p_Object.transform;
    }
    public void OnGrabbedEnd(GrabbedObject p_Object){
        handTransform = null;
    }

    public void OnGrabbedFixedUpdate(GrabbedObject p_Object){ //Called every FixedUpdate while grabbed — ideal place for physics-based weight
        if (handTransform != null) AddWeight();
    }

    public void OnGrabbedHandPose(InteractionHand p_Hand, GrabbedObject.Info p_Info){
        //Required by interface: NO custom hand posing
    }

    public void OnAddedInteractingInfo(GrabbedObject p_Object, GrabbedObject.Info p_Info){
        //Required by interface; leave empty
    }

    public void OnRemovedInteractingInfo(GrabbedObject p_Object, GrabbedObject.Info p_Info){
        //Required by interface; leave empty
    }

// --- Weight logic ---
    void AddWeight(){
        // weightFactor = 1: follows hand exactly
        // weightFactor < 1: lags behind (feels heavy)
        // weightFactor > 1: overshoots (negative resistance, light/accelerating)
        transform.position = handTransform.position + (handTransform.position - transform.position) * (weightFactor - 1);
    }
}