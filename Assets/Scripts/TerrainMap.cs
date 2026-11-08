using UnityEngine;

// a hand-painted terrain map: one pixel per grid cell, one colour per type.
// the field runs -halfX..halfX by -halfZ..halfZ, texture row 0 is the south edge.
// gameplay reads this (speed, blocked, concealed); TerrainView draws it. never writes gameplay state.
[CreateAssetMenu(menuName = "AncientArmies/Terrain Map")]
public class TerrainMap : ScriptableObject
{
    public Texture2D painted;   // must be readable
    public float halfX = 205f;
    public float halfZ = 170f;

    private static readonly Color[] Keys =
    {
        Hex(0x9DB36A), Hex(0xB58F5E), Hex(0x7A5C3E), Hex(0x7FB8C9),
        Hex(0x3E7FA6), Hex(0x8E8C86), Hex(0x6E8450), Hex(0x8B5A2B),
    };
    private static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1f);

    private TerrainType[] cells;
    private int width, height;
    public int Width { get { Ensure(); return width; } }
    public int Height { get { Ensure(); return height; } }
    public float CellSizeX => 2f * halfX / Mathf.Max(1, Width);
    public float CellSizeZ => 2f * halfZ / Mathf.Max(1, Height);

    private static TerrainMap active;
    private static bool activeLoaded;

    // the map battles use. null means flat grass everywhere (the old behaviour)
    public static TerrainMap Active
    {
        get
        {
            if (!activeLoaded) { active = Resources.Load<TerrainMap>("Terrain/Map_Default"); activeLoaded = true; }
            return active;
        }
        set { active = value; activeLoaded = true; }
    }

    public static TerrainMap FromTexture(Texture2D tex, float halfX, float halfZ)
    {
        var m = CreateInstance<TerrainMap>();
        m.painted = tex; m.halfX = halfX; m.halfZ = halfZ;
        m.Build();
        return m;
    }

    private void OnEnable() { cells = null; }

    private void Build()
    {
        if (painted == null) { width = height = 1; cells = new TerrainType[1]; return; }
        width = painted.width; height = painted.height;
        cells = new TerrainType[width * height];
        var px = painted.GetPixels32();
        for (int i = 0; i < cells.Length; i++) cells[i] = Classify(px[i]);
    }

    private static TerrainType Classify(Color32 p)
    {
        float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;
        int best = 0; float bd = float.MaxValue;
        for (int k = 0; k < Keys.Length; k++)
        {
            float dr = r - Keys[k].r, dg = g - Keys[k].g, db = b - Keys[k].b;
            float d = dr * dr + dg * dg + db * db;
            if (d < bd) { bd = d; best = k; }
        }
        return (TerrainType)best;
    }

    private void Ensure() { if (cells == null) Build(); }

    public Vector2Int ToCell(Vector3 w)
    {
        Ensure();
        int x = Mathf.Clamp(Mathf.FloorToInt((w.x + halfX) / CellSizeX), 0, Width - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt((w.z + halfZ) / CellSizeZ), 0, Height - 1);
        return new Vector2Int(x, z);
    }

    public Vector3 CellCenter(int x, int z) => new Vector3(-halfX + (x + 0.5f) * CellSizeX, 0f, -halfZ + (z + 0.5f) * CellSizeZ);

    public TerrainType AtCell(int x, int z)
    {
        Ensure();
        return cells[Mathf.Clamp(z, 0, Height - 1) * Width + Mathf.Clamp(x, 0, Width - 1)];
    }

    public TerrainType At(Vector3 w) { var c = ToCell(w); return AtCell(c.x, c.y); }
    public float SpeedAt(Vector3 w) => TerrainRules.Speed(At(w));
    public bool Blocked(Vector3 w) => TerrainRules.Blocked(At(w));
    public bool ConcealedAt(Vector3 w) => TerrainRules.Concealed(At(w));
}
