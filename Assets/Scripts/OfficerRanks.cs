using System.Collections.Generic;
using UnityEngine;

// Officer grades per SoldierRole (Chunk D). A separate ScriptableObject from
// MoraleConfig because grades are command structure, not morale. Grades are
// open-ended integers: higher outranks lower, 0 is a line soldier, and grades
// 5+ are reserved for army-level officers — nothing here assumes a maximum.
// Future factions map their own role names onto the same grade numbers.
[CreateAssetMenu(fileName = "OfficerRanks", menuName = "Ancient Armies/Officer Ranks")]
public class OfficerRanks : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public SoldierRole role;
        [Tooltip("0 = line soldier. Higher outranks lower; succession is implicit")]
        public int grade;
        [Tooltip("Shock debt added to the formation when this officer is killed (not when he flees)")]
        public float deathShock;
        [Tooltip("Spawn health = unit maxHealth x this (0 is treated as 1). Officers should die in a real fight, not in the first seconds of contact")]
        public float healthMultiplier;
    }

    [Tooltip("Romans formed on the standard: reform anchors on the Signifer when he is alive in the main pack, before the highest grade")]
    public bool reformOnStandardFirst = true;
    public Entry[] entries = DefaultEntries();

    private Dictionary<SoldierRole, Entry> lookup;

    public int Grade(SoldierRole role) => Find(role).grade;
    public float DeathShock(SoldierRole role) => Find(role).deathShock;
    public bool IsOfficer(SoldierRole role) => Grade(role) > 0;
    public float HealthMultiplier(SoldierRole role) { float m = Find(role).healthMultiplier; return m > 0f ? m : 1f; }

    private Entry Find(SoldierRole role)
    {
        if (lookup == null || lookup.Count != entries.Length)
        {
            lookup = new Dictionary<SoldierRole, Entry>();
            foreach (var e in entries) lookup[e.role] = e;
        }
        return lookup.TryGetValue(role, out var found) ? found : default;
    }

    private void OnValidate() => lookup = null;

    public static Entry[] DefaultEntries() => new[]
    {
        new Entry { role = SoldierRole.Legionary,   grade = 0, deathShock = 0f, healthMultiplier = 1f },
        new Entry { role = SoldierRole.Archer,      grade = 0, deathShock = 0f, healthMultiplier = 1f },
        new Entry { role = SoldierRole.Tesserarius, grade = 1, deathShock = 4f, healthMultiplier = 1f },
        new Entry { role = SoldierRole.Cornicen,    grade = 1, deathShock = 4f, healthMultiplier = 1f },
        new Entry { role = SoldierRole.Signifer,    grade = 2, deathShock = 28f, healthMultiplier = 2f },
        new Entry { role = SoldierRole.Optio,       grade = 3, deathShock = 12f, healthMultiplier = 1.5f },
        new Entry { role = SoldierRole.Centurion,   grade = 4, deathShock = 24f, healthMultiplier = 2f },
    };
}
