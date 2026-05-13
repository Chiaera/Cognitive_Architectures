using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraSetup : MonoBehaviour
{
    void Awake()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        UniversalAdditionalCameraData cameraData = 
            GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = false;
        }
    }
}