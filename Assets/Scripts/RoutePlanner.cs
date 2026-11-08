using System.Collections.Generic;
using UnityEngine;

// Route planning for ONE block per formation (never per soldier). Friendly and
// enemy formations are circular obstacles; a blocked straight leg is bent
// around the first obstacle in its way, choosing the shorter side, to a small
// fixed depth. Enemies get an extra margin so a march that is not an attack
// stays out of contact range. No route found = straight line (the old
// behaviour). Pure function: Formation owns the anchor and follows the result.
public static class RoutePlanner
{
    public const float EnemyMargin = 7f;      // extra clearance around enemy blocks
    public const float FriendMargin = 2f;
    private const int MaxDepth = 4;

    private struct Obstacle { public Vector3 c; public float r; public Formation who; }

    // Waypoints from `from` to `to`, excluding `from`, always ending at `to`.
    // `ignore` (an attack target) is never an obstacle. An obstacle containing
    // the start or the goal is skipped, you cannot route around what you are
    // standing in, or are ordered onto.
    // Clearance tiers (friend margin, enemy margin), widest first. The planner takes
    // the widest tier that still yields a route that is not absurdly long, so
    // blocks keep plenty of room from friends and only brush them when no roomier
    // route exists. The last tier is the tight, old behaviour.
    private static readonly float[,] Tiers = { { 10f, 12f }, { 6f, 8f }, { 3f, 5f }, { 1f, 3f } };
    private const float MaxDetourFactor = 2.6f;
    private const float MaxDetourExtra = 30f;

    // terrain first: a grid route that keeps off water and rock, then the circle planner bends each leg around other
    // formations. a circle detour that would cross blocked ground is dropped for the plain leg. no terrain route at
    // all (or no map) falls back to the old behaviour, so a move order is never refused
    public static List<Vector3> Plan(Formation self, Vector3 from, Vector3 to, Formation ignore, bool keepFromEnemies)
    {
        var map = TerrainMap.Active;
        if (map == null) return PlanCircles(self, from, to, ignore, keepFromEnemies);
        float clr = Mathf.Min(5f, 0.6f * Mathf.Max(self.FootprintHalfExtents.x, self.FootprintHalfExtents.y));
        var grid = new List<Vector3>(8);
        if (!TerrainGridPlanner.TryPlan(from, to, clr, grid, map)) return PlanCircles(self, from, to, ignore, keepFromEnemies);
        var result = new List<Vector3>(8);
        Vector3 a = from;
        foreach (var wp in grid)
        {
            var leg = PlanCircles(self, a, wp, ignore, keepFromEnemies);
            bool ok = true; Vector3 p = a;
            foreach (var q in leg) { if (!TerrainGridPlanner.SegmentClear(p, q, map)) { ok = false; break; } p = q; }
            if (ok) result.AddRange(leg); else result.Add(wp);
            a = wp;
        }
        return result;
    }

    private static List<Vector3> PlanCircles(Formation self, Vector3 from, Vector3 to, Formation ignore, bool keepFromEnemies)
    {
        var result = new List<Vector3>(4);
        var bs = BattleSetup.Instance;
        from.y = to.y = 0f;
        if (bs == null) { result.Add(to); return result; }
        float straight = Vector3.Distance(from, to);
        float limit = straight * MaxDetourFactor + MaxDetourExtra;
        List<Vector3> fallback = null;
        for (int t = 0; t < Tiers.GetLength(0); t++)
        {
            var obs = BuildObstacles(self, from, to, ignore, keepFromEnemies, Tiers[t, 0], Tiers[t, 1], bs);
            if (obs.Count == 0 || Clear(from, to, obs)) { result.Add(to); return result; }   // roomy enough as is
            var path = new List<Vector3>(6);
            if (!ShortestPath(from, to, obs, bs, path)) continue;
            float len = 0f; Vector3 p = from; foreach (var q in path) { len += Vector3.Distance(p, q); p = q; }
            if (len <= limit) return Smooth(from, path, obs, bs);
            if (fallback == null) fallback = Smooth(from, path, obs, bs);   // valid but long: keep as a last resort
        }
        if (fallback != null) return fallback;
        result.Add(to);
        return result;
    }

