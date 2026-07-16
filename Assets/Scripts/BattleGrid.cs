using System.Collections.Generic;
using UnityEngine;

// Uniform spatial hash grid over the battlefield XZ plane, one grid per team.
//
// Why: the per-team soldier registries in BattleSetup are scanned brute-force
// by every soldier (nearest-enemy acquisition, formation engagement scans,
// friendly separation). At 1,280 soldiers that is O(n^2) — ~1.6M pair tests
// per pass — which does not fit a mobile frame budget. A grid makes each
// query O(cells touched * occupancy): a soldier looking 6 m around itself
// visits ~9-25 cells holding a handful of soldiers each, so the whole army's
// queries stay roughly linear in soldier count.
//
// Cost model:
//   Rebuild  — O(n): one cell hash + linked-list push per living soldier.
//              Allocation-free after warmup (grow-only arrays, reused
//              dictionaries); expected to run every FixedUpdate, or on a
//              short interval chosen by BattleSetup. Query results are
//              as-of the last Rebuild: at ~3.2 m/s move speed a 0.1 s
//              interval means at most ~0.3 m of positional staleness, so
//              callers should not rely on sub-cell precision between
//              rebuilds (the Alive check IS live, so the dead never leak).
//   Queries  — expand outward ring by ring from the query cell up to
//              ceil(radius / CellSize) rings. NearestEnemy early-exits as
//              soon as the best hit is closer than anything the next ring
//              could contain. All distances are horizontal (y ignored).
//
// Storage: per team, a Dictionary<long,int> maps cell key -> head index of
// an intrusive singly-linked list threaded through parallel int[] / data
// arrays (indices, not nodes), so a full rebuild is just Clear() + writes —
// zero garbage regardless of soldier count. Main thread only.
public static class BattleGrid
{
    // ~2-4 soldiers per cell at melee density (1.15 m spacing), and typical
    // query radii (strike ~2 m, separation ~1 m, engagement ~6 m) resolve in
    // 1-2 rings. Bigger cells scan too many soldiers per cell; smaller cells
    // scan too many cells per query.
    private const float CellSize = 4f;
    private const float InvCellSize = 1f / CellSize;

    // Grow-only capacity, doubled on demand. Initial size covers the target
    // scenario (1,280 soldiers = 640/team) without any growth.
    private const int InitialCapacity = 1024;

    private sealed class TeamGrid
    {
        public readonly Dictionary<long, int> heads = new Dictionary<long, int>(256);
        public Soldier[] soldiers = new Soldier[InitialCapacity];
        public Vector3[] positions = new Vector3[InitialCapacity];  // cached at Rebuild
        public int[] next = new int[InitialCapacity];               // intrusive list links
        public int count;

        public void Clear()
        {
            heads.Clear();
            count = 0;
        }

        public void Add(Soldier s)
        {
            if (count == soldiers.Length) Grow();
            Vector3 p = s.transform.position;
            soldiers[count] = s;
            positions[count] = p;
            long key = KeyOf(CellCoord(p.x), CellCoord(p.z));
            next[count] = heads.TryGetValue(key, out int head) ? head : -1;
            heads[key] = count;
            count++;
        }

        private void Grow()
        {
            int cap = soldiers.Length * 2;
            System.Array.Resize(ref soldiers, cap);
            System.Array.Resize(ref positions, cap);
            System.Array.Resize(ref next, cap);
        }
    }

    private static readonly TeamGrid blueGrid = new TeamGrid();
    private static readonly TeamGrid redGrid = new TeamGrid();

    private static TeamGrid GridOf(Team t) => t == Team.Blue ? blueGrid : redGrid;

    private static int CellCoord(float v) => Mathf.FloorToInt(v * InvCellSize);

