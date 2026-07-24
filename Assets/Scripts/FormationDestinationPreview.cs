using UnityEngine;
using UnityEngine.Rendering;

// Pooled destination preview, one per formation: while the formation is
// selected with a pending Move order — or while PlayerCommander feeds a live
// drag CANDIDATE pose — every planned destination slot is shown as a small
// ground circle plus one facing arrow at the destination pose.
// Circles are one combined world-space mesh — a triangle-fan disc per slot —
// on an ordinary MeshRenderer with one shared transparent material, the same
// non-instanced runtime-material path the arrows and command discs use.
// Instancing is deliberately avoided: runtime-generated materials have no
// serialized asset referencing the INSTANCING_ON shader variant, so device
// builds strip it and instanced draws render only in the editor. The arrow is
// a single pooled Transform from BattleVisuals, toggled with the preview.
public class FormationDestinationPreview : MonoBehaviour
{
    private const int MaxCircles = 80;            // hard cap on rendered slot circles
    private const float CircleRadius = 0.35f;
    private const int CircleSegments = 16;
    private const float CircleY = 0.05f;          // just above the ground plane
    private const float ArrowY = 0.1f;            // matches FormationArrow's lift
    private const float RefreshInterval = 0.25f;  // slow rebuild (casualties shrink SlotCount)
    private const float ChangeEpsilonSq = 0.0001f; // destination drag detection (1 cm)
    private const float CircleAlpha = 0.4f;

    private const int VertsPerCircle = CircleSegments + 1;   // fan center + rim
    private const int IndicesPerCircle = CircleSegments * 3;

    // shared across all formations: one transparent material
    private static Material circleMat;

    private Formation f;
    private Transform arrow;
    private PlayerCommander commander;

    // Combined circle mesh with world-space vertices. Its GameObject lives at
    // the scene root at identity — parenting it to the formation would drag
    // the world-space discs along with the moving formation transform.
    private GameObject circlesGo;
    private Mesh circlesMesh;

    // preallocated at max size; rebuilt on order change / drag / interval
    private readonly Vector3[] vertices = new Vector3[MaxCircles * VertsPerCircle];
    private readonly int[] triangles = new int[MaxCircles * IndicesPerCircle];
    private readonly Vector3[] discOffsets = new Vector3[VertsPerCircle];

    private Vector3 cachedDest;
    private Vector3 cachedFacing;
    private OrderType cachedOrder = OrderType.None;
    private int cachedSlotCount = -1;
    private bool cachedCandidate;
    private float refreshTimer;

    // Candidate pose: fed every frame by PlayerCommander while a move drag is
    // in progress, before any order exists. While set, it overrides the
    // selection/order gates — the player must see the exact grid the release
    // will issue, even though the formation has no Move order yet.
    private bool hasCandidate;
    private Vector3 candidateDest;
    private Vector3 candidateFacing;

    public void SetCandidate(Vector3 dest, Vector3 facing)
    {
        hasCandidate = true;
        candidateDest = dest;
        candidateFacing = facing;
    }

    public void ClearCandidate() { hasCandidate = false; }

    private static Material CircleMat
    {
        get
        {
            if (circleMat != null) return circleMat;
            circleMat = BattleVisuals.TransparentUnlit(new Color(1f, 1f, 1f, CircleAlpha));
            return circleMat;
        }
    }

    private void Start()
    {
        f = GetComponent<Formation>();
        arrow = BattleVisuals.CreateArrow("DestPreviewArrow", preview: true);
        arrow.gameObject.SetActive(false);

        // One flat disc's local layout (fan center + rim), stamped at each
        // slot position on rebuild.
        discOffsets[0] = Vector3.zero;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = i / (float)CircleSegments * Mathf.PI * 2f;
            discOffsets[i + 1] = new Vector3(Mathf.Cos(a) * CircleRadius, 0f,
                                             Mathf.Sin(a) * CircleRadius);
        }
        // Fan indices never depend on slot positions, so the full pattern is
        // written once; rebuilds only vary how much of it is used.
        for (int c = 0; c < MaxCircles; c++)
        {
            int v = c * VertsPerCircle;
            int t = c * IndicesPerCircle;
            for (int i = 0; i < CircleSegments; i++)
            {
                triangles[t + i * 3] = v;
                triangles[t + i * 3 + 1] = v + (i + 1) % CircleSegments + 1;   // wraps to close the fan
                triangles[t + i * 3 + 2] = v + i + 1;
            }
        }