    // Corner rounding: every interior corner of the shortest path becomes a quadratic curve
    // (tangent points on the two legs, the corner as control point), sampled every few degrees. The
    // rounding is shrunk, up to three times, until the curve clears every obstacle; if it never does
    // the sharp corner stays. The first and last points are untouched, so the route still ends exactly
    // on the order's destination.
    private static List<Vector3> Smooth(Vector3 from, List<Vector3> pts, List<Obstacle> obs, BattleSetup bs)
    {
        var cc = bs.combatConfig;
        if (pts.Count < 2 || (cc != null && !cc.smoothRoutes)) return pts;
        float reach = cc != null ? cc.routeFilletReach : 10f;
        var poly = new List<Vector3>(pts.Count + 1) { from }; poly.AddRange(pts);
        var outp = new List<Vector3>(pts.Count * 6);
        Vector3 last = from;
        for (int i = 1; i < poly.Count; i++)
        {
            if (i == poly.Count - 1) { outp.Add(poly[i]); break; }
            Vector3 P = poly[i], A = poly[i - 1], B = poly[i + 1];
            Vector3 din = P - A, dout = B - P; din.y = dout.y = 0f;
            float lin = din.magnitude, lout = dout.magnitude;
            if (lin < 0.5f || lout < 0.5f) { outp.Add(P); last = P; continue; }
            din /= lin; dout /= lout;
            float turn = Vector3.Angle(din, dout);
            if (turn < 6f) { outp.Add(P); last = P; continue; }
            float t = Mathf.Min(reach, 0.45f * lin, 0.45f * lout);
            bool done = false;
            for (int attempt = 0; attempt < 3 && !done; attempt++, t *= 0.5f)
            {
                Vector3 s0 = P - din * t, e0 = P + dout * t;
                int n = Mathf.Clamp(Mathf.CeilToInt(turn / 12f), 3, 9);
                var arc = new List<Vector3>(n + 1);
                for (int k = 0; k <= n; k++)
                {
                    float u = k / (float)n, v = 1f - u;
                    arc.Add(v * v * s0 + 2f * u * v * P + u * u * e0);
                }
                bool ok = Clear(last, arc[0], obs);
                for (int k = 1; k < arc.Count && ok; k++) ok = Clear(arc[k - 1], arc[k], obs);
                if (!ok) continue;
                outp.AddRange(arc); last = arc[arc.Count - 1]; done = true;
            }
            if (!done) { outp.Add(P); last = P; }
        }
        return outp;
    }

    // Is the remaining route (from `from` through `pts`) still free of obstacles at the
    // tightest clearance? Used to keep a route stable instead of re-planning it into a
    // different shape every second.
    public static bool RouteIsClear(Formation self, Vector3 from, List<Vector3> pts, Formation ignore, bool keepFromEnemies)
    {
        var bs = BattleSetup.Instance;
        if (bs == null || pts.Count == 0) return true;
        var tmap = TerrainMap.Active;
        if (tmap != null) { Vector3 tp = from; foreach (var q in pts) { if (!TerrainGridPlanner.SegmentClear(tp, q, tmap)) return false; tp = q; } }
        var obs = BuildObstacles(self, from, pts[pts.Count - 1], ignore, keepFromEnemies, Tiers[Tiers.GetLength(0) - 1, 0], Tiers[Tiers.GetLength(0) - 1, 1], bs);
        Vector3 a = from;
        foreach (var b in pts) { if (!Clear(a, b, obs)) return false; a = b; }
        return true;
    }

    private static List<Obstacle> BuildObstacles(Formation self, Vector3 from, Vector3 to, Formation ignore,
                                                 bool keepFromEnemies, float friendMargin, float enemyMargin, BattleSetup bs)
    {
        float myR = Mathf.Max(self.FootprintHalfExtents.x, self.FootprintHalfExtents.y) * 0.8f;
        var obs = new List<Obstacle>(bs.formations.Count);
        foreach (var g in bs.formations)
        {
            if (g == self || g == ignore || g.soldiers.Count == 0 || g.IsRouting) continue;
            bool enemy = g.team != self.team;
            if (enemy && !keepFromEnemies) continue;
            float core = Mathf.Max(g.FootprintHalfExtents.x, g.FootprintHalfExtents.y) * 0.8f;
            float gr = core + (enemy ? enemyMargin : friendMargin) + myR * 0.9f;
            Vector3 gc = g.AnchorPos; gc.y = 0f;
            // skip only when the start/goal is inside the block itself, not its margin
            if ((gc - from).sqrMagnitude < core * core || (gc - to).sqrMagnitude < core * core) continue;
            // an endpoint inside the margin ring (not the block) must stay reachable:
            // shrink this obstacle just enough that start and goal lie outside it
            float dEnd = Mathf.Min((gc - from).magnitude, (gc - to).magnitude);
            gr = Mathf.Min(gr, Mathf.Max(core + 0.5f, dEnd * 0.95f));
            obs.Add(new Obstacle { c = gc, r = gr, who = g });
        }
        return obs;
    }

