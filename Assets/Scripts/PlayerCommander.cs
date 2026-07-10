using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Mobile-first control grammar (docs/MOBILE_CONTROLS.md, V1.2 selection rules):
//   tap friendly = select that formation EXCLUSIVELY (any prior selection drops)
//   tap empty ground = clear all selection
//   drag from the selected formation = command drag (move or attack), then the
//   formation auto-deselects — issuing an order ends the interaction
//   drag on empty ground = camera pan       pinch / scroll = zoom
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
    private BattleCamera camRig;
    private PointerMode mode;
    private Vector2 pressScreenPos;
    private Vector2 lastPanScreen;         // previous pointer position while panning
    private Formation dragOrigin;          // selected formation a command drag started from
    private Formation dragEnemyTarget;     // enemy currently under the command drag

    private LineRenderer commandLine;
    private GameObject destMarker;
    private GameObject enemyRing;
    private Transform rotatePreviewArrow;
    private float lastPinchDist = -1f;

    private const float FieldX = 54f, FieldZ = 36f;  // order destination clamp

    // Touch slop: small finger movement after touch-down must not instantly
    // commit the gesture to a drag. ~1.5mm on a real screen, 22px fallback
    // where dpi is unavailable (editor).
    private float TouchSlopPixels => Mathf.Max(22f, Screen.dpi * 0.06f);

    // World units covered by one screen pixel at the current zoom.
    private float WorldPerPixel => cam.orthographicSize * 2f / Screen.height;

    // Tap forgiveness beyond a formation's footprint/soldiers. Zoom-aware so
    // the padding stays finger-sized on screen; never smaller than 2.5m.
    private float FormationTapPadding => Mathf.Max(2.5f, WorldPerPixel * 60f);

    // Command drags may begin this far (world units) outside the selected
    // formation's footprint and still count as commanding it. More generous
    // than tap selection: once a formation is selected, grabbing it should be
    // nearly impossible to miss.
    private float CommandGrabTolerance => Mathf.Max(4f, WorldPerPixel * 100f);

    private void Start()
    {
        cam = Camera.main;
        if (cam != null) camRig = cam.GetComponent<BattleCamera>();
    }

    private void Update()
    {
        PruneSelection();
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        if (camRig == null) camRig = cam.GetComponent<BattleCamera>();

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
            (pos - pressScreenPos).magnitude > TouchSlopPixels)
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
                case PointerMode.CameraPan: if (camRig != null) camRig.EndPan(); break;
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

    // Formation-level hit test: a direct soldier hit, or the closest formation
    // whose interaction footprint (soldiers + oriented rectangle + generous
    // padding) contains the tapped battlefield point. A formation of 40-50
    // soldiers is one big touch target — taps between soldiers, on the block's
    // edge, or slightly outside it all resolve to that formation. Overlapping
    // footprints resolve to the closest formation, never a random one.
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
        float bestD = FormationTapPadding;
        foreach (var f in BattleSetup.Instance.formations)
        {
            if (f.soldiers.Count == 0) continue;
            float d = f.InteractionDistance(pt);
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
            DeselectAll();   // tap on empty battlefield clears everything
            return;
        }
        if (f.team == Team.Blue)
        {
            SelectOnly(f);
        }
        else
        {
            InspectedEnemy = f;   // read-only inspection; keeps any current selection
        }
    }

    // V1.2 selection model: tapping a friendly formation selects it EXCLUSIVELY.
    // Any previously selected formation is dropped — selections never accumulate
    // by accident. (The selection list stays a list so a deliberate multi-select
    // mode can be added later without rearchitecting.)
    public void SelectOnly(Formation f)
    {
        InspectedEnemy = null;
        if (selection.Count == 1 && selection[0] == f) return;   // already sole selection
        foreach (var s in selection)
            if (s != null) s.SetSelected(false);
        selection.Clear();
        selection.Add(f);
        f.SetSelected(true);
        if (RotateMode) ExitRotateMode();
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
        Formation f = BattleActive ? FindCommandGrabFormation(startPos) : null;
        if (f != null)
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
            lastPanScreen = startPos;
            if (camRig != null) camRig.BeginPan();
        }
    }

    // A command drag wins when it starts on a selected formation's soldiers OR
    // anywhere within a forgiving, zoom-aware region around the selected
    // formation's footprint. Camera pan only happens when the drag clearly
    // begins on empty ground away from the selection.
    private Formation FindCommandGrabFormation(Vector2 screenPos)
    {
        var direct = HitFormation(screenPos);
        if (direct != null && direct.team == Team.Blue && selection.Contains(direct))
            return direct;

        if (selection.Count == 0 || !GroundPoint(screenPos, out Vector3 pt)) return null;
        Formation best = null;
        float bestD = CommandGrabTolerance;
        foreach (var s in selection)
        {
            if (s == null || s.soldiers.Count == 0) continue;
            float d = s.InteractionDistance(pt);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    private void UpdateCameraPan(Vector2 pos)
    {
        Vector2 screenDelta = pos - lastPanScreen;
        lastPanScreen = pos;
        if (camRig == null || screenDelta == Vector2.zero) return;
        camRig.PanBy(-ScreenDeltaToGroundDelta(screenDelta));
    }

    // Ground-plane displacement that keeps the grabbed battlefield point under
    // the moving finger, for the fixed-angle orthographic camera. Screen-space
    // math so it stays exact while the camera itself is still easing.
    private Vector3 ScreenDeltaToGroundDelta(Vector2 screenDelta)
    {
        float wpp = WorldPerPixel;
        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();
        Vector3 upGround = cam.transform.up;
        upGround.y = 0f;
        float upScale = Mathf.Max(0.2f, upGround.magnitude);   // sin(camera tilt)
        upGround /= upScale;
        return right * (screenDelta.x * wpp) + upGround * (screenDelta.y * wpp / upScale);
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
        if (enemy == null && origin.InteractionDistance(pt) < 1.2f) return;

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

        // V1.2: issuing an order ends the interaction — auto-deselect so the
        // next tap starts clean and selections never linger unnoticed
        DeselectAll();
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
        if (m == null || camRig == null) return;
        float scroll = m.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;
        float step = Mathf.Clamp(scroll, -3f, 3f);
        camRig.ZoomBy(1f - step * 0.06f);   // eased toward the target by the rig
    }

    private bool HandlePinch()
    {
        var ts = Touchscreen.current;
        if (ts == null || ts.touches.Count < 2) { lastPinchDist = -1f; return false; }
        var t0 = ts.touches[0];
        var t1 = ts.touches[1];
        if (!t0.press.isPressed || !t1.press.isPressed) { lastPinchDist = -1f; return false; }

        float dist = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
        if (lastPinchDist > 0f && dist > 1f && camRig != null)
        {
            camRig.ZoomBy(lastPinchDist / dist);
        }
        lastPinchDist = dist;
        mode = PointerMode.Idle;
        CancelCommandDrag();
        if (camRig != null) camRig.CancelPan();   // a pinch never leaves glide behind
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
