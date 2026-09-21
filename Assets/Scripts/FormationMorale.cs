using UnityEngine;

// One formation's morale (DECISIONS 2026-09-21). Pure math: Formation feeds it
// the alive count and engagedCount each frame and reads Value back. It never
// changes formation state — Formation owns the rout and rally transitions.
//
// Reported morale = Base - ShockDebt, clamped to 0-100.
//   Base      — capped by a ceiling built from cumulative losses against
//               STARTING strength; only recovery raises it. In melee the
//               stricter contact ceiling applies, so a slow bleed still routs.
//   ShockDebt — sudden pressure. Casualties (melee or arrows) add to it and it
//               decays at all times, in melee too, so losing ten men at once
//               hurts far more than losing them over half a minute.
//
// "In melee" is Formation's existing engagedCount > 0: an enemy within
// personalEngageRadius (3 m) of any soldier. Arrows never make it true.
public class FormationMorale
{
    private readonly MoraleConfig cfg;
    private readonly int startingStrength;

    public float Base { get; private set; } = 100f;
    public float ShockDebt { get; private set; }
    public float Value => Mathf.Clamp(Base - ShockDebt, 0f, 100f);
    public float LossFraction { get; private set; }
    public bool InMelee { get; private set; }

    public float Ceiling => InMelee ? ContactCeiling : RestCeiling;
    public float RestCeiling => 100f - LossFraction * 100f * cfg.restCeilingScale;
    public float ContactCeiling =>
        100f - Mathf.Max(0f, LossFraction - lossAtRally * cfg.rallyLossForgiveness)
               * 100f * cfg.contactCeilingScale;

    private int lastAlive;
    private float lossAtRally;              // loss fraction when this formation last rallied
    private float sinceCasualty = 1e6f;     // seconds since the last casualty
    private float tickElapsed;

    public FormationMorale(MoraleConfig cfg, int startingStrength)
    {
        this.cfg = cfg;
        this.startingStrength = Mathf.Max(1, startingStrength);
        lastAlive = startingStrength;
    }

    // The one door for sudden pressure. Casualties use it today; flank
    // exposure, rout contagion and officer deaths add debt here in later
    // chunks.
    public void AddShock(float amount)
    {
        if (amount > 0f) ShockDebt += amount;
    }

    // A routing formation rallied: part of the losses so far stop counting
    // against the CONTACT ceiling, so a rallied century can fight again. The
    // rest ceiling keeps counting every loss.
    public void RecordRally() => lossAtRally = LossFraction;

    // Called every frame; the math runs on the throttled tickInterval and
    // spends the whole elapsed time on decay and recovery.
    public void Tick(float dt, int alive, int engagedCount)
    {
        tickElapsed += dt;
        if (tickElapsed < cfg.tickInterval) return;
        float step = tickElapsed;
        tickElapsed = 0f;

        LossFraction = 1f - Mathf.Clamp01((float)alive / startingStrength);
        InMelee = engagedCount > 0;

        // shock: decay first, then add this tick's casualties at full weight
        ShockDebt = Mathf.Max(0f, ShockDebt - cfg.shockDecayPerSecond * step);
        int lost = Mathf.Max(0, lastAlive - alive);
        lastAlive = alive;
        if (lost > 0)
        {
            AddShock((float)lost / startingStrength * 100f * cfg.shockMultiplier);
            sinceCasualty = 0f;
        }
        else sinceCasualty += step;

        // base: recovers only out of melee and after the post-casualty pause,
        // and never above the ceiling for the current situation
        if (!InMelee && sinceCasualty >= cfg.recoveryLossCooldown)
            Base += cfg.recoveryPerSecond * step;
        Base = Mathf.Clamp(Mathf.Min(Base, Ceiling), 0f, 100f);
    }
}