    // Visibility graph over the obstacle outlines + Dijkstra: the shortest route
    // that keeps every leg clear of every obstacle. A solid wall of friendly
    // blocks is therefore walked around by its end instead of through its middle.
    private static bool ShortestPath(Vector3 from, Vector3 to, List<Obstacle> obs, BattleSetup bs, List<Vector3> outPts)
    {
        const int Ring = 16;
        var nodes = new List<Vector3>(obs.Count * Ring + 2) { from, to };
        float hx = bs.fieldHalfX, hz = bs.fieldHalfZ;
        for (int i = 0; i < obs.Count; i++)
            for (int k = 0; k < Ring; k++)
            {
                float ang = k * Mathf.PI * 2f / Ring;
                Vector3 p = obs[i].c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (obs[i].r * 1.12f + 0.5f);
                if (Mathf.Abs(p.x) > hx || Mathf.Abs(p.z) > hz) continue;
                bool inside = false;
                for (int j = 0; j < obs.Count && !inside; j++)
                    if (j != i && (p - obs[j].c).sqrMagnitude < obs[j].r * obs[j].r * 0.96f) inside = true;
                if (!inside) nodes.Add(p);
            }
        int n = nodes.Count;
        var dist = new float[n]; var prev = new int[n]; var done = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
        dist[0] = 0f;
        for (int iter = 0; iter < n; iter++)
        {
            int u = -1; float best = float.MaxValue;
            for (int i = 0; i < n; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0 || u == 1) break;
            done[u] = true;
            for (int v = 0; v < n; v++)
            {
                if (done[v]) continue;
                float d = dist[u] + Vector3.Distance(nodes[u], nodes[v]);
                if (d >= dist[v]) continue;
                if (!Clear(nodes[u], nodes[v], obs)) continue;
                dist[v] = d; prev[v] = u;
            }
        }
        if (prev[1] < 0) return false;
        var rev = new List<Vector3>();
        for (int v = 1; v != 0; v = prev[v]) rev.Add(nodes[v]);
        for (int i = rev.Count - 1; i >= 0; i--) outPts.Add(rev[i]);
        // string-pull: drop every waypoint the path can cut past without touching an obstacle,
        // so the line is a few straight legs, not a staircase of outline corners
        var pulled = new List<Vector3>(outPts.Count);
        Vector3 cur = from; int pk = 0;
        while (pk < outPts.Count)
        {
            int far = pk;
            for (int j = outPts.Count - 1; j > pk; j--) if (Clear(cur, outPts[j], obs)) { far = j; break; }
            pulled.Add(outPts[far]); cur = outPts[far]; pk = far + 1;
        }
        outPts.Clear(); outPts.AddRange(pulled);
        return true;
    }

    private static bool Clear(Vector3 a, Vector3 b, List<Obstacle> obs)
    {
        for (int i = 0; i < obs.Count; i++)
        {
            Closest(a, b, obs[i].c, out float d);
            if (d < obs[i].r * 0.98f) return false;
        }
        return true;
    }

    // Parameter (0-1) and distance of the closest point of segment a-b to c.
    private static float Closest(Vector3 a, Vector3 b, Vector3 c, out float dist)
    {
        Vector3 ab = b - a; ab.y = 0f;
        float l2 = ab.sqrMagnitude;
        float t = l2 < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(c - a, ab) / l2);
        Vector3 p = a + ab * t;
        dist = new Vector2(p.x - c.x, p.z - c.z).magnitude;
        return t;
    }
}