    private static long KeyOf(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

    // Rebuild both team grids from the live registries. Call every
    // FixedUpdate (or on a short interval) from BattleSetup, before any
    // soldier queries run that tick.
    public static void Rebuild(List<Soldier> blue, List<Soldier> red)
    {
        RebuildTeam(blueGrid, blue);
        RebuildTeam(redGrid, red);
    }

    private static void RebuildTeam(TeamGrid grid, List<Soldier> soldiers)
    {
        grid.Clear();
        if (soldiers == null) return;
        for (int i = 0; i < soldiers.Count; i++)
        {
            Soldier s = soldiers[i];
            if (s == null || !s.Alive) continue;   // registry holds only the living, but be safe
            grid.Add(s);
        }
    }

    // Nearest living enemy of myTeam within maxRadius of pos (XZ distance),
    // or null (dist = float.MaxValue). Early-exits once the best hit is
    // provably closer than anything an unvisited ring could contain.
    public static Soldier NearestEnemy(Vector3 pos, Team myTeam, float maxRadius, out float dist)
    {
        TeamGrid grid = GridOf(myTeam == Team.Blue ? Team.Red : Team.Blue);
        dist = float.MaxValue;
        Soldier best = null;
        if (grid.count == 0 || maxRadius <= 0f) return null;

        int cx = CellCoord(pos.x);
        int cz = CellCoord(pos.z);
        float max2 = maxRadius * maxRadius;
        float best2 = max2;
        // A soldier within maxRadius can sit at most this many cells away
        // (the +1 covers pos being at the far edge of its own cell).
        int maxRing = (int)(maxRadius * InvCellSize) + 1;

        for (int ring = 0; ring <= maxRing; ring++)
        {
            // Anything in ring r is at least (r-1) * CellSize away from pos
            // (pos may touch its own cell's boundary), so once the best hit
            // is within that bound no unvisited ring can beat it.
            if (best != null && ring > 1)
            {
                float ringMin = (ring - 1) * CellSize;
                if (best2 <= ringMin * ringMin) break;
            }

            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    // ring perimeter only; ring 0 is the single center cell
                    if (ring > 0 && dx > -ring && dx < ring && dz > -ring && dz < ring)
                        continue;
                    if (!grid.heads.TryGetValue(KeyOf(cx + dx, cz + dz), out int i))
                        continue;
                    for (; i >= 0; i = grid.next[i])
                    {
                        Soldier s = grid.soldiers[i];
                        if (s == null || !s.Alive) continue;
                        Vector3 to = grid.positions[i] - pos;
                        float d2 = to.x * to.x + to.z * to.z;
                        if (d2 < best2)
                        {
                            best2 = d2;
                            best = s;
                        }
                    }
                }
            }
        }

        if (best != null) dist = Mathf.Sqrt(best2);
        return best;
    }

    // Fills results with living enemies of myTeam within radius of pos
    // (XZ distance), up to results.Length; returns the count. Unordered.
    public static int CollectEnemies(Vector3 pos, Team myTeam, float radius, Soldier[] results)
    {
        return Collect(GridOf(myTeam == Team.Blue ? Team.Red : Team.Blue), pos, radius, results);
    }

    // Same, for myTeam's own soldiers. NOTE: the querying soldier is included
    // if it stands within radius of pos — callers filter self (as the old
    // registry loops did with `f == this`).
    public static int CollectFriends(Vector3 pos, Team myTeam, float radius, Soldier[] results)
    {
        return Collect(GridOf(myTeam), pos, radius, results);
    }

    private static int Collect(TeamGrid grid, Vector3 pos, float radius, Soldier[] results)
    {
        int n = 0;
        if (grid.count == 0 || radius <= 0f || results == null || results.Length == 0)
            return 0;

        int cx = CellCoord(pos.x);
        int cz = CellCoord(pos.z);
        float r2 = radius * radius;
        int maxRing = (int)(radius * InvCellSize) + 1;

        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    if (ring > 0 && dx > -ring && dx < ring && dz > -ring && dz < ring)
                        continue;
                    if (!grid.heads.TryGetValue(KeyOf(cx + dx, cz + dz), out int i))
                        continue;
                    for (; i >= 0; i = grid.next[i])
                    {
                        Soldier s = grid.soldiers[i];
                        if (s == null || !s.Alive) continue;
                        Vector3 to = grid.positions[i] - pos;
                        if (to.x * to.x + to.z * to.z >= r2) continue;
                        results[n++] = s;
                        if (n == results.Length) return n;   // caller's buffer is full
                    }
                }
            }
        }
        return n;
    }
}
