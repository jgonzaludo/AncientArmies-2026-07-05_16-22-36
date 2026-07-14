using UnityEngine;

// Individual arrow: flies a ballistic arc that tracks its target, so a shot
// validated at fire time always lands for its full damage regardless of range
// or target movement — no hidden dice. Only the target dying mid-flight
// cancels the hit (the arrow falls where they fell).
public class Projectile : MonoBehaviour
{
    private Vector3 start;
    private Vector3 impact;
    private Soldier target;
    private float damage;
    private float flightTime;
    private float arcHeight;
    private float t;

    private static Material arrowMat;
    // Shared arrow visual (presentation only). Loaded once; null => sphere fallback.
    private static GameObject arrowVisual;
    private static bool arrowVisualLoaded;
    // Measured from the imported PROP_Archer_Arrow mesh: the head is the
    // zero-radius tip at mesh-local -Y (fletching at +Y), and the FBX importer
    // bakes Euler(270,0,0) on the prefab root. This same rotation maps the
    // head onto +Z, so the visual child aligns with the root's flight tangent.
    private static readonly Quaternion ArrowAxisCorrection = Quaternion.Euler(270f, 0f, 0f);

    public static void Spawn(Vector3 from, Soldier target, float damage, float speed)
    {
        if (target == null) return;

        if (!arrowVisualLoaded)
        {
            arrowVisualLoaded = true;
            arrowVisual = Resources.Load<GameObject>("PROP_Archer_Arrow");
        }
        // Bare root carries flight + rotation; the mesh sits on a child so the
        // one-time axis correction never fights the per-frame tangent LookRotation.
        GameObject go = new GameObject("Arrow");
        GameObject visual;
        if (arrowVisual != null)
        {
            visual = Object.Instantiate(arrowVisual, go.transform);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * 0.18f;
            if (arrowMat == null)
                arrowMat = SoldierFactory.Unlit(new Color(0.16f, 0.11f, 0.05f));
            visual.GetComponent<Renderer>().sharedMaterial = arrowMat;
        }
        visual.name = "ArrowVisual";
        visual.transform.localPosition = Vector3.zero;
        // Mesh-axis fixup lives here and only here, applied once at spawn.
        visual.transform.localRotation = ArrowAxisCorrection;

        var p = go.AddComponent<Projectile>();
        p.start = from;
        p.target = target;
        p.damage = damage;

        Vector3 aim = target.transform.position;
        float dist = (aim - from).magnitude;
        p.flightTime = Mathf.Clamp(dist / Mathf.Max(speed, 1f), 0.35f, 2.4f);
        p.impact = aim;   // tracked onto the live target every frame in Update
        p.impact.y = 0f;
        p.arcHeight = Mathf.Clamp(dist * 0.15f, 1.2f, 7f);
        go.transform.position = from;

        // Analytic launch tangent: d/dt of the flight path at t = 0. The lerp term
        // contributes (impact + 0.8 up - start) / flightTime; the parabola contributes
        // arcHeight * 4 / flightTime upward. flightTime scales both equally, so it
        // drops out of the direction. Frame deltas are zero at spawn, so orient here.
        Vector3 launchDir = (p.impact + Vector3.up * 0.8f - from) + Vector3.up * (p.arcHeight * 4f);
        if (launchDir.sqrMagnitude > 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(launchDir.normalized, Vector3.up);
    }

    private void Update()
    {
        t += Time.deltaTime / flightTime;
        // Track the live target so the arc bends onto them: the half-flight-time
        // lead guess used before let advancing melee outrun the fixed impact
        // point and eat silent misses at long range, while stationary archers
        // took every hit. A dead target freezes the impact where they fell.
        bool alive = target != null && target.Alive;
        if (alive)
        {
            impact = target.transform.position;
            impact.y = 0f;
        }
        if (t >= 1f)
        {
            bool battleActive = BattleSetup.Instance != null &&
                                BattleSetup.Instance.Phase == BattlePhase.Active;
            if (battleActive && alive)
                target.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        Vector3 pos = Vector3.Lerp(start, impact + Vector3.up * 0.8f, t);
        pos.y += arcHeight * 4f * t * (1f - t);
        // orient the root (+Z) along the flight tangent; mesh-axis fixup lives on the visual child
        Vector3 dir = pos - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.position = pos;
    }
}
