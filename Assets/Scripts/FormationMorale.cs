using System.Collections.Generic;
using UnityEngine;

// Where a formation's morale pressure came from; totals feed the rout-cause
// breakdown. Grind is base morale pushed down by the loss ceiling; Outnumber is
// the decay forgone while several enemy formations are in contact (approximate).
public enum ShockSource { Grind, FrontCasualty, Burst, Flank, Rear, Arrow, Surround, Outnumber, Contagion, Officer }

// One formation's morale (DECISIONS 2026-09-21). Pure math: Formation feeds it
// casualties, the alive count and the melee situation, and reads Value back.
// It never changes formation state — Formation owns the rout and rally.
//
// Reported morale = Base - ShockDebt, clamped to 0-100.
//   Base      — capped by a ceiling built from cumulative losses against
//               STARTING strength; only recovery raises it. Deliberately
//               gentle: a frontal grind alone routs only at very heavy losses.
//   ShockDebt — panic, the main road to a rout: being surrounded, sudden
//               massacres, flank and rear kills, contagion, officer deaths.
//               Everything arrives through AddShock and decays at all times,
//               slower while more than one enemy formation is in contact.
//
// "In melee" is Formation's existing engagedCount > 0: an enemy within
// personalEngageRadius (3 m) of any soldier. Arrows never make it true.
public class FormationMorale
{
    private readonly MoraleConfig cfg;
    private readonly int startingStrength;

    public float Base { get; private set; } = 100f;
    public float ShockDebt { get; private set; }
    public float QueuedShock { get; private set; }   // panic waiting behind the per-second cap
    public float Value => Mathf.Clamp(Base - ShockDebt, 0f, 100f);
    public float LossFraction { get; private set; }
    public bool InMelee { get; private set; }
    public int EnemyFormationsEngaged { get; private set; }
    public int EngagedZones { get; private set; }

    public float Ceiling => InMelee ? ContactCeiling : RestCeiling;
    public float RestCeiling => 100f - LossFraction * 100f * cfg.restCeilingScale;
    public float ContactCeiling =>
        100f - Mathf.Max(0f, LossFraction - lossAtRally * cfg.rallyLossForgiveness)
               * 100f * cfg.contactCeilingScale;

    private readonly float[] sourceTotals = new float[System.Enum.GetValues(typeof(ShockSource)).Length];
    private readonly Queue<float> recentCasualties = new Queue<float>();   // sim times inside burstWindow
    private float clock;                    // simulation seconds this morale has been ticking
    private float lossAtRally;              // loss fraction when this formation last rallied
    private float sinceCasualty = 1e6f;     // seconds since the last casualty
    private float tickElapsed;

    public FormationMorale(MoraleConfig cfg, int startingStrength)
    {
        this.cfg = cfg;
        this.startingStrength = Mathf.Max(1, startingStrength);
    }

    public float SourceTotal(ShockSource s) => sourceTotals[(int)s];

    // The largest contributor so far — read at the moment of a rout.
    public ShockSource PrimaryCause
    {
        get
        {
            int best = 0;
            for (int i = 1; i < sourceTotals.Length; i++) if (sourceTotals[i] > sourceTotals[best]) best = i;
            return (ShockSource)best;
        }
    }

    // The one door for sudden pressure. scaled: multiplied by panicScale
    // (everything except officer deaths and surround). capped: rate-limited by
    // maxShockPerSecond — the excess queues and lands on later ticks, never
    // discarded (casualties and contagion; the cap smooths single-tick spikes,
    // it must not blunt sustained encirclement or an officer's death).
    public void AddShock(float amount, ShockSource source, bool scaled = true, bool capped = true)
    {
        if (amount <= 0f) return;
        if (scaled) amount *= cfg.panicScale;
        sourceTotals[(int)source] += amount;
        if (capped && cfg.enableShockCap) QueuedShock += amount;
        else ShockDebt += amount;
    }

