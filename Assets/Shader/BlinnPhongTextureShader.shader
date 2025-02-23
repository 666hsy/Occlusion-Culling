Shader "Custom/Blinn-Phong"
{
    Properties
    {
        _BaseMap("MainTex", 2D) = "White" { }
        _BaseColor("BaseColor", Color) = (1.0, 1.0, 1.0, 1.0)
        _SpecColor("Specular", Color) = (1.0, 1.0, 1.0, 1.0)
        _Smoothness("Gloss", Range(8.0, 256)) = 20
    }

    SubShader
    {
        // URP的shader要在Tags中注明渲染管线是UniversalPipeline
        Tags
        {
            "RanderPipline" = "UniversalPipeline"
            "RanderType" = "Opaque"
        }

        HLSLINCLUDE

            // 引入Core.hlsl头文件，替换UnityCG中的cginc
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : POSITION_WS;
                float2 uv         : TEXCOORD0;
                float3 normalWS    : NORMAL_WS;
            };

        ENDHLSL

        Pass
        {
            // 声明Pass名称，方便调用与识别
            Name "ForwardUnlit"
            Tags {"LightMode" = "UniversalForward"}

            HLSLPROGRAM

                // 声明顶点/片段着色器对应的函数
                #pragma vertex vert
                #pragma fragment frag

                // 顶点着色器
                Varyings vert(Attributes input)
                {
                    // GetVertexPositionInputs方法根据使用情况自动生成各个坐标系下的定点信息
                    const VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                    const VertexNormalInputs   vertexNormalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                    Varyings output;
                    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                    output.positionCS = vertexInput.positionCS;
                    output.positionWS = vertexInput.positionWS;
                    output.normalWS   = vertexNormalInput.normalWS;
                    return output;
                }

                // 片段着色器
                half4 frag(Varyings input) : SV_Target
                {
                    real3 positionWS = input.positionWS;
                    Light mainLight = GetMainLight(); // 主光源
                    real3 lightColor = mainLight.color; // 主光源颜色
                    real3 normalWS = normalize(input.normalWS);
                    real3 lightDir = normalize(mainLight.direction); // 主光源方向
                    real3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
              
                    real3 ambient = SampleSH(normalWS) * albedo; // 环境光

                    real3 diffuse = saturate(dot(lightDir,normalWS)) * lightColor * albedo; // 漫反射

                    real3 viewDirectionWS = SafeNormalize(GetCameraPositionWS() - positionWS); // safe防止分母为0
                    real3 h = SafeNormalize(viewDirectionWS + lightDir);
                    real3 specular = pow(saturate(dot(h, input.normalWS)), _Smoothness) * lightColor * saturate(_SpecColor); // 高光
              
                    return real4(ambient + diffuse + specular, 1.0);
                }
            ENDHLSL
        }
    }
}