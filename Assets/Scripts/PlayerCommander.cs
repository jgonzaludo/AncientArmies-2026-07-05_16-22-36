using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Mobile-first control grammar (docs/MOBILE_CONTROLS.md):
//   tap = select / inspect        drag = issue orders
//   drag on empty ground = camera pan       pinch / scroll = zoom
//   drag from a SELECTED formation = command drag (move or attack)
// Mouse input in the editor mirrors the touch model 1:1 (click = tap, etc.).
public class PlayerCommander : MonoBehaviour
{
    private enum PointerMode { Idle, Pending, CameraPan, CommandDrag, RotateDrag }

    public IReadOnlyList<Formation> Selection => selection;
    public Formation InspectedEnemy { get; private set; }
    public bool RotateMode { get; private set; }
    public bool CanEnterRotateMode =>
        selection.Count == 1 && selection[0].soldiers.Count > 0 &&
        selection[0].State == FormationState.Ordered;

    private readonly List<Formation> selection = new List<Formation>();
    private Camera cam;
    private PointerMode mode;
    private Vector2 pressScreenPos;
    private Vector3 panGrabWorld;          // battlefield point grabbed at pan start
    private Formation dragOrigin;          // selected formation a command drag started from
    private Formation dragEnemyTarget;     // enemy currently under the command drag

    private LineRenderer commandLine;
    private GameObject destMarker;
    private GameObject enemyRing;
    private Transform rotatePreviewArrow;
    private float lastPinchDist = -1f;

    private const float TapMaxPixels = 22f;
    private const float FormationTapRadius = 1.8f;   // forgiveness around soldiers
    private const float ZoomMin = 8f, ZoomMax = 30f;
    private const float FieldX = 46f, FieldZ = 27f;  // order destination clamp

    private void Start()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        PruneSelection();
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        HandleZoom();
        if (HandlePinch()) return;   // two fingers down: zoom only, no tap/drag

        if (!TryReadPointer(out Vector2 pos, out bool down, out bool held, out bool up))
            return;

        if (down && !PointerOverUI())
        {
            pressScreenPos = pos;
            if (RotateMode && selection.Count == 1)
            {
                mode = PointerMode.RotateDrag;
            }
            else
            {
                mode = PointerMode.Pending;
            }
        }

        if (mode == PointerMode.Pending && held &&
            (pos - pressScreenPos).magnitude > TapMaxPixels)
        {
            BeginDrag(pressScreenPos);
        }

        if (held)
        {
            switch (mode)
            {
                case PointerMode.CameraPan: UpdateCameraPan(pos); break;
                case PointerMode.CommandDrag: UpdateCommandDrag(pos); break;
                case PointerMode.RotateDrag: UpdateRotateDrag(pos); break;
            }
        }

