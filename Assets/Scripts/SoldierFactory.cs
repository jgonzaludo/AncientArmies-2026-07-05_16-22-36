using UnityEngine;

// Builds placeholder soldiers entirely from primitives: capsule body, cube weapon as a
// facing indicator, and a selection disc. No prefab assets needed for V0.
public static class SoldierFactory
{
    private static Material blueMat, redMat, blueArcherMat, redArcherMat, weaponMat, discMat;
    private static PhysicsMaterial slipMat;

    // Roman visuals (presentation only). Loaded once; null => capsule fallback.
    private static GameObject romanMeleeVisual;
    private static bool romanVisualLoaded;
    private static GameObject romanArcherVisual;
    private static bool romanArcherLoaded;

    private static GameObject RomanMeleeVisual()
    {
        if (!romanVisualLoaded)
        {
            romanVisualLoaded = true;
            romanMeleeVisual = Resources.Load<GameObject>("VIS_Roman_Legionary_Basic");
        }
        return romanMeleeVisual;
    }

    private static GameObject RomanArcherVisual()
    {
        if (!romanArcherLoaded)
        {
            romanArcherLoaded = true;
            romanArcherVisual = Resources.Load<GameObject>("VIS_Roman_Archer_Basic");
        }
        return romanArcherVisual;
    }

    private static readonly Color BlueMelee = new Color(0.2f, 0.4f, 0.95f);
    private static readonly Color BlueArcher = new Color(0.45f, 0.7f, 1f);
    private static readonly Color RedMelee = new Color(0.9f, 0.22f, 0.18f);
    private static readonly Color RedArcher = new Color(1f, 0.6f, 0.45f);

    public static Soldier Create(Formation f, int slot, Vector3 pos)
    {
        EnsureShared();
        bool ranged = f.stats.isRanged;
        Color color = f.team == Team.Blue ? (ranged ? BlueArcher : BlueMelee)
                                          : (ranged ? RedArcher : RedMelee);
        Material mat = f.team == Team.Blue ? (ranged ? blueArcherMat : blueMat)
                                           : (ranged ? redArcherMat : redMat);

        var root = new GameObject($"{f.displayName.Replace(' ', '_')}_S{slot}");
        root.transform.position = pos;
        root.transform.rotation = f.AnchorRot;

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1f, 0f);
        col.radius = 0.35f;
        col.height = 1.8f;
        col.material = slipMat;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = 0f;

        // Soldiers use their Roman visual when available; the missing-prefab
        // case falls back to the original capsule placeholder.
        Renderer bodyR = null;
        Transform weaponT = null;
        GameObject visualPrefab = ranged ? RomanArcherVisual() : RomanMeleeVisual();
        if (visualPrefab != null)
        {
            var vis = Object.Instantiate(visualPrefab, root.transform);
            vis.name = "VisualRoot";
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            bodyR = vis.GetComponentInChildren<SkinnedMeshRenderer>();
            color = Color.white;   // Romans keep their own palette; tint = white base
        }
        else
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            float girth = ranged ? 0.55f : 0.7f;
            body.transform.localScale = new Vector3(girth, 0.85f, girth);
            bodyR = body.GetComponent<Renderer>();
            bodyR.sharedMaterial = mat;

            var weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(weapon.GetComponent<Collider>());
            weapon.name = "Weapon";
            weapon.transform.SetParent(root.transform, false);
            if (ranged)
            {
                weapon.transform.localPosition = new Vector3(0.3f, 1.15f, 0.25f);
                weapon.transform.localScale = new Vector3(0.08f, 0.85f, 0.08f);
            }
            else
            {
                weapon.transform.localPosition = new Vector3(0.3f, 1.05f, 0.45f);
                weapon.transform.localScale = new Vector3(0.14f, 0.14f, 0.7f);
            }
            weapon.GetComponent<Renderer>().sharedMaterial = weaponMat;
            weaponT = weapon.transform;
        }

        // clean white selection circle, flat on the ground beneath the soldier
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<Collider>());
        disc.name = "SelectionDisc";
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        disc.transform.localScale = new Vector3(1.05f, 0.012f, 1.05f);
        var discR = disc.GetComponent<Renderer>();
        discR.sharedMaterial = discMat;
        discR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        disc.SetActive(false);

        var s = root.AddComponent<Soldier>();
        // Roman visuals tint per material slot (faction cloth, damage, flash) in
        // RomanLegionaryVisualController; the renderer-wide tint would wipe the palette.
        if (visualPrefab != null && bodyR != null) s.suppressTint = true;
        float skill = 0.85f + ((slot * 37) % 13) / 13f * 0.3f;   // deterministic, visible variety
        s.Init(f, slot, rb, bodyR, weaponT, disc, color, skill);
        return s;
    }

    private static void EnsureShared()
    {
        if (blueMat != null) return;
        blueMat = Lit(BlueMelee);
        redMat = Lit(RedMelee);
        blueArcherMat = Lit(BlueArcher);
        redArcherMat = Lit(RedArcher);
        weaponMat = Lit(new Color(0.2f, 0.2f, 0.22f));
        discMat = BattleVisuals.TransparentUnlit(new Color(1f, 1f, 1f, 0.62f));
        slipMat = new PhysicsMaterial("SoldierSlip")
        {
            dynamicFriction = 0.05f,
            staticFriction = 0.05f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f
        };
    }

    public static Material Lit(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        return m;
    }

    public static Material Unlit(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c);
        return m;
    }
}
