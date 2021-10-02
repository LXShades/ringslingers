Shader "Custom/SRB2 Skybox"
{
	Properties
	{
		_Texture("Texture", 2D) = "white" {}
		_VerticalScale("Vertical Scale", Range(1, 3)) = 1
		_HorizontalScale("Horizontal Scale", Range(0, 10)) = 1
		_ClampVertical("Clamp", Range(1, 2)) = 1
	}

	SubShader
	{
		Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
		Cull Off
		ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			sampler2D _Texture;
			float _VerticalScale;
			float _HorizontalScale;
			float _ClampVertical;

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 texcoord : TEXCOORD0;
			};

			v2f vert(appdata v)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(v.vertex);
				output.texcoord = float3(output.vertex.x / output.vertex.w, (clamp(v.vertex.y * _VerticalScale, -_ClampVertical, _ClampVertical) + 1) / 2, 0);
				output.texcoord.x = output.texcoord.x * _HorizontalScale - atan2(unity_CameraToWorld[2].x, unity_CameraToWorld[2].z) * _HorizontalScale;
				return output;
			}

			half4 frag(v2f input) : SV_Target
			{
				fixed4 pixel = tex2D(_Texture, input.texcoord.xy);
				return pixel;
			}

			ENDCG
		}
	}

	Fallback Off
}