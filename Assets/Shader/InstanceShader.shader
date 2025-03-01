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
                float3 normal : NORMAL;
                uint instanceID : SV_InstanceID;  // 关键：通过 SV_InstanceID 获取实例索引
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                float4x4 instanceMatrix = InstanceDataList[v.instanceID];
                float4 worldPos = mul(instanceMatrix, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.worldPos = worldPos.xyz;
                float3x3 normalMatrix = transpose((float3x3)instanceMatrix);
                o.normal = mul(normalMatrix, v.normal);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 L = _MainLightPosition.xyz;
                float3 diffuse = saturate(dot(N, L)) * _MainLightColor.rgb;
                
                return half4(diffuse, 1.0);
            }

            ENDHLSL
        }
    }
}
