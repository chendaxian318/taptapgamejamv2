Shader "Habitat/IslandSky"
{
    Properties
    {
        _Zenith ("Upper sky", Color) = (.24,.55,.76,1)
        _Horizon ("Horizon haze", Color) = (.77,.87,.84,1)
        _CloudColor ("Soft clouds", Color) = (.91,.94,.88,1)
        _SunDirection ("Sun direction", Vector) = (.4,.7,.5,0)
        _SunColor ("Sun glow", Color) = (1,.90,.68,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _CloudColor, _SunColor;
                float4 _SunDirection;
            CBUFFER_END
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.direction=i.positionOS.xyz; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);
                half3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.55));
                float2 p=d.xz/max(.18,d.y+.3);
                float clouds=sin(p.x*2.1+sin(p.y*1.4))*sin(p.y*2.5-p.x*.4);
                float veil=smoothstep(.3,.85,clouds)*smoothstep(.03,.25,d.y)*(1-smoothstep(.7,1,d.y));
                color=lerp(color,_CloudColor.rgb,veil*.30);
                float sun=saturate(dot(d,normalize(_SunDirection.xyz)));
                color+=_SunColor.rgb*(pow(sun,48)*.12+smoothstep(.9995,.9999,sun)*.3);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
