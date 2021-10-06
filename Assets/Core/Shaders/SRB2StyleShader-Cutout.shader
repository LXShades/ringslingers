Shader "Custom/SRB2 Style (Cutout)"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
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