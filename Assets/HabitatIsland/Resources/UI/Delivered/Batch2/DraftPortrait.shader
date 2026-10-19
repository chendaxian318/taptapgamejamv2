Shader "Habitat/UI Draft Portrait"
{
 Properties { _MainTex("Art",2D)="white"{} _ReferenceNotes("Reference note mask",Float)=0 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   sampler2D _MainTex;float _ReferenceNotes;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 c=tex2D(_MainTex,i.uv);float white=min(c.r,min(c.g,c.b));float opacity=1-smoothstep(.82,.97,white);c.a*=opacity;
    c.rgb=saturate((c.rgb-(1-opacity))/max(opacity,.01));
    // The delivered front reference has handwritten notes and a colour palette outside the figure.
    if(_ReferenceNotes>.5&&((i.uv.x<.36&&i.uv.y>.875)||(i.uv.x<.14&&i.uv.y>.50)))c.a=0;
    return c*i.color;
   }
  ENDCG }
 }
}
