using System.Collections.Generic;
using UnityEngine;

// Routing (Chunk A) is appended last so existing values keep their numbers.
public enum FormationState { Ordered, Attacking, Engaged, BrokenRanks, Withdrawing, Reforming, Charging, Routing }

// Current strategic order (Phase 2). Stored, never inferred: a move order
// keeps its DestinationPosition and DestinationFacing from issue to arrival,
// so current facing, travel direction, and final facing are three values.
public enum OrderType { None, Move, Attack, Reform, BreakRanks, Withdraw, Charge }

// Rotate-command maneuvers (Patch 4): a formation turns like a rectangular
// body, not a liquid. SmallTurn/AboutFace snap facing and redress in place;
// Wheel rotates the whole slot grid rigidly around an inner front corner.
public enum FormationManeuverState { None, SmallTurn, Wheel, AboutFace, Redressing }

// Owns the high-level state machine, the anchor (position + facing) and the slot grid.
// Soldiers stay lightweight agents that blend slot attraction with local combat.
public class Formation : MonoBehaviour
{
    public Team team;
    public string displayName = "Formation";
    public UnitStats stats;
    public bool autoPilot;   // red side: strategically stationary, fights back, auto-tidies

    [Header("Layout")]
    public int columns = 6;
    public float spacing = 1.3f;

    [Header("Movement")]
    public float moveSpeed = 2.6f;
    public float rotateSpeedDeg = 70f;
    [Tooltip("How fast an engaged formation wheels its canonical facing toward the melee contact (deg/s). Directional flank/rear bonuses persist while it turns, then fade — an ill-timed Rotate can no longer leave a unit permanently rear-facing its attackers")]
    public float engagedReorientSpeedDeg = 25f;

    [Header("Defensive pivot (Phase 2)")]
    [Tooltip("Unit-type permission: rigid future types (pikes) can turn this off")]
    public bool allowDefensivePivot = true;
    [Tooltip("A flank/rear threat must persist this long before the formation starts pivoting")]
    public float defensivePivotDelay = 1.2f;
    [Tooltip("At or above this engaged fraction the formation is pinned — no automatic pivot, the tactical flank stays exposed")]
    public float heavyEngagePivotBlockFraction = 0.35f;
    [Tooltip("Pivot speed multiplier while lightly engaged")]
    public float lightEngagePivotFactor = 0.5f;

    [Header("Reform (Phase 3)")]
    [Tooltip("Reform completes when this fraction of survivors stands in the new slots")]
    public float reformCompleteFraction = 0.75f;
    [Tooltip("Reform aborts back to Broken when this fraction of reforming survivors is melee-engaged (projectile fire alone never aborts)")]
    public float reformAbortEngagedFraction = 0.12f;
    [Tooltip("Fallback: reform completes after this many seconds regardless of stragglers")]
    public float reformTimeoutSeconds = 25f;

    [Header("Rotate maneuvers (Patch 4)")]
    [Tooltip("Yaw deltas at or below this pivot in place (facing snaps, slots keep their owners)")]
    public float smallTurnMaxDeg = 20f;
    [Tooltip("Yaw deltas at or above this are an about-face: soldiers turn in place and ranks are reinterpreted via nearest-slot remap")]
    public float aboutFaceMinDeg = 135f;
    [Tooltip("Ground speed allowed for the outer flank during a wheel; sets the wheel's angular speed")]
    public float wheelOuterSpeed = 2.3f;
    [Tooltip("A wheel pauses while more than this fraction of living soldiers is mid-attack/hit (committed actions are never dragged)")]
    public float wheelPauseLockedFraction = 0.4f;
    [Tooltip("Redressing after a maneuver ends when this fraction of living soldiers is inside slot tolerance (or after the redress timeout)")]
    public float redressCompleteFraction = 0.85f;
    public float redressTimeoutSeconds = 4f;

    public FormationManeuverState Maneuver { get; private set; } = FormationManeuverState.None;

    [Header("Engagement")]
    public float personalEngageRadius = 3f;    // enemy this close => soldier is "engaged"
    public float acquireRadiusOrdered = 2.2f;  // fight-back reach while holding formation
    public float acquireRadiusEngaged = 4.5f;
    public float acquireRadiusBroken = 28f;
    public float disengageRadius = 9f;         // no enemy within this of any soldier => can reform
    public float brokenLeash = 26f;
    public float meleeChaseStopDist = 3.5f;    // anchor-to-anchor stop distance when charging

    [Header("Charge (rush -> pursuit)")]
    [Tooltip("Sprint multiplier on the anchor and every soldier while the rush is on")]
    public float chargeSpeedFactor = 1.3f;
    [Tooltip("A rush that never finds contact tips into pursuit after this long")]
    public float chargeMaxSeconds = 5f;
    [Tooltip("Seconds of impact bonus after first contact; then the rush dissolves into pursuit")]
    public float chargeImpactWindow = 2f;
    [Tooltip("Melee damage multiplier inside the impact window (stacks with the directional bonus)")]
    public float chargeImpactBonus = 1.5f;
    [Tooltip("Max anchor distance at which a charge can pick or accept a target")]
    public float chargeRange = 45f;
    [Tooltip("A pursuing pack flows to the next enemy formation this close — never across the map")]
    public float pursuitRetargetRadius = 35f;
    [Tooltip("Tighter soldier leash while pursuing: the pack stays a pack")]
    public float pursuitLeash = 18f;

    [Header("Rank replacement (ordered melee)")]
    public float promoteInterval = 0.6f;       // how often vacancies are scanned
    public float promoteVacancyDist = 1.6f;    // slot counts as open when its fighter strays this far
    public float promoteCoherenceDist = 2.2f;  // only soldiers still near their own slot advance

    [Header("Auto-close (ordered formations compress over losses)")]
    public float autoCloseInterval = 2f;       // how often structural gaps are scanned
    public int autoCloseDeficit = 2;           // casualties since last rebuild that trigger compaction

    [Header("Edge engagement (local freedom near the fight)")]
    public float edgeEngageRadius = 6f;        // unengaged soldiers this close to an enemy loosen up

    [Header("Dominant group (banner tracking + reform rally)")]
    [Tooltip("Soldiers closer than this multiple of formation spacing belong to the same local group")]
    public float clusterLinkFactor = 2.5f;
    [Tooltip("Seconds between dominant-group recomputes (the banner interpolates in between)")]
    public float clusterInterval = 0.4f;
    [Tooltip("A rival group must outnumber the current dominant group by this factor to take the banner")]
    public float clusterSwitchFactor = 1.3f;

    public FormationState State { get; private set; } = FormationState.Ordered;
    public bool HasMoveDestination => hasDestination;      // read-only, for visuals

    // ---- order model (Phase 2) ----
    public OrderType CurrentOrderType { get; private set; } = OrderType.None;
    public Vector3 DestinationPosition => destination;
    public Vector3 DestinationFacing { get; private set; } = Vector3.forward;
    public int SlotCount => slotOffsets.Length;

    // Slot position at the DESTINATION pose (planned position + planned
    // facing) — what the destination previews render.
    public Vector3 GetPlannedSlotWorldPos(int slot)
    {
        return GetSlotWorldPosAt(destination, DestinationFacing, slot);
    }

    // Same slot math at an ARBITRARY pose: the live drag preview renders
    // un-issued candidate destinations through the exact grid a released
    // order will fill, so preview and outcome cannot disagree.
    public Vector3 GetSlotWorldPosAt(Vector3 dest, Vector3 facing, int slot)
    {
        if (slot < 0 || slot >= slotOffsets.Length) return dest;
        Vector3 fwd = facing.sqrMagnitude > 0.01f ? facing : AnchorForward;
        return dest + Quaternion.LookRotation(fwd, Vector3.up) * slotOffsets[slot];
    }

    // ---- break/reform rally (Phase 3) ----
    // Frozen at the formation center the moment ranks break; the banner sits
    // here and Reform rebuilds here. Never recomputed from scattering soldiers.
    public Vector3 RallyAnchor { get; private set; }
    private Quaternion rallyFacing = Quaternion.identity;

