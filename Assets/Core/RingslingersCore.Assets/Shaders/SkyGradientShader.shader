Shader "Unlit/SkyGradientShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _UpperColor ("Upper Color", Color) = (1, 1, 1, 1)
        _MiddleColor ("Middle Color", Color) = (1, 1, 1, 1)
        _LowerColor ("Lower Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite Off

        Pass
        {
            Cull Front
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            float4 _UpperColor;
            float4 _MiddleColor;
            float4 _LowerColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 originalPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex.xyz += _WorldSpaceCameraPos.xyz;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.originalPosition = v.vertex;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float YNormalized = (i.originalPosition.y) + 0.5f;
                fixed4 col = tex2D(_MainTex, i.uv) * lerp(_LowerColor, lerp(_MiddleColor, _UpperColor, clamp(YNormalized * 2 - 1, 0, 1)), min(YNormalized * 2, 1));
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
