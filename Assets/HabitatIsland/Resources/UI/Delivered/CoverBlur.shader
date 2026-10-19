Shader "Habitat/UI Cover Gaussian"
{
    Properties { _MainTex("Texture",2D)="white"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _Direction;
            fixed4 frag(v2f_img i):SV_Target
            {
                // Dense samples avoid repeated outlines when the entrance blur is wide.
                float4 c=0;float total=0;
                [unroll] for(int tap=-12;tap<=12;tap++)
                {
                    float offset=tap*.25;
                    float weight=exp(-.5*offset*offset);
                    c+=tex2D(_MainTex,i.uv+_Direction.xy*offset)*weight;
                    total+=weight;
                }
                return c/total;
            }
            ENDCG
        }
    }
}
