Shader "A Day at the Zoo/Water Styles"
{
    Properties
    {
        [Header(Water Color)]
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.78, 0.90, 1)
        _DeepColor ("Deep Color", Color) = (0.02, 0.24, 0.55, 1)
        _SkyColor ("Reflection Tint", Color) = (0.58, 0.82, 1.0, 1)
        _Opacity ("Base Opacity", Range(0, 1)) = 0.68
        _DepthFade ("Depth Blend Distance", Range(0.05, 20)) = 5
        _ReflectionStrength ("Sky Reflection", Range(0, 1)) = 0.32

        [Header(Flow and Waves)]
        _FlowDirection ("Flow Direction (XZ)", Vector) = (1, 0.15, 0, 0)
        _WaveAmplitude ("Vertex Wave Height", Range(0, 1)) = 0.10
        _WaveFrequency ("Wave Frequency", Range(0.1, 12)) = 2.4
        _WaveSpeed ("Slow Flow Speed", Range(0, 3)) = 0.32
        _NormalStrength ("Ripple Normal", Range(0, 1)) = 0.18
        _FacetStrength ("Low-poly Facets", Range(0, 1)) = 0.35

        [Header(Sparkles)]
        _SparkleColor ("Sparkle Color", Color) = (1, 1, 1, 1)
        _SparkleStrength ("Sparkle Strength", Range(0, 3)) = 0.45
        _SparkleScale ("Sparkle Scale", Range(0.05, 3)) = 0.50
        _SparkleSpeed ("Sparkle Speed", Range(0.05, 4)) = 0.72

        [Header(Foam and Shoreline)]
        _FoamColor ("Foam Color", Color) = (0.92, 1, 1, 1)
        _FoamStrength ("Foam Strength", Range(0, 2)) = 0.65
        _FoamDepth ("Shoreline Width", Range(0.02, 4)) = 0.55
        _FoamScale ("Foam Pattern Scale", Range(0.1, 12)) = 1.8

        [Header(Lighting)]
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularPower ("Specular Focus", Range(4, 512)) = 110
        _SpecularStrength ("Specular Strength", Range(0, 6)) = 2
        _ToonLighting ("Toon Light Steps", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 300
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _SkyColor;
            float _Opacity;
            float _DepthFade;
            float _ReflectionStrength;

            float4 _FlowDirection;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _NormalStrength;
            float _FacetStrength;

            fixed4 _SparkleColor;
            float _SparkleStrength;
            float _SparkleScale;
            float _SparkleSpeed;

            fixed4 _FoamColor;
            float _FoamStrength;
            float _FoamDepth;
            float _FoamScale;

            fixed4 _SpecularColor;
            float _SpecularPower;
            float _SpecularStrength;
            float _ToonLighting;

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Sparkles(float2 uv, float time)
            {
                float2 grid = uv * _SparkleScale;
                float2 cell = floor(grid);
                float2 local = frac(grid);
                float seed = Hash21(cell);
                float2 center = 0.25 + 0.5 * float2(Hash21(cell + 7.1), Hash21(cell + 19.7));
                float radius = 0.035 + seed * 0.10;
                float star = smoothstep(radius, 0.0, length(local - center));
                float pulse = pow(0.5 + 0.5 * sin(time * (0.8 + seed * 1.8) + seed * 17.0), 7.0);
                return star * pulse;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 flow = normalize(_FlowDirection.xz + 0.0001);
                float t = _Time.y * _WaveSpeed;
                float waveA = sin(dot(worldPos.xz, flow) * _WaveFrequency + t);
                float waveB = sin(dot(worldPos.xz, flow.yx) * (_WaveFrequency * 0.63) - t * 0.72);
                float waveC = sin((worldPos.x + worldPos.z) * (_WaveFrequency * 0.31) + t * 0.43);
                worldPos.y += (waveA * 0.56 + waveB * 0.29 + waveC * 0.15) * _WaveAmplitude;

                o.pos = UnityWorldToClipPos(worldPos);
                o.screenPos = ComputeScreenPos(o.pos);
                o.worldPos = worldPos;
                o.eyeDepth = -UnityWorldToViewPos(worldPos).z;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                float sceneEyeDepth = LinearEyeDepth(rawDepth);
                float waterThickness = max(sceneEyeDepth - i.eyeDepth, 0.0);
                float depthBlend = saturate(waterThickness / max(_DepthFade, 0.001));

                float3 dx = ddx(i.worldPos);
                float3 dy = ddy(i.worldPos);
                float3 facetNormal = normalize(cross(dx, dy));
                if (facetNormal.y < 0.0)
                {
                    facetNormal = -facetNormal;
                }

                float2 flow = normalize(_FlowDirection.xz + 0.0001);
                float flowPhase = dot(i.worldPos.xz, flow) * _WaveFrequency - _Time.y * _WaveSpeed * 2.0;
                float2 rippleVector = float2(sin(flowPhase), cos(flowPhase * 1.37 + i.worldPos.x * 0.71));
                float3 smoothNormal = normalize(float3(
                    rippleVector.x * _NormalStrength,
                    1.0,
                    rippleVector.y * _NormalStrength
                ));
                float3 normalWS = normalize(lerp(smoothNormal, facetNormal, _FacetStrength));

                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 halfDir = normalize(lightDir + viewDir);

                float nDotL = saturate(dot(normalWS, lightDir));
                nDotL = lerp(0.28 + nDotL * 0.72, smoothstep(0.42, 0.56, nDotL) * 0.72 + 0.28, _ToonLighting);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 5.0);
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthBlend);
                waterColor *= 0.76 + 0.24 * nDotL;
                waterColor = lerp(waterColor, _SkyColor.rgb, fresnel * _ReflectionStrength);

                float specular = pow(saturate(dot(normalWS, halfDir)), _SpecularPower);
                if (_ToonLighting > 0.5)
                {
                    specular = smoothstep(0.20, 0.24, specular) * 0.72;
                }
                waterColor += _SpecularColor.rgb * specular * _SpecularStrength;

                float2 sparkleUV = i.worldPos.xz + flow * _Time.y * _WaveSpeed * 2.4;
                float sparkleA = Sparkles(sparkleUV, _Time.y * _SparkleSpeed);
                float sparkleB = Sparkles(sparkleUV * 1.73 + 11.2, _Time.y * _SparkleSpeed * 1.31);
                waterColor += _SparkleColor.rgb * (sparkleA + sparkleB * 0.55) * _SparkleStrength;

                float shoreline = 1.0 - smoothstep(0.0, max(_FoamDepth, 0.001), waterThickness);
                float foamNoise = ValueNoise(i.worldPos.xz * _FoamScale + flow * _Time.y * 0.35);
                foamNoise *= ValueNoise(i.worldPos.xz * (_FoamScale * 2.3) - flow * _Time.y * 0.22);
                float foam = shoreline * saturate(foamNoise * 1.8 + 0.08) * _FoamStrength;
                waterColor = lerp(waterColor, _FoamColor.rgb, saturate(foam));

                float alpha = lerp(_Opacity, max(_Opacity, 0.94), depthBlend);
                alpha = saturate(alpha + fresnel * 0.12 + foam * 0.12);

                fixed4 color = fixed4(waterColor, alpha);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }

    Fallback "Transparent/Diffuse"
}
