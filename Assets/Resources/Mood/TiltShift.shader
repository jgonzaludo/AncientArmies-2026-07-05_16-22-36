// screen-space tilt-shift: the middle of the screen stays sharp, blur ramps up toward the top and bottom edges,
// like a miniature photographed with a tilted lens. one pass, a 16-tap disc whose radius follows the edge ramp.
Shader "AncientArmies/TiltShift"
{
    Properties
    {
        _Band ("Sharp band half-height (0-0.5)", Float) = 0.22
        _MaxRadius ("Max blur radius in pixels", Float) = 7
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "TiltShift"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Band; float _MaxRadius;

            static const float2 Disc[16] = {
                float2(0.000, 0.250), float2(0.354, 0.354), float2(0.500, 0.000), float2(0.354, -0.354),
                float2(0.000, -0.500), float2(-0.354, -0.354), float2(-0.500, 0.000), float2(-0.354, 0.354),
                float2(0.000, 0.750), float2(0.707, 0.707), float2(1.000, 0.000), float2(0.707, -0.707),
                float2(0.000, -1.000), float2(-0.707, -0.707), float2(-1.000, 0.000), float2(-0.707, 0.707) };

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float e = saturate((abs(uv.y - 0.5) - _Band) / (0.5 - _Band));
                float r = e * e * _MaxRadius;
                half4 c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                if (r < 0.3) return c;
                float2 px = _BlitTexture_TexelSize.xy * r;
                half4 acc = c;
                [unroll] for (int k = 0; k < 16; k++) acc += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + Disc[k] * px);
                return acc / 17.0;
            }
            ENDHLSL
        }
    }
}
