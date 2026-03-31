using UnityEngine;

/// <summary>
/// Simple parallax background scroll.
/// followTarget is optional — if null, only camera movement drives the effect.
/// </summary>
public class ParallaxEffect : MonoBehaviour
{
    public Camera    cam;
    [Tooltip("Optional. If left empty, parallax is driven by camera movement alone.")]
    public Transform followTarget;
    [Range(0f, 1f)]
    public float     parallaxStrength = 0.2f;

    private Vector3 _lastCamPos;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) _lastCamPos = cam.transform.position;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 delta = cam.transform.position - _lastCamPos;
        transform.position += new Vector3(delta.x * parallaxStrength, delta.y * parallaxStrength * 0.3f, 0f);
        _lastCamPos = cam.transform.position;
    }
}
