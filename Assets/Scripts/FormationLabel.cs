using UnityEngine;

// Floating world-space label above each formation: name, state, and living soldier count.
public class FormationLabel : MonoBehaviour
{
    private const float DefeatedHoldSeconds = 2.5f;   // full visibility after the wipe
    private const float DefeatedFadeSeconds = 3f;     // then fade to nothing

    private Formation f;
    private TextMesh tm;
    private Transform label;
    private Transform cam;
    private float defeatedAt = -1f;

    private void Start()
    {
        f = GetComponent<Formation>();
        var go = new GameObject("Label");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 4.5f, 0f);
        label = go.transform;
        tm = go.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.12f;
        tm.fontSize = 64;
    }

    private void LateUpdate()
    {
        if (f == null || tm == null) return;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;

        if (f.soldiers.Count == 0)
        {
            // The formation anchor freezes on defeat (Formation.Update), so this
            // label stays at the final battlefield position and fades out — it
            // must never travel with the surviving attacker.
            if (defeatedAt < 0f) defeatedAt = Time.time;
            float alpha = 1f - Mathf.Clamp01((Time.time - defeatedAt - DefeatedHoldSeconds)
                                             / DefeatedFadeSeconds);
            if (alpha <= 0f)
            {
                if (label.gameObject.activeSelf) label.gameObject.SetActive(false);
                return;
            }
            tm.text = $"{f.displayName}\nDefeated";
            tm.color = new Color(0.6f, 0.6f, 0.6f, alpha);
        }
        else
        {
            tm.text = $"{f.displayName}\n{Formation.StateLabel(f.State)}  {f.soldiers.Count}/{f.TotalSpawned}";
            if (f.IsSelected)
                tm.color = new Color(1f, 0.9f, 0.2f);
            else
                tm.color = f.team == Team.Blue ? new Color(0.62f, 0.8f, 1f)
                                               : new Color(1f, 0.66f, 0.56f);
        }

        if (cam != null) label.rotation = cam.rotation;
    }
}
