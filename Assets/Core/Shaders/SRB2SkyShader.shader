Shader "Custom/SRB2 Skybox"
{
	Properties
	{
		_Texture("Texture", 2D) = "white" {}
		_ImageScale("Image Scale", Vector) = (1, 1, 1, 1)
		_VerticalScale("Vertical Scroll Scale", Range(1, 10)) = 1
		_HorizontalScale("Horizontal Scroll Scale", Range(1, 10)) = 1
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
			float4 _ImageScale;
			float _ClampVertical;

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert(appdata v)
			{
				v2f output;
				float4 clipPos = mul(UNITY_MATRIX_M, float4(v.vertex.xyz, 1));
				float WidthHeightRatio = _ScreenParams.x / _ScreenParams.y;

				clipPos.xyz -= unity_CameraToWorld[3].xyz; // relative to camera, but with camera theoretically facing forward so the vertices don't move when turning
				clipPos = mul(UNITY_MATRIX_P, clipPos);

				output.vertex = clipPos;
				output.texcoord = (clipPos.xy * _ImageScale.xy / clipPos.w + float2(1, 1)) / 2;
				output.texcoord.x += atan2(unity_CameraToWorld[0].z, unity_CameraToWorld[2].z) / (6.28 * WidthHeightRatio) * _HorizontalScale;
				output.texcoord.y -= asin(unity_CameraToWorld[1].z) / (3.14) * _VerticalScale;


				output.texcoord.x *= _ScreenParams.x / _ScreenParams.y;
				output.texcoord.y = -output.texcoord.y;


				return output;
			}

			half4 frag(v2f input) : SV_Target
			{
				fixed4 pixel = tex2D(_Texture, input.vertex.xy / _ScreenParams.y);
				pixel = tex2D(_Texture, input.texcoord.xy);
				return pixel;
			}

			ENDCG
		}
	}

	Fallback Off
}