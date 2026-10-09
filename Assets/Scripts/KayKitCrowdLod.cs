using UnityEngine;

// Zoom-driven level of detail for the animated soldiers, computed once per frame
// from the same 1080p-reference soldier pixel height the impostor renderer uses.
// Tiers (with hysteresis so nothing flickers at a threshold):
//   >= 110 px  mesh LOD0, animator every frame, nobody hidden
//   70-110     mesh LOD1
//   45-70      mesh LOD2, animator every 2nd frame
//   30-45      mesh LOD3, animator every 3rd frame, every 2nd man not drawn
//   <= 30      the impostor renderer takes over (this class does nothing there)
// Hidden men are still simulated; only their drawing is skipped, and the
// impostor quads / remaining men keep the block readable.
public static class KayKitCrowdLod
{
    public static int MeshLod { get; private set; }
    public static int AnimStride { get; private set; } = 1;
    public static int ThinMod { get; private set; }   // 0 = draw everyone, 2 = hide every 2nd man
    public static int Version { get; private set; }
    private static int frame = -1;
    private static int tier;

    public static void Refresh()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        var mgr = FormationBannerManager.Instance;
        float px = mgr != null ? mgr.SoldierScreenPixels : 200f;
        int want = tier;
        // thresholds with a 10% dead band
        float[] up = { 110f, 70f, 45f };          // tier t -> t-1 when px rises above up[t-1]*1.1
        while (want > 0 && px > up[want - 1] * 1.1f) want--;
        while (want < 3 && px < up[want] * 0.9f) want++;
        if (want == tier && tier == 0 && px < up[0] * 0.9f) want = 1;
        if (want != tier)
        {
            tier = want; Version++;
            MeshLod = Mathf.Clamp(tier, 0, 3);
            AnimStride = tier <= 1 ? 1 : (tier == 2 ? 2 : 3);
            ThinMod = tier >= 3 ? 2 : 0;
        }
    }
}
