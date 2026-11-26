Shader "ZooPC/JournalPaper"
{
    Properties { _MainTex ("Printed page", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float3 normal:NORMAL; };
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float shade:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex);
                // EndlessBook's supplied FBX page occupies U=0..0.803714
                // in a square template. Our portrait page uses the full texture.
                o.uv=float2(v.uv.x/.803714,v.uv.y);
                float3 n=normalize(UnityObjectToWorldNormal(v.normal));
                o.shade=.78+.22*saturate(dot(n,normalize(float3(-.15,1,-.2))));return o;
            }
            fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv);c.rgb*=i.shade;return c; }
            ENDCG
        }
    }
}
