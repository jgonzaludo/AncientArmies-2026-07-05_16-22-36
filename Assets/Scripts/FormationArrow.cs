using UnityEngine;

// One arrow per formation, shown while selected: sits just ahead of the front rank
// and points along the formation's facing, or its travel heading when it has orders.
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
        bool show = f.IsSelected && f.soldiers.Count > 0;
        if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        if (!show) return;

        Vector3 heading = f.CurrentHeading;
        arrow.position = f.AnchorPos + heading * (f.BoundingRadius * 0.55f + 1.0f)
                         + Vector3.up * 0.1f;
        arrow.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
    }
}
