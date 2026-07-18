using UnityEngine;

// One arrow per formation, shown while selected: sits just ahead of the front
// rank and points along the formation's authoritative facing (AnchorForward —
// the same value directional damage and slot orientation read).
public class FormationArrow : MonoBehaviour
{
    private Formation f;
    private Transform arrow;
    private PlayerCommander commander;
    private bool arrowYellow;   // cached so the material only swaps on change

    private void Start()
    {
        f = GetComponent<Formation>();
        commander = FindAnyObjectByType<PlayerCommander>();
        arrow = BattleVisuals.CreateArrow("FacingArrow");
        arrow.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (f == null || arrow == null) return;
        // Single-arrow ownership (v1.8.2): this white arrow shows ONLY for a
        // selected, stationary, non-rotating formation. While a Move order's
        // yellow destination arrow shows, or the rotate session's yellow arrow
        // is live, they own the direction readout and this one hides — exactly
        // one arrow per formation context. Broken ranks show no arrow at all.
        bool movePreviewOwns = f.CurrentOrderType == OrderType.Move && f.HasMoveDestination;
        bool rotateOwns = commander != null && commander.IsRotating(f);
        bool show = f.IsSelected && f.soldiers.Count > 0 &&
                    f.State != FormationState.BrokenRanks &&
                    !movePreviewOwns && !rotateOwns;
        if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        if (!show) return;

        // Color flow (v1.8.2): after a committed rotation the arrow stays
        // YELLOW while the century is still physically turning/redressing
        // (Maneuver active), then settles back to white once the formation
        // actually faces the ordered direction.
        bool yellow = f.Maneuver != FormationManeuverState.None;
        if (yellow != arrowYellow)
        {
            arrowYellow = yellow;
            BattleVisuals.SetArrowPreviewStyle(arrow, yellow);
        }

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
