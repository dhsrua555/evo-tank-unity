// 화면 그리기 하나로 다 쓴다: uv가 (0,0)이면 꽉 찬 사각형·선, 아니면 uv(−1–1) 원판(가장자리 부드럽게). 정점 색 그대로.
Shader "Life/Disc"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv), w = max(fwidth(d), 1e-4);
                float a = dot(i.uv, i.uv) == 0 ? 1 : saturate((1 - d) / w);
                return fixed4(i.color.rgb, i.color.a * a);
            }
            ENDCG
        }
    }
}
