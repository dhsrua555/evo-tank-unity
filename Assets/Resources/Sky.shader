// 물속 배경: 보는 방향마다 물빛(위가 밝고 머리 위엔 스넬 창). 몸의 물 안개도 같은 색으로 녹아 이음매가 없다
Shader "Evo/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "EvoWater.cginc"
            struct v2f { float4 pos : SV_POSITION; float3 d : TEXCOORD0; };
            v2f vert (float4 v : POSITION) { v2f o; o.pos = UnityObjectToClipPos(v); o.d = v.xyz; return o; }
            fixed4 frag (v2f i) : SV_Target { return fixed4(WaterCol(normalize(i.d)), 1); }
            ENDCG
        }
    }
}
