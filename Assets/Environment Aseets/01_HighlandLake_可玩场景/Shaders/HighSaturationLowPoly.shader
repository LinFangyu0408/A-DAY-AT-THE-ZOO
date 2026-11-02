Shader "Zoo/High Saturation Low Poly"
{
    Properties
    {
        [MainTexture] _MainTex ("Palette / Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 2)) = 1.35
        _Brightness ("Brightness", Range(0, 2)) = 1.12
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.72
        _ShadowLift ("Shadow Lift", Range(0, 0.6)) = 0.14
        _ShadowTint ("Shadow Tint", Color) = (0.76, 0.88, 1, 1)
        _LightTint ("Sun Tint", Color) = (1, 0.96, 0.86, 1)
        _YellowToGreen ("Yellow to Green", Range(0, 1)) = 0
        _RockTint ("Rock Tint", Color) = (0.78, 0.88, 0.96, 1)
        _RockTintStrength ("Rock Tint Strength", Range(0, 1)) = 0
        _ToonThreshold ("Light Threshold", Range(-1, 1)) = 0.12
        _ToonSoftness ("Light Softness", Range(0.001, 0.5)) = 0.2
        _RimColor ("Rim Color", Color) = (0.78, 0.94, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.08
        [Toggle] _UseVertexColor ("Use Vertex Color", Float) = 0
        _Tint ("Per-object Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf ZooToon fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _Tint;
        fixed4 _RimColor;
        fixed4 _ShadowTint;
        fixed4 _LightTint;
        fixed4 _RockTint;
        half _Saturation;
        half _Brightness;
        half _AmbientStrength;
        half _ShadowLift;
        half _YellowToGreen;
        half _RockTintStrength;
        half _ToonThreshold;
        half _ToonSoftness;
        half _RimStrength;
        half _UseVertexColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldNormal;
            float3 viewDir;
            fixed4 color : COLOR;
        };

        fixed3 Boost(fixed3 c)
        {
            half luma = dot(c, fixed3(0.299, 0.587, 0.114));
            c = lerp(luma.xxx, c, _Saturation) * _Brightness;
            return max(c, 0);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed3 palette = tex2D(_MainTex, IN.uv_MainTex).rgb;
            fixed3 vertex = lerp(fixed3(1, 1, 1), IN.color.rgb, _UseVertexColor);
            fixed3 albedo = Boost(palette * _Color.rgb * _Tint.rgb * vertex);
            half yellow = saturate(max(albedo.r - albedo.b, 0) * 1.6 +
                                   max(albedo.g - albedo.b, 0) * 0.4);
            fixed3 greener = fixed3(albedo.r * 0.32, albedo.g * 1.15, albedo.b * 0.7);
            albedo = lerp(albedo, greener, yellow * _YellowToGreen);

            half upward = saturate(normalize(IN.worldNormal).y);
            albedo *= lerp(fixed3(1, 1, 1), _RockTint.rgb * 1.18,
                           (1 - upward) * _RockTintStrength);
            o.Albedo = albedo;
            o.Alpha = 1;

            half rim = pow(1 - saturate(dot(normalize(IN.worldNormal), normalize(IN.viewDir))), 4);
            o.Emission = _RimColor.rgb * rim * _RimStrength;
        }

        half4 LightingZooToon(SurfaceOutput s, half3 lightDir, half atten)
        {
            half lambert = dot(normalize(s.Normal), normalize(lightDir)) * 0.5 + 0.5;
            half band = smoothstep(_ToonThreshold - _ToonSoftness,
                                   _ToonThreshold + _ToonSoftness,
                                   lambert);
            half3 ambient = ShadeSH9(half4(s.Normal, 1)) *
                            _ShadowTint.rgb * (_AmbientStrength + _ShadowLift);
            half3 lit = _LightColor0.rgb * _LightTint.rgb * band * atten * 0.82;
            half4 c;
            c.rgb = saturate(s.Albedo * (ambient + lit) + s.Emission);
            c.a = s.Alpha;
            return c;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
