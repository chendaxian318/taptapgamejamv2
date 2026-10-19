Shader "Habitat/WhiteKeySprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original FigJam Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyLow ("White threshold start", Range(0,1)) = 0.86
        _KeyHigh ("White threshold end", Range(0,1)) = 0.975
        _TrimArtistFront ("Hide keeper front annotation regions", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Name "FigJamSprite"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _KeyLow;
                half _KeyHigh;
                half _TrimArtistFront;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Original 347 × 382 keeper sheet: remove only the two note/palette
                // regions. UV masking retains the untouched full-resolution artwork.
                if (_TrimArtistFront > 0.5h)
                {
                    if (input.uv.x < 53.0 / 347.0 && input.uv.y > 199.0 / 382.0) clip(-1);
                    if (input.uv.x < 155.0 / 347.0 && input.uv.y > 347.0 / 382.0) clip(-1);
                }
                half4 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half white = min(art.r, min(art.g, art.b));
                half coverage = 1.0h - smoothstep(_KeyLow, _KeyHigh, white);
                // Remove some of the white matte mixed into antialiased edge pixels.
                // Opaque painted colors remain unchanged; the floor avoids unstable
                // division on the almost-transparent fringe of the original JPEG.
                if (coverage > 0.008h && coverage < 0.999h)
                {
                    half3 cleanEdge = saturate((art.rgb - (1.0h - coverage)) / max(coverage, 0.05h));
                    art.rgb = lerp(art.rgb, cleanEdge, 0.75h);
                }
                art.a *= coverage;
                art *= input.color;
                clip(art.a - 0.008h);
                return art;
            }
            ENDHLSL
        }
    }
}
