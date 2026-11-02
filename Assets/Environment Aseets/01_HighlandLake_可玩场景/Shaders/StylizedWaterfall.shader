Shader "Zoo/Stylized Waterfall"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.55, 0.98, 1, 1)
        _BottomColor ("Bottom Color", Color) = (0.08, 0.55, 0.78, 1)
        _FoamColor ("Foam Color", Color) = (0.92, 1, 1, 1)
        _Speed ("Flow Speed", Float) = 1.2
        _StripeDensity ("Stripe Density", Float) = 18
        _Opacity ("Opacity", Range(0, 1)) = 0.86
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _BottomColor;
            fixed4 _FoamColor;
            fixed4 _Tint;
            float _Speed;
            float _StripeDensity;
            float _Opacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.x += sin(v.uv.y * 19 + _Time.y * 2.2) * 0.035 * v.uv.y;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float flow = i.uv.y * -7.0 + _Time.y * _Speed;
                float ripple = sin((i.uv.x + sin(flow * 0.47) * 0.08) * _StripeDensity + flow * 5.0);
                float foam = smoothstep(0.67, 0.98, ripple);
                float edge = smoothstep(0.0, 0.13, i.uv.x) * smoothstep(0.0, 0.13, 1.0 - i.uv.x);
                fixed3 water = lerp(_BottomColor.rgb, _TopColor.rgb, saturate(i.uv.y * 0.85 + 0.2));
                fixed3 color = lerp(water, _FoamColor.rgb, foam * 0.56) * _Tint.rgb;
                float alpha = _Opacity * edge * (0.72 + foam * 0.28);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