    // ---- archer combat state (Phase 3): firing is engagement for slot
    // compaction purposes, but not melee entrapment ----
    private float lastRangedShotTime = -999f;
    public void NotifyRangedShot() { lastRangedShotTime = Time.time; }
    public bool IsFiring => stats != null && stats.isRanged &&
                            Time.time - lastRangedShotTime < 2f;

    // ---- volley fire (v1.9) ----
    // Ranged formations loose as a FORMATION: a volley window opens every
    // volleyInterval and each archer fires once inside it at its own
    // deterministic offset — Total War-style readable volleys with lulls,
    // instead of 80 independent timers drizzling arrows. Clocks are staggered
    // per formation so centuries don't sync with each other.
    [Header("Volley fire (v1.9, ranged formations only)")]
    [Tooltip("Seconds between volleys; governs archer output (keep archer attackCooldown below this)")]
    public float volleyInterval = 8f;
    [Tooltip("Seconds the loose window stays open — arrows spread across it")]
    public float volleyWindow = 1.5f;

    private float volleyClock;
    public bool VolleyOpen { get; private set; } = true;   // melee formations never gate
    public float VolleyPhase { get; private set; }         // seconds since the window opened

    private void UpdateVolley()
    {
        if (stats == null || !stats.isRanged) return;
        volleyClock += Time.deltaTime;
        if (volleyClock >= volleyInterval) volleyClock -= volleyInterval;
        VolleyOpen = volleyClock < volleyWindow;
        VolleyPhase = volleyClock;
    }
    public event System.Action OnFacingSnapped;            // Rotate command executed (visual hook)
    public event System.Action OnOrderIssued;               // any successful player/AI order (visual hook)
    public bool CanReform { get; private set; }
    public bool IsSelected { get; private set; }
    public Formation attackTarget;

    // Facing authority: AnchorRot is the single authoritative facing —
    // directional damage, slots, soldier idle facing, and the ground arrow all
    // read it. IsAutoFacing marks the states where combat, not the player,
    // steers it (chase rotation while Attacking or Charging, contact wheeling
    // while Engaged); manual Rotate is unavailable there.
    public bool IsAutoFacing =>
        soldiers.Count > 0 &&
        (((State == FormationState.Attacking || State == FormationState.Charging) &&
          attackTarget != null) ||
         State == FormationState.Engaged);

    public enum RotateBlock { None, AutoFacing, Broken, Busy, Destroyed }

    // Why the Rotate command is currently unavailable (None = available).
    public RotateBlock GetRotateBlock()
    {
        if (soldiers.Count == 0) return RotateBlock.Destroyed;
        if (State == FormationState.BrokenRanks || IsRouting) return RotateBlock.Broken;
        if (IsAutoFacing) return RotateBlock.AutoFacing;
        if (State != FormationState.Ordered) return RotateBlock.Busy;
        return RotateBlock.None;
    }

    public enum ChargeBlock { None, NoTarget, Broken, Busy, Destroyed }

    // Why the Charge command is currently unavailable (None = available).
    // Ordered, Attacking, and Engaged may all charge — an engaged century can
    // still throw itself forward.
    public ChargeBlock GetChargeBlock()
    {
        if (soldiers.Count == 0) return ChargeBlock.Destroyed;
        if (State == FormationState.BrokenRanks || State == FormationState.Reforming ||
            IsRouting)
            return ChargeBlock.Broken;
        if (State == FormationState.Withdrawing || State == FormationState.Charging ||
            Maneuver == FormationManeuverState.Wheel)
            return ChargeBlock.Busy;
        if (NearestEnemyFormation(AnchorPos, chargeRange) == null) return ChargeBlock.NoTarget;
        return ChargeBlock.None;
    }

    // True while the post-charge free-for-all runs on the broken-ranks
    // machinery: soldiers hunt as individuals, but the standard moves with the
    // pack until Reform plants it.
    public bool IsPursuing { get; private set; }

    public float SpeedMultiplier =>
        State == FormationState.Charging ? chargeSpeedFactor : 1f;

    // Impact bonus lives only inside the rush's contact window. Archers may
    // charge as a last resort but get no bonus — their sidearm's damage
    // discount already prices that desperation.
    public float ChargeDamageMultiplier =>
        State == FormationState.Charging && !stats.isRanged &&
        (firstContactTime < 0f || Time.time - firstContactTime <= chargeImpactWindow)
            ? chargeImpactBonus : 1f;

    // The leash tightens while pursuing so the pack stays a pack.
    public float EffectiveLeash => IsPursuing ? pursuitLeash : brokenLeash;

    public readonly List<Soldier> soldiers = new List<Soldier>();
    public int TotalSpawned { get; private set; }

    public Vector3 AnchorPos { get; private set; }
    public Quaternion AnchorRot { get; private set; } = Quaternion.identity;
    public Vector3 AnchorForward => AnchorRot * Vector3.forward;
    public float BoundingRadius { get; private set; } = 4f;
    public Vector2 FootprintHalfExtents { get; private set; } = new Vector2(4f, 4f);

    public float TotalHealth
    {
        get { float h = 0f; foreach (var s in soldiers) h += s.Health; return h; }
    }

    // Aggregate formation health (Phase 5): denominator is EVERY ORIGINAL
    // member, so dead soldiers count as zero — 40 healthy survivors of 80
    // reads 50%, not 100%.
    public float TotalMaxHealth => TotalSpawned * stats.maxHealth;

    private Vector3[] slotOffsets = new Vector3[0];
    private Vector3 destination;
    private bool hasDestination;
    private float engageTimer;
    private float noContactTime;
    private float reformTimer;
    private float promoteTimer;
    private float autoCloseTimer;
    private int lostSinceSlotRebuild;
    private int engagedCount;
    private Vector3 engagedCentroid;   // mean position of own engaged soldiers (the contact surface)

    // dominant-group state (banner + reform rally)
    public Vector3 DominantGroupCenter { get; private set; }
    public int DominantGroupCount { get; private set; }
    private float clusterTimer;
    private bool hasDominantCenter;
    private static readonly List<Vector3> clusterScratch = new List<Vector3>(64);
    private static int[] clusterParent = new int[64];
    private static int[] clusterSize = new int[64];

    private float flankThreatTime;   // defensive-pivot reaction timer

    // morale (Chunk A): the math lives in FormationMorale; this class owns the
    // state changes it triggers (rout, rally). Null until the config exists.
    private FormationMorale morale;
    public FormationMorale Morale => morale;
    private MoraleConfig MoraleCfg => BattleSetup.Instance != null ? BattleSetup.Instance.moraleConfig : null;
    public bool IsRouting => State == FormationState.Routing;
    private float lastReformAbortTime = -999f;
    private Vector3 fleeThreatPos;          // the enemy formation a rout runs from
    private bool hasFleeThreat;
    private float fleeThreatTimer;
    private const float FleeThreatInterval = 0.5f;
    private const float FleeEdgeInset = 1f;  // routers stop this far inside the field edge
    private static readonly List<bool> dominantMembers = new List<bool>(64);

    // charge timing: when the rush began and when it first found contact
    private float chargeStartTime;
    private float firstContactTime = -1f;

    // wheel maneuver state: one formation-level progress, no per-soldier data
    private Quaternion maneuverTargetRot;
    private Vector3 wheelPivot;
    private float wheelAngleRemaining;   // signed degrees still to turn
    private float wheelAngularSpeed;     // deg/s magnitude
    private float redressTimer;

    // ---------------- setup ----------------

    public void Init(Team team, string name, UnitStats stats, Vector3 pos, float yawDeg, int columns, bool autoPilot)
    {
        // stagger volley clocks so archer centuries don't fire in unison
        volleyClock = Mathf.Abs(name.GetHashCode() % 1000) * 0.001f * volleyInterval;
        this.team = team;
        displayName = name;
        this.stats = stats;
        this.columns = Mathf.Max(1, columns);
        this.autoPilot = autoPilot;
        AnchorPos = pos;
        AnchorRot = Quaternion.Euler(0f, yawDeg, 0f);
        transform.position = pos;
        transform.rotation = AnchorRot;
    }