        if (up)
        {
            switch (mode)
            {
                case PointerMode.Pending: HandleTap(pos); break;
                case PointerMode.CommandDrag: EndCommandDrag(pos); break;
                case PointerMode.RotateDrag: EndRotateDrag(pos); break;
            }
            mode = PointerMode.Idle;
        }
    }

    // ---------------- pointer plumbing ----------------

    private bool TryReadPointer(out Vector2 pos, out bool down, out bool held, out bool up)
    {
        var ts = Touchscreen.current;
        if (ts != null)
        {
            var t = ts.primaryTouch;
            if (t.press.isPressed || t.press.wasReleasedThisFrame || t.press.wasPressedThisFrame)
            {
                pos = t.position.ReadValue();
                down = t.press.wasPressedThisFrame;
                held = t.press.isPressed;
                up = t.press.wasReleasedThisFrame;
                return true;
            }
        }
        var m = Mouse.current;
        if (m != null)
        {
            pos = m.position.ReadValue();
            down = m.leftButton.wasPressedThisFrame;
            held = m.leftButton.isPressed;
            up = m.leftButton.wasReleasedThisFrame;
            return true;
        }
        pos = default;
        down = held = up = false;
        return false;
    }

    private static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool GroundPoint(Vector2 screenPos, out Vector3 point)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
        point = Vector3.zero;
        return false;
    }

    // Formation-level hit test: a direct soldier hit, or any formation whose soldiers
    // are within the forgiveness radius of the tapped battlefield point.
    private Formation HitFormation(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f);
        float bestDist = float.MaxValue;
        Soldier hitSoldier = null;
        foreach (var h in hits)
        {
            var s = h.collider.GetComponentInParent<Soldier>();
            if (s == null || !s.Alive) continue;
            if (h.distance < bestDist) { bestDist = h.distance; hitSoldier = s; }
        }
        if (hitSoldier != null) return hitSoldier.formation;

        if (!GroundPoint(screenPos, out Vector3 pt) || BattleSetup.Instance == null) return null;
        Formation best = null;
        float bestD = FormationTapRadius;
        foreach (var f in BattleSetup.Instance.formations)
        {
            if (f.soldiers.Count == 0) continue;
            float d = f.DistanceToNearestSoldier(pt);
            if (d < bestD) { bestD = d; best = f; }
        }
        return best;
    }

    // ---------------- taps ----------------

    private static bool BattleActive =>
        BattleSetup.Instance != null && BattleSetup.Instance.Phase == BattlePhase.Active;

    private void HandleTap(Vector2 pos)
    {
        if (!BattleActive) return;   // pre-battle / ended: camera only
        var f = HitFormation(pos);
        if (f == null)
        {
            DeselectAll();
            return;
        }
        if (f.team == Team.Blue)
        {
            ToggleSelect(f);
        }
        else
        {
            InspectedEnemy = f;   // read-only inspection; keeps any current selection
        }
    }

    public void ToggleSelect(Formation f)
    {
        InspectedEnemy = null;
        if (selection.Contains(f))
        {
            f.SetSelected(false);
            selection.Remove(f);
        }
        else
        {
            selection.Add(f);
            f.SetSelected(true);
        }
        if (RotateMode && selection.Count != 1) ExitRotateMode();
    }

    public void DeselectAll()
    {
        foreach (var f in selection)
            if (f != null) f.SetSelected(false);
        selection.Clear();
        InspectedEnemy = null;
        if (RotateMode) ExitRotateMode();
    }

    private void PruneSelection()
    {
        for (int i = selection.Count - 1; i >= 0; i--)
        {
            if (selection[i] == null || selection[i].soldiers.Count == 0)
            {
                if (selection[i] != null) selection[i].SetSelected(false);
                selection.RemoveAt(i);
            }
        }
        if (InspectedEnemy != null && InspectedEnemy.soldiers.Count == 0)
            InspectedEnemy = null;
        if (RotateMode && selection.Count != 1) ExitRotateMode();
    }

    // ---------------- drags ----------------

    private void BeginDrag(Vector2 startPos)
    {
        var f = BattleActive ? HitFormation(startPos) : null;
        if (f != null && f.team == Team.Blue && selection.Contains(f))
        {
            mode = PointerMode.CommandDrag;
            dragOrigin = f;
            dragEnemyTarget = null;
            if (commandLine == null) commandLine = BattleVisuals.CreateCommandLine();
            if (destMarker == null) destMarker = BattleVisuals.CreateGroundDisc("DestPreview", 1.6f, false);
            commandLine.gameObject.SetActive(true);
            destMarker.SetActive(true);
        }
        else
        {
            mode = PointerMode.CameraPan;
            GroundPoint(startPos, out panGrabWorld);
        }
    }

    private void UpdateCameraPan(Vector2 pos)
    {
        if (!GroundPoint(pos, out Vector3 now)) return;
        Vector3 delta = panGrabWorld - now;
        delta.y = 0f;
        Vector3 p = cam.transform.position + delta;
        p.x = Mathf.Clamp(p.x, -34f, 34f);
        p.z = Mathf.Clamp(p.z, -56f, -4f);
        cam.transform.position = p;
    }

    private void UpdateCommandDrag(Vector2 pos)
    {
        if (dragOrigin == null || dragOrigin.soldiers.Count == 0) { CancelCommandDrag(); return; }
        if (!GroundPoint(pos, out Vector3 pt)) return;

        var over = HitFormation(pos);
        dragEnemyTarget = (over != null && over.team == Team.Red) ? over : null;

        Vector3 from = dragOrigin.AnchorPos + Vector3.up * 0.15f;
        Vector3 to = dragEnemyTarget != null
            ? dragEnemyTarget.AnchorPos + Vector3.up * 0.15f
            : new Vector3(pt.x, 0.15f, pt.z);

        commandLine.SetPosition(0, from);
        commandLine.SetPosition(1, to);
        BattleVisuals.SetLineAttackStyle(commandLine, dragEnemyTarget != null);

        if (dragEnemyTarget != null)
        {
            destMarker.SetActive(false);
            if (enemyRing == null) enemyRing = BattleVisuals.CreateGroundDisc("EnemyTarget", 1f, true);
            enemyRing.SetActive(true);
            float dia = dragEnemyTarget.BoundingRadius * 2.1f;
            enemyRing.transform.localScale = new Vector3(dia, 0.02f, dia);
            enemyRing.transform.position = new Vector3(dragEnemyTarget.AnchorPos.x, 0.06f,
                                                       dragEnemyTarget.AnchorPos.z);
        }
        else
        {
            if (enemyRing != null) enemyRing.SetActive(false);
            destMarker.SetActive(true);
            destMarker.transform.position = new Vector3(pt.x, 0.06f, pt.z);
        }
    }

    private void EndCommandDrag(Vector2 pos)
    {
        var origin = dragOrigin;
        var enemy = dragEnemyTarget;
        bool hasPoint = GroundPoint(pos, out Vector3 pt);
        CancelCommandDrag();
        if (origin == null || origin.soldiers.Count == 0 || !hasPoint) return;

        // dragging back onto the origin formation cancels the command
        if (enemy == null && origin.DistanceToNearestSoldier(pt) < FormationTapRadius) return;

        if (enemy != null && enemy.soldiers.Count > 0)
        {
            foreach (var f in selection) f.IssueAttack(enemy);
            BattleVisuals.SpawnPulse(enemy.AnchorPos, true);
        }
        else
        {
            foreach (var f in selection)
            {
                Vector3 offset = f.AnchorPos - origin.AnchorPos;
                Vector3 dest = pt + offset;
                dest.x = Mathf.Clamp(dest.x, -FieldX, FieldX);
                dest.z = Mathf.Clamp(dest.z, -FieldZ, FieldZ);
                f.IssueMove(dest);
            }
            BattleVisuals.SpawnPulse(pt, false);
        }
    }

    private void CancelCommandDrag()
    {
        dragOrigin = null;
        dragEnemyTarget = null;
        if (commandLine != null) commandLine.gameObject.SetActive(false);
        if (destMarker != null) destMarker.SetActive(false);
        if (enemyRing != null) enemyRing.SetActive(false);
    }

    // ---------------- rotate mode ----------------

    public void ToggleRotateMode()
    {
        if (RotateMode) { ExitRotateMode(); return; }
        if (!CanEnterRotateMode) return;
        RotateMode = true;
        if (rotatePreviewArrow == null)
            rotatePreviewArrow = BattleVisuals.CreateArrow("RotatePreview", preview: true);
        rotatePreviewArrow.gameObject.SetActive(true);
        UpdateRotatePreview(selection[0].AnchorForward);
    }

    private void ExitRotateMode()
    {
        RotateMode = false;
        if (rotatePreviewArrow != null) rotatePreviewArrow.gameObject.SetActive(false);
    }

    private void UpdateRotateDrag(Vector2 pos)
    {
        if (!RotateMode || selection.Count != 1) return;
        if (!GroundPoint(pos, out Vector3 pt)) return;
        Vector3 dir = pt - selection[0].AnchorPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.25f) return;
        UpdateRotatePreview(dir.normalized);
    }

    private void EndRotateDrag(Vector2 pos)
    {
        if (RotateMode && selection.Count == 1 && GroundPoint(pos, out Vector3 pt))
        {
            Vector3 dir = pt - selection[0].AnchorPos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.25f) selection[0].IssueFace(dir);
        }
        ExitRotateMode();
    }

    private void UpdateRotatePreview(Vector3 dir)
    {
        if (rotatePreviewArrow == null || selection.Count != 1) return;
        var f = selection[0];
        rotatePreviewArrow.position = f.AnchorPos + dir * (f.BoundingRadius * 0.55f + 1.0f)
                                      + Vector3.up * 0.14f;
        rotatePreviewArrow.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    // ---------------- zoom ----------------

    private void HandleZoom()
    {
        var m = Mouse.current;
        if (m == null) return;
        float scroll = m.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;
        float step = Mathf.Clamp(scroll, -3f, 3f);
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (1f - step * 0.045f),
                                           ZoomMin, ZoomMax);
    }

    private bool HandlePinch()
    {
        var ts = Touchscreen.current;
        if (ts == null || ts.touches.Count < 2) { lastPinchDist = -1f; return false; }
        var t0 = ts.touches[0];
        var t1 = ts.touches[1];
        if (!t0.press.isPressed || !t1.press.isPressed) { lastPinchDist = -1f; return false; }

        float dist = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
        if (lastPinchDist > 0f && dist > 1f)
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (lastPinchDist / dist),
                                               ZoomMin, ZoomMax);
        }
        lastPinchDist = dist;
        mode = PointerMode.Idle;
        CancelCommandDrag();
        return true;
    }

    // ---------------- test hooks (drive the exact tap/drag code paths) ----------------

    public void SimulateTap(Vector2 screenPos) => HandleTap(screenPos);

    public void SimulateCommandDrag(Vector2 fromScreen, Vector2 toScreen)
    {
        pressScreenPos = fromScreen;
        BeginDrag(fromScreen);
        if (mode == PointerMode.CommandDrag)
        {
            UpdateCommandDrag(toScreen);
            EndCommandDrag(toScreen);
        }
        mode = PointerMode.Idle;
    }
}
