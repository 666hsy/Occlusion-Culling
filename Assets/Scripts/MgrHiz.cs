using Unity.Collections;
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
    private string hiZCullingTag = "HiZCullingTag";
    
    private RenderTexture hzbDepthRT; //深度图RT
    private Material genDepthRTMat;   //生成深度图的材质
    
    public ComputeShader GenerateMipmapCS;     //生成Mip的CS
    public ComputeShader GPUCullingCS;         //GPU剔除CS
    
    public ComputeBuffer StaticMeshBuffer;
    public ComputeBuffer CullingResultBuffer;
    
    public NativeArray<int> cullResultBackArray ;
    public bool readBackSuccess = false;
    
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
        public static readonly int MaxCountID = Shader.PropertyToID("MaxCount");
        public static readonly int StaticMeshBufferID = Shader.PropertyToID("StaticBoundBuffer");
        public static readonly int CullingResultBufferID = Shader.PropertyToID("CullResult");
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
        if (GPUCullingCS == null)
            GPUCullingCS = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shader/GPUCulling.compute");
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
    
    /// <summary>
    /// 执行剔除
    /// </summary>
    /// <param name="context"></param>
    /// <param name="renderingData"></param>
    public void ExecuteCull(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // scene view下不做cull，直接使用GameView下的cull result
        if (renderingData.cameraData.camera.name == "SceneCamera" ||
            renderingData.cameraData.camera.name == "Preview Camera")
            return;
        
        EnsureResourceReady(renderingData);
        var camera = renderingData.cameraData.camera;
        
        CommandBuffer cmd = CommandBufferPool.Get(hiZCullingTag);
        cmd.SetComputeIntParam(GPUCullingCS, ShaderConstants.MaxCountID, StaticMeshBuffer.count);
        cmd.SetComputeBufferParam(GPUCullingCS, 0, ShaderConstants.StaticMeshBufferID, StaticMeshBuffer);
        cmd.SetComputeBufferParam(GPUCullingCS, 0, ShaderConstants.CullingResultBufferID, CullingResultBuffer);
        cmd.DispatchCompute(GPUCullingCS, 0, Mathf.CeilToInt(StaticMeshBuffer.count / 64f), 1, 1);
        
        cmd.RequestAsyncReadback(CullingResultBuffer, (req)=>OnGPUCullingReadBack(req));
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void OnGPUCullingReadBack(AsyncGPUReadbackRequest request)
    {
        if (request.done && !request.hasError)
        {
            cullResultBackArray.CopyFrom(request.GetData<int>());
            readBackSuccess = true;
        }
        else
        {
            readBackSuccess = false;
        }
    }
}
