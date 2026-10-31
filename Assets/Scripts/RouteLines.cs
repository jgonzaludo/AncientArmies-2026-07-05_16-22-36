using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Path display for the player's own moving formations. Every friendly unit
// with a pending move or attack order shows one quiet line (enemy routes are
// never drawn); the selected ones draw brighter and thicker. Units heading to
// the same place from the same area share ONE arrow, centroid to destination
// centroid, so a group order reads as a single arrow. The remaining route is
// drawn, so the line trims as the unit walks and vanishes on arrival.
// Presentation only: reads Formation, never writes it.
public class RouteLines : MonoBehaviour
{
    private const float GroupStartRadius = 40f;   // anchors this close may share an arrow
    private const float GroupDestRadius = 25f;    // and so may destinations
    private const float LineY = 0.4f;   // well above the ground and decor, so the line never z-fights or breaks up

    private Material quietMove, brightMove, quietAttack, brightAttack;
    private readonly List<LineRenderer> pool = new List<LineRenderer>();
    private readonly List<Formation> moving = new List<Formation>();
    private readonly List<int> groupOf = new List<int>();
    private readonly List<Vector3> scratch = new List<Vector3>();
    private AnimationCurve arrowCurve;

    private void Awake()
    {
        quietMove = BattleVisuals.TransparentUnlit(new Color(1f, 1f, 1f, 0.4f));
        brightMove = BattleVisuals.TransparentUnlit(new Color(1f, 1f, 1f, 0.85f));
        quietAttack = BattleVisuals.TransparentUnlit(new Color(1f, 0.3f, 0.25f, 0.42f));
        brightAttack = BattleVisuals.TransparentUnlit(new Color(1f, 0.3f, 0.25f, 0.9f));
        arrowCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.9f, 1f), new Keyframe(0.905f, 3.2f), new Keyframe(1f, 0f));
    }

    private static bool IsTravelling(Formation f)
    {
        if (f.soldiers.Count == 0 || f.IsRouting || !f.HasMoveDestination) return false;
        if (f.State == FormationState.Ordered) return f.CurrentOrderType == OrderType.Move;
        return f.State == FormationState.Attacking && f.attackTarget != null;
    }

    private void LateUpdate()
    {
        var bs = BattleSetup.Instance;
        int used = 0;
        if (bs != null && bs.Phase != BattlePhase.Ended)
        {
            moving.Clear();
            foreach (var f in bs.formations) if (f.team == Team.Blue && IsTravelling(f)) moving.Add(f);
            Group();
            int groups = 0; foreach (int g in groupOf) groups = Mathf.Max(groups, g + 1);
            for (int g = 0; g < groups; g++) used = DrawGroup(g, used);
        }
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }

    // Grouping is fixed by the ORDER, not by where units drift to: units share an arrow only
    // when they were ordered together (same command batch) toward the same place. Stable
    // while they march, so arrows never split, merge or jump.
    private bool SameKind(Formation a, Formation b)
    {
        bool aa = a.State == FormationState.Attacking, ab = b.State == FormationState.Attacking;
        if (aa != ab) return false;
        if (aa && a.attackTarget != b.attackTarget) return false;
        if (Mathf.Abs(a.OrderTime - b.OrderTime) > 0.3f) return false;
        Vector3 d = a.CurrentDestination - b.CurrentDestination; d.y = 0f;
        return d.magnitude < GroupDestRadius;
    }

    // simple single-link grouping
    private void Group()
    {
        groupOf.Clear();
        for (int i = 0; i < moving.Count; i++) groupOf.Add(-1);
        int next = 0;
        for (int i = 0; i < moving.Count; i++)
        {
            if (groupOf[i] >= 0) continue;
            groupOf[i] = next;
            for (bool grew = true; grew;)
            {
                grew = false;
                for (int a = 0; a < moving.Count; a++)
                {
                    if (groupOf[a] != next) continue;
                    for (int b = 0; b < moving.Count; b++)
                        if (groupOf[b] < 0 && SameKind(moving[a], moving[b])) { groupOf[b] = next; grew = true; }
                }
            }
            next++;
        }
    }

    private int DrawGroup(int g, int used)
    {
        Vector3 start = Vector3.zero, end = Vector3.zero; int n = 0; bool selected = false, attack = false;
        Formation lead = null;   // the first member in the stable formation order: never changes while the group lives
        for (int i = 0; i < moving.Count; i++)
            if (groupOf[i] == g)
            {
                if (lead == null) lead = moving[i];
                start += moving[i].AnchorPos; end += moving[i].CurrentDestination; n++;
                selected |= moving[i].IsSelected; attack |= moving[i].State == FormationState.Attacking;
            }
        if (n == 0) return used;
        start /= n; end /= n;
        lead.GetRemainingRoute(scratch);
        if (attack && lead.attackTarget != null) end = lead.attackTarget.AnchorPos;
        // a group's line keeps the lead's route shape, carried to the group centre
        Vector3 from = n == 1 ? lead.AnchorPos : start;
        Vector3 shift = from - lead.AnchorPos;
        for (int i = 0; i < scratch.Count - 1; i++) scratch[i] += shift;
        if (scratch.Count > 0) scratch[scratch.Count - 1] = end; else scratch.Add(end);
        if ((scratch[scratch.Count - 1] - from).sqrMagnitude < 4f) return used;   // essentially arrived

        LineRenderer lr = Get(used);
        lr.positionCount = scratch.Count + 1;
        lr.SetPosition(0, new Vector3(from.x, LineY, from.z));
        for (int i = 0; i < scratch.Count; i++) lr.SetPosition(i + 1, new Vector3(scratch[i].x, LineY, scratch[i].z));
        lr.widthMultiplier = selected ? 0.8f : 0.5f;
        lr.sharedMaterial = attack ? (selected ? brightAttack : quietAttack) : (selected ? brightMove : quietMove);
        lr.gameObject.SetActive(true);
        return used + 1;
    }

    private LineRenderer Get(int i)
    {
        while (pool.Count <= i)
        {
            var go = new GameObject("RouteLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.widthCurve = arrowCurve;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 3;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            pool.Add(lr);
        }
        return pool[i];
    }
}
