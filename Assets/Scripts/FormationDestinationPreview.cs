using UnityEngine;
using UnityEngine.Rendering;

// Pooled destination preview, one per formation: while the formation is
// selected with a pending Move order, every planned destination slot is shown
// as a small ground circle plus one facing arrow at the destination pose.
// Circles are drawn with Graphics.RenderMeshInstanced from one shared disc
// mesh and one shared transparent material — zero GameObjects, one cached
// matrix buffer per formation. The arrow is a single pooled Transform from
// BattleVisuals, toggled with the preview.
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

    // shared across all formations: one mesh, one instanced material
    private static Mesh circleMesh;
    private static Material circleMat;

    private Formation f;
    private Transform arrow;
    private PlayerCommander commander;

    // cached instance matrices — rebuilt on order change / drag / interval,
    // rendered every frame in between
    private readonly Matrix4x4[] matrices = new Matrix4x4[MaxCircles];
    private int circleCount;
    private RenderParams renderParams;

    private Vector3 cachedDest;
    private Vector3 cachedFacing;
    private OrderType cachedOrder = OrderType.None;
    private int cachedSlotCount = -1;
    private float refreshTimer;

    // Flat disc on the XZ plane, normals up, pivot at the center. Triangle fan.
    private static Mesh CircleMesh
    {
        get
        {
            if (circleMesh != null) return circleMesh;
            circleMesh = new Mesh { name = "DestSlotCircle" };
            var verts = new Vector3[CircleSegments + 1];
            var tris = new int[CircleSegments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i / (float)CircleSegments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * CircleRadius, 0f,
                                           Mathf.Sin(a) * CircleRadius);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % CircleSegments + 1;   // wraps to close the fan
                tris[i * 3 + 2] = i + 1;
            }
            circleMesh.vertices = verts;
            circleMesh.triangles = tris;
            circleMesh.RecalculateNormals();
            circleMesh.RecalculateBounds();
            return circleMesh;
        }
    }

    private static Material CircleMat
    {
        get
        {
            if (circleMat != null) return circleMat;
            circleMat = BattleVisuals.TransparentUnlit(new Color(1f, 1f, 1f, CircleAlpha));
            circleMat.enableInstancing = true;   // required by RenderMeshInstanced
            return circleMat;
        }
    }

    private void Start()
    {
        f = GetComponent<Formation>();
        arrow = BattleVisuals.CreateArrow("DestPreviewArrow", preview: true);
        arrow.gameObject.SetActive(false);
        renderParams = new RenderParams(CircleMat)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };
    }

    private void LateUpdate()
    {
        if (f == null || arrow == null) return;
        if (commander == null) commander = FindAnyObjectByType<PlayerCommander>();
        bool show = f.IsSelected && f.CurrentOrderType == OrderType.Move &&
                    f.HasMoveDestination && f.soldiers.Count > 0;
        // Single-arrow ownership (v1.8.2): while the rotate session edits this
        // formation, its yellow arrow IS the direction readout — hide this
        // one. The slot circles stay and rotate live under the drag.
        bool arrowShown = show && !(commander != null && commander.IsRotating(f));
        if (arrow.gameObject.activeSelf != arrowShown) arrow.gameObject.SetActive(arrowShown);
        if (!show)
        {
            cachedOrder = OrderType.None;   // force a rebuild when the preview returns
            return;
        }

        // Rebuild matrices when the plan changes (new order, destination drag,
        // facing drag in rotation-edit mode, headcount change) or on the slow
        // interval; otherwise just re-render the cached buffer.
        refreshTimer -= Time.deltaTime;
        bool dirty = cachedOrder != OrderType.Move ||
                     cachedSlotCount != f.SlotCount ||
                     (f.DestinationPosition - cachedDest).sqrMagnitude > ChangeEpsilonSq ||
                     (f.DestinationFacing - cachedFacing).sqrMagnitude > ChangeEpsilonSq ||
                     refreshTimer <= 0f;
        if (dirty) RebuildMatrices();

        if (circleCount > 0)
            Graphics.RenderMeshInstanced(renderParams, CircleMesh, 0, matrices, circleCount);
    }

    private void RebuildMatrices()
    {
        refreshTimer = RefreshInterval;
        cachedOrder = OrderType.Move;
        cachedSlotCount = f.SlotCount;
        cachedDest = f.DestinationPosition;
        cachedFacing = f.DestinationFacing;

        circleCount = Mathf.Min(f.SlotCount, MaxCircles);
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < circleCount; i++)
        {
            Vector3 p = f.GetPlannedSlotWorldPos(i);
            p.y = CircleY;
            matrices[i] = Matrix4x4.Translate(p);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        if (circleCount > 0)
        {
            // explicit culling bounds around the planned block (RenderMeshInstanced
            // does no per-instance culling and defaults to zero-size bounds)
            var b = new Bounds();
            b.SetMinMax(min, max);
            b.Expand(CircleRadius * 2f + 1f);
            renderParams.worldBounds = b;
        }

        // Arrow just ahead of the destination front rank, mirroring the live
        // FormationArrow offset with sqrt(SlotCount)*0.7 as the bounding radius.
        Vector3 facing = cachedFacing.sqrMagnitude > 0.0001f
            ? cachedFacing.normalized : Vector3.forward;
        float boundingRadius = Mathf.Sqrt(Mathf.Max(1, f.SlotCount)) * 0.7f;
        arrow.position = cachedDest + facing * (boundingRadius * 0.55f + 1.0f)
                         + Vector3.up * ArrowY;
        arrow.rotation = Quaternion.LookRotation(facing, Vector3.up);
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
    }
}
