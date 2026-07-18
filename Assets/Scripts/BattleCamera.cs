using UnityEngine;

// Buttery camera motion for the fixed isometric orthographic camera.
// PlayerCommander feeds raw pan deltas and zoom factors in; this rig owns the
// target position/zoom, eases the real camera toward them, and adds a short
// natural glide after a pan is released. Angle and projection never change.
[RequireComponent(typeof(Camera))]
public class BattleCamera : MonoBehaviour
{
    [Header("Pan feel")]
    [Tooltip("Seconds of smoothing while the finger drives the camera; small = responsive, slightly weighted")]
    public float panSmoothTime = 0.08f;
    [Tooltip("How quickly the release glide dies off (higher = settles sooner)")]
    public float glideDamping = 5.5f;
    [Tooltip("Cap on the glide speed at release, world units/sec")]
    public float maxGlideSpeed = 40f;

    [Header("Zoom feel")]
    public float zoomSmoothTime = 0.12f;
    public float zoomMin = 5.5f;
    public float zoomMax = 36f;

    [Header("Battlefield bounds (ground-focus clamp, pitch-aware)")]
    [Tooltip("How far the center-screen ground focus may travel from the battlefield center")]
    public float focusHalfX = 40f;
    public float focusHalfZ = 38f;

    // camera-position clamps derived from the camera's actual pitch, height,
    // and current zoom, so the FOCUS point is what gets clamped and the whole
    // visible ground rectangle stays on the field — pan bounds stay correct
    // if the fixed angle is ever tuned again
    private float minX, maxX, minZ, maxZ;

    public bool IsPanning { get; private set; }

    private Camera cam;
    private Vector3 targetPos;
    private Vector3 smoothVel;          // SmoothDamp scratch
    private Vector3 glideVelocity;      // world units/sec after release
    private Vector3 inputVelocity;      // smoothed finger velocity while panning
    private float targetZoom;
    private float zoomVel;
    private float lastAspect;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        targetPos = transform.position;
        lastAspect = cam.aspect;
        targetZoom = Mathf.Clamp(cam.orthographicSize, zoomMin, EffectiveZoomMax());
        RecomputeBounds();
    }

    // Phase 6: battlefield scale is parameterized on BattleSetup — the setup
    // pushes pan bounds and zoom limits here instead of this rig guessing.
    public void Configure(float halfX, float halfZ, float minZoom, float maxZoom)
    {
        focusHalfX = halfX;
        focusHalfZ = halfZ;
        zoomMin = minZoom;
        zoomMax = maxZoom;
        if (cam == null) cam = GetComponent<Camera>();
        targetPos = transform.position;
        targetZoom = Mathf.Clamp(cam.orthographicSize, zoomMin, EffectiveZoomMax());
        RecomputeBounds();
    }

    // at ortho size S the view covers a ground rectangle of half-width
    // S * aspect and half-depth S / sin(pitch); the configured zoomMax may
    // exceed the largest S that still fits the field, so cap it — zoomMin
    // wins if even that is too big, keeping the camera usable
    private float EffectiveZoomMax()
    {
        float sinPitch = Mathf.Max(0.1f, Mathf.Sin(transform.eulerAngles.x * Mathf.Deg2Rad));
        float fitMax = Mathf.Min(focusHalfX / Mathf.Max(0.1f, cam.aspect),
                                 focusHalfZ * sinPitch);
        return Mathf.Max(zoomMin, Mathf.Min(zoomMax, fitMax));
    }

    private void RecomputeBounds()
    {
        // focus = cameraPos + forward * (height / sin(pitch)); its ground
        // offset from the camera is height / tan(pitch) along +Z (yaw 0).
        // the focus may only travel until the view edge reaches the field
        // edge, so its room shrinks as the visible rectangle grows with zoom
        float pitch = transform.eulerAngles.x * Mathf.Deg2Rad;
        float zOffset = transform.position.y / Mathf.Max(0.1f, Mathf.Tan(pitch));
        float limX = Mathf.Max(0f, focusHalfX - targetZoom * cam.aspect);
        float limZ = Mathf.Max(0f, focusHalfZ - targetZoom / Mathf.Max(0.1f, Mathf.Sin(pitch)));
        minX = -limX; maxX = limX;
        minZ = -limZ - zOffset; maxZ = limZ - zOffset;
        targetPos = ClampPos(targetPos);
    }

    // ---------------- input feed (called by PlayerCommander) ----------------

    public void BeginPan()
    {
        IsPanning = true;
        glideVelocity = Vector3.zero;
        inputVelocity = Vector3.zero;
    }

    public void PanBy(Vector3 worldDelta)
    {
        worldDelta.y = 0f;
        targetPos = ClampPos(targetPos + worldDelta);
        if (Time.deltaTime > 0f)
        {
            Vector3 v = worldDelta / Time.deltaTime;
            inputVelocity = Vector3.Lerp(inputVelocity, v, 0.35f);
        }
    }

    public void EndPan()
    {
        IsPanning = false;
        glideVelocity = Vector3.ClampMagnitude(inputVelocity, maxGlideSpeed);
    }

    // pinch interrupted the pan, or the gesture became something else: no glide
    public void CancelPan()
    {
        IsPanning = false;
        glideVelocity = Vector3.zero;
        inputVelocity = Vector3.zero;
    }

    public void ZoomBy(float factor)
    {
        targetZoom = Mathf.Clamp(targetZoom * factor, zoomMin, EffectiveZoomMax());
        RecomputeBounds();
    }

    // ---------------- motion ----------------

    private void LateUpdate()
    {
        // aspect shifts on rotation/resize and changes both the zoom cap and
        // how much ground the view covers
        if (!Mathf.Approximately(cam.aspect, lastAspect))
        {
            lastAspect = cam.aspect;
            targetZoom = Mathf.Clamp(targetZoom, zoomMin, EffectiveZoomMax());
            RecomputeBounds();
        }

        if (!IsPanning && glideVelocity.sqrMagnitude > 0.04f)
        {
            targetPos = ClampPos(targetPos + glideVelocity * Time.deltaTime);
            glideVelocity *= Mathf.Exp(-glideDamping * Time.deltaTime);
            if (glideVelocity.sqrMagnitude <= 0.04f) glideVelocity = Vector3.zero;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPos,
                                                ref smoothVel, panSmoothTime);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom,
                                                ref zoomVel, zoomSmoothTime);
    }

    private Vector3 ClampPos(Vector3 p)
    {
        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.z = Mathf.Clamp(p.z, minZ, maxZ);
        return p;
    }
}
