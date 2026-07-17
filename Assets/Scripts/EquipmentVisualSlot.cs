using UnityEngine;

// Visual state of one piece of swappable equipment (dagger, sword, standard,
// horn, ...). Purely presentational: no gameplay ownership, no inventory —
// gameplay stays authoritative for what a soldier can do; this only controls
// which representation is visible.
public enum EquipmentVisualState { Stowed, Drawing, Active, Returning, Hidden }

// One equipment slot with a stowed representation (e.g. sheathed dagger on
// the belt) and an active representation (e.g. dagger prop in the hand).
// Both objects live in the prefab and are toggled — never instantiated or
// destroyed — with references cached at serialization time (no hierarchy
// searches, no per-frame cost; SetState is only called on actual changes).
//
// Visibility semantics: Drawing shows the stowed object (the hand hasn't
// visibly taken the weapon yet) and Returning shows the active object — the
// caller decides the handoff moment (animation event or normalized-time
// threshold) by advancing to Active/Stowed, so the two full weapons are
// never visible together.
public class EquipmentVisualSlot : MonoBehaviour
{
    [Tooltip("Representation shown while the equipment is put away (may be null for equipment with no stowed visual)")]
    [SerializeField] private GameObject stowedObject;
    [Tooltip("Representation shown while the equipment is in use (e.g. a bone-parented hand prop)")]
    [SerializeField] private GameObject activeObject;
    [SerializeField] private EquipmentVisualState defaultState = EquipmentVisualState.Stowed;

    public EquipmentVisualState State { get; private set; }

    private void Awake()
    {
        State = (EquipmentVisualState)(-1);   // force the first apply
        SetState(defaultState);
    }

    public void SetState(EquipmentVisualState state)
    {
        if (state == State) return;
        State = state;
        bool stowedVisible = state == EquipmentVisualState.Stowed ||
                             state == EquipmentVisualState.Drawing;
        bool activeVisible = state == EquipmentVisualState.Active ||
                             state == EquipmentVisualState.Returning;
        if (stowedObject != null && stowedObject.activeSelf != stowedVisible)
            stowedObject.SetActive(stowedVisible);
        if (activeObject != null && activeObject.activeSelf != activeVisible)
            activeObject.SetActive(activeVisible);
    }

#if UNITY_EDITOR
    // Editor wiring helper (prefab build scripts); not used at runtime.
    public void EditorConfigure(GameObject stowed, GameObject active, EquipmentVisualState def)
    {
        stowedObject = stowed;
        activeObject = active;
        defaultState = def;
    }
#endif
}
