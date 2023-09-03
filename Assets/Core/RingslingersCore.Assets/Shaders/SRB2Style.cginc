#pragma vertex vert
#pragma fragment frag

// compile shader into multiple variants, with and without shadows
// (we don't care about any lightmaps yet, so skip these variants)
#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
// shadow helper functions and macros
#include "AutoLight.cginc"
#include "UnityCG.cginc"
#include "Lighting.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;

struct v2f
{
    float2 uv : TEXCOORD0;
    SHADOW_COORDS(1) // put shadows data into TEXCOORD1
    fixed3 diff : COLOR0;
    fixed3 ambient : COLOR1;
    float4 pos : SV_POSITION;
    fixed shadowEffect : TEXCOORD2;
};

v2f vert(appdata_base v)
{
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
    half3 worldNormal = UnityObjectToWorldNormal(v.normal);
    half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));

    float upness = abs(dot(worldNormal, half3(0, 1, 0))); // 1 = floor is directly upward (full shadow effect), 0.1 = no shadow effect
    const float buffer = 0.9f;
    o.shadowEffect = clamp(upness - (1 - buffer) / buffer, 0, 1);
    nl = 1;

    const float brightnessMultiplier = 0.85f;
    o.diff = _LightColor0.rgb * nl * brightnessMultiplier;
    o.ambient = ShadeSH9(half4(worldNormal, 1));

    TRANSFER_SHADOW(o)
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    fixed4 col = tex2D(_MainTex, i.uv);

#ifdef CUTOUT_ENABLED
    clip(col.a - 0.5);
#endif

    fixed shadow = SHADOW_ATTENUATION(i);
    fixed3 lighting = i.diff * lerp(1, shadow, i.shadowEffect) + i.ambient;
    col.rgb *= lighting;
    return col;
}