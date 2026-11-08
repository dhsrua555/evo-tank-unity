// 미생물 관찰(암시야 현미경): 막 가장자리가 빛나고 속은 비친다. 더하기 혼합이라 그리는 순서와 무관
Shader "Evo/Micro"
{
    Properties { _Fade ("Fade", Float) = 1 _Sel ("Selected", Float) = 0 _T0 ("T0", Float) = 0 _Rate ("Rate", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "EvoAnim.cginc"
            float _Fade, _Sel;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float4 uv0 : TEXCOORD0; float3 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; float4 col : COLOR; };
            v2f vert (appdata v)
            {
                v2f o; float3 p = EvoAnim(v.vertex.xyz, v.uv0, v.uv1);
                o.pos = UnityObjectToClipPos(float4(p, 1)); o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz; o.col = v.color; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 vd = normalize(_WorldSpaceCameraPos - i.wp);
                float r = 1 - abs(dot(normalize(i.n), vd));
                float a = (0.1 + 0.9 * pow(r, 2.2)) * i.col.a * _Fade;
                return fixed4(i.col.rgb * (1 + 0.8 * _Sel), saturate(a * (1 + _Sel)));
            }
            ENDCG
        }
    }
}
