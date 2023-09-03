Shader "Custom/Shield"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _RotationSpeed ("Wobble speed (degrees/sec)", Range(-720, 720)) = 360.0
        _WobbleStrength ("Wobble strength", Range(0, 5)) = 2
        _InnerAlpha ("Inner alpha", Range(0, 1)) = 1
        _OuterAlpha ("Outer alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True"}
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard alpha vertex:vert

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _RotationSpeed;
        float _WobbleStrength;
        float _InnerAlpha;
        float _OuterAlpha;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v) {
            float rotation = _RotationSpeed * _Time.y * UNITY_PI / 180;
            float sine, cosine;

            rotation += v.vertex.z + v.vertex.y * 215.f;
            sincos(rotation % (UNITY_PI*2), sine, cosine);

            float3 camFwd = unity_CameraToWorld._m02_m12_m22;
            v.vertex.xyz += v.normal * (sine * cosine + v.vertex.x / 1.1f + v.vertex.z / 1.5f + v.vertex.x / 1.7f + v.vertex.y / -1.6f) * _WobbleStrength * 0.01f;
            v.color = float4(1, 1, 1, lerp(_OuterAlpha, _InnerAlpha, max(0, -dot(mul(unity_ObjectToWorld, v.normal.xyz), camFwd))));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = IN.color.rgb * c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = IN.color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
