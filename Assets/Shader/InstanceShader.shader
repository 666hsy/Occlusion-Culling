Shader "Custom/InstanceShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "UniversalForward" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Common.hlsl"
            StructuredBuffer<float4x4> InstanceDataList;    //剔除的结果
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float norm: NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                half3 color : TEXCOORD1;
            };

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                float4 inVertex = v.vertex;
                float2 uv = v.uv;
                //绘制实例化物体
                float4x4 instanceMatrix = InstanceDataList[v.instanceID];
                // inVertex = mul(instanceMatrix, inVertex);
                // o.vertex = TransformWorldToHClip(inVertex.xyz);
                o.vertex=TransformObjectToHClip(inVertex.xyz);
                o.uv = uv;
                
                Light light = GetMainLight();
                o.color = max(0.05,dot(light.direction,v.norm));
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.color;
                return col;
            }

            ENDHLSL
        }
    }
}
