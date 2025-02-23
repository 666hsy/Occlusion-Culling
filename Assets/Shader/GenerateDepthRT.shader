Shader "Custom/GenerateDepthRT"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positioonOS  : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            TEXTURE2D_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;
            

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positioonOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float frag (Varyings i) : SV_Target
            {
                float2 offset = _CameraDepthTexture_TexelSize.xy * 0.5;
                float x = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv + offset, 0).x;
                float y = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv - offset, 0).x;
                float z = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv + float2(offset.x, -offset.y), 0).x;
                float w = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv + float2(-offset.x, offset.y), 0).x;
                float4 readDepth = float4(x,y,z,w);
                #if UNITY_REVERSED_Z
                    readDepth.xy = min(readDepth.xy, readDepth.zw);
                    readDepth.x = min(readDepth.x, readDepth.y);
                #else
                    readDepth.xy = max(readDepth.xy, readDepth.zw);
                    readDepth.x = max(readDepth.x, readDepth.y);
                #endif
                return readDepth.x;
            }
            ENDHLSL
        }
    }
}
