using UnityEngine;

public class HandContactVolumeController : MonoBehaviour {
    [Header("Contact Point Proxies")]
    [Tooltip("Stopped palm proxy")]
    public Transform palmProxy;

    [Tooltip("Stopped thumb proxy")]
    public Transform thumbProxy;

    [Tooltip("Stopped little finger proxy")]
    public Transform littleProxy;

    [Header("Contact Volumes")]
    [Tooltip("Capsule volume representing the thumb contact area")]
    public Transform thumbContactVolume;

    [Tooltip("Capsule volume representing the little finger contact area")]
    public Transform littleContactVolume;

    [Header("Thumb Volume Settings")]
    [Tooltip("Radius of the thumb contact volume")]
    public float thumbVolumeRadiusMeters = 0.014f;

    [Tooltip("Extra length added to the thumb contact volume")]
    public float thumbExtraLengthMeters = 0.010f;

    [Header("Little Volume Settings")]
    [Tooltip("Radius of the little finger contact volume")]
    public float littleVolumeRadiusMeters = 0.011f;

    [Tooltip("Extra length added to the little finger contact volume")]
    public float littleExtraLengthMeters = 0.006f;

    [Header("Visibility")]
    [Tooltip("Show contact volumes for debugging")]
    public bool showVolumes = true;

    [Tooltip("Hide volume when one of its endpoints is inactive")]
    public bool hideWhenEndpointsInactive = true;

    [Header("Segment Crop")]
    [Range(0f, 1f)]
    public float thumbSegmentStart01 = 0.35f;

    [Range(0f, 1f)]
    public float thumbSegmentEnd01 = 1.0f;

    [Range(0f, 1f)]
    public float littleSegmentStart01 = 0.45f;

    [Range(0f, 1f)]
    public float littleSegmentEnd01 = 1.0f;

    [Header("Smoothing")]
    [Tooltip("How fast the contact volumes follow the proxy segment")]
    public float followSpeed = 18f;

    [Header("Debug")]
    public bool thumbVolumeActive = false;
    public bool littleVolumeActive = false;
    public float thumbSegmentLength = 0f;
    public float littleSegmentLength = 0f;

    void Start() {
        SetVolumeVisible(thumbContactVolume, false);
        SetVolumeVisible(littleContactVolume, false);

        Debug.Log("Hand contact volume controller initialized");
    }

    void Update() {
        UpdateThumbVolume();
        UpdateLittleVolume();
    }

    void UpdateThumbVolume() {
        thumbVolumeActive = false;
        thumbSegmentLength = 0f;

        if (thumbContactVolume == null || palmProxy == null || thumbProxy == null) {
            SetVolumeVisible(thumbContactVolume, false);
            return;
        }

        if (hideWhenEndpointsInactive && (!palmProxy.gameObject.activeSelf || !thumbProxy.gameObject.activeSelf)) {
            SetVolumeVisible(thumbContactVolume, false);
            return;
        }

        thumbVolumeActive = true;

        Vector3 thumbStart = Vector3.Lerp(palmProxy.position, thumbProxy.position, thumbSegmentStart01);
        Vector3 thumbEnd = Vector3.Lerp(palmProxy.position, thumbProxy.position, thumbSegmentEnd01);

        UpdateCapsuleBetweenPoints(
            thumbContactVolume,
            thumbStart,
            thumbEnd,
            thumbVolumeRadiusMeters,
            thumbExtraLengthMeters,
            out thumbSegmentLength
        );

        SetVolumeVisible(thumbContactVolume, showVolumes);
    }

    void UpdateLittleVolume() {
        littleVolumeActive = false;
        littleSegmentLength = 0f;

        if (littleContactVolume == null || palmProxy == null || littleProxy == null) {
            SetVolumeVisible(littleContactVolume, false);
            return;
        }

        if (hideWhenEndpointsInactive && (!palmProxy.gameObject.activeSelf || !littleProxy.gameObject.activeSelf)) {
            SetVolumeVisible(littleContactVolume, false);
            return;
        }

        littleVolumeActive = true;

        Vector3 littleStart = Vector3.Lerp(palmProxy.position, littleProxy.position, littleSegmentStart01);
        Vector3 littleEnd = Vector3.Lerp(palmProxy.position, littleProxy.position, littleSegmentEnd01);

        UpdateCapsuleBetweenPoints(
            littleContactVolume,
            littleStart,
            littleEnd,
            littleVolumeRadiusMeters,
            littleExtraLengthMeters,
            out littleSegmentLength
        );

        SetVolumeVisible(littleContactVolume, showVolumes);
    }

    void UpdateCapsuleBetweenPoints(
        Transform volume,
        Vector3 startPoint,
        Vector3 endPoint,
        float radiusMeters,
        float extraLengthMeters,
        out float segmentLength
    ) {
        Vector3 segment = endPoint - startPoint;
        segmentLength = segment.magnitude;

        if (segmentLength < 0.0001f) {
            return;
        }

        Vector3 center = (startPoint + endPoint) * 0.5f;
        Vector3 direction = segment.normalized;

        float visualLength = segmentLength + extraLengthMeters;

        volume.position = Vector3.Lerp(
            volume.position,
            center,
            Time.deltaTime * followSpeed
        );

        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, direction);

        volume.rotation = Quaternion.Slerp(
            volume.rotation,
            targetRotation,
            Time.deltaTime * followSpeed
        );

        // Unity capsule height is along local Y. Scale Y is half of the final visual height.
        volume.localScale = Vector3.Lerp(
            volume.localScale,
            new Vector3(radiusMeters * 2f, visualLength * 0.5f, radiusMeters * 2f),
            Time.deltaTime * followSpeed
        );
    }

    void SetVolumeVisible(Transform volume, bool visible) {
        if (volume == null) {
            return;
        }

        Renderer renderer = volume.GetComponent<Renderer>();

        if (renderer != null) {
            renderer.enabled = visible;
        }
    }

    public bool TryGetThumbSegment(out Vector3 startPoint, out Vector3 endPoint, out float radiusMeters) {
        startPoint = Vector3.zero;
        endPoint = Vector3.zero;
        radiusMeters = thumbVolumeRadiusMeters;

        if (!thumbVolumeActive || palmProxy == null || thumbProxy == null) {
            return false;
        }

        startPoint = Vector3.Lerp(palmProxy.position, thumbProxy.position, thumbSegmentStart01);
        endPoint = Vector3.Lerp(palmProxy.position, thumbProxy.position, thumbSegmentEnd01);

        return true;
    }

    public bool TryGetLittleSegment(out Vector3 startPoint, out Vector3 endPoint, out float radiusMeters) {
        startPoint = Vector3.zero;
        endPoint = Vector3.zero;
        radiusMeters = littleVolumeRadiusMeters;

        if (!littleVolumeActive || palmProxy == null || littleProxy == null) {
            return false;
        }

        startPoint = Vector3.Lerp(palmProxy.position, littleProxy.position, littleSegmentStart01);
        endPoint = Vector3.Lerp(palmProxy.position, littleProxy.position, littleSegmentEnd01);

        return true;
    }
}