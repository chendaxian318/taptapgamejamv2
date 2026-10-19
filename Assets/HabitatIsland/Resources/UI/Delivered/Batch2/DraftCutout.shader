Shader "Habitat/UI Draft Cutout"
{
 Properties{[PerRendererData]_MainTex("Texture",2D)="white"{}}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 sampler2D _MainTex;
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(v2f i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv);float hi=max(c.r,max(c.g,c.b)),lo=min(c.r,min(c.g,c.b));float keep=max(smoothstep(.025,.08,hi-lo),max(smoothstep(.76,.92,hi),1-smoothstep(.16,.3,hi)));c.a*=keep;return c*i.color;}
 ENDCG}
 }
}
