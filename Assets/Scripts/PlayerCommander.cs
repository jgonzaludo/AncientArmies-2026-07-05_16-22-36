using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Mobile-first control grammar (docs/MOBILE_CONTROLS.md):
//   tap friendly = select that formation EXCLUSIVELY (single taps hop between
//   centuries; tapping the sole selected century deselects); DOUBLE-tapping a
//   century adds it to the existing group — keep double-tapping to build it
//   tap empty ground = clear all selection      tap enemy = inspect
//   drag from a selected formation = command drag: a move drag previews the
//   destination slot dots live and a second finger twists the final facing;
//   dragging onto an enemy shows the attack line + ring instead
//   drag on empty ground = camera pan       pinch / scroll = zoom
// Mouse input in the editor mirrors the touch model 1:1 (click = tap, etc.);
// with no second pointer, a move's facing defaults to the travel direction.
public class PlayerCommander : MonoBehaviour
{
    private enum PointerMode { Idle, Pending, CameraPan, CommandDrag, RotateDrag }

    public IReadOnlyList<Formation> Selection => selection;
    public Formation InspectedEnemy { get; private set; }
    public bool RotateMode { get; private set; }

    // Rotate is a group edit now (Phase 2G): available when every live
    // selected century is eligible (no auto-facing, not broken, not busy).
    public bool CanEnterRotateMode
    {
        get
        {
            int live = 0;
            for (int i = 0; i < selection.Count; i++)
            {
                Formation f = selection[i];
                if (f == null || f.soldiers.Count == 0) continue;
                live++;
                if (f.GetRotateBlock() != Formation.RotateBlock.None) return false;
            }
            return live >= 1;
        }
    }

    // rotate-edit session (Phase 2G): pivot, entry facing, and the rigid
    // destination arrangement captured when the mode was entered
    private Vector3 rotatePivot;
    private Vector3 rotateInitialDir;
    private Vector3 rotateEditDir;
    private bool rotateEdited;   // arrow stays white until the first real drag
    private float rotateArrowRadius = 6f;
    private readonly List<Formation> rotateMovers = new List<Formation>();
    private readonly List<Vector3> rotateDestOffsets = new List<Vector3>();
    private readonly List<Formation> rotateStationary = new List<Formation>();
    private const float RotateDeadzone = 1.5f;   // meters around the pivot: ignore unstable input

    // Arrow ownership (v1.8.2): while a formation participates in the rotate
    // session, the yellow rotate arrow is THE arrow — FormationArrow and the
    // destination-preview arrow both consult this and hide.
    public bool IsRotating(Formation f)
    {
        return RotateMode && (rotateMovers.Contains(f) || rotateStationary.Contains(f));
    }

    private readonly List<Formation> selection = new List<Formation>();
    private Camera cam;
    private BattleCamera camRig;
    private PointerMode mode;
    private Vector2 pressScreenPos;
    private Vector2 lastPanScreen;         // previous pointer position while panning
    private Formation dragOrigin;          // selected formation a command drag started from
    private Formation dragEnemyTarget;     // enemy currently under the command drag

    // Place-and-twist facing: during a move drag the SECOND finger aims the
    // group's final facing. Once past the deadzone the facing LOCKS — lifting
    // the second finger keeps it so position can still be adjusted one-handed;
    // only starting a new drag clears the lock. Unlocked moves fall back to
    // facing the direction of travel.
    private bool dragFacingLocked;
    private Vector3 dragLockedFacing = Vector3.forward;

    // candidate-preview plumbing: per-formation components resolved lazily
    // (no per-frame GetComponent), plus the previews currently holding a
    // candidate pose so cancel / attack-hover can clear exactly those
    private readonly Dictionary<Formation, FormationDestinationPreview> previewCache =
        new Dictionary<Formation, FormationDestinationPreview>();
    private readonly List<FormationDestinationPreview> fedCandidates =
        new List<FormationDestinationPreview>();

    // Double-tap group building: the FIRST tap on a century acts immediately
    // (exclusive switch — no laggy delayed selection); a second tap on the
    // SAME century inside the window upgrades it to a group add by restoring
    // the selection snapshotted before the first tap and keeping the century
    // in it. Two taps on DIFFERENT centuries are just two switches.
    private const float DoubleTapWindow = 0.35f;
    private Formation lastTapFormation;
    private float lastTapTime = -999f;
    private readonly List<Formation> preTapSelection = new List<Formation>();

    private LineRenderer commandLine;
    private GameObject enemyRing;
    private Transform rotatePreviewArrow;
    private float lastPinchDist = -1f;

