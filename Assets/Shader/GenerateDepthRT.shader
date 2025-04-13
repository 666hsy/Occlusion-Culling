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
                return SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv, 0).x;
            }
            ENDHLSL
        }
    }
}
