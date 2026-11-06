// 상자 테두리·바닥 격자: 꼭짓점 색 그대로, 반투명
Shader "Evo/Line"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; };
            v2f vert(float4 v : POSITION, fixed4 c : COLOR) { v2f o; o.pos = UnityObjectToClipPos(v); o.col = c; return o; }
            fixed4 frag(v2f i) : SV_Target { return i.col; }
            ENDCG
        }
    }
}
