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
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Cull Back
        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows
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
            o.Alpha = c.a;
        }
        ENDCG

        Cull Front
        CGPROGRAM

        #pragma surface surf Standard vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 _OutlineColor;
        half _OutlineThickness;
        half _OutlinePushback;

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
            o.Alpha = _OutlineColor.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
