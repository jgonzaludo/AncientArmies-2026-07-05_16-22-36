using System.Collections.Generic;
using UnityEngine;

// Far-zoom impostor LOD for one formation. Below a soldier-screen-height
// threshold the century's fully animated soldiers (skinned mesh, animator,
// shadow caster) go dormant and the whole formation renders as ONE dynamic
// mesh of team-colored billboard quads — sixteen draw calls for the entire
// battle instead of ~1,280 animated characters. Purely presentational:
// positions are always simulated, so the markers still march, scatter, and
// pack. A nearer tier drops soldier shadow casting alone (soldier shadows
// roughly double the drawn army). Both tiers read the banner manager's
// 1080p-reference soldier pixel height with dual-threshold hysteresis, and
// the impostor swaps are staggered a few frames apart per formation so the
// sixteen centuries never exchange 1,280 soldiers' visuals in one frame.
public class FormationImposterRenderer : MonoBehaviour
{
    [Header("Tier thresholds (soldier height in 1080p-reference pixels)")]
    [Tooltip("Enter impostor mode at or below this soldier pixel height (aligned with the banner's Expanded band)")]
    [SerializeField] private float impostorEnterPixels = 34f;
    [Tooltip("Leave impostor mode above this (hysteresis)")]
    [SerializeField] private float impostorExitPixels = 40f;
    [Tooltip("Soldier shadow casting turns off below this")]
    [SerializeField] private float shadowsOffPixels = 50f;
    [Tooltip("Soldier shadow casting returns at or above this (hysteresis)")]
    [SerializeField] private float shadowsOnPixels = 56f;

    private const float QuadWidth = 0.95f;
    private const float QuadHeight = 1.75f;
    private const float FlashSeconds = 0.09f;
    private const float DyingFallSeconds = 0.3f;   // vertical -> flat on the ground
    private const float DyingGoneSeconds = 0.9f;   // then shrink to nothing
    private const float StaggerStepSeconds = 0.04f;
    // living + dying quads can never exceed TotalSpawned; headroom is safety
    private const int QuadHeadroom = 8;

    // Quad materials (team base, brightened selected variant, hit flash) —
    // static so every formation shares the same handful of materials and the
    // sixteen impostor meshes stay at sixteen distinct material sets total.
    private static Material blueMeleeMat, blueArcherMat, redMeleeMat, redArcherMat;
    private static Material blueMeleeSel, blueArcherSel, redMeleeSel, redArcherSel;
    private static Material flashMat;

    private Formation formation;
    private Camera cam;

    private bool impostorActive;
    private bool swapArmed;
    private float swapTime;
    private bool shadowsCast = true;

    private GameObject meshGO;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material baseMat, selMat;
    private readonly Material[] matPair = new Material[2];
    private bool selectedApplied;

    private List<Vector3> verts;
    private List<int> trisBase, trisFlash;   // submesh 0 normal, submesh 1 flashing
    private int maxVerts;

    private struct DyingQuad { public Vector3 pos; public float start; }
    private readonly List<DyingQuad> dying = new List<DyingQuad>(16);
    private readonly Dictionary<Soldier, float> flashUntil = new Dictionary<Soldier, float>(32);
    // OnHurt/OnDeath are parameterless events, so each subscription closes
    // over its soldier; the delegates are kept for exact unsubscription.
    private readonly Dictionary<Soldier, System.Action> hurtHandlers =
        new Dictionary<Soldier, System.Action>(96);
    private readonly Dictionary<Soldier, System.Action> deathHandlers =
        new Dictionary<Soldier, System.Action>(96);
    private static readonly List<Soldier> unsubScratch = new List<Soldier>(96);

    private void Awake()
    {
        formation = GetComponent<Formation>();
    }

    private void OnDestroy()
    {
        UnsubscribeAll();
        if (mesh != null) Destroy(mesh);
        if (meshGO != null) Destroy(meshGO);
    }

    private void LateUpdate()
    {
        if (formation == null) return;
        var mgr = FormationBannerManager.Instance;
        if (mgr == null) return;              // no zoom signal: stay full-detail
        float px = mgr.SoldierScreenPixels;
        if (px <= 0f) return;                 // manager hasn't measured yet

        UpdateShadowTier(px);
        UpdateImpostorTier(px);
        if (impostorActive) RebuildMesh();
    }

    // ---------------- tier transitions ----------------

    private void UpdateShadowTier(float px)
    {
        bool want = shadowsCast;
        if (px < shadowsOffPixels) want = false;
        else if (px >= shadowsOnPixels) want = true;
        if (want == shadowsCast) return;
        shadowsCast = want;
        var soldiers = formation.soldiers;
        for (int i = 0; i < soldiers.Count; i++) soldiers[i].SetShadowCasting(want);
    }

