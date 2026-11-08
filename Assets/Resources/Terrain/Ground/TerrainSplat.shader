// the terrain ground: painted tile textures blended by two weight maps baked from the terrain map.
// unlit on purpose, the paint carries its own light; the mood (dry patches, bare earth, soft cloud shade, wet banks,
// murky water) comes from a tileable macro map so large areas never look flat. sampled in world space.
Shader "AncientArmies/TerrainSplat"
{
    Properties
    {
        _Control ("Weights: grass road mud forest", 2D) = "red" {}
        _Control2 ("Weights: rock, water, ford, -", 2D) = "black" {}
        _Grass ("Grass", 2D) = "white" {}
        _Dry ("Dry grass", 2D) = "white" {}
        _Road ("Road", 2D) = "white" {}
        _Mud ("Mud", 2D) = "white" {}
        _Forest ("Forest floor", 2D) = "white" {}
        _Water ("Water", 2D) = "white" {}
        _Macro ("Macro: dry, dirt, light", 2D) = "gray" {}
        _Field ("Field: halfX, halfZ, tileA m, tileB m", Vector) = (205, 170, 16, 23)
        _MacroTile ("Macro tile m", Float) = 140
        _RockTint ("Rock ground tint", Color) = (0.80, 0.78, 0.74, 1)
        _FordTint ("Ford tint", Color) = (0.62, 0.70, 0.62, 1)
        _Light ("Light: shade, sun, warm", Vector) = (0.90, 1.16, 0.04, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Control);  SAMPLER(sampler_Control);
            TEXTURE2D(_Control2); SAMPLER(sampler_Control2);
            TEXTURE2D(_Grass);  SAMPLER(sampler_Grass);
            TEXTURE2D(_Dry);    SAMPLER(sampler_Dry);
            TEXTURE2D(_Road);   SAMPLER(sampler_Road);
            TEXTURE2D(_Mud);    SAMPLER(sampler_Mud);
            TEXTURE2D(_Forest); SAMPLER(sampler_Forest);
            TEXTURE2D(_Water);  SAMPLER(sampler_Water);
            TEXTURE2D(_Macro);  SAMPLER(sampler_Macro);
            CBUFFER_START(UnityPerMaterial)
                float4 _Field; float _MacroTile; float4 _RockTint; float4 _FordTint; float4 _Light;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; };
            struct Vary { float4 cs : SV_POSITION; float3 ws : TEXCOORD0; };

            Vary vert(Attr a)
            {
                Vary v; float3 ws = TransformObjectToWorld(a.pos.xyz);
                v.ws = ws; v.cs = TransformWorldToHClip(ws); return v;
            }

            float Ss(float e0, float e1, float x) { float t = saturate((x - e0) / (e1 - e0)); return t * t * (3.0 - 2.0 * t); }

            // two samples of one tile at different scales and angles; a noise mask picks between them instead of
            // averaging, so the repeat breaks up but the paint keeps its contrast
            float3 Tile(TEXTURE2D_PARAM(tex, smp), float2 p, float pick)
            {
                float2 a = p / _Field.z;
                float2 q = float2(p.x * 0.8572 - p.y * 0.5150, p.x * 0.5150 + p.y * 0.8572) / _Field.w;
                float3 c1 = SAMPLE_TEXTURE2D(tex, smp, a).rgb;
                float3 c2 = SAMPLE_TEXTURE2D(tex, smp, q + 0.37).rgb;
                return lerp(c1, c2, pick);
            }

            float4 frag(Vary i) : SV_Target
            {
                float2 uv = (i.ws.xz + _Field.xy) / (2.0 * _Field.xy);
                float4 c1 = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv);
                float4 c2 = SAMPLE_TEXTURE2D(_Control2, sampler_Control2, uv);
                float2 p = i.ws.xz;
                float3 mac = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, p / _MacroTile).rgb;
                float3 mac2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, p / (_MacroTile * 0.23) + 0.5).rgb;
                float pick = Ss(0.35, 0.65, mac2.b);

                float3 grass = Tile(TEXTURE2D_ARGS(_Grass, sampler_Grass), p, pick);
                float3 dry = Tile(TEXTURE2D_ARGS(_Dry, sampler_Dry), p, pick);
                float3 road = Tile(TEXTURE2D_ARGS(_Road, sampler_Road), p * 0.8, pick);
                float3 mud = Tile(TEXTURE2D_ARGS(_Mud, sampler_Mud), p * 0.6, pick);
                float3 forest = Tile(TEXTURE2D_ARGS(_Forest, sampler_Forest), p * 0.7, pick);

                // open ground: green grass broken by large dry patches and a few bare spots
                float dryAmt = Ss(0.52, 0.74, mac.r * 0.75 + mac2.r * 0.25) * 0.85;
                float dirtAmt = Ss(0.70, 0.88, mac.g) * 0.7;
                float3 open = lerp(grass, dry, dryAmt);
                open = lerp(open, road, dirtAmt);

                float3 rt = Tile(TEXTURE2D_ARGS(_Road, sampler_Road), p * 1.7, pick);
                float3 rock = dot(rt, float3(0.3, 0.59, 0.11)).xxx * _RockTint.rgb * 1.05;
                float wsum = c1.r + c1.g + c1.b + c1.a + c2.r + 1e-4;
                float3 land = (open * c1.r + road * c1.g + mud * c1.b + forest * c1.a + rock * c2.r) / wsum;
                // canopy shade: the forest floor and its edges sit in shadow
                land *= lerp(1.0, 0.86, saturate(c1.a * 1.2));

                // water: deep, murky, darker toward the middle; the bank is wet mud, not sand
                float wm = c2.g;
                // ragged, natural banks: the soft water mask is nudged by small-scale noise before it is thresholded
                float edgeN = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, p / 9.0 + 0.4).r - 0.5;
                float edgeN2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, p / 3.1 + 0.8).g - 0.5;
                wm = saturate(wm + edgeN * 0.16 + edgeN2 * 0.05);
                float inWater = Ss(0.4, 0.6, wm);
                float2 flow = float2(_Time.y * 0.5, _Time.y * 0.2);
                float3 water = Tile(TEXTURE2D_ARGS(_Water, sampler_Water), p * 0.5 + flow, 0.5);
                float deep = Ss(0.55, 0.95, wm);
                water *= lerp(1.85, 1.65, deep);
                // the treeline on the far (north) bank reflects as a dark green band just below it
                float2 upUv = uv + float2(0.0, 7.0 / (2.0 * _Field.y));
                float wUp = SAMPLE_TEXTURE2D(_Control2, sampler_Control2, upUv).g;
                float treesUp = SAMPLE_TEXTURE2D(_Control, sampler_Control, upUv + float2(0.0, 6.0 / (2.0 * _Field.y))).a;
                float refl = (1.0 - Ss(0.4, 0.6, wUp)) * saturate(0.45 + treesUp);
                water = lerp(water, float3(0.09, 0.15, 0.11), 0.35 * refl);
                // a pale sky sheen in soft streaks, and low mist drifting off the shallows
                float sheen = Ss(0.45, 0.85, SAMPLE_TEXTURE2D(_Macro, sampler_Macro, float2(p.x / 60.0 + _Time.y * 0.004, p.y / 14.0)).b);
                water += float3(0.03, 0.035, 0.03) * sheen * (1.0 - refl);
                float mist = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, p / 38.0 + float2(_Time.y * 0.006, 0.0)).r;
                water = lerp(water, float3(0.55, 0.57, 0.52), 0.03 * Ss(0.4, 0.8, mist) * (1.0 - deep * 0.6));
                float ford = Ss(0.3, 0.7, c2.b);
                water = lerp(water, water * _FordTint.rgb + float3(0.03, 0.03, 0.02), saturate(ford * 0.25));
                water = lerp(water, dot(water, float3(0.3, 0.59, 0.11)).xxx * float3(0.96, 1.03, 1.0), 0.7);   // murky, low saturation
                float bank = saturate(1.0 - abs(wm - 0.30) / 0.2) * (1.0 - inWater) * step(0.08, wm);
                land = lerp(land, mud * 0.92, 0.6 * bank);
                // fine detail: a small-scale copy of the grass paint modulates brightness so blades read up close
                // mid-scale mottling: light and dark streaks a few metres across, the grain the eye reads at play zoom
                float mott = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, float2(p.x / 11.0, p.y / 7.0) + 0.21).b;
                float mott2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, p / 4.3 + 0.63).r;
                land *= lerp(1.0, 0.72 + 0.34 * mott + 0.22 * mott2, 1.0 - c1.a * 0.6);
                float3 col = lerp(land, water, inWater);
                col *= lerp(1.0, 0.7, saturate(1.0 - abs(wm - 0.5) / 0.08) * 0.6);   // dark wet line at the waterline

                // sunlight on the painted relief: the paint's own brightness is read as height (tufts, ruts, stones stand up),
                // its screen-space slope gives a normal, and the real sun lights it, so the ground has a lit and a shaded side
                float hgt = dot(land, float3(0.3, 0.59, 0.11));
                float3 dpx = ddx(i.ws), dpy = ddy(i.ws);
                float hx = ddx(hgt), hy = ddy(hgt);
                float3 upv = float3(0, 1, 0);
                float3 r1 = cross(dpy, upv), r2 = cross(upv, dpx);
                float det = dot(dpx, r1);
                float3 grad = sign(det) * (hx * r1 + hy * r2) / max(abs(det), 1e-5);
                float3 nrm = normalize(upv - grad * 0.9);
                Light sun = GetMainLight();
                float ndl = saturate(dot(nrm, sun.direction));
                float flatNdl = saturate(dot(upv, sun.direction));
                float relief = saturate(ndl / max(flatNdl, 0.2));
                half3 sunTint = sun.color / max(max(sun.color.r, sun.color.g), max(sun.color.b, 1e-3));
                col *= lerp(0.72, 1.18, saturate(relief * 0.55 + 0.2)) * lerp(1.0.xxx, sunTint, 0.35 * (1.0 - inWater));
                // soft painted light: slow cloud shade over the field, warm sunlit side
                float lightAmt = Ss(0.3, 0.7, mac.b * 0.7 + mac2.g * 0.3);
                col *= lerp(_Light.x, _Light.y, lightAmt);
                col += float3(_Light.z, _Light.z * 0.6, 0.0) * lightAmt;
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
