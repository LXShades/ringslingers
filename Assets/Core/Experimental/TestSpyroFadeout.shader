Shader "Custom/TestSpyroFadeout"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _FadeStart("FadeStart", Range(0,100)) = 0.0
        _FadeEnd("FadeEnd", Range(0,100)) = 0.0
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows
        #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float4 color : COLOR;
        };

        half _Glossiness;
        half _Metallic;
        half _FadeStart;
        half _FadeEnd;
        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            float4 screenSpace = mul(UNITY_MATRIX_VP, float4(IN.worldPos, 1));
            screenSpace = screenSpace / screenSpace.w;
            half depth = LinearEyeDepth(screenSpace.z);
            fixed4 c = lerp(tex2D(_MainTex, IN.uv_MainTex), IN.color, clamp((depth - _FadeStart) / (_FadeEnd - _FadeStart), 0, 1)) * _Color;

            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
