Shader "Custom/SRB2 Style (Cutout)"
{
    Properties
    {
        _MainTex("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            Tags { "LightMode"="ForwardBase" "RenderType" = "Transparent" "DisableBatching" = "True" "Queue"="Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }
            Cull Off
            LOD 200

            CGPROGRAM
            #define CUTOUT_ENABLED
            #pragma vertex vert
            #pragma fragment frag
            #include "./SRB2Style.cginc"
            ENDCG
        }
    }
    FallBack "Diffuse"
}