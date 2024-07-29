Shader "Custom/SRB2 Style (Transparent)"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Tags { "Queue" = "Transparent" "LightMode" = "ForwardBase" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "SRB2Style.cginc"
            ENDCG
        }
    }
}
