Shader "Zoo/Low Poly Wind Grass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.12, 0.42, 0.08, 1)
        _TipColor ("Tip Color", Color) = (0.46, 0.88, 0.22, 1)
        _WindDirection ("Wind Direction", Vector) = (0.85, 0, 0.53, 0)
        _WindStrength ("Wind Strength", Range(0, 0.25)) = 0.075
        _WindSpeed ("Wind Speed", Range(0, 4)) = 1.15
        _WindFrequency ("World Wave Frequency", Range(0, 2)) = 0.42
        _Flutter ("Local Flutter", Range(0, 1)) = 0.28
        _Ambient ("Ambient", Range(0, 1)) = 0.56
        _SunStrength ("Sun Strength", Range(0, 2)) = 0.64
        _Tint ("Overall Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _TipColor;
            fixed4 _Tint;
            float4 _WindDirection;
            float _WindStrength;
            float _WindSpeed;
            float _WindFrequency;
            float _Flutter;
            float _Ambient;
            float _SunStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float2 sideDirection = float2(-windDirection.y, windDirection.x);
                float heightWeight = saturate(v.color.a);
                float worldWave = sin(
                    dot(worldPosition.xz, windDirection) * _WindFrequency +
                    _Time.y * _WindSpeed);
                float slowGust = sin(
                    dot(worldPosition.xz, windDirection) * _WindFrequency * 0.37 +
                    _Time.y * _WindSpeed * 0.41 + 1.7);
                float flutter = sin(
                    worldPosition.x * 1.73 + worldPosition.z * 1.11 +
                    _Time.y * _WindSpeed * 2.2);
                float sway = (worldWave * 0.72 + slowGust * 0.28) +
                             flutter * _Flutter;
                float3 bend = float3(windDirection.x, 0, windDirection.y) * sway;
                bend += float3(sideDirection.x, 0, sideDirection.y) *
                        flutter * _Flutter * 0.32;
                v.vertex.xyz += bend * heightWeight * heightWeight * _WindStrength;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float sun = saturate(dot(normal, lightDirection) * 0.5 + 0.5);
                float heightMix = saturate(i.color.a);
                fixed3 palette = lerp(_BaseColor.rgb, _TipColor.rgb, heightMix);
                fixed3 color = palette * i.color.rgb * _Tint.rgb;
                color *= _Ambient + sun * _SunStrength;
                UNITY_APPLY_FOG(i.fogCoord, color);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
