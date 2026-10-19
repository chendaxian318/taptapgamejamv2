Shader "Habitat/QuietPaintedWater"
{
    Properties
    {
        _BaseMap ("Original painted water", 2D) = "white" {}
        _BaseColor ("Calm water tint", Color) = (0.25, 0.58, 0.59, 1)
        _TextureStrength ("Paint visibility", Range(0, 1)) = 0.2
        _ShallowColor ("Shallow lagoon", Color) = (.24,.59,.62,1)
        _FoamColor ("Shore foam", Color) = (.79,.92,.85,1)
        _WaveStrength ("Wave normal strength", Range(0,.15)) = .035
        _FoamStrength ("Foam strength", Range(0,1)) = .35
        _DepthRange ("Shallow depth in metres", Range(.2,5)) = .8
        _MotionSpeed ("Calm water motion speed", Range(0,2)) = 1.1
        _RippleStrength ("Original paint detail", Range(0,.6)) = .14
        _FlowStrength ("Paint drift amount", Range(0,.01)) = .008
    }
    SubShader
    {
        // Draw after opaque depth is available. Solid color coverage preserves puzzle readability.
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Pass
        {
            Name "PaintedWater"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS:TEXCOORD1; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _TextureStrength;
                half4 _ShallowColor, _FoamColor;
                float _WaveStrength, _FoamStrength, _DepthRange;
                float _MotionSpeed, _RippleStrength, _FlowStrength;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p=input.positionWS.xz;
                float t=_Time.y*_MotionSpeed;
                float waveA=sin(p.x*1.7+p.y*.8+t*.55);
                float waveB=sin(p.y*2.3-p.x*.6-t*.42);
                half3 normal=normalize(float3(waveA*_WaveStrength,1,waveB*_WaveStrength));
                float2 screenUV=GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth=SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    rawDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,rawDepth);
                #endif
                float3 floorWS=ComputeWorldSpacePosition(screenUV,rawDepth,UNITY_MATRIX_I_VP);
                float depth=max(0,input.positionWS.y-floorWS.y);
                float shallow=exp2(-depth/max(.01,_DepthRange));
                half3 water=lerp(_BaseColor.rgb,_ShallowColor.rgb,shallow);
                // Animate the artist's existing washes instead of stamping
                // repeated white wave symbols over the whole sea.
                float2 drift=float2(t*.24,t*.10)*_FlowStrength;
                float2 bend=float2(
                    sin(p.x*.43+p.y*.27+t*.65)+.4*sin(p.y*.83-t*.42),
                    cos(p.y*.38-p.x*.19-t*.51)+.4*sin(p.x*.71+t*.38))*_FlowStrength*.65;
                half3 paintA=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv+drift+bend).rgb;
                half3 paintB=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv-drift*.65-bend*.6).rgb;
                half3 paint=lerp(paintA,paintB,.25);
                water=lerp(water,paint,saturate(_TextureStrength*.55+_RippleStrength*.9));
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normal,sun.direction));
                half illumination=.78+.22*diffuse;
                water*=illumination*lerp(half3(1,1,1),sun.color,.25);
                water*=lerp(.79,1,sun.shadowAttenuation);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfway=SafeNormalize(sun.direction+view);
                half glint=pow(saturate(dot(normal,halfway)),96)*.18*sun.shadowAttenuation;
                water+=sun.color*glint;
                float shore=1-smoothstep(.03,.24,depth);
                float ripple=.65+.35*sin(depth*40-t*1.2+waveA*.6);
                water=lerp(water,_FoamColor.rgb,shore*ripple*_FoamStrength);
                return half4(water,1);
            }
            ENDHLSL
        }
    }
}
