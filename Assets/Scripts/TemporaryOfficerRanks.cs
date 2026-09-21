// ============================================================================
// TEMPORARY (Chunk A). Hardcoded officer orderings until the proper officer
// grade system arrives in a later chunk — replace this whole file then. No
// grade data, no ScriptableObject, no morale effects hang off these numbers.
// ============================================================================
public static class TemporaryOfficerRanks
{
    // Who a rallying century gathers around: the highest-priority officer
    // still alive in the main pack. 0 = not an officer.
    public static int RallyPriority(SoldierRole role)
    {
        switch (role)
        {
            case SoldierRole.Signifer:    return 5;
            case SoldierRole.Centurion:   return 4;
            case SoldierRole.Optio:       return 3;
            case SoldierRole.Tesserarius: return 2;
            case SoldierRole.Cornicen:    return 1;
            default:                      return 0;
        }
    }

    // Grade number drawn over an officer's head by the debug morale overlay.
    // 0 = line soldier, draw nothing.
    public static int DebugGrade(SoldierRole role)
    {
        switch (role)
        {
            case SoldierRole.Centurion:   return 4;
            case SoldierRole.Optio:       return 3;
            case SoldierRole.Signifer:    return 2;
            case SoldierRole.Tesserarius: return 1;
            case SoldierRole.Cornicen:    return 1;
            default:                      return 0;
        }
    }
}
