sampler2D _MainTex;

struct Input
{
    float2 uv_MainTex;
};

half _Glossiness;
half _Metallic;
fixed4 _Color;

// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
// #pragma instancing_options assumeuniformscaling
UNITY_INSTANCING_BUFFER_START(Props)
    // put more per-instance properties here
UNITY_INSTANCING_BUFFER_END(Props)

void vert(inout appdata_full v) {
    // neutralise lighting
    v.normal = normalize(mul(unity_WorldToObject, float3(0, 1, 0)));
}

void surf(Input IN, inout SurfaceOutputStandard o)
{
    // Albedo comes from a texture tinted by color
    fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

#if defined(CUTOUT_ENABLED)
    if (c.a < 0.5)
        discard;
#endif

    o.Albedo = c.rgb;
    o.Metallic = _Metallic;
    o.Smoothness = _Glossiness;
    o.Alpha = c.a;
}