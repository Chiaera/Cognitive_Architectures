using UnityEngine;

public class test : MonoBehaviour
{
    [Header("Reference")]
    public HandSqueezeDetector detector;

    [Header("Debug")]
    [Range(0f, 1f)]
    public float currentValue;

    private Renderer rend;
    private Material mat;

    void Start()
    {
        rend = GetComponent<Renderer>();

        if (rend != null)
            mat = rend.material;
    }

    void Update()
    {
        if (detector == null || mat == null)
            return;

        currentValue = detector.squeezeAmount;
        currentValue = Mathf.Clamp01(currentValue);

        Color c = Color.Lerp(Color.green, Color.red, currentValue);

        mat.color = c;
        mat.SetColor("_BaseColor", c);
    }
}