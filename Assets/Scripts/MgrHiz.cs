using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//存放一些公共数据
public class MgrHiz
{
    private static MgrHiz instance;
    private Matrix4x4 lastVp;       //存储当前帧的VP矩阵，供下一帧进行重投影
    private string generateDepthMipTag = "GenerateDepthMip";
    private RenderTexture hzbDepthRT; //深度图RT
    private Material genDepthRTMat;   //生成深度图的材质
    
    public ComputeShader GenerateMipmapCS;     //生成Mip的CS
    
    
    public static MgrHiz Instance
    {
        get
        {
            if (instance == null)
                instance = new MgrHiz();
            return instance;
        }
        private set { }
    }
    
    private class ShaderConstants
    {
        public static readonly int HzbSourceTexID = Shader.PropertyToID("SourceTex");
        public static readonly int HzbDestTexID = Shader.PropertyToID("DestTex");
        public static readonly int DestTetSizeID = Shader.PropertyToID("DepthRTSize");
    }

    private void CreateHzbRT(int width,int height)
    {
        int resizeX = Mathf.IsPowerOfTwo(width) ? width : Mathf.NextPowerOfTwo(width); 
        int resizeY = Mathf.IsPowerOfTwo(height) ? height : Mathf.NextPowerOfTwo(height);

        hzbDepthRT = new RenderTexture(resizeX, resizeY, 0, RenderTextureFormat.RHalf);
        hzbDepthRT.name = "HzbDepthRT";
        hzbDepthRT.useMipMap = true;
        hzbDepthRT.autoGenerateMips = false;
        hzbDepthRT.enableRandomWrite = true;
        hzbDepthRT.filterMode = FilterMode.Point;
        hzbDepthRT.wrapMode = TextureWrapMode.Clamp;
        hzbDepthRT.Create();
    }

    private void EnsureResourceReady(RenderingData renderingData)
    {
        if (hzbDepthRT == null)
            CreateHzbRT(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
        if (GenerateMipmapCS == null)
            GenerateMipmapCS = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shader/GenerateHzb.compute");
        if (genDepthRTMat == null)
            genDepthRTMat = new Material(Shader.Find("Custom/GenerateDepthRT"));
    }

    /// <summary>
    /// 生成深度图的Mip，每帧调用
    /// </summary>
    /// <param name="renderingData"></param>
    public void GenerateDepthMip(ScriptableRenderContext context,ref RenderingData renderingData)
    {
        // scene view下不生成深度图
        if (renderingData.cameraData.camera.name == "SceneCamera" || renderingData.cameraData.camera.name == "Preview Camera")
            return;
        
        var proj = GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix, true);
        lastVp = proj * renderingData.cameraData.camera.worldToCameraMatrix;
        EnsureResourceReady(renderingData);
        
        CommandBuffer cmd = CommandBufferPool.Get(generateDepthMipTag);
        cmd.Blit(Texture2D.blackTexture, hzbDepthRT,genDepthRTMat);
        
        int mipLevel = 0, w = hzbDepthRT.width, h = hzbDepthRT.height;
        do
        {
            mipLevel++;
            w = Mathf.Max(1, w / 2);    //要生成的Mip的宽
            h = Mathf.Max(1, h / 2);    //要生成的Mip的高
            cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HzbSourceTexID, hzbDepthRT, mipLevel - 1);
            cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HzbDestTexID, hzbDepthRT, mipLevel);
            cmd.SetComputeVectorParam(GenerateMipmapCS, ShaderConstants.DestTetSizeID, new Vector2(w, h));
            
            cmd.DispatchCompute(GenerateMipmapCS, 0, Mathf.Max(1, w / 8), Mathf.Max(1, h / 8), 1);
        } while (w > 1 || h > 1);
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