    // order destination clamps read the parameterized battlefield (Phase 6)
    private static float FieldX => BattleSetup.Instance != null
        ? BattleSetup.Instance.fieldHalfX - BattleSetup.Instance.fieldEdgeMargin : 54f;
    private static float FieldZ => BattleSetup.Instance != null
        ? BattleSetup.Instance.fieldHalfZ - BattleSetup.Instance.fieldEdgeMargin : 36f;

    // 1080p-reference pixels expressed at the current resolution
    private static float RefPx(float px) => px / 1080f * Screen.height;

    // Touch slop: small finger movement after touch-down must not instantly
    // commit the gesture to a drag. ~1.5mm where dpi is trustworthy; else a
    // screen-height fraction (22px at 1080p) so the slop scales with
    // resolution instead of shrinking on dense screens.
    private float TouchSlopPixels => Mathf.Max(RefPx(22f), Screen.dpi > 0f ? Screen.dpi * 0.06f : 0f);

    // World units covered by one screen pixel at the current zoom.
    private float WorldPerPixel => cam.orthographicSize * 2f / Screen.height;

    // Tap forgiveness beyond a formation's footprint/soldiers. Zoom-aware so
    // the padding stays finger-sized on screen; never smaller than 2.5m.
    // 60 is 1080p-reference pixels: WorldPerPixel * RefPx cancels
    // Screen.height, so the world padding depends only on zoom, not device
    // resolution.
    private float FormationTapPadding => Mathf.Max(2.5f, WorldPerPixel * RefPx(60f));

    // Command drags may begin this far (world units) outside the selected
    // formation's footprint and still count as commanding it. More generous
    // than tap selection: once a formation is selected, grabbing it should be
    // nearly impossible to miss. 100 is 1080p-reference pixels; as above,
    // the world tolerance depends only on zoom, not device resolution.
    private float CommandGrabTolerance => Mathf.Max(4f, WorldPerPixel * RefPx(100f));

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
            if (RotateMode && selection.Count >= 1)
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

    // Deployment (Pre) allows full command interaction — select, move, rotate
    // — with physical marching; only ATTACK orders wait for Start Battle.
    private static bool CommandsAllowed =>
        BattleSetup.Instance != null && BattleSetup.Instance.Phase != BattlePhase.Ended;

    private static bool Deploying =>
        BattleSetup.Instance != null && BattleSetup.Instance.Phase == BattlePhase.Pre;

    private void HandleTap(Vector2 pos)
    {
        if (!CommandsAllowed) return;   // battle ended: camera only
        var f = HitFormation(pos);
        if (f == null)
        {
            DeselectAll();   // tap on empty battlefield clears everything
            return;
        }
        if (f.team == Team.Blue)
        {
            // Tap grammar: single tap SWITCHES (exclusive select; tapping the
            // sole selected century deselects it), a true double tap on one
            // century ADDS it to the group that existed before the first tap
            // — so "select A, then double-tap B, double-tap C" builds
            // {A,B,C}, while single taps just hop between centuries.
            float now = Time.unscaledTime;
            bool doubleTap = f == lastTapFormation && now - lastTapTime < DoubleTapWindow;
            if (doubleTap)
            {
                // restore the pre-first-tap selection with this century in it;
                // double-tapping an existing member is a harmless no-op
                DeselectAll();
                foreach (var p in preTapSelection)
                    if (p != null && p.soldiers.Count > 0 && !selection.Contains(p))
                        ToggleSelect(p);
                if (!selection.Contains(f)) ToggleSelect(f);
                lastTapFormation = null;   // consume: a third tap starts fresh
            }
            else
            {
                preTapSelection.Clear();
                preTapSelection.AddRange(selection);
                if (selection.Count == 1 && selection[0] == f) DeselectAll();
                else SelectOnly(f);
                lastTapFormation = f;
                lastTapTime = now;
            }
        }
        else
        {
            InspectedEnemy = f;   // read-only inspection; keeps any current selection
        }
    }

