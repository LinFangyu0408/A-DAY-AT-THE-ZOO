Shader "Zoo/Monkey Wind Color"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WindDirection ("Wind Direction", Vector) = (0.83, 0, 0.56, 0)
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.12
        _WindSpeed ("Wind Speed", Range(0, 4)) = 1.15
        _WindFrequency ("World Wave Frequency", Range(0, 2)) = 0.34
        _Flutter ("Local Flutter", Range(0, 1)) = 0.25
        _Saturation ("Saturation", Range(0, 2)) = 1.12
        _Brightness ("Brightness", Range(0, 2)) = 1.08
        _ShadowLift ("Shadow Lift", Range(0, 0.6)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        float4 _WindDirection;
        float _WindStrength;
        float _WindSpeed;
        float _WindFrequency;
        float _Flutter;
        half _Saturation;
        half _Brightness;
        half _ShadowLift;

        struct Input
        {
            float4 color : COLOR;
        };

        void vert(inout appdata_full v)
        {
            float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
            float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
            float2 sideDirection = float2(-windDirection.y, windDirection.x);
            float heightWeight = saturate(v.color.a);
            float wave = sin(dot(worldPosition.xz, windDirection) * _WindFrequency +
                             _Time.y * _WindSpeed);
            float slowGust = sin(dot(worldPosition.xz, windDirection) * _WindFrequency * 0.37 +
                                 _Time.y * _WindSpeed * 0.41 + 1.7);
            float flutter = sin(worldPosition.x * 1.73 + worldPosition.z * 1.11 +
                                _Time.y * _WindSpeed * 2.2);
            float sway = wave * 0.72 + slowGust * 0.28 + flutter * _Flutter;
            float3 bend = float3(windDirection.x, 0, windDirection.y) * sway;
            bend += float3(sideDirection.x, 0, sideDirection.y) * flutter * _Flutter * 0.3;
            float3 bendObject = mul(unity_WorldToObject, float4(bend, 0)).xyz;
            v.vertex.xyz += bendObject * heightWeight * heightWeight * _WindStrength;
        }

        fixed3 Boost(fixed3 value)
        {
            half luma = dot(value, fixed3(0.299, 0.587, 0.114));
            return max(lerp(luma.xxx, value, _Saturation) * _Brightness, 0);
        }

        void surf(Input input, inout SurfaceOutput output)
        {
            fixed3 color = Boost(input.color.rgb * _Color.rgb);
            output.Albedo = color;
            output.Emission = color * _ShadowLift * 0.08;
            output.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
