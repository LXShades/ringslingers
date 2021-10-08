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
            Tags { "RenderType" = "Opaque" "DisableBatching" = "True" }
            Cull Off
            LOD 200

            CGPROGRAM
            #define CUTOUT_ENABLED
            #include "./SRB2Style.cginc"
            ENDCG
        }
    }
    FallBack "Diffuse"
}