    // A soldier of this formation was killed. Weight by the zone of the blow
    // (steady frontal losses are muted), or by the burst rule when more than
    // burstThreshold of starting strength has fallen inside burstWindow —
    // whichever is higher. A sudden massacre panics; a grind does not.
    public void RecordCasualty(DamageZone zone, bool ranged)
    {
        sinceCasualty = 0f;
        while (recentCasualties.Count > 0 && clock - recentCasualties.Peek() > cfg.burstWindow) recentCasualties.Dequeue();
        recentCasualties.Enqueue(clock);

        float weight; ShockSource source;
        if (ranged && cfg.enableArrowShockWeight) { weight = cfg.arrowShockWeight; source = ShockSource.Arrow; }
        else if (zone == DamageZone.Rear && cfg.enableFlankShock) { weight = cfg.rearShockMultiplier; source = ShockSource.Rear; }
        else if (zone == DamageZone.Flank && cfg.enableFlankShock) { weight = cfg.flankShockMultiplier; source = ShockSource.Flank; }
        else { weight = cfg.frontShockWeight; source = ranged ? ShockSource.Arrow : ShockSource.FrontCasualty; }

        bool burst = recentCasualties.Count > cfg.burstThreshold * startingStrength;
        if (burst && cfg.burstShockWeight > weight) { weight = cfg.burstShockWeight; source = ShockSource.Burst; }

        AddShock(weight / startingStrength * 100f * cfg.shockMultiplier, source);
    }

    // A routing formation rallied: part of the losses so far stop counting
    // against the CONTACT ceiling, so a rallied century can fight again.
    public void RecordRally() => lossAtRally = LossFraction;

    // Called every frame; the math runs on the throttled tickInterval and
    // spends the whole elapsed time on decay and recovery.
    // engagedZones: distinct attack zones (front, left flank, right flank,
    // rear) that currently hold engaged enemies.
    public void Tick(float dt, int alive, int engagedCount, int enemyFormationsEngaged, int engagedZones)
    {
        clock += dt;
        tickElapsed += dt;
        if (tickElapsed < cfg.tickInterval) return;
        float step = tickElapsed;
        tickElapsed = 0f;

        LossFraction = 1f - Mathf.Clamp01((float)alive / startingStrength);
        InMelee = engagedCount > 0;
        EnemyFormationsEngaged = enemyFormationsEngaged;
        EngagedZones = engagedZones;

        // decay first (slower while outnumbered)
        float decay = cfg.shockDecayPerSecond;
        if (cfg.enableOutnumberShock && enemyFormationsEngaged > 1)
        {
            float slowed = decay / (1f + cfg.outnumberDecaySlowdown * (enemyFormationsEngaged - 1));
            sourceTotals[(int)ShockSource.Outnumber] += Mathf.Min(ShockDebt, decay * step) - Mathf.Min(ShockDebt, slowed * step);
            decay = slowed;
        }
        ShockDebt = Mathf.Max(0f, ShockDebt - decay * step);

        // surrounded: sustained pressure for every zone beyond the first
        if (cfg.enableSurroundShock && engagedZones > 1)
            AddShock(cfg.surroundShockPerSecond * (engagedZones - 1) * step, ShockSource.Surround, scaled: false, capped: false);

        if (QueuedShock > 0f)
        {
            float land = cfg.enableShockCap ? Mathf.Min(QueuedShock, cfg.maxShockPerSecond * step) : QueuedShock;
            ShockDebt += land;
            QueuedShock -= land;
        }
        if (sinceCasualty < 1e5f) sinceCasualty += step;

        // base: recovers only out of melee and after the post-casualty pause,
        // and never above the ceiling for the current situation
        float before = Base;
        if (!InMelee && sinceCasualty >= cfg.recoveryLossCooldown)
            Base += cfg.recoveryPerSecond * step;
        Base = Mathf.Clamp(Mathf.Min(Base, Ceiling), 0f, 100f);
        // grind = only a NET fall forced by the ceiling (recovery that the
        // ceiling merely refuses is not pressure)
        if (Base < before) sourceTotals[(int)ShockSource.Grind] += before - Base;
    }
}
