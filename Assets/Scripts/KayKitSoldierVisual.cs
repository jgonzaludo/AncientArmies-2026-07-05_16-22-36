using UnityEngine;

// Presentation only: drives one soldier's KayKit animator from simulation state
// (velocity, formation state, combat events). It never writes gameplay state —
// the single exception is the bow release, which the soldier hands over via
// deferRangedRelease so the arrow leaves on the draw's release moment; a plain
// timer (not an animator state) triggers it, so it still fires if the model is
// asleep (impostor LOD) or culled. Lives on the soldier ROOT, not inside the
// visual prefab, so it keeps running while VisualRoot is inactive.
public class KayKitSoldierVisual : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Idle = Animator.StringToHash("Idle_A");
    private static readonly int Block = Animator.StringToHash("Melee_Blocking");
    private static readonly int Walk = Animator.StringToHash("Walking_A");
    private static readonly int Run = Animator.StringToHash("Running_A");
    private static readonly int Stab = Animator.StringToHash("Melee_1H_Attack_Stab");
    private static readonly int Chop = Animator.StringToHash("Melee_1H_Attack_Chop");
    private static readonly int Slice = Animator.StringToHash("Melee_1H_Attack_Slice_Diagonal");
    private static readonly int BlockHit = Animator.StringToHash("Melee_Block_Hit");
    private static readonly int Hit = Animator.StringToHash("Hit_A");
    private static readonly int Death = Animator.StringToHash("Death_A");
    private static readonly int DeathPose = Animator.StringToHash("Death_A_Pose");
    private static readonly int Cheer = Animator.StringToHash("Cheering");
    private static readonly int Aim = Animator.StringToHash("Ranged_Bow_Aiming_Idle");
    private static readonly int Draw = Animator.StringToHash("Ranged_Bow_Draw");
    private static readonly int Release = Animator.StringToHash("Ranged_Bow_Release");

    // clip coverage counters (read by the scenario runner to confirm every state appears in a battle)
    public static readonly System.Collections.Generic.Dictionary<string, int> PlayCounts = new System.Collections.Generic.Dictionary<string, int>();
    private static readonly System.Collections.Generic.Dictionary<int, string> HashNames = new System.Collections.Generic.Dictionary<int, string>();
    private static string NameOf(int h)
    {
        if (HashNames.Count == 0)
            foreach (var n in new[] { "Idle_A", "Melee_Blocking", "Walking_A", "Running_A", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop", "Melee_1H_Attack_Slice_Diagonal", "Melee_Block_Hit", "Hit_A", "Death_A", "Death_A_Pose", "Cheering", "Ranged_Bow_Aiming_Idle", "Ranged_Bow_Draw", "Ranged_Bow_Release" })
                HashNames[Animator.StringToHash(n)] = n;
        return HashNames.TryGetValue(h, out var r) ? r : h.ToString();
    }

    private Soldier s;
    private KayKitVisualConfig cfg;
    private Animator anim;
    private SkinnedMeshRenderer smr;
    private bool ranged;
    private int current;
    private float oneShotUntil;
    private float releaseAt = -1f;
    private bool dead, cheering, moving, wasImposted;
    private float deathPoseAt = -1f;
    private int lodVersion = -1;
    private float baseSpeed = 1f;
    private float nextVisCheck;
    private bool offscreen;

    public void Init(Soldier soldier, KayKitVisualConfig config, GameObject visualRoot, bool isRanged)
    {
        s = soldier; cfg = config; ranged = isRanged;
        anim = visualRoot.GetComponentInChildren<Animator>(true);
        smr = visualRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        s.suppressFallRotation = true;   // the death clip plays instead
        s.suppressTint = true;           // team colour lives in the material; MPBs would break the SRP Batcher
        s.deferRangedRelease = isRanged;
        s.deferMeleeImpact = !isRanged;
        s.OnAttack += OnAttack;
        s.OnHurt += OnHurt;
        s.OnDeath += OnDeath;
        nextVisCheck = Random.value * 0.25f;
        // a random start offset so a century never moves in lockstep
        Play(isRanged ? Aim : Idle, 0f, Random.value * 1.0f);
    }

    private void OnDestroy()
    {
        if (s == null) return;
        s.OnAttack -= OnAttack; s.OnHurt -= OnHurt; s.OnDeath -= OnDeath;
    }

    private void Play(int state, float fade, float offsetSeconds = 0f)
    {
        current = state;
        { string nm = NameOf(state); PlayCounts[nm] = PlayCounts.TryGetValue(nm, out int pc) ? pc + 1 : 1; }
        if (anim == null || s.Imposted || offscreen) return;
        if (fade <= 0f) anim.Play(state, 0, 0f); 
        else anim.CrossFadeInFixedTime(state, fade, 0, offsetSeconds);
        if (fade <= 0f && offsetSeconds > 0f) anim.Play(state, 0, offsetSeconds % 1f);
    }

    private void OnAttack()
    {
        if (dead) return;
        if (ranged)
        {
            Play(Draw, cfg.crossFade);
            oneShotUntil = Time.time + cfg.bowDrawSeconds + 0.5f;
            releaseAt = Time.time + cfg.bowDrawSeconds;
        }
        else
        {
            int v = s.MeleeAttackVariant;
            Play(v == 0 ? Stab : v == 1 ? Chop : Slice, cfg.crossFade * 0.6f);
            oneShotUntil = Time.time + 0.8f;
        }
    }

    private void OnHurt()
    {
        if (dead || Time.time < oneShotUntil - 0.35f && !ranged && current != Hit && current != BlockHit) { }
        if (dead) return;
        bool formed = s.formation.State == FormationState.Ordered || s.formation.State == FormationState.Engaged || s.formation.State == FormationState.Attacking;
        bool block = !ranged && formed && s.LastHitZone == DamageZone.Front && !s.LastHitRanged;
        Play(block ? BlockHit : Hit, cfg.crossFade * 0.5f);
        oneShotUntil = Time.time + 0.45f;
        releaseAt = -1f > 0 ? releaseAt : releaseAt;   // a drawn bow is still released by the soldier itself
    }

    private void OnDeath()
    {
        dead = true; releaseAt = -1f;
        Play(Death, cfg.crossFade * 0.5f);
        deathPoseAt = Time.time + 0.75f;
    }

    private void Update()
    {
        if (s == null) return;
        float t = Time.time;

        // timer-driven bow release: independent of the animator
        if (releaseAt > 0f && t >= releaseAt)
        {
            releaseAt = -1f;
            if (!dead) { s.ReleasePendingShot(); Play(Release, cfg.crossFade * 0.5f); }
        }
        if (dead)
        {
            if (deathPoseAt > 0f && t >= deathPoseAt) { deathPoseAt = -1f; Play(DeathPose, 0.1f); }
            return;
        }

        // victory cheer for the winning side
        var bs = BattleSetup.Instance;
        if (!cheering && bs != null && bs.Phase == BattlePhase.Ended &&
            bs.ResultText.StartsWith(s.formation.team.ToString().ToUpper() + " WINS"))
        { cheering = true; Play(Cheer, 0.25f, Random.value * 1.0f); return; }
        if (cheering) return;

        // visibility "load": men outside the view (plus margin) stop animating
        if (t >= nextVisCheck)
        {
            nextVisCheck = t + 0.25f;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                bool off = vp.x < -0.12f || vp.x > 1.12f || vp.y < -0.2f || vp.y > 1.2f || vp.z < 0f;
                if (off != offscreen)
                {
                    offscreen = off;
                    if (smr != null) smr.enabled = !off && !ThinnedOut();
                    if (anim != null && !s.Imposted) { anim.enabled = !off; if (!off) { anim.Update(0f); Play(current, 0f, Random.value); } }
                }
            }
        }

        if (s.Imposted) { wasImposted = true; return; }
        if (wasImposted) { wasImposted = false; lodVersion = -1; Play(current, 0f, Random.value); }

        // zoom tier: mesh LOD, animator stride, thinning
        KayKitCrowdLod.Refresh();
        if (lodVersion != KayKitCrowdLod.Version && smr != null)
        {
            lodVersion = KayKitCrowdLod.Version;
            smr.forceMeshLod = (short)KayKitCrowdLod.MeshLod;
            if (!offscreen) smr.enabled = !ThinnedOut();
        }
        if (anim != null && !offscreen)
        {
            int stride = KayKitCrowdLod.AnimStride;
            bool tick = stride <= 1 || ((Time.frameCount + s.slotIndex) % stride) == 0;
            anim.enabled = tick;
            anim.speed = baseSpeed * stride;   // a skipped frame advances the clock by the whole stride
        }

        if (t < oneShotUntil) return;   // an attack/hit clip is playing

        // locomotion
        Vector3 v = s.Velocity; v.y = 0f;
        float sp = v.magnitude;
        if (moving) { if (sp < 0.2f) moving = false; } else if (sp > 0.45f) moving = true;
        var st = s.formation.State;
        int want;
        if (moving)
        {
            bool run = st == FormationState.Charging || st == FormationState.Routing || sp > 3.0f;
            want = run ? Run : Walk;
            baseSpeed = Mathf.Clamp(sp / (run ? cfg.runReferenceSpeed : cfg.walkReferenceSpeed), 0.6f, 1.6f);
            if (anim != null && anim.isActiveAndEnabled) anim.SetFloat(Speed, 1f);
        }
        else
        {
            baseSpeed = 1f;
            want = ranged ? Aim : (st == FormationState.Engaged && s.IsEngaged ? Block : Idle);
        }
        if (want != current) Play(want, cfg.crossFade);
    }

    private bool ThinnedOut()
    {
        int m = KayKitCrowdLod.ThinMod;
        return m > 0 && (s.slotIndex % m) == 1;
    }
}