    public void AddSoldier(Soldier s)
    {
        soldiers.Add(s);
        TotalSpawned++;
    }

    // Closed form of the front-row half-width that BuildSlots bakes into
    // FootprintHalfExtents.x — lets BattleSetup space a line of formations by
    // real footprints before any of them exist. ValidateLineGaps cross-checks
    // the spawned footprints against this, so the two can't silently drift.
    public static float LineHalfWidth(int count, int columns, float spacing)
    {
        int inFrontRow = Mathf.Min(count, Mathf.Max(1, columns));
        return (inFrontRow - 1) * 0.5f * spacing + spacing * 0.5f;
    }

    public void BuildSlots(int count)
    {
        int rows = Mathf.CeilToInt(count / (float)columns);
        slotOffsets = new Vector3[count];
        float maxSq = 0f, maxX = 0f, maxZ = 0f;
        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int col = i % columns;
            int inThisRow = Mathf.Min(columns, count - row * columns);
            float x = (col - (inThisRow - 1) * 0.5f) * spacing;
            float z = ((rows - 1) * 0.5f - row) * spacing;   // row 0 is the front rank
            slotOffsets[i] = new Vector3(x, 0f, z);
            maxSq = Mathf.Max(maxSq, slotOffsets[i].sqrMagnitude);
            maxX = Mathf.Max(maxX, Mathf.Abs(x));
            maxZ = Mathf.Max(maxZ, Mathf.Abs(z));
        }
        BoundingRadius = Mathf.Sqrt(maxSq) + spacing;
        FootprintHalfExtents = new Vector2(maxX + spacing * 0.5f, maxZ + spacing * 0.5f);
        lostSinceSlotRebuild = 0;
    }

    // smallest XZ distance from a battlefield point to any living soldier
    public float DistanceToNearestSoldier(Vector3 point)
    {
        float best = float.MaxValue;
        foreach (var s in soldiers)
        {
            Vector3 d = s.transform.position - point;
            d.y = 0f;
            best = Mathf.Min(best, d.sqrMagnitude);
        }
        return best == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best);
    }

    // XZ distance from a battlefield point to the formation's oriented
    // rectangular footprint (0 when the point is inside it)
    public float DistanceToFootprint(Vector3 point)
    {
        Vector3 local = Quaternion.Inverse(AnchorRot) * (point - AnchorPos);
        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - FootprintHalfExtents.x);
        float dz = Mathf.Max(0f, Mathf.Abs(local.z) - FootprintHalfExtents.y);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // The formation-level touch target: soldiers or the footprint rectangle,
    // whichever the point is closest to. Combat scatter and the ordered block
    // both stay grabbable this way.
    public float InteractionDistance(Vector3 point)
    {
        return Mathf.Min(DistanceToNearestSoldier(point), DistanceToFootprint(point));
    }

    public Vector3 GetSlotWorldPos(int slot)
    {
        if (slot < 0 || slot >= slotOffsets.Length) return AnchorPos;
        return AnchorPos + AnchorRot * slotOffsets[slot];
    }

    // ---------------- commands ----------------

    public void IssueMove(Vector3 dest)
    {
        // Broken centuries take no movement orders — Reform is their only
        // command (Phase 3); Reforming centuries are mid-recovery; Routing
        // centuries take no orders at all (Chunk A).
        if (State == FormationState.BrokenRanks || State == FormationState.Reforming ||
            State == FormationState.Routing) return;
        Maneuver = FormationManeuverState.None;   // a new order supersedes a turn
        dest.y = 0f;
        Vector3 travel = dest - AnchorPos;
        travel.y = 0f;
        if (State == FormationState.Engaged)
        {
            // any movement order while in melee is an attempt to disengage
            State = FormationState.Withdrawing;
            attackTarget = null;
            CurrentOrderType = OrderType.Withdraw;
        }
        else if (State == FormationState.Withdrawing)
        {
            CurrentOrderType = OrderType.Withdraw;
        }
        else
        {
            State = FormationState.Ordered;
            attackTarget = null;
            CurrentOrderType = OrderType.Move;
        }
        destination = dest;
        hasDestination = true;
        // default final facing = direction of travel; the free-arrow rotate
        // mode can override it later without changing the destination
        DestinationFacing = travel.sqrMagnitude > 0.04f ? travel.normalized : AnchorForward;
        OnOrderIssued?.Invoke();
    }

    // Rotate-edit of an order in flight: move the planned destination and/or
    // planned final facing without re-deriving either from current motion.
    public void RedirectMove(Vector3 dest, Vector3 facing)
    {
        if (IsRouting) return;
        if (!hasDestination ||
            (CurrentOrderType != OrderType.Move && CurrentOrderType != OrderType.Withdraw)) return;
        dest.y = 0f;
        destination = dest;
        SetDestinationFacing(facing);
    }

    public void SetDestinationFacing(Vector3 dir)
    {
        if (IsRouting) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        DestinationFacing = dir.normalized;
    }

    public void IssueAttack(Formation target)
    {
        if (target == null || target.team == team || target.soldiers.Count == 0) return;
        if (State == FormationState.BrokenRanks || State == FormationState.Reforming ||
            State == FormationState.Routing) return;
        attackTarget = target;
        State = FormationState.Attacking;
        hasDestination = true;
        CurrentOrderType = OrderType.Attack;
        OnOrderIssued?.Invoke();
    }

    // Charge: a committed sprint at a nearby enemy that trades formation
    // discipline for a burst of impact, then dissolves into pursuit on the
    // broken-ranks machinery. Blocked charges are refused outright — never
    // queued — so the button state and the outcome can't disagree.
    public void IssueCharge(Formation target = null)
    {
        if (GetChargeBlock() != ChargeBlock.None) return;
        Formation resolved =
            IsChargeable(target) ? target :
            IsChargeable(attackTarget) ? attackTarget :
            NearestEnemyFormation(AnchorPos, chargeRange);
        if (resolved == null) return;
        Maneuver = FormationManeuverState.None;
        attackTarget = resolved;
        State = FormationState.Charging;
        CurrentOrderType = OrderType.Charge;
        hasDestination = true;
        chargeStartTime = Time.time;
        firstContactTime = -1f;
        OnOrderIssued?.Invoke();
    }

    // A charge only ever picks or accepts a living enemy within chargeRange
    // of the anchor — no cross-map death runs.
    private bool IsChargeable(Formation t)
    {
        if (t == null || t.team == team || t.soldiers.Count == 0) return false;
        Vector3 d = t.AnchorPos - AnchorPos;
        d.y = 0f;
        return d.sqrMagnitude <= chargeRange * chargeRange;
    }

    // Nearest living enemy formation within maxDist of a point. Bounded by the
    // caller's radius so charge targeting and pursuit flow stay local.
    private Formation NearestEnemyFormation(Vector3 point, float maxDist)
    {
        if (BattleSetup.Instance == null) return null;
        Formation best = null;
        float best2 = maxDist * maxDist;
        foreach (var o in BattleSetup.Instance.formations)
        {
            if (o.team == team || o.soldiers.Count == 0) continue;
            Vector3 d = o.AnchorPos - point;
            d.y = 0f;
            float d2 = d.sqrMagnitude;
            if (d2 < best2) { best2 = d2; best = o; }
        }
        return best;
    }

    // Rotate command, classified by the shortest signed yaw delta (Patch 4):
    //   <= smallTurnMaxDeg  -> small turn: facing snaps, slot owners keep their
    //                          slots, soldiers pivot in place and redress.
    //   >= aboutFaceMinDeg  -> about-face: facing snaps and the nearest-slot
    //                          remap reinterprets ranks in place (the old rear
    //                          becomes the new front; near-zero displacement).
    //   otherwise           -> inner-flank wheel: the whole slot grid rotates
    //                          rigidly around the inner front corner; the outer
    //                          flank walks the long arc, no one crosses the
    //                          block. (Ordered only, as before.)
    public void IssueFace(Vector3 direction)
    {
        if (!ApplyFace(direction)) return;
        OnOrderIssued?.Invoke();
    }

    // In-place tactical rotation core, shared by the player Rotate command and
    // move-arrival facing (which must not fire the order-feedback event).
    private bool ApplyFace(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return false;
        if (State != FormationState.Ordered) return false;
        Quaternion want = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float signed = Mathf.DeltaAngle(AnchorRot.eulerAngles.y, want.eulerAngles.y);
        float a = Mathf.Abs(signed);
        if (a <= smallTurnMaxDeg)
        {
            AnchorRot = want;
            BeginRedress(FormationManeuverState.SmallTurn);
            OnFacingSnapped?.Invoke();
        }
        else if (a >= aboutFaceMinDeg)
        {
            AnchorRot = want;
            AssignNearestSlots();   // rank reinterpretation, minimal displacement
            BeginRedress(FormationManeuverState.AboutFace);
            OnFacingSnapped?.Invoke();
        }
        else
        {
            BeginWheel(want, signed);
            // no pivot animation: soldiers physically walk the wheel arcs
        }
        return true;
    }

    private void BeginRedress(FormationManeuverState kind)
    {
        Maneuver = kind;
        redressTimer = redressTimeoutSeconds;
    }

    private void BeginWheel(Quaternion want, float signedDeg)
    {
        Maneuver = FormationManeuverState.Wheel;
        maneuverTargetRot = want;
        wheelAngleRemaining = signedDeg;
        // Inner front corner: right turns (positive yaw delta) wheel around the
        // front-right corner, left turns around the front-left corner.
        float side = signedDeg > 0f ? 1f : -1f;
        Vector3 pivotLocal = new Vector3(side * FootprintHalfExtents.x, 0f,
                                         FootprintHalfExtents.y);
        wheelPivot = AnchorPos + AnchorRot * pivotLocal;
        // Angular speed from the outer arc: farthest slot from the pivot walks
        // at wheelOuterSpeed; duration clamped to a sane window.
        float outerR = spacing;
        for (int i = 0; i < slotOffsets.Length; i++)
            outerR = Mathf.Max(outerR, (slotOffsets[i] - pivotLocal).magnitude);
        float speed = Mathf.Rad2Deg * (wheelOuterSpeed / Mathf.Max(0.5f, outerR));
        float duration = Mathf.Clamp(Mathf.Abs(signedDeg) / Mathf.Max(1f, speed), 0.8f, 10f);
        wheelAngularSpeed = Mathf.Abs(signedDeg) / duration;
        hasDestination = false;
    }

    // One rigid-body wheel step: rotate the anchor frame around the pivot.
    // Slots are derived from AnchorPos/AnchorRot, so every slot follows its
    // arc exactly — no per-slot math, no straight paths through the block.
    // Static so validation can exercise the same code path.
    public static void WheelStep(ref Vector3 anchorPos, ref Quaternion anchorRot,
                                 Vector3 pivot, float stepDeg)
    {
        Quaternion dq = Quaternion.Euler(0f, stepDeg, 0f);
        anchorRot = dq * anchorRot;
        anchorPos = pivot + dq * (anchorPos - pivot);
    }

    private void UpdateManeuver()
    {
        if (Maneuver == FormationManeuverState.None) return;
        if (Maneuver == FormationManeuverState.Wheel)
        {
            // committed fighters are never dragged: pause while too many locked
            if (LockedFraction() > wheelPauseLockedFraction) return;
            float step = Mathf.Min(Mathf.Abs(wheelAngleRemaining),
                                   wheelAngularSpeed * Time.deltaTime)
                         * Mathf.Sign(wheelAngleRemaining);
            Vector3 p = AnchorPos; Quaternion r = AnchorRot;
            WheelStep(ref p, ref r, wheelPivot, step);
            AnchorPos = p; AnchorRot = r;
            wheelAngleRemaining -= step;
            if (Mathf.Abs(wheelAngleRemaining) < 0.5f)
            {
                AnchorRot = maneuverTargetRot;
                BeginRedress(FormationManeuverState.Redressing);
            }
            return;
        }
        // SmallTurn / AboutFace / Redressing: wait for enough soldiers to
        // settle into their slots, with a timeout so a locked or dead soldier
        // never blocks completion.
        redressTimer -= Time.deltaTime;
        if (redressTimer <= 0f || FractionNearSlots(0.6f) >= redressCompleteFraction)
            Maneuver = FormationManeuverState.None;
    }

    private float LockedFraction()
    {
        if (soldiers.Count == 0) return 0f;
        int locked = 0;
        for (int i = 0; i < soldiers.Count; i++)
            if (soldiers[i].IsInCommittedCombatAction) locked++;
        return (float)locked / soldiers.Count;
    }

    private float FractionNearSlots(float tolerance)
    {
        if (soldiers.Count == 0) return 1f;
        float t2 = tolerance * tolerance;
        int near = 0;
        for (int i = 0; i < soldiers.Count; i++)
        {
            Vector3 d = soldiers[i].transform.position - GetSlotWorldPos(soldiers[i].slotIndex);
            d.y = 0f;
            if (d.sqrMagnitude <= t2) near++;
        }
        return (float)near / soldiers.Count;
    }

    // ---------------- dominant group ----------------

    // Connected components over the soldier proximity graph (union-find).
    // Returns the centroid of the winning cluster. Hysteresis: the cluster
    // nearest prevCenter (the incumbent) keeps ownership unless a rival is
    // switchFactor times larger, so a 26/24 split never flip-flops while a
    // 40/10 split clearly resolves. Static and list-driven so editor
    // validation can exercise the exact shipped code path (like WheelStep).
    // Cost: <= n^2/2 sqr-distance checks per recompute, n <= ~50 per
    // formation, on a several-per-second interval — no allocations beyond the
    // grow-only scratch arrays.
    // Optional `members` (Chunk A): filled parallel to `positions` with true
    // for each point in the winning cluster — the rally needs to know which
    // officers are actually in the main pack. Omitted, it costs nothing.
    public static Vector3 ComputeDominantGroup(List<Vector3> positions, float linkDist,
                                               Vector3 prevCenter, bool hasPrev,
                                               float switchFactor, out int count,
                                               List<bool> members = null)
    {
        int n = positions.Count;
        count = 0;
        if (n == 0) return prevCenter;
        if (clusterParent.Length < n)
        {
            clusterParent = new int[Mathf.NextPowerOfTwo(n)];
            clusterSize = new int[Mathf.NextPowerOfTwo(n)];
        }
        for (int i = 0; i < n; i++) clusterParent[i] = i;
        float link2 = linkDist * linkDist;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                Vector3 d = positions[i] - positions[j];
                d.y = 0f;
                if (d.sqrMagnitude <= link2) Union(i, j);
            }
        for (int i = 0; i < n; i++) clusterSize[i] = 0;
        for (int i = 0; i < n; i++) clusterSize[Find(i)]++;

        // incumbent: the cluster of the member nearest the previous center,
        // if any member is still within two link distances of it
        int incumbentRoot = -1;
        if (hasPrev)
        {
            float best = link2 * 4f;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = positions[i] - prevCenter;
                d.y = 0f;
                float d2 = d.sqrMagnitude;
                if (d2 < best) { best = d2; incumbentRoot = Find(i); }
            }
        }
        int largestRoot = Find(0);
        for (int i = 1; i < n; i++)
        {
            int r = Find(i);
            if (clusterSize[r] > clusterSize[largestRoot]) largestRoot = r;
        }

        int winner = largestRoot;
        if (incumbentRoot >= 0 && incumbentRoot != largestRoot &&
            clusterSize[largestRoot] < clusterSize[incumbentRoot] * switchFactor)
            winner = incumbentRoot;

        members?.Clear();
        Vector3 c = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            bool inWinner = Find(i) == winner;
            members?.Add(inWinner);
            if (inWinner) { c += positions[i]; count++; }
        }
        c /= Mathf.Max(1, count);
        c.y = 0f;
        return c;

        int Find(int x)
        {
            while (clusterParent[x] != x)
            {
                clusterParent[x] = clusterParent[clusterParent[x]];
                x = clusterParent[x];
            }
            return x;
        }
        void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a != b) clusterParent[b] = a;
        }
    }

    private void UpdateDominantGroup(bool force = false, List<bool> members = null)
    {
        clusterTimer -= Time.deltaTime;
        if (!force && clusterTimer > 0f) return;
        clusterTimer = clusterInterval;
        if (soldiers.Count == 0) { DominantGroupCount = 0; hasDominantCenter = false; return; }
        clusterScratch.Clear();
        foreach (var s in soldiers) clusterScratch.Add(s.transform.position);
        DominantGroupCenter = ComputeDominantGroup(
            clusterScratch, spacing * clusterLinkFactor,
            DominantGroupCenter, hasDominantCenter, clusterSwitchFactor, out int c, members);
        DominantGroupCount = c;
        hasDominantCenter = c > 0;
    }

    public void IssueBreakRanks()
    {
        if (soldiers.Count == 0 || State == FormationState.BrokenRanks || IsRouting) return;
        // Freeze the rally point and tactical facing at the moment ranks
        // break: the banner stays here and Reform rebuilds here, regardless
        // of where the soldiers scatter (Phase 3 locked behavior).
        RallyAnchor = AnchorPos;
        rallyFacing = AnchorRot;
        IsPursuing = false;   // a deliberate break is anchored, not a pursuit
        attackTarget = null;
        hasDestination = false;
        Maneuver = FormationManeuverState.None;
        CurrentOrderType = OrderType.BreakRanks;
        State = FormationState.BrokenRanks;
        OnOrderIssued?.Invoke();
    }

    // Manual recovery from Broken (Phase 3): survivor-only slots are built at
    // the FIXED rally anchor with the tactical facing stored when ranks broke.
    // Soldiers stop acquiring targets (GetAcquireRadius returns 0 while
    // Reforming) and physically walk back; meaningful melee contact aborts
    // (see UpdateStateMachine), projectile fire alone never does.
    public void IssueReform()
    {
        if (State != FormationState.BrokenRanks || soldiers.Count == 0) return;

        // Planting the standard: a pursuit has no pre-frozen rally point — the
        // standard moved with the men, and Reform strikes it into the ground
        // wherever the pack stands NOW. From here the ordinary fixed-rally
        // reform flow applies, and an abort back to Broken leaves the standard
        // planted rather than resuming the pursuit.
        if (IsPursuing)
        {
            RallyAnchor = DominantGroupCount > 0 ? DominantGroupCenter : AnchorPos;
            rallyFacing = AnchorRot;
            IsPursuing = false;
        }

        BeginReform();
        OnOrderIssued?.Invoke();
    }

    // The reform itself, shared by the Reform command and the automatic rally
    // (Chunk A): survivor-only slots at RallyAnchor facing rallyFacing, then
    // the ordinary Reforming state — completion at reformCompleteFraction,
    // abort to Broken at reformAbortEngagedFraction.
    private void BeginReform()
    {
        AnchorPos = RallyAnchor;
        AnchorRot = rallyFacing;

        int n = soldiers.Count;
        columns = Mathf.Clamp(columns, 1, n);   // keep the century's frontage
        BuildSlots(n);
        AssignNearestSlots();

        attackTarget = null;
        hasDestination = false;
        reformTimer = 0f;
        CurrentOrderType = OrderType.Reform;
        State = FormationState.Reforming;
    }

    private void AssignNearestSlots()
    {
        var unassigned = new List<Soldier>(soldiers);
        for (int slot = 0; slot < slotOffsets.Length && unassigned.Count > 0; slot++)
        {
            Vector3 wp = GetSlotWorldPos(slot);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < unassigned.Count; i++)
            {
                float d = (unassigned[i].transform.position - wp).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            unassigned[best].slotIndex = slot;
            unassigned.RemoveAt(best);
        }
        PinCenturionSlot();
    }

    // The centurion owns the front-right corner slot (columns - 1). The
    // nearest-slot pass assigns by pure distance, so after every remap
    // (about-face, auto-close, reform) he swaps back with whoever landed
    // there. No living centurion => no-op — he is never promoted or respawned.
    // Engaged rank replacement may still pull him off the corner mid-melee;
    // that is combat disorder and is left alone.
    private void PinCenturionSlot()
    {
        int want = columns - 1;
        if (want < 0 || want >= slotOffsets.Length) return;
        Soldier centurion = null;
        foreach (var s in soldiers)
            if (s.role == SoldierRole.Centurion) { centurion = s; break; }
        if (centurion == null || centurion.slotIndex == want) return;
        foreach (var s in soldiers)
            if (s != centurion && s.slotIndex == want)
            {
                s.slotIndex = centurion.slotIndex;
                break;
            }
        centurion.slotIndex = want;
    }

    public void NotifyDeath(Soldier s)
    {
        soldiers.Remove(s);
        lostSinceSlotRebuild++;
    }

    public void SetSelected(bool sel)
    {
        IsSelected = sel;
        foreach (var s in soldiers) s.SetSelected(sel);
    }

    // ---------------- per-soldier behavior knobs ----------------

    // Local engagement allowance: the closer a soldier stands to the active
    // fight, the more slot freedom and target reach it gets; soldiers far from
    // contact stay strongly constrained. This lets uneven melee edges bend and
    // wrap slightly without the whole formation dissolving into a mob.
    private bool NearCombat(Soldier s) => s.NearestEnemyDist <= edgeEngageRadius;

    public float GetSlotWeight(Soldier s)
    {
        switch (State)
        {
            case FormationState.Ordered: return 1f;
            case FormationState.Attacking: return 0.95f;
            case FormationState.Engaged:
                if (s.IsEngaged) return 0.12f;
                return NearCombat(s) ? 0.35f : 0.75f;
            case FormationState.BrokenRanks: return 0f;   // Phase 3: zero slot steering while broken
            case FormationState.Routing: return 0f;       // fleeing men hold no slots
            case FormationState.Withdrawing: return 1f;
            case FormationState.Reforming: return 1f;
            case FormationState.Charging: return 0.4f;    // a loose pack, not a parade
            default: return 1f;
        }
    }

    public float GetAcquireRadius(Soldier s)
    {
        float r;
        switch (State)
        {
            case FormationState.Ordered:
            case FormationState.Attacking:
                r = acquireRadiusOrdered; break;
            case FormationState.Engaged:
                r = s.IsEngaged || NearCombat(s) ? acquireRadiusEngaged
                                                 : personalEngageRadius; break;
            case FormationState.BrokenRanks:
            case FormationState.Charging:      // rushing soldiers hunt like broken ones
                r = acquireRadiusBroken; break;
            default:
                return 0f;   // Withdrawing / Reforming / Routing: stop seeking engagements
        }
        if (stats.isRanged) r = Mathf.Max(r, stats.rangedRange);
        return r;
    }

    public static string StateLabel(FormationState s)
    {
        return s == FormationState.BrokenRanks ? "Broken Ranks" : s.ToString();
    }

    // ---------------- update loop ----------------

    private void Update()
    {
        // Deployment (Pre) runs the full formation movement stack — centuries
        // physically march to their deployment positions; combat cannot start
        // because soldiers only fight while the battle is Active.
        if (BattleSetup.Instance == null || BattleSetup.Instance.Phase == BattlePhase.Ended)
            return;

        // A destroyed formation is inert: its anchor must never keep chasing a
        // target, or its "Defeated" label follows the survivor around the map.
        if (soldiers.Count == 0)
        {
            attackTarget = null;
            hasDestination = false;
            return;
        }

        UpdateManeuver();
        UpdateAnchorMovement();
        UpdateEngagement();
        UpdateMorale();
        UpdateFleeThreat();
        UpdateVolley();
        UpdateDominantGroup();
        UpdateEngagedFacing();
        UpdateStateMachine();
        UpdateRankReplacement();
        UpdateAutoClose();
        transform.position = AnchorPos;
        transform.rotation = AnchorRot;
    }

    // Auto-close: baseline competence of a formation trying to stay ordered.
    // Casualties leave permanently empty slots behind; once enough accumulate,
    // rebuild the slot grid for the surviving headcount (same anchor, same
    // facing, same frontage) and let everyone walk to their nearest new slot.
    // Rear soldiers flow forward and lateral holes squeeze shut over a few
    // seconds — no Reform needed for ordinary attrition. Engaged fighters keep
    // fighting (their slot pull is tiny), so combat still deforms the unit;
    // this only stops the grid from preserving empty historical positions.
    private void UpdateAutoClose()
    {
        // Patch 4 priority: auto-close never runs mid-melee (the column-local
        // rank promotion handles vacancies there without a global remap) and
        // never during an explicit Rotate maneuver. Phase 3 extends this to
        // FIRING formations: an archer century mid-volley keeps its casualty
        // gaps — full compaction happens only through an explicit Reform.
        if (State != FormationState.Ordered && State != FormationState.Attacking) return;
        if (Maneuver != FormationManeuverState.None) return;
        if (IsFiring) return;
        autoCloseTimer -= Time.deltaTime;
        if (autoCloseTimer > 0f) return;
        autoCloseTimer = autoCloseInterval;

        if (lostSinceSlotRebuild < autoCloseDeficit || soldiers.Count == 0) return;
        if (soldiers.Count >= slotOffsets.Length) return;
        columns = Mathf.Clamp(columns, 1, soldiers.Count);
        BuildSlots(soldiers.Count);
        AssignNearestSlots();
    }

    // During ordered melee, depth must matter: when a front slot's fighter surges
    // into combat or dies, the coherent soldier one row behind is promoted into
    // that slot. Front-to-back cascading compresses each column forward one step
    // per pass, feeding soldiers into the fight progressively while the rest of
    // the formation stays structured. Break Ranks stays a separate, wilder mode.
    private void UpdateRankReplacement()
    {
        if (State != FormationState.Engaged) return;
        promoteTimer -= Time.deltaTime;
        if (promoteTimer > 0f) return;
        promoteTimer = promoteInterval;

        if (slotOffsets.Length == 0 || columns <= 0 || soldiers.Count == 0) return;

        var owner = new Soldier[slotOffsets.Length];
        foreach (var s in soldiers)
            if (s.slotIndex >= 0 && s.slotIndex < owner.Length) owner[s.slotIndex] = s;

        float vac2 = promoteVacancyDist * promoteVacancyDist;
        float coh2 = promoteCoherenceDist * promoteCoherenceDist;

        for (int slot = 0; slot < slotOffsets.Length; slot++)
        {
            var holder = owner[slot];
            bool vacant = holder == null ||
                          (holder.IsEngaged &&
                           (holder.transform.position - GetSlotWorldPos(slot)).sqrMagnitude > vac2);
            if (!vacant) continue;

            int behind = slot + columns;   // same column, one row back
            if (behind >= slotOffsets.Length) continue;
            var candidate = owner[behind];
            if (candidate == null || candidate.IsEngaged) continue;
            if ((candidate.transform.position - GetSlotWorldPos(behind)).sqrMagnitude > coh2) continue;

            // advance the rear soldier; the displaced fighter rejoins at the rear
            // slot once it disengages, which naturally rotates tired ranks back
            candidate.slotIndex = slot;
            if (holder != null) holder.slotIndex = behind;
            owner[slot] = candidate;
            owner[behind] = holder;        // null lets the next row cascade forward
        }
    }

    private void UpdateAnchorMovement()
    {
        if (Maneuver == FormationManeuverState.Wheel) return;   // the wheel owns the anchor
        // Pursuit is the one exception to the frozen broken-state anchor: the
        // standard moves with the men, so the anchor (leash center, reform
        // safety reads) eases after the dominant pack. AnchorRot is untouched.
        if (State == FormationState.BrokenRanks && IsPursuing && DominantGroupCount > 0)
        {
            AnchorPos = Vector3.Lerp(AnchorPos, DominantGroupCenter,
                                     1f - Mathf.Exp(-2f * Time.deltaTime));
            return;
        }
        // Routing (Chunk A): the anchor — and so the banner and the eventual
        // rally point — goes with the fleeing men instead of staying where
        // the line broke.
        if (IsRouting && DominantGroupCount > 0)
        {
            AnchorPos = Vector3.Lerp(AnchorPos, DominantGroupCenter,
                                     1f - Mathf.Exp(-2f * Time.deltaTime));
            return;
        }
        // Phase 3: broken centuries have NO centralized movement — the anchor
        // stays at the rally point and soldiers act as individuals.
        bool charging = State == FormationState.Charging;
        bool chasing = State == FormationState.Attacking && attackTarget != null;
        bool canMove = State == FormationState.Ordered ||
                       State == FormationState.Withdrawing || chasing || charging;
        if (!canMove) return;

        if (charging)
        {
            if (attackTarget == null || attackTarget.soldiers.Count == 0)
            {
                // Rush target destroyed: flow to the next enemy already within
                // pursuit reach; with nothing close, the state machine tips
                // the charge into pursuit — a charge never map-chases.
                attackTarget = NearestEnemyFormation(AnchorPos, pursuitRetargetRadius);
                if (attackTarget == null) return;
            }
            // Follow the target's real mass, not a stale anchor: a scattered
            // enemy is chased where its soldiers actually are.
            destination = attackTarget.DominantGroupCount > 0
                ? attackTarget.DominantGroupCenter : attackTarget.AnchorPos;
            hasDestination = true;
            Vector3 toTgt = destination - AnchorPos;
            toTgt.y = 0f;
            if (toTgt.sqrMagnitude > 0.04f) DestinationFacing = toTgt.normalized;
        }
        else if (chasing)
        {
            if (attackTarget.soldiers.Count == 0)
            {
                // Target destroyed (Phase 2H): clear the explicit target, hold
                // position and CURRENT tactical facing. Local threats are
                // handled by soldier-level acquisition; the formation never
                // spins toward a distant map-wide "nearest enemy".
                attackTarget = null;
                State = FormationState.Ordered;
                hasDestination = false;
                CurrentOrderType = OrderType.None;
                return;
            }
            destination = attackTarget.AnchorPos;
            hasDestination = true;
            Vector3 toTarget = destination - AnchorPos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.04f) DestinationFacing = toTarget.normalized;
        }
        if (!hasDestination) return;

        Vector3 to = destination - AnchorPos;
        to.y = 0f;
        float dist = to.magnitude;
        // Ranged formations hold their preferred firing distance instead of
        // marching into melee range; melee (and every charge) closes to contact.
        float stopDist = charging ? meleeChaseStopDist
            : chasing
            ? (stats.isRanged ? stats.rangedPreferredRange : meleeChaseStopDist)
            : 0.2f;
        if (dist <= stopDist)
        {
            if (chasing && dist > 0.5f)
            {
                // hold position but wheel to face the target (archer firing line)
                Quaternion face = Quaternion.LookRotation(to / dist, Vector3.up);
                AnchorRot = Quaternion.RotateTowards(AnchorRot, face, rotateSpeedDeg * Time.deltaTime);
            }
            if (!chasing && !charging)
            {
                // Arrival (Phase 2): settle onto the STORED destination facing
                // — never whatever direction the final approach happened to be.
                hasDestination = false;
                if (CurrentOrderType == OrderType.Move)
                {
                    CurrentOrderType = OrderType.None;
                    if (Vector3.Angle(AnchorForward, DestinationFacing) > 2f)
                        ApplyFace(DestinationFacing);
                }
            }
            return;
        }
        Vector3 dir = to / dist;

        if (State == FormationState.Withdrawing)
        {
            // back away without wheeling the whole grid through the melee
            AnchorPos += dir * Mathf.Min(moveSpeed * 0.85f * Time.deltaTime, dist);
            return;
        }

        Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
        AnchorRot = Quaternion.RotateTowards(AnchorRot, want, rotateSpeedDeg * Time.deltaTime);
        float align = Vector3.Dot(AnchorForward, dir);
        if (align > 0.3f)
            AnchorPos += AnchorForward *
                         Mathf.Min(moveSpeed * SpeedMultiplier * align * Time.deltaTime, dist);
    }

    private void UpdateEngagement()
    {
        engageTimer -= Time.deltaTime;
        if (engageTimer > 0f) return;
        engageTimer = 0.25f;

        if (BattleSetup.Instance == null) return;

        // Phase 6F: per-soldier nearest-enemy via the spatial grid. The scan
        // radius covers every consumer of NearestEnemyDist (edge engagement 6,
        // visual guard 12); beyond it the distance is treated as infinite.
        const float scanRadius = 14f;
        engagedCount = 0;
        Vector3 engagedSum = Vector3.zero;
        foreach (var s in soldiers)
        {
            Vector3 p = s.transform.position;
            Soldier e = BattleGrid.NearestEnemy(p, team, scanRadius, out float d);
            bool engaged = e != null && d < personalEngageRadius;
            s.IsEngaged = engaged;
            s.NearestEnemyDist = e != null ? d : float.MaxValue;
            if (engaged) { engagedCount++; engagedSum += p; }
        }
        engagedCentroid = engagedCount > 0 ? engagedSum / engagedCount : AnchorPos;

        // Phase 3: Reform is available from Broken, period — contested reforms
        // are attempted and abort on meaningful melee contact instead of being
        // pre-blocked by a disengage radius.
        CanReform = State == FormationState.BrokenRanks && soldiers.Count > 0;

        // Pursuit retarget: informational for the banner and AI (soldiers hunt
        // on their own acquire reach). The pack only flows to enemies already
        // near it; with nothing in reach the target stays null and the pack
        // mills where it stands instead of map-chasing.
        if (State == FormationState.BrokenRanks && IsPursuing &&
            (attackTarget == null || attackTarget.soldiers.Count == 0))
            attackTarget = NearestEnemyFormation(
                DominantGroupCount > 0 ? DominantGroupCenter : AnchorPos,
                pursuitRetargetRadius);
    }

    // An engaged formation gradually wheels its canonical facing toward the
    // fight (own engaged soldiers mark the contact surface). AnchorForward is
    // what directional damage reads, so flank/rear charges keep their bonus
    // while the defender turns (~3.5 s for a flank, ~7 s for a full rear turn
    // at the default rate) and then fade — the melting-forever failure case
    // (Rotate south, get hit from the north) resolves itself. Slots wheel
    // with the anchor, which engaged fighters barely feel (tiny slot weight)
    // and rear ranks follow as a controlled reorientation.
    private void UpdateEngagedFacing()
    {
        if (Maneuver == FormationManeuverState.Wheel) return;   // the wheel owns AnchorRot
        if (State != FormationState.Engaged || engagedCount == 0 || !allowDefensivePivot)
        {
            flankThreatTime = 0f;
            return;
        }
        Vector3 to = engagedCentroid - AnchorPos;
        to.y = 0f;
        if (to.sqrMagnitude < 0.5f) return;   // surrounded/on top: keep current facing
        float angle = Vector3.Angle(AnchorForward, to);
        if (angle < 30f) { flankThreatTime = 0f; return; }   // frontal: already facing it

        // Conditional defensive pivot (Phase 2I): heavily engaged formations
        // are pinned — soldiers defend visually, but the tactical flank stays
        // exposed until the FORMATION has genuinely turned. Lighter contact
        // pivots slowly after a reaction delay. The flank/rear damage bonus
        // reads AnchorForward, so it persists exactly as long as this does.
        float engagedFrac = soldiers.Count > 0 ? (float)engagedCount / soldiers.Count : 0f;
        if (engagedFrac >= heavyEngagePivotBlockFraction) return;

        flankThreatTime += Time.deltaTime;
        if (flankThreatTime < defensivePivotDelay) return;

        float speed = engagedReorientSpeedDeg *
                      (engagedFrac > 0.05f ? lightEngagePivotFactor : 1f);
        Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
        AnchorRot = Quaternion.RotateTowards(AnchorRot, want, speed * Time.deltaTime);
    }

    private void UpdateStateMachine()
    {
        // Morale break (Chunk A) pre-empts every state: whatever the century
        // was doing, it now runs.
        var cfg = MoraleCfg;
        if (morale != null && cfg != null && !IsRouting && morale.Value < cfg.routeThreshold)
        {
            EnterRouting();
            return;
        }

        switch (State)
        {
            case FormationState.Ordered:
            case FormationState.Attacking:
                if (engagedCount > 0)
                {
                    State = FormationState.Engaged;
                    hasDestination = false;
                    noContactTime = 0f;
                }
                break;

            case FormationState.Engaged:
                if (engagedCount == 0)
                {
                    noContactTime += Time.deltaTime;
                    if (noContactTime > 1.5f)
                    {
                        if (attackTarget != null && attackTarget.soldiers.Count > 0)
                        {
                            State = FormationState.Attacking;   // re-close with the target
                        }
                        else
                        {
                            State = FormationState.Ordered;     // auto-close compacts the stragglers
                            attackTarget = null;
                            hasDestination = false;
                        }
                    }
                }
                else noContactTime = 0f;
                break;

            case FormationState.Withdrawing:
                // a completed withdrawal with no remaining contact is over
                if (!hasDestination && engagedCount == 0)
                {
                    State = FormationState.Ordered;
                    CurrentOrderType = OrderType.None;
                }
                break;

            case FormationState.Charging:
                // The rush is a timed commitment, never a settled state: it
                // tips into pursuit once the impact window after first contact
                // is spent, once it ran its whole length without finding
                // anyone, or once the target died with nothing else in reach.
                // It must never relax into the generic Engaged transition.
                if (engagedCount > 0 && firstContactTime < 0f)
                    firstContactTime = Time.time;
                bool impactSpent = firstContactTime >= 0f &&
                                   Time.time - firstContactTime >= chargeImpactWindow;
                bool rushExpired = firstContactTime < 0f &&
                                   Time.time - chargeStartTime >= chargeMaxSeconds;
                bool nothingLeft = (attackTarget == null || attackTarget.soldiers.Count == 0) &&
                                   NearestEnemyFormation(AnchorPos, pursuitRetargetRadius) == null;
                if (impactSpent || rushExpired || nothingLeft) EnterPursuit();
                break;

            case FormationState.Reforming:
                reformTimer += Time.deltaTime;
                // Abort on MEANINGFUL melee contact only (Phase 3E): a
                // configurable fraction of survivors melee-engaged. Projectile
                // hits never set IsEngaged, so arrow fire cannot cancel a
                // reform; one isolated straggler fighting stays under the
                // fraction for any century-sized formation.
                float engagedFrac = soldiers.Count > 0
                    ? (float)engagedCount / soldiers.Count : 0f;
                if (engagedFrac >= reformAbortEngagedFraction)
                {
                    State = FormationState.BrokenRanks;   // same rally anchor
                    CurrentOrderType = OrderType.BreakRanks;
                    lastReformAbortTime = Time.time;      // CanStartReform cooldown
                    break;
                }
                if (FractionNearSlots(1.0f) >= reformCompleteFraction ||
                    reformTimer > reformTimeoutSeconds)
                {
                    State = FormationState.Ordered;
                    CurrentOrderType = OrderType.None;
                }
                break;

            case FormationState.Routing:
                // Rally (Chunk A): morale recovered past rallyThreshold and the
                // shared start rule passes -> the ordinary reform path, around
                // the senior officer still with the main pack.
                if (cfg != null && morale != null && morale.Value > cfg.rallyThreshold &&
                    CanStartReform())
                {
                    morale.RecordRally();
                    RallyAnchor = OfficerRallyPoint();
                    Formation threat = NearestEnemyFormation(RallyAnchor, float.MaxValue);
                    Vector3 face = threat != null ? threat.AnchorPos - RallyAnchor : AnchorForward;
                    face.y = 0f;
                    rallyFacing = face.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(face.normalized, Vector3.up) : AnchorRot;
                    BeginReform();
                }
                break;
        }
        // Broken (BrokenRanks) centuries still reform only on command in Chunk
        // A — the player's Reform button, the AI's TryReformBroken. Chunk B
        // points both at CanStartReform. Routing centuries rally on their own.
    }

    // The charge dissolves into an individual free-for-all on the broken-ranks
    // machinery. Unlike a deliberate break, the standard is NOT planted: the
    // rally seed starts at the pack and the anchor keeps following it until
    // Reform strikes it into the ground.
    private void EnterPursuit()
    {
        State = FormationState.BrokenRanks;
        CurrentOrderType = OrderType.BreakRanks;
        IsPursuing = true;
        hasDestination = false;
        RallyAnchor = DominantGroupCount > 0 ? DominantGroupCenter : AnchorPos;
        rallyFacing = AnchorRot;
    }

    // ---------------- morale (Chunk A) ----------------

    // Created on the first frame the config exists (TotalSpawned is final by
    // then) and ticked only while the battle is Active, so the deployment
    // march never moves morale. With no config BattleSetup has already logged
    // the loud error and morale simply stays off — no default fallback.
    private void UpdateMorale()
    {
        if (morale == null)
        {
            var cfg = MoraleCfg;
            if (cfg == null) return;
            morale = new FormationMorale(cfg, TotalSpawned);
        }
        if (BattleSetup.Instance.Phase != BattlePhase.Active) return;
        morale.Tick(Time.deltaTime, soldiers.Count, engagedCount);
    }

    // The century breaks and runs. Deliberately NOT EnterPursuit: that enters
    // BrokenRanks (soldiers hunt out to acquireRadiusBroken) with IsPursuing
    // (the tight pursuit leash, retargeting onto new victims) — a charge's
    // aftermath, not a flight. Routing men acquire nothing (GetAcquireRadius
    // returns 0), ignore the leash and flee (GetFleeVelocity); the anchor
    // follows the pack (UpdateAnchorMovement). Enemies keep targeting them
    // normally — that is the pursuit.
    private void EnterRouting()
    {
        State = FormationState.Routing;
        CurrentOrderType = OrderType.None;
        IsPursuing = false;
        attackTarget = null;
        hasDestination = false;
        Maneuver = FormationManeuverState.None;
        hasFleeThreat = false;
        fleeThreatTimer = 0f;
        foreach (var s in soldiers) s.DropTarget();
    }

    // THE shared automatic-reform start rule (DECISIONS 2026-09-21): only
    // active melee blocks a reform. Enemies beyond personalEngageRadius and
    // arrow fire never do. After an aborted reform it waits
    // reformRetryCooldown so a contested reform cannot flicker between abort
    // and restart. The 12% abort while Reforming is unchanged. Used by the
    // rally in Chunk A; Chunk B points BrokenRanks auto-reform and
    // EnemyCommander.TryReformBroken at it as well.
    public bool CanStartReform()
    {
        var cfg = MoraleCfg;
        if (cfg == null || soldiers.Count == 0) return false;
        if (Time.time - lastReformAbortTime < cfg.reformRetryCooldown) return false;
        return (float)engagedCount / soldiers.Count <= cfg.reformStartEngagedFraction;
    }

    // Where a rallying century re-forms: on the highest-priority officer still
    // alive IN the main pack, else the pack's center. "Main pack" is the
    // dominant cluster, freshly recomputed, so scattered stragglers cannot
    // drag the point into empty ground. Priorities are TEMPORARY — see
    // TemporaryOfficerRanks.
    private Vector3 OfficerRallyPoint()
    {
        UpdateDominantGroup(force: true, members: dominantMembers);
        Soldier best = null;
        int bestPriority = 0;
        for (int i = 0; i < soldiers.Count && i < dominantMembers.Count; i++)
        {
            if (!dominantMembers[i]) continue;
            int pri = TemporaryOfficerRanks.RallyPriority(soldiers[i].role);
            if (pri > bestPriority) { bestPriority = pri; best = soldiers[i]; }
        }
        Vector3 at = best != null ? best.transform.position
                   : DominantGroupCount > 0 ? DominantGroupCenter : AnchorPos;
        at.y = 0f;
        return at;
    }

    // Which enemy formation a rout runs from: the nearest living one to the
    // main pack, refreshed a couple of times a second (not per soldier).
    private void UpdateFleeThreat()
    {
        if (!IsRouting) return;
        fleeThreatTimer -= Time.deltaTime;
        if (fleeThreatTimer > 0f) return;
        fleeThreatTimer = FleeThreatInterval;
        Vector3 from = DominantGroupCount > 0 ? DominantGroupCenter : AnchorPos;
        Formation threat = NearestEnemyFormation(from, float.MaxValue);
        hasFleeThreat = threat != null;
        if (hasFleeThreat)
            fleeThreatPos = threat.DominantGroupCount > 0 ? threat.DominantGroupCenter
                                                          : threat.AnchorPos;
    }

    // A routing soldier's desired velocity: away from the threat formation,
    // bent toward this army's own back edge by fleeBackBias, at moveSpeed *
    // fleeSpeedMultiplier. At the field edge the outward component is dropped:
    // routers stop there (or slide along it) and never leave the map.
    public Vector3 GetFleeVelocity(Vector3 pos, float moveSpeed)
    {
        var cfg = MoraleCfg;
        var bs = BattleSetup.Instance;
        if (cfg == null || bs == null) return Vector3.zero;

        Vector3 back = team == Team.Blue ? Vector3.back : Vector3.forward;   // Blue deploys south
        Vector3 away = hasFleeThreat ? pos - fleeThreatPos : back;
        away.y = 0f;
        Vector3 dir = (away.sqrMagnitude > 0.01f ? away.normalized : back) + back * cfg.fleeBackBias;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = back;
        Vector3 v = dir.normalized * moveSpeed * cfg.fleeSpeedMultiplier;

        float hx = bs.fieldHalfX - FleeEdgeInset;
        float hz = bs.fieldHalfZ - FleeEdgeInset;
        if ((pos.x >= hx && v.x > 0f) || (pos.x <= -hx && v.x < 0f)) v.x = 0f;
        if ((pos.z >= hz && v.z > 0f) || (pos.z <= -hz && v.z < 0f)) v.z = 0f;
        return v;
    }

    // The hard stop behind GetFleeVelocity's soft one. Separation pushes and
    // collider contacts act after the flee velocity is chosen, so a packed
    // crowd at the edge can still shove a router outward; this runs last in
    // the soldier's physics step, puts anyone past the line back on it and
    // cancels the outward velocity.
    public void KeepRouterInField(Rigidbody rb)
    {
        var bs = BattleSetup.Instance;
        if (bs == null) return;
        float hx = bs.fieldHalfX - FleeEdgeInset;
        float hz = bs.fieldHalfZ - FleeEdgeInset;
        Vector3 p = rb.position;
        Vector3 v = rb.linearVelocity;
        bool moved = false;
        if (p.x > hx)  { p.x = hx;  if (v.x > 0f) v.x = 0f; moved = true; }
        if (p.x < -hx) { p.x = -hx; if (v.x < 0f) v.x = 0f; moved = true; }
        if (p.z > hz)  { p.z = hz;  if (v.z > 0f) v.z = 0f; moved = true; }
        if (p.z < -hz) { p.z = -hz; if (v.z < 0f) v.z = 0f; moved = true; }
        if (!moved) return;
        rb.position = p;
        rb.linearVelocity = v;
    }
}
