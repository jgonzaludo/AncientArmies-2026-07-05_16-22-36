using System.Collections.Generic;
using UnityEngine;

// route planning over the terrain grid: A* with per-terrain costs, deep water and rock impassable, a soft
// extra cost near blocked ground so formations keep a little room from banks, then a line-of-sight pull so
// the route is a few straight legs. Pure functions over a TerrainMap; Formation still owns the anchor.
// RoutePlanner (formations as circles) runs on top of the legs this returns.
public static class TerrainGridPlanner
{
    private const float Cap = 12f;   // metres: clearance beyond this does not matter

    private static TerrainMap preparedFor;
    private static float[] edgeDist;   // metres from a cell to the nearest blocked cell, capped
    private static int[] comp;         // connected dry component per cell, -1 where blocked
    private static float[] g;
    private static int[] parent, openStamp, closedStamp;
    private static int stamp;

    private static void Prepare(TerrainMap m)
    {
        if (preparedFor == m && edgeDist != null) return;
        preparedFor = m;
        int w = m.Width, h = m.Height, n = w * h;
        edgeDist = new float[n]; comp = new int[n];
        g = new float[n]; parent = new int[n]; openStamp = new int[n]; closedStamp = new int[n]; stamp = 0;
        float sx = m.CellSizeX, sz = m.CellSizeZ;
        int rx = Mathf.CeilToInt(Cap / sx), rz = Mathf.CeilToInt(Cap / sz);
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                if (TerrainRules.Blocked(m.AtCell(x, z))) { edgeDist[z * w + x] = 0f; continue; }
                float best = Cap;
                for (int dz = -rz; dz <= rz; dz++)
                    for (int dx = -rx; dx <= rx; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h || !TerrainRules.Blocked(m.AtCell(nx, nz))) continue;
                        float d = Mathf.Sqrt(dx * sx * dx * sx + dz * sz * dz * sz) - 0.5f * Mathf.Min(sx, sz);
                        if (d < best) best = d;
                    }
                edgeDist[z * w + x] = Mathf.Max(0f, best);
            }
        for (int i = 0; i < n; i++) comp[i] = TerrainRules.Blocked(m.AtCell(i % w, i / w)) ? -1 : -2;
        int id = 0; var q = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            if (comp[i] != -2) continue;
            comp[i] = id; q.Enqueue(i);
            while (q.Count > 0)
            {
                int c = q.Dequeue(); int cx = c % w, cz = c / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int ni = nz * w + nx;
                    if (comp[ni] != -2) continue;
                    comp[ni] = id; q.Enqueue(ni);
                }
            }
            id++;
        }
    }

    private static float DistAt(TerrainMap m, Vector3 p) { var c = m.ToCell(p); return edgeDist[c.y * m.Width + c.x]; }

    // straight segment keeps off blocked cells, and (when clearance > 0) a margin from them, except right at its ends
    private static bool LineClear(TerrainMap m, Vector3 a, Vector3 b, float clearance)
    {
        float len = Vector3.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len));
        for (int i = 0; i <= steps; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)steps);
            var c = m.ToCell(p);
            if (TerrainRules.Blocked(m.AtCell(c.x, c.y))) return false;
            if (clearance > 0f)
            {
                float fromEnd = Mathf.Min(i, steps - i) * (len / steps);
                if (fromEnd > 3f && edgeDist[c.y * m.Width + c.x] < clearance * 0.5f) return false;
            }
        }
        return true;
    }

    // hard-blocked check only, for validating a leg someone else produced
    public static bool SegmentClear(Vector3 a, Vector3 b, TerrainMap map = null)
    {
        var m = map != null ? map : TerrainMap.Active;
        if (m == null) return true;
        Prepare(m);
        return LineClear(m, a, b, 0f);
    }

    // waypoints from `from` to `to` (excluding `from`, ending exactly on `to`). false: no route (goal blocked or unreachable)
    public static bool TryPlan(Vector3 from, Vector3 to, float clearance, List<Vector3> outPts, TerrainMap map = null)
    {
        outPts.Clear();
        var m = map != null ? map : TerrainMap.Active;
        if (m == null) { outPts.Add(to); return true; }
        Prepare(m);
        int w = m.Width;
        var sc = m.ToCell(from); var gc = m.ToCell(to);
        int s = sc.y * w + sc.x, e = gc.y * w + gc.x;
        if (TerrainRules.Blocked(m.AtCell(gc.x, gc.y))) return false;
        if (TerrainRules.Blocked(m.AtCell(sc.x, sc.y)))
        {
            // standing in water or rock (a broken unit rallied there): plan from the nearest dry cell, with that cell as the first waypoint
            var dry = NearestReachable(from, from, m, comp[e]);
            if (dry == from) return false;
            var rest = new List<Vector3>();
            if (!TryPlan(dry, to, clearance, rest, m)) return false;
            outPts.Add(dry); outPts.AddRange(rest);
            return true;
        }
        if (comp[s] != comp[e]) return false;
        if (LineClear(m, from, to, clearance)) { outPts.Add(to); return true; }

        stamp++;
        var heap = new List<KeyValuePair<float, int>>(256);
        g[s] = 0f; parent[s] = -1; openStamp[s] = stamp;
        HeapPush(heap, 0f, s);
        float minCost = 1f / 1.15f;
        float sx = m.CellSizeX, sz = m.CellSizeZ;
        bool found = false;
        while (heap.Count > 0)
        {
            int cur = HeapPop(heap);
            if (closedStamp[cur] == stamp) continue;
            closedStamp[cur] = stamp;
            if (cur == e) { found = true; break; }
            int cx = cur % w, cz = cur / w;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= m.Height) continue;
                    var ty = m.AtCell(nx, nz);
                    if (TerrainRules.Blocked(ty)) continue;
                    if (dx != 0 && dz != 0 && (TerrainRules.Blocked(m.AtCell(cx + dx, cz)) || TerrainRules.Blocked(m.AtCell(cx, cz + dz)))) continue;   // no cutting a blocked corner
                    int ni = nz * w + nx;
                    if (closedStamp[ni] == stamp) continue;
                    float step = Mathf.Sqrt(dx * sx * dx * sx + dz * sz * dz * sz);
                    float near = clearance > 0f ? Mathf.Clamp01((clearance - edgeDist[ni]) / clearance) : 0f;
                    float ng = g[cur] + step * TerrainRules.Cost(ty) * (1f + 2f * near);
                    if (openStamp[ni] == stamp && ng >= g[ni]) continue;
                    openStamp[ni] = stamp; g[ni] = ng; parent[ni] = cur;
                    float hx = (nx - gc.x) * sx, hz = (nz - gc.y) * sz;
                    HeapPush(heap, ng + Mathf.Sqrt(hx * hx + hz * hz) * minCost, ni);
                }
        }
        if (!found) return false;
        var cells = new List<Vector3>();
        for (int c = e; c != -1; c = parent[c]) cells.Add(m.CellCenter(c % w, c / w));
        cells.Reverse();
        cells[cells.Count - 1] = to;
        // string pull: skip every waypoint the route can cut past without touching blocked ground
        var pulled = new List<Vector3>();
        Vector3 anchor = from; int k = 0;
        while (k < cells.Count)
        {
            int far = k;
            for (int j = cells.Count - 1; j > k; j--) if (LineClear(m, anchor, cells[j], clearance)) { far = j; break; }
            pulled.Add(cells[far]); anchor = cells[far]; k = far + 1;
        }
        outPts.AddRange(pulled);
        return true;
    }

    // the walkable spot nearest `target` that `from` can actually reach: the target itself when it is dry and
    // reachable, otherwise the closest reachable dry cell (a point in a river, or on an enclosed island)
    public static Vector3 NearestReachable(Vector3 from, Vector3 target, TerrainMap map = null) => NearestReachable(from, target, map, int.MinValue);

    // builds the cell data now instead of on the first order, so a phone does not stall on the first move
    public static void Warm(TerrainMap map) { if (map != null) Prepare(map); }

    private static Vector3 NearestReachable(Vector3 from, Vector3 target, TerrainMap map, int wantComp)
    {
        var m = map != null ? map : TerrainMap.Active;
        if (m == null) return target;
        Prepare(m);
        int w = m.Width;
        var sc = m.ToCell(from); var tc = m.ToCell(target);
        int want = wantComp != int.MinValue ? wantComp : comp[sc.y * w + sc.x];
        if (want < 0)   // standing on blocked ground: use the nearest dry cell as home
        {
            float bd = float.MaxValue;
            for (int r = 1; r < 40 && bd == float.MaxValue; r++)
                for (int dz = -r; dz <= r; dz++) for (int dx = -r; dx <= r; dx++)
                {
                    int nx = sc.x + dx, nz = sc.y + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= m.Height || comp[nz * w + nx] < 0) continue;
                    float d = dx * dx + dz * dz; if (d < bd) { bd = d; want = comp[nz * w + nx]; }
                }
            if (want < 0) return target;
        }
        if (comp[tc.y * w + tc.x] == want) return target;
        float best = float.MaxValue; Vector3 bestP = target;
        int max = Mathf.CeilToInt(80f / Mathf.Min(m.CellSizeX, m.CellSizeZ));
        for (int r = 1; r <= max; r++)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;   // ring only
                    int nx = tc.x + dx, nz = tc.y + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= m.Height || comp[nz * w + nx] != want) continue;
                    Vector3 p = m.CellCenter(nx, nz);
                    float d = (p - target).sqrMagnitude;
                    if (d < best) { best = d; bestP = p; }
                }
            if (best < float.MaxValue && r * Mathf.Min(m.CellSizeX, m.CellSizeZ) * r * Mathf.Min(m.CellSizeX, m.CellSizeZ) > best) break;   // nothing nearer can be on a wider ring
        }
        return bestP;
    }

    // ---- tiny binary min-heap on (priority, cell) ----
    private static void HeapPush(List<KeyValuePair<float, int>> h, float pri, int v)
    {
        h.Add(new KeyValuePair<float, int>(pri, v));
        int i = h.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (h[p].Key <= h[i].Key) break;
            var t = h[p]; h[p] = h[i]; h[i] = t; i = p;
        }
    }

    private static int HeapPop(List<KeyValuePair<float, int>> h)
    {
        int top = h[0].Value;
        h[0] = h[h.Count - 1]; h.RemoveAt(h.Count - 1);
        int i = 0;
        while (true)
        {
            int l = 2 * i + 1, r = l + 1, s = i;
            if (l < h.Count && h[l].Key < h[s].Key) s = l;
            if (r < h.Count && h[r].Key < h[s].Key) s = r;
            if (s == i) break;
            var t = h[s]; h[s] = h[i]; h[i] = t; i = s;
        }
        return top;
    }
}