    // Dual-threshold hysteresis plus a small per-formation delay before the
    // swap actually runs: sixteen formations crossing one camera threshold
    // would otherwise toggle 1,280 soldiers' hierarchies in a single frame.
    // The entity-id hash spreads the swaps over ~0.3 s; re-crossing the
    // threshold during the delay cancels the armed swap.
    private void UpdateImpostorTier(float px)
    {
        bool want = impostorActive;
        if (px <= impostorEnterPixels) want = true;
        else if (px > impostorExitPixels) want = false;

        if (want == impostorActive) { swapArmed = false; return; }
        if (!swapArmed)
        {
            swapArmed = true;
            swapTime = Time.time + (GetEntityId().GetHashCode() & 7) * StaggerStepSeconds;
            return;
        }
        if (Time.time < swapTime) return;
        swapArmed = false;
        SetImpostorActive(want);
    }

    private void SetImpostorActive(bool on)
    {
        impostorActive = on;
        var soldiers = formation.soldiers;
        if (on)
        {
            EnsureMeshObject();
            for (int i = 0; i < soldiers.Count; i++)
            {
                soldiers[i].SetImpostor(true);
                Subscribe(soldiers[i]);
            }
            meshGO.SetActive(true);
        }
        else
        {
            UnsubscribeAll();
            // living soldiers get their visuals back (their controllers'
            // OnEnable re-applies the accumulated damage tint)
            for (int i = 0; i < soldiers.Count; i++) soldiers[i].SetImpostor(false);
            flashUntil.Clear();
            dying.Clear();
            if (meshGO != null) meshGO.SetActive(false);
        }
    }

    // ---------------- soldier events ----------------

    private void Subscribe(Soldier s)
    {
        if (hurtHandlers.ContainsKey(s)) return;
        System.Action hurt = () => HandleHurt(s);
        System.Action death = () => HandleDeath(s);
        hurtHandlers.Add(s, hurt);
        deathHandlers.Add(s, death);
        s.OnHurt += hurt;
        s.OnDeath += death;
    }

    private void UnsubscribeSoldier(Soldier s)
    {
        if (hurtHandlers.TryGetValue(s, out var hurt)) { s.OnHurt -= hurt; hurtHandlers.Remove(s); }
        if (deathHandlers.TryGetValue(s, out var death)) { s.OnDeath -= death; deathHandlers.Remove(s); }
    }

    private void UnsubscribeAll()
    {
        if (hurtHandlers.Count == 0) return;
        unsubScratch.Clear();
        foreach (var kv in hurtHandlers) unsubScratch.Add(kv.Key);
        for (int i = 0; i < unsubScratch.Count; i++) UnsubscribeSoldier(unsubScratch[i]);
        unsubScratch.Clear();
    }

    private void HandleHurt(Soldier s)
    {
        flashUntil[s] = Time.time + FlashSeconds;
    }

    // The handler-table check guards a death surfacing for a soldier already
    // processed (a swap can race the death event mid-frame) so the dying
    // quad appends exactly once; the handler then retires itself.
    private void HandleDeath(Soldier s)
    {
        if (!deathHandlers.ContainsKey(s)) return;
        UnsubscribeSoldier(s);
        flashUntil.Remove(s);
        Vector3 p = s.transform.position;
        p.y = 0f;
        dying.Add(new DyingQuad { pos = p, start = Time.time });
    }

    // ---------------- mesh building ----------------

    private void EnsureMeshObject()
    {
        if (meshGO != null) return;
        EnsureMaterials();
        bool ranged = formation.stats != null && formation.stats.isRanged;
        if (formation.team == Team.Blue)
        {
            baseMat = ranged ? blueArcherMat : blueMeleeMat;
            selMat = ranged ? blueArcherSel : blueMeleeSel;
        }
        else
        {
            baseMat = ranged ? redArcherMat : redMeleeMat;
            selMat = ranged ? redArcherSel : redMeleeSel;
        }

        int quadCap = formation.TotalSpawned + QuadHeadroom;
        maxVerts = quadCap * 4;
        verts = new List<Vector3>(maxVerts);
        trisBase = new List<int>(quadCap * 6);
        trisFlash = new List<int>(quadCap * 6);

        // Scene-root object: the quads are built in world space every frame,
        // and the formation transform moves — it must never drag the mesh.
        meshGO = new GameObject($"Impostor_{formation.displayName.Replace(' ', '_')}");
        mesh = new Mesh { name = "FormationImpostorQuads" };
        mesh.MarkDynamic();
        meshGO.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = meshGO.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        matPair[1] = flashMat;
        ApplySelectionMaterial(true);
        meshGO.SetActive(false);
    }

