using UnityEngine;

// One arrow per formation, shown while selected: sits just ahead of the front
// rank and points along the formation's authoritative facing (AnchorForward —
// the same value directional damage and slot orientation read).
public class FormationArrow : MonoBehaviour
{
    private Formation f;
    private Transform arrow;

    private void Start()
    {
        f = GetComponent<Formation>();
        arrow = BattleVisuals.CreateArrow("FacingArrow");
        arrow.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (f == null || arrow == null) return;
        // Broken ranks have no meaningful formation facing — no arrow at all.
        bool show = f.IsSelected && f.soldiers.Count > 0 &&
                    f.State != FormationState.BrokenRanks;
        if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        if (!show) return;

        Vector3 heading = f.AnchorForward;
        arrow.position = f.AnchorPos + heading * (f.BoundingRadius * 0.55f + 1.0f)
                         + Vector3.up * 0.1f;
        arrow.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
    }
}
