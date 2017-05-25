Shader "Unlit/downsampleShader"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" { }
		_ResampleOffset ("_ResampleOffset", Vector) = (0,0,0,0)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float2 _ResampleOffset;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col =
					tex2D(_MainTex, i.uv) * 0.38026
					+ tex2D(_MainTex, i.uv - _ResampleOffset) * 0.27667
					+ tex2D(_MainTex, i.uv + _ResampleOffset) * 0.27667
					+ tex2D(_MainTex, i.uv - 2.0 * _ResampleOffset) * 0.08074
					+ tex2D(_MainTex, i.uv + 2.0 * _ResampleOffset) * 0.08074
					+ tex2D(_MainTex, i.uv - 3.0 * _ResampleOffset) * -0.02612
					+ tex2D(_MainTex, i.uv + 3.0 * _ResampleOffset) * -0.02612
					+ tex2D(_MainTex, i.uv - 4.0 * _ResampleOffset) * -0.02143
					+ tex2D(_MainTex, i.uv + 4.0 * _ResampleOffset) * -0.02143;
				col.w = 1;
				return col;
			}
			ENDCG
		}
	}
}
