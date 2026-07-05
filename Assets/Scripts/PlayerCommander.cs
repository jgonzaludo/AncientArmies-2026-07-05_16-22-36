using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// Reads mouse input (new Input System) and turns it into formation-level orders:
// left click selects a friendly formation (with a little click forgiveness near
// its soldiers), right click issues a move or an attack order to the selected
// formation. Placed once on the GameManager object.
public class PlayerCommander : MonoBehaviour
{
    public Formation Selected { get; private set; }

    private const float SelectForgivenessRadius = 2.5f;
    private const float RaycastDistance = 300f;

    private Camera cam;
    private static Material moveMarkerMat;

    // ---------------- lifecycle ----------------

    private void Start()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if (cam == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 screenPos = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame) HandleLeftClick(screenPos);
        if (mouse.rightButton.wasPressedThisFrame) HandleRightClick(screenPos);
    }

    // ---------------- click handling ----------------

    private void HandleLeftClick(Vector2 screenPos)
    {
        if (RaycastSoldier(screenPos, out Soldier hit) && hit.team == Team.Blue)
        {
            Select(hit.formation);
            return;
        }

        Formation forgiven = null;
        if (RaycastGround(screenPos, out Vector3 point))
            forgiven = FindNearestBlueFormation(point, SelectForgivenessRadius);

        Select(forgiven);
    }

    private void HandleRightClick(Vector2 screenPos)
    {
        if (Selected == null || Selected.soldiers.Count == 0) return;

        if (RaycastSoldier(screenPos, out Soldier hit) && hit.team == Team.Red)
        {
            Selected.IssueAttack(hit.formation);
            return;
        }

        if (RaycastGround(screenPos, out Vector3 point))
        {
            Selected.IssueMove(point);
            SpawnMoveMarker(point);
        }
    }

    // ---------------- selection ----------------

    public void Select(Formation f)
    {
        if (f == Selected) return;
        if (Selected != null) Selected.SetSelected(false);
        if (f != null) f.SetSelected(true);
        Selected = f;
    }

    private Formation FindNearestBlueFormation(Vector3 point, float maxDist)
    {
        if (BattleSetup.Instance == null) return null;

        var blues = BattleSetup.Instance.GetSoldiers(Team.Blue);
        float bestD2 = maxDist * maxDist;
        Soldier nearest = null;
        foreach (var s in blues)
        {
            if (!s.Alive) continue;
            float d2 = (s.transform.position - point).sqrMagnitude;
            if (d2 <= bestD2)
            {
                bestD2 = d2;
                nearest = s;
            }
        }
        return nearest != null ? nearest.formation : null;
    }

    // ---------------- raycasting ----------------

    private bool RaycastSoldier(Vector2 screenPos, out Soldier soldier)
    {
        soldier = null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, RaycastDistance);

        float bestDist = float.MaxValue;
        foreach (var hit in hits)
        {
            Soldier s = hit.collider.GetComponentInParent<Soldier>();
            if (s == null || !s.Alive) continue;
            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                soldier = s;
            }
        }
        return soldier != null;
    }

    private bool RaycastGround(Vector2 screenPos, out Vector3 point)
    {
        point = Vector3.zero;
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float dist))
        {
            point = ray.GetPoint(dist);
            return true;
        }
        return false;
    }

    // ---------------- move marker ----------------

    private void SpawnMoveMarker(Vector3 point)
    {
        if (moveMarkerMat == null) moveMarkerMat = SoldierFactory.Unlit(new Color(0.3f, 1f, 0.4f));

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "MoveMarker";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.position = new Vector3(point.x, 0.05f, point.z);
        marker.transform.localScale = new Vector3(1.4f, 0.03f, 1.4f);
        marker.GetComponent<Renderer>().sharedMaterial = moveMarkerMat;

        StartCoroutine(ShrinkAndDestroy(marker.transform));
    }

    private IEnumerator ShrinkAndDestroy(Transform t)
    {
        Vector3 startScale = t.localScale;
        Vector3 endScale = new Vector3(0.2f, startScale.y, 0.2f);
        const float duration = 0.6f;

        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            if (t == null) yield break;
            t.localScale = Vector3.Lerp(startScale, endScale, time / duration);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }
}
