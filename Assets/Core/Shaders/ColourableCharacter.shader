// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/ColourableCharacter"
{
    Properties
    {
        _SourceColorRange ("Source color range", Range(0,1.73)) = 0.1
        _SourceColor ("Source color", Color) = (0,0,1,1)
        _OutlineThickness("Outline thickness", Range(0, 0.005)) = 0.002
        _OutlineColor ("Outline color", Color) = (0, 0, 0, 1)
        _OutlinePushbask ("Outline pushback", Range(0, 0.2)) = 0.1
        _Color ("Target color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        LOD 200

        // Render transparent with depth, so that transparent rendering can happen afterwards without overlapping itself
        Pass
        {
            Tags { "RenderType"="Transparent" "Queue"="Opaque" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = fixed4(0, 0, 0, 0);
                return col;
            }

            ENDCG
        }
        
        // Render base colours
        Cull Back
        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows alpha
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _SourceColor;
        half _SourceColorRange;
        half _Alpha;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            half lerpFactor = clamp(1.73 - distance(tex.rgb, _SourceColor.rgb) / _SourceColorRange, 0, 1);
            lerpFactor = min(length(_Color.rgb) * 9999, lerpFactor); // if sourcecolor is 100% black, don't lerp
            fixed4 c = lerp(tex, _Color, lerpFactor);

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = _Alpha;
        }
        ENDCG

        // Render outline
        Cull Front
        CGPROGRAM

        #pragma surface surf Standard vertex:vert alpha
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 _OutlineColor;
        half _OutlineThickness;
        half _OutlinePushback;
        half _Alpha;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v)
        {
            v.vertex.xyz += v.normal * _OutlineThickness;
            v.vertex.xyz += normalize(v.vertex.xyz - mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1))) * _OutlinePushback;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _OutlineColor;
            o.Emission = _OutlineColor;
            o.Metallic = 0;
            o.Smoothness = 0;
            o.Alpha = _OutlineColor.a * _Alpha;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
