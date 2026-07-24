using UnityEngine;

// Presentation-only: the centurion mimes the century's orders. Lives on the
// centurion visual prefab (the editor integration attaches it); listens to
// Formation.OnOrderIssued and fires a Command layer trigger for Move, Charge,
// and Reform. Never any gameplay effect — gameplay does not know he gestures.
public class CenturionCommandGestures : MonoBehaviour
{
    // Anti-spam: one gesture per cooldown; a Move arriving hot on a Charge is
    // dropped outright so the rush gesture is never cut short by a follow-up.
    private const float GestureCooldown = 1.5f;
    private const float ChargePriorityWindow = 0.6f;

    private static readonly int CommandMoveId = Animator.StringToHash("CommandMove");
    private static readonly int CommandChargeId = Animator.StringToHash("CommandCharge");
    private static readonly int CommandReformId = Animator.StringToHash("CommandReform");

    private Soldier soldier;
    private Formation formation;
    private Animator animator;

    // Controller wiring probed once: while the art / Command layer hasn't
    // landed, the triggers don't exist and every gesture is silently skipped.
    private bool hasMove, hasCharge, hasReform;

    private float lastGestureTime = -999f;
    private float lastChargeTime = -999f;

    private void Start()
    {
        animator = GetComponent<Animator>();
        soldier = GetComponentInParent<Soldier>();
        formation = soldier != null ? soldier.formation : null;
        if (animator == null || soldier == null || formation == null)
        {
            enabled = false;
            return;
        }
        foreach (var p in animator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Trigger) continue;
            if (p.nameHash == CommandMoveId) hasMove = true;
            else if (p.nameHash == CommandChargeId) hasCharge = true;
            else if (p.nameHash == CommandReformId) hasReform = true;
        }
        formation.OnOrderIssued += OnOrder;
    }

    private void OnDestroy()
    {
        if (formation != null) formation.OnOrderIssued -= OnOrder;
    }

    private void OnOrder()
    {
        if (soldier == null || !soldier.Alive) return;
        if (soldier.role != SoldierRole.Centurion) return;
        // dormant while imposted (far zoom): the impostor quad can't gesture
        if (animator == null || !animator.isActiveAndEnabled) return;
        // A broken centurion gives no orders — except Reform, which is issued
        // FROM Broken and is exactly the moment he rallies the century.
        OrderType order = formation.CurrentOrderType;
        if (formation.State == FormationState.BrokenRanks &&
            order != OrderType.Reform) return;

        switch (order)
        {
            case OrderType.Move:
                if (Time.time - lastChargeTime < ChargePriorityWindow) return;
                Fire(hasMove, CommandMoveId);
                break;
            case OrderType.Charge:
                if (Fire(hasCharge, CommandChargeId)) lastChargeTime = Time.time;
                break;
            case OrderType.Reform:
                Fire(hasReform, CommandReformId);
                break;
        }
    }

    private bool Fire(bool wired, int triggerId)
    {
        if (!wired) return false;
        if (Time.time - lastGestureTime < GestureCooldown) return false;
        animator.SetTrigger(triggerId);
        lastGestureTime = Time.time;
        return true;
    }
}