        circlesMesh = new Mesh { name = "DestSlotCircles" };
        circlesMesh.MarkDynamic();
        circlesGo = new GameObject("DestSlotCircles");
        circlesGo.AddComponent<MeshFilter>().sharedMesh = circlesMesh;
        var mr = circlesGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CircleMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        circlesGo.SetActive(false);
    }

    private void LateUpdate()
    {
        if (f == null || arrow == null) return;
        if (commander == null) commander = FindAnyObjectByType<PlayerCommander>();
        bool candidate = hasCandidate && f.soldiers.Count > 0;
        bool show = candidate ||
                    (f.IsSelected && f.CurrentOrderType == OrderType.Move &&
                     f.HasMoveDestination && f.soldiers.Count > 0);
        // Single-arrow ownership: while the rotate session edits this
        // formation, its yellow arrow IS the direction readout — hide this
        // one. The slot circles stay and rotate live under the drag.
        bool arrowShown = show && !(commander != null && commander.IsRotating(f));
        if (arrow.gameObject.activeSelf != arrowShown) arrow.gameObject.SetActive(arrowShown);
        if (circlesGo.activeSelf != show) circlesGo.SetActive(show);
        if (!show)
        {
            cachedOrder = OrderType.None;   // force a rebuild when the preview returns
            return;
        }

        Vector3 dest = candidate ? candidateDest : f.DestinationPosition;
        Vector3 facing = candidate ? candidateFacing : f.DestinationFacing;

        // Rebuild the mesh when the plan changes (new order, destination drag,
        // facing drag in rotation-edit mode, candidate motion, mode switch,
        // headcount change) or on the slow interval; otherwise the renderer
        // keeps drawing the cached mesh.
        refreshTimer -= Time.deltaTime;
        bool dirty = cachedOrder != OrderType.Move ||
                     cachedCandidate != candidate ||
                     cachedSlotCount != f.SlotCount ||
                     (dest - cachedDest).sqrMagnitude > ChangeEpsilonSq ||
                     (facing - cachedFacing).sqrMagnitude > ChangeEpsilonSq ||
                     refreshTimer <= 0f;
        if (dirty) RebuildMesh(dest, facing, candidate);
    }

    private void RebuildMesh(Vector3 dest, Vector3 facing, bool candidate)
    {
        refreshTimer = RefreshInterval;
        cachedOrder = OrderType.Move;
        cachedCandidate = candidate;
        cachedSlotCount = f.SlotCount;
        cachedDest = dest;
        cachedFacing = facing;

        int circleCount = Mathf.Min(f.SlotCount, MaxCircles);
        for (int c = 0; c < circleCount; c++)
        {
            Vector3 p = f.GetSlotWorldPosAt(dest, facing, c);
            p.y = CircleY;
            int v = c * VertsPerCircle;
            for (int i = 0; i < VertsPerCircle; i++)
                vertices[v + i] = p + discOffsets[i];
        }
        // Clear before shrinking so stale indices never reference removed
        // vertices. No normals: the unlit shader ignores them.
        circlesMesh.Clear();
        circlesMesh.SetVertices(vertices, 0, circleCount * VertsPerCircle);
        circlesMesh.SetTriangles(triangles, 0, circleCount * IndicesPerCircle, 0, false);
        circlesMesh.RecalculateBounds();

        // Arrow just ahead of the destination front rank, mirroring the live
        // FormationArrow offset with sqrt(SlotCount)*0.7 as the bounding radius.
        Vector3 arrowFacing = facing.sqrMagnitude > 0.0001f
            ? facing.normalized : Vector3.forward;
        float boundingRadius = Mathf.Sqrt(Mathf.Max(1, f.SlotCount)) * 0.7f;
        arrow.position = dest + arrowFacing * (boundingRadius * 0.55f + 1.0f)
                         + Vector3.up * ArrowY;
        arrow.rotation = Quaternion.LookRotation(arrowFacing, Vector3.up);
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
        if (circlesGo != null) Destroy(circlesGo);
        if (circlesMesh != null) Destroy(circlesMesh);
    }
}
