// 피사계 심도용: 생물 몸의 카메라 거리(화면 단위)를 따로 그린다. 꼭짓점 움직임은 본 셰이더와 같다
Shader "Evo/Depth"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "EvoAnim.cginc"
            struct appdata { float4 vertex : POSITION; float4 uv0 : TEXCOORD0; float3 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float z : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o; float3 p = EvoAnim(v.vertex.xyz, v.uv0, v.uv1);
                o.pos = UnityObjectToClipPos(float4(p, 1)); o.z = -UnityObjectToViewPos(p).z; return o;
            }
            float4 frag (v2f i) : SV_Target { return i.z; }
            ENDCG
        }
    }
}