    private static void EnsureMaterials()
    {
        if (flashMat != null) return;
        // Solid team colors matched to SoldierFactory's palette so the quads
        // read as the same army; selection brightens toward white because the
        // per-soldier discs are hidden while imposted.
        Color blueMelee = new Color(0.2f, 0.4f, 0.95f);
        Color blueArcher = new Color(0.45f, 0.7f, 1f);
        Color redMelee = new Color(0.9f, 0.22f, 0.18f);
        Color redArcher = new Color(1f, 0.6f, 0.45f);
        blueMeleeMat = SoldierFactory.Unlit(blueMelee);
        blueArcherMat = SoldierFactory.Unlit(blueArcher);
        redMeleeMat = SoldierFactory.Unlit(redMelee);
        redArcherMat = SoldierFactory.Unlit(redArcher);
        blueMeleeSel = SoldierFactory.Unlit(Color.Lerp(blueMelee, Color.white, 0.35f));
        blueArcherSel = SoldierFactory.Unlit(Color.Lerp(blueArcher, Color.white, 0.35f));
        redMeleeSel = SoldierFactory.Unlit(Color.Lerp(redMelee, Color.white, 0.35f));
        redArcherSel = SoldierFactory.Unlit(Color.Lerp(redArcher, Color.white, 0.35f));
        flashMat = SoldierFactory.Unlit(Color.white);
    }

    private void ApplySelectionMaterial(bool force)
    {
        bool sel = formation.IsSelected;
        if (!force && sel == selectedApplied) return;
        selectedApplied = sel;
        matPair[0] = sel ? selMat : baseMat;
        meshRenderer.sharedMaterials = matPair;
    }

    private void RebuildMesh()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }
        ApplySelectionMaterial(false);

        // Camera-yaw billboard for the fixed-tilt orthographic camera: quads
        // stand on world up and span the camera's horizontal right.
        Vector3 right = cam.transform.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        Vector3 away = cam.transform.forward;   // dying quads tip away from the view
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

        verts.Clear();
        trisBase.Clear();
        trisFlash.Clear();
        float now = Time.time;

        var soldiers = formation.soldiers;
        for (int i = 0; i < soldiers.Count; i++)
        {
            Soldier s = soldiers[i];
            if (s == null || !s.Alive) continue;
            if (verts.Count + 4 > maxVerts) break;
            Vector3 p = s.transform.position;
            p.y = 0f;
            bool flashing = flashUntil.TryGetValue(s, out float until) && now < until;
            AddQuad(p, right, Vector3.up, QuadWidth * 0.5f, QuadHeight,
                    flashing ? trisFlash : trisBase);
        }

        // Dying quads: fall flat over the first window, then shrink away —
        // the shrink covers the fade read without needing transparency.
        for (int i = dying.Count - 1; i >= 0; i--)
        {
            float t = now - dying[i].start;
            if (t >= DyingGoneSeconds) { dying.RemoveAt(i); continue; }
            if (verts.Count + 4 > maxVerts) continue;
            float fall = Mathf.Clamp01(t / DyingFallSeconds);
            float scale = 1f - Mathf.Clamp01((t - DyingFallSeconds) /
                                             (DyingGoneSeconds - DyingFallSeconds));
            Vector3 up = Vector3.Slerp(Vector3.up, away, fall);
            AddQuad(dying[i].pos, right, up, QuadWidth * 0.5f * scale,
                    QuadHeight * scale, trisBase);
        }

        mesh.Clear();   // keeps the dynamic flag; must precede shrinking SetVertices
        mesh.subMeshCount = 2;
        mesh.SetVertices(verts);
        mesh.SetTriangles(trisBase, 0, false);
        mesh.SetTriangles(trisFlash, 1, false);
        mesh.RecalculateBounds();
    }

    // Two triangles wound clockwise toward the camera (Unity front faces).
    private void AddQuad(Vector3 basePos, Vector3 right, Vector3 up,
                         float halfWidth, float height, List<int> tris)
    {
        int v = verts.Count;
        Vector3 r = right * halfWidth;
        Vector3 top = up * height;
        verts.Add(basePos - r);          // bottom left
        verts.Add(basePos + r);          // bottom right
        verts.Add(basePos - r + top);    // top left
        verts.Add(basePos + r + top);    // top right
        tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
        tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
    }
}
