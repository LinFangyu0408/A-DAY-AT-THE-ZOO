Shader "Zoo/Deep Stylized Lake"
{
    Properties
    {
        _DeepColor ("Deep Blue", Color) = (0.035, 0.25, 0.53, 1)
        _MidColor ("Lake Blue", Color) = (0.055, 0.48, 0.72, 1)
        _ShallowColor ("Shore Blue", Color) = (0.22, 0.76, 0.86, 1)
        _SkyColor ("Sky Reflection", Color) = (0.54, 0.86, 0.98, 1)
        _FoamColor ("Edge Foam", Color) = (0.75, 0.96, 0.95, 1)
        _WaveHeight ("Wave Height", Range(0, 0.08)) = 0.022
        _WaveScale ("Wave Scale", Range(0.1, 8)) = 2.7
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.72
        _FoamWidth ("Shore Foam Width", Range(0.01, 0.35)) = 0.12
        _FoamStrength ("Shore Foam Strength", Range(0, 1)) = 0.58
        _Tint ("Overall Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _DeepColor;
            fixed4 _MidColor;
            fixed4 _ShallowColor;
            fixed4 _SkyColor;
            fixed4 _FoamColor;
            fixed4 _Tint;
            float _WaveHeight;
            float _WaveScale;
            float _WaveSpeed;
            float _FoamWidth;
            float _FoamStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                float wave =
                    sin(worldPosition.x * _WaveScale + _Time.y * _WaveSpeed) +
                    sin(worldPosition.z * _WaveScale * 0.81 -
                        _Time.y * _WaveSpeed * 0.73 + 1.9);
                v.vertex.y += wave * _WaveHeight * 0.5;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPosition = worldPosition;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = (i.uv - 0.5) * 2.0;
                float radial = saturate(length(centered));
                float centerBlue = smoothstep(0.0, 0.78, radial);
                fixed3 color = lerp(_DeepColor.rgb, _MidColor.rgb, centerBlue);
                color = lerp(color, _ShallowColor.rgb, smoothstep(0.78, 1.0, radial) * 0.55);

                float waveA = sin(
                    (i.worldPosition.x + i.worldPosition.z) * _WaveScale * 1.7 +
                    _Time.y * _WaveSpeed * 1.3);
                float waveB = sin(
                    (i.worldPosition.x * 0.72 - i.worldPosition.z) *
                    _WaveScale * 2.1 - _Time.y * _WaveSpeed * 0.9);
                float ripple = saturate(waveA * 0.5 + waveB * 0.35 + 0.5);
                color += _SkyColor.rgb * ripple * 0.075;

                float shore = smoothstep(1.0 - _FoamWidth, 0.995, radial);
                float brokenFoam = saturate(sin(
                    i.worldPosition.x * 2.2 + i.worldPosition.z * 1.9 +
                    _Time.y * 0.55) * 0.45 + 0.72);
                color = lerp(
                    color,
                    _FoamColor.rgb,
                    shore * brokenFoam * _FoamStrength);
                color *= _Tint.rgb;
                UNITY_APPLY_FOG(i.fogCoord, color);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
