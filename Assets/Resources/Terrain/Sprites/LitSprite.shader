// painted props lit by the real sun: each sprite carries a normal map (built from its own paint, see
// ArtSource/Terrain/mood/build_mood_art.py), so the side facing the sun brightens warm and the far side falls into shade.
// the quads always face the fixed camera, so the normal map's x/y/z are the camera's right/up/back.
// vertex alpha is the tree fade (TerrainSprites.TreeFade).
Shader "AncientArmies/LitSprite"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _NormalTex ("Normal atlas", 2D) = "bump" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Shade ("Shade multiplier", Float) = 0.66
        _Lit ("Lit multiplier", Float) = 1.32
        _Wrap ("Wrap", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Color; float _Shade; float _Lit; float _Wrap;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct Vary { float4 cs : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };

            Vary vert(Attr a)
            {
                Vary v; v.cs = TransformObjectToHClip(a.pos.xyz); v.uv = a.uv; v.col = a.col; return v;
            }

            half4 frag(Vary i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color * i.col;
                half3 nt = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, i.uv).rgb * 2.0 - 1.0;
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz, back = UNITY_MATRIX_V[2].xyz;   // the camera's axes
                Light sun = GetMainLight();
                // light the picture in its own plane: where the sun sits on screen (left/right, up/down), always in front of
                // the painting, since a camera-facing card would otherwise go dark whenever the sun is behind the camera's view
                float3 ls = float3(dot(sun.direction, right), dot(sun.direction, up), 0.0);
                ls.z = abs(dot(sun.direction, back)) + 0.55;
                ls = normalize(ls);
                float ndl = dot(normalize(nt), ls);
                float wrap = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half3 sunTint = sun.color / max(max(sun.color.r, sun.color.g), max(sun.color.b, 1e-3));   // the sun's hue, not its intensity
                half3 light = lerp(_Shade.xxx, _Lit * sunTint, wrap * wrap);
                return half4(albedo.rgb * light, albedo.a);
            }
            ENDHLSL
        }
    }
}
