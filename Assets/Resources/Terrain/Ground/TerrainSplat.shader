// the terrain ground: painted tile textures blended by two weight maps baked from the terrain map.
// unlit on purpose, the paint already carries its own light. sampled in world space so it ignores the plane's uv.
Shader "AncientArmies/TerrainSplat"
{
    Properties
    {
        _Control ("Weights: grass road mud forest", 2D) = "red" {}
        _Control2 ("Weights: rock, water, ford, -", 2D) = "black" {}
        _Grass ("Grass", 2D) = "white" {}
        _Road ("Road", 2D) = "white" {}
        _Mud ("Mud", 2D) = "white" {}
        _Forest ("Forest floor", 2D) = "white" {}
        _Water ("Water", 2D) = "white" {}
        _Field ("Field: halfX, halfZ, tileA m, tileB m", Vector) = (205, 170, 44, 71)
        _SandColor ("Sand", Color) = (0.79, 0.70, 0.49, 1)
        _RockTint ("Rock ground tint", Color) = (0.80, 0.78, 0.74, 1)
        _FordTint ("Ford tint", Color) = (0.70, 0.86, 0.86, 1)
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

            TEXTURE2D(_Control);  SAMPLER(sampler_Control);
            TEXTURE2D(_Control2); SAMPLER(sampler_Control2);
            TEXTURE2D(_Grass);  SAMPLER(sampler_Grass);
            TEXTURE2D(_Road);   SAMPLER(sampler_Road);
            TEXTURE2D(_Mud);    SAMPLER(sampler_Mud);
            TEXTURE2D(_Forest); SAMPLER(sampler_Forest);
            TEXTURE2D(_Water);  SAMPLER(sampler_Water);
            CBUFFER_START(UnityPerMaterial)
                float4 _Field; float4 _SandColor; float4 _RockTint; float4 _FordTint;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; };
            struct Vary { float4 cs : SV_POSITION; float3 ws : TEXCOORD0; };

            Vary vert(Attr a)
            {
                Vary v; float3 ws = TransformObjectToWorld(a.pos.xyz);
                v.ws = ws; v.cs = TransformWorldToHClip(ws); return v;
            }

            // two samples of one tile at different scales and angles, so the repeat does not show
            float3 Tile(TEXTURE2D_PARAM(tex, smp), float2 p)
            {
                float2 a = p / _Field.z;
                float2 q = float2(p.x * 0.8572 - p.y * 0.5150, p.x * 0.5150 + p.y * 0.8572) / _Field.w;
                float3 c1 = SAMPLE_TEXTURE2D(tex, smp, a).rgb;
                float3 c2 = SAMPLE_TEXTURE2D(tex, smp, q + 0.37).rgb;
                return lerp(c1, c2, 0.45);
            }

            float Ss(float e0, float e1, float x) { float t = saturate((x - e0) / (e1 - e0)); return t * t * (3.0 - 2.0 * t); }

            float4 frag(Vary i) : SV_Target
            {
                float2 uv = (i.ws.xz + _Field.xy) / (2.0 * _Field.xy);
                float4 c1 = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv);
                float4 c2 = SAMPLE_TEXTURE2D(_Control2, sampler_Control2, uv);
                float2 p = i.ws.xz;
                float3 grass = Tile(TEXTURE2D_ARGS(_Grass, sampler_Grass), p);
                float3 road = Tile(TEXTURE2D_ARGS(_Road, sampler_Road), p);
                float3 mud = Tile(TEXTURE2D_ARGS(_Mud, sampler_Mud), p);
                float3 forest = Tile(TEXTURE2D_ARGS(_Forest, sampler_Forest), p);
                float3 rt = Tile(TEXTURE2D_ARGS(_Road, sampler_Road), p * 1.7);
                float3 rock = dot(rt, float3(0.3, 0.59, 0.11)).xxx * _RockTint.rgb * 1.1;   // grey stony ground from the road paint
                float wsum = c1.r + c1.g + c1.b + c1.a + c2.r + 1e-4;
                float3 land = (grass * c1.r + road * c1.g + mud * c1.b + forest * c1.a + rock * c2.r) / wsum;
                // water: the baked mask is a soft 0..1, threshold it here so banks are smooth and round
                float wm = c2.g;
                float inWater = Ss(0.4, 0.6, wm);
                float2 flow = float2(_Time.y * 0.9, _Time.y * 0.35);
                float3 water = Tile(TEXTURE2D_ARGS(_Water, sampler_Water), p + flow);
                float ford = Ss(0.3, 0.7, c2.b);
                float shallow = 1.0 - Ss(0.6, 0.98, wm);
                water = lerp(water, water * _FordTint.rgb + 0.12, saturate(ford + 0.4 * shallow));
                float3 col = lerp(land, water, inWater);
                float rim = saturate(1.0 - abs(wm - 0.28) / 0.3) * (1.0 - inWater) * step(0.1, wm) * step(wm, 0.55);
                col = lerp(col, _SandColor.rgb, 0.7 * rim);
                col = col * 1.03 + float3(0.0, 0.01, 0.05);   // nudged toward the mockups' slightly lighter, greener grass
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
