using UnityEngine;

// Individual arrow: flies a fixed ballistic arc to where the target was heading at launch.
// If the target moved away or died, the shot misses — no hidden dice.
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

    public static void Spawn(Vector3 from, Soldier target, float damage, float speed)
    {
        if (target == null) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Arrow";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 0.18f;
        if (arrowMat == null)
            arrowMat = SoldierFactory.Unlit(new Color(0.16f, 0.11f, 0.05f));
        go.GetComponent<Renderer>().sharedMaterial = arrowMat;

        var p = go.AddComponent<Projectile>();
        p.start = from;
        p.target = target;
        p.damage = damage;

        Vector3 aim = target.transform.position;
        float dist = (aim - from).magnitude;
        p.flightTime = Mathf.Clamp(dist / Mathf.Max(speed, 1f), 0.35f, 1.6f);
        p.impact = aim + target.Velocity * (p.flightTime * 0.5f);
        p.impact.y = 0f;
        p.arcHeight = Mathf.Clamp(dist * 0.22f, 0.6f, 3.5f);
        go.transform.position = from;
    }

    private void Update()
    {
        t += Time.deltaTime / flightTime;
        if (t >= 1f)
        {
            if (target != null && target.Alive &&
                (target.transform.position - impact).sqrMagnitude < 1.1f)
            {
                target.TakeDamage(damage);
            }
            Destroy(gameObject);
            return;
        }
        Vector3 pos = Vector3.Lerp(start, impact + Vector3.up * 0.8f, t);
        pos.y += arcHeight * 4f * t * (1f - t);
        transform.position = pos;
    }
}