    // Selection-membership primitive behind the tap grammar; HUD and scripted
    // tests drive it directly. Selection persists through orders so
    // destination previews stay attached to moving centuries.
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
        if (RotateMode) ExitRotateMode();
    }

    // exclusive selection kept for scripted tests and future UI paths
    public void SelectOnly(Formation f)
    {
        DeselectAll();
        ToggleSelect(f);
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
        // Facing authority: losing rotate eligibility mid-preview (target
        // acquired, engagement, break ranks, defeat) cancels rotate mode —
        // auto-facing must never fight a queued manual facing.
        if (RotateMode && !CanEnterRotateMode)
            ExitRotateMode();
    }

    // ---------------- drags ----------------

    private void BeginDrag(Vector2 startPos)
    {
        Formation f = CommandsAllowed ? FindCommandGrabFormation(startPos) : null;
        if (f != null)
        {
            mode = PointerMode.CommandDrag;
            dragOrigin = f;
            dragEnemyTarget = null;
            dragFacingLocked = false;   // the facing lock is per-drag
            if (commandLine == null) commandLine = BattleVisuals.CreateCommandLine();
            // visibility is owned by UpdateCommandDrag: only ATTACK drags show
            // the line — move drags show the destination slot dots instead
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
        // no attack orders during deployment — combat starts at Start Battle
        dragEnemyTarget = (!Deploying && over != null && over.team == Team.Red) ? over : null;

        if (dragEnemyTarget != null)
        {
            // ATTACK drag: red line to the target plus a ring around it. The
            // slot-dot preview is a move-only readout — an attack has no
            // planned arrival grid to show.
            ClearCandidates();
            commandLine.gameObject.SetActive(true);
            commandLine.SetPosition(0, dragOrigin.AnchorPos + Vector3.up * 0.15f);
            commandLine.SetPosition(1, dragEnemyTarget.AnchorPos + Vector3.up * 0.15f);
            BattleVisuals.SetLineAttackStyle(commandLine, true);
            if (enemyRing == null) enemyRing = BattleVisuals.CreateGroundDisc("EnemyTarget", 1f, true);
            enemyRing.SetActive(true);
            float dia = dragEnemyTarget.BoundingRadius * 2.1f;
            enemyRing.transform.localScale = new Vector3(dia, 0.02f, dia);
            enemyRing.transform.position = new Vector3(dragEnemyTarget.AnchorPos.x, 0.06f,
                                                       dragEnemyTarget.AnchorPos.z);
            return;
        }

        // MOVE drag: no line, no ground disc — the live candidate slot dots
        // ARE the preview. Fed through the same arrangement math the release
        // will issue, so the dots never lie.
        commandLine.gameObject.SetActive(false);
        if (enemyRing != null) enemyRing.SetActive(false);
        UpdateDragFacing(pt);
        if (!ComputeGroupArrangement(pt, out Vector3 pivot, out Quaternion arrange,
                                     out Vector3 facing)) return;
        GetDestBoundsZ(out float zMin, out float zMax);
        foreach (var f in selection)
        {
            if (f == null || f.soldiers.Count == 0) continue;
            var preview = PreviewFor(f);
            if (preview == null) continue;
            preview.SetCandidate(ClampDest(pt + arrange * (f.AnchorPos - pivot), zMin, zMax),
                                 facing);
            if (!fedCandidates.Contains(preview)) fedCandidates.Add(preview);
        }
    }

    // Second-finger facing (place-and-twist): project the second touch to the
    // ground and aim the group from the candidate destination toward it.
    // Inside the deadzone the direction is unstable — keep the previous
    // facing (locked or travel-derived) rather than jitter.
    private void UpdateDragFacing(Vector3 groupDest)
    {
        var ts = Touchscreen.current;
        if (ts == null) return;   // editor mouse: no second pointer exists
        int primaryId = ts.primaryTouch.touchId.ReadValue();
        for (int i = 0; i < ts.touches.Count; i++)
        {
            var t = ts.touches[i];
            if (!t.press.isPressed || t.touchId.ReadValue() == primaryId) continue;
            if (!GroundPoint(t.position.ReadValue(), out Vector3 gp)) return;
            Vector3 dir = gp - groupDest;
            dir.y = 0f;
            if (dir.sqrMagnitude < RotateDeadzone * RotateDeadzone) return;
            dragFacingLocked = true;   // survives lifting the second finger
            dragLockedFacing = dir.normalized;
            return;
        }
    }

    // Single source of truth for the group move pose (Phase 2E math): the
    // live drag preview and the issued order both run THIS, so the dots the
    // player sees are exactly the slots the order fills. Pivot at the
    // selected anchors' center; relative offsets rigidly rotated so the whole
    // arrangement faces the group facing — the direction of travel, unless
    // the second finger has locked an explicit facing. (Selected centuries
    // with different individual facings are aligned to the common group
    // facing — documented simplification.)
    private bool ComputeGroupArrangement(Vector3 pt, out Vector3 pivot,
                                         out Quaternion arrange, out Vector3 groupFacing)
    {
        pivot = Vector3.zero;
        arrange = Quaternion.identity;
        groupFacing = Vector3.forward;
        Vector3 avgFwd = Vector3.zero;
        int live = 0;
        foreach (var f in selection)
        {
            if (f == null || f.soldiers.Count == 0) continue;
            pivot += f.AnchorPos;
            avgFwd += f.AnchorForward;
            live++;
        }
        if (live == 0) return false;
        pivot /= live;

        Vector3 travel = pt - pivot;
        travel.y = 0f;
        bool fwdOk = avgFwd.sqrMagnitude > 0.01f;
        Vector3 fwd = fwdOk ? new Vector3(avgFwd.x, 0f, avgFwd.z).normalized : Vector3.forward;
        groupFacing = dragFacingLocked ? dragLockedFacing
            : travel.sqrMagnitude > 0.04f ? travel.normalized : fwd;
        // a locked facing is stable by construction; an inferred travel
        // facing needs a meaningful drag distance before it may rotate the
        // arrangement
        if (live > 1 && fwdOk && (dragFacingLocked || travel.sqrMagnitude > 1f))
            arrange = Quaternion.FromToRotation(fwd, groupFacing);
        return true;
    }

    // destination clamps shared by preview and order; deployment destinations
    // stay inside the friendly zone (Phase 6C)
    private void GetDestBoundsZ(out float zMin, out float zMax)
    {
        zMin = -FieldZ;
        zMax = FieldZ;
        var bs = BattleSetup.Instance;
        if (Deploying && bs != null)
            zMax = -bs.fieldHalfZ + bs.deploymentZoneDepth;   // blue deploys south
    }

    private static Vector3 ClampDest(Vector3 dest, float zMin, float zMax)
    {
        dest.x = Mathf.Clamp(dest.x, -FieldX, FieldX);
        dest.z = Mathf.Clamp(dest.z, zMin, zMax);
        return dest;
    }

    private FormationDestinationPreview PreviewFor(Formation f)
    {
        if (!previewCache.TryGetValue(f, out var p) || p == null)
        {
            p = f.GetComponent<FormationDestinationPreview>();
            previewCache[f] = p;
        }
        return p;
    }

    private void ClearCandidates()
    {
        for (int i = 0; i < fedCandidates.Count; i++)
            if (fedCandidates[i] != null) fedCandidates[i].ClearCandidate();
        fedCandidates.Clear();
    }

    private void EndCommandDrag(Vector2 pos)
    {
        var origin = dragOrigin;
        var enemy = dragEnemyTarget;
        bool hasPoint = GroundPoint(pos, out Vector3 pt);
        CancelCommandDrag();   // the facing lock survives — the order below reads it
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
            // Group order snapshot: the same arrangement math that drove the
            // candidate preview all drag long — release issues what was shown.
            if (!ComputeGroupArrangement(pt, out Vector3 pivot, out Quaternion arrange,
                                         out Vector3 groupFacing)) return;
            GetDestBoundsZ(out float zMin, out float zMax);
            foreach (var f in selection)
            {
                if (f == null || f.soldiers.Count == 0) continue;
                f.IssueMove(ClampDest(pt + arrange * (f.AnchorPos - pivot), zMin, zMax));
                f.SetDestinationFacing(groupFacing);
            }
            BattleVisuals.SpawnPulse(pt, false);
        }
        // Selection persists after orders (Phase 2): destination previews
        // follow the moving centuries until the player deselects.
    }

    private void CancelCommandDrag()
    {
        dragOrigin = null;
        dragEnemyTarget = null;
        ClearCandidates();
        if (commandLine != null) commandLine.gameObject.SetActive(false);
        if (enemyRing != null) enemyRing.SetActive(false);
    }

    // ---------------- free-arrow rotate mode (Phase 2G) ----------------
    //
    // The Rotate button toggles an edit session: a freely movable facing arrow
    // anchored to the session pivot. Moving centuries edit their DESTINATION
    // facing (pivot = destination arrangement center; positions rotate rigidly
    // around it on release); stationary centuries pivot in place. Attack
    // orders never reach here — GetRotateBlock reports AutoFacing and the
    // button is greyed out.

    public void ToggleRotateMode()
    {
        if (RotateMode) { ExitRotateMode(); return; }
        if (!CanEnterRotateMode) return;

        rotateMovers.Clear();
        rotateDestOffsets.Clear();
        rotateStationary.Clear();
        Vector3 pivot = Vector3.zero;
        Vector3 initial = Vector3.zero;
        float radius = 4f;
        int movers = 0, live = 0;
        foreach (var f in selection)
        {
            if (f == null || f.soldiers.Count == 0) continue;
            live++;
            radius = Mathf.Max(radius, f.BoundingRadius);
            bool moving = f.HasMoveDestination &&
                          (f.CurrentOrderType == OrderType.Move ||
                           f.CurrentOrderType == OrderType.Withdraw);
            if (moving) { rotateMovers.Add(f); movers++; }
            else rotateStationary.Add(f);
        }
        if (live == 0) return;

        // pivot around the planned destination center when anything is moving,
        // otherwise around the current formation anchors
        if (movers > 0)
        {
            foreach (var f in rotateMovers) { pivot += f.DestinationPosition; initial += f.DestinationFacing; }
            pivot /= movers;
        }
        else
        {
            foreach (var f in rotateStationary) { pivot += f.AnchorPos; initial += f.AnchorForward; }
            pivot /= rotateStationary.Count;
        }
        rotatePivot = pivot;
        rotateInitialDir = initial.sqrMagnitude > 0.01f
            ? new Vector3(initial.x, 0f, initial.z).normalized : Vector3.forward;
        rotateEditDir = rotateInitialDir;
        // same offset as FormationArrow so the handoff is seamless (no jump)
        rotateArrowRadius = radius * 0.55f + 1.0f;
        foreach (var f in rotateMovers)
            rotateDestOffsets.Add(f.DestinationPosition - pivot);

        RotateMode = true;
        rotateEdited = false;
        if (rotatePreviewArrow == null)
            rotatePreviewArrow = BattleVisuals.CreateArrow("RotatePreview", preview: true);
        // v1.8.2 color flow: entering rotate mode changes NOTHING visually —
        // the arrow stays white at the current facing until the player
        // actually starts dragging a new direction, then it turns yellow.
        BattleVisuals.SetArrowPreviewStyle(rotatePreviewArrow, false);
        rotatePreviewArrow.gameObject.SetActive(true);
        UpdateRotatePreview(rotateEditDir);
    }

    private void ExitRotateMode()
    {
        RotateMode = false;
        rotateMovers.Clear();
        rotateDestOffsets.Clear();
        rotateStationary.Clear();
        if (rotatePreviewArrow != null) rotatePreviewArrow.gameObject.SetActive(false);
    }

    private void UpdateRotateDrag(Vector2 pos)
    {
        if (!RotateMode) return;
        if (!GroundPoint(pos, out Vector3 pt)) return;
        Vector3 dir = pt - rotatePivot;
        dir.y = 0f;
        if (dir.sqrMagnitude < RotateDeadzone * RotateDeadzone) return;   // unstable near pivot
        rotateEditDir = dir.normalized;
        if (!rotateEdited)
        {
            rotateEdited = true;   // first real edit: the arrow goes yellow
            BattleVisuals.SetArrowPreviewStyle(rotatePreviewArrow, true);
        }
        UpdateRotatePreview(rotateEditDir);
        // live preview: moving centuries' planned facing follows the arrow so
        // the destination slot previews rotate under the finger
        foreach (var f in rotateMovers) f.SetDestinationFacing(rotateEditDir);
    }

    private void EndRotateDrag(Vector2 pos)
    {
        if (RotateMode)
        {
            if (GroundPoint(pos, out Vector3 pt))
            {
                Vector3 dir = pt - rotatePivot;
                dir.y = 0f;
                if (dir.sqrMagnitude >= RotateDeadzone * RotateDeadzone)
                    rotateEditDir = dir.normalized;
            }
            // lock: rotate the destination arrangement rigidly around the
            // pivot by the edited delta; stationary centuries pivot in place
            Quaternion delta = Quaternion.FromToRotation(rotateInitialDir, rotateEditDir);
            for (int i = 0; i < rotateMovers.Count; i++)
            {
                var f = rotateMovers[i];
                if (f == null || f.soldiers.Count == 0) continue;
                Vector3 dest = rotatePivot + delta * rotateDestOffsets[i];
                dest.x = Mathf.Clamp(dest.x, -FieldX, FieldX);
                dest.z = Mathf.Clamp(dest.z, -FieldZ, FieldZ);
                f.RedirectMove(dest, rotateEditDir);
            }
            foreach (var f in rotateStationary)
            {
                if (f == null || f.soldiers.Count == 0) continue;
                f.IssueFace(rotateEditDir);
            }
        }
        ExitRotateMode();
    }

    private void UpdateRotatePreview(Vector3 dir)
    {
        if (rotatePreviewArrow == null) return;
        rotatePreviewArrow.position = rotatePivot + dir * rotateArrowRadius
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
        // an active move drag owns the second finger (place-and-twist facing)
        // — never steal it for zoom mid-order; two fingers on empty ground
        // still pinch-zoom as before
        if (mode == PointerMode.CommandDrag) return false;
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
