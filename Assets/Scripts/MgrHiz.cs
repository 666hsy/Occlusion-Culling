using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//存放一些公共数据
public class MgrHiz
{
    private static MgrHiz instance;

    public const int HZBInfoCount = 3;

    public HZBInfo[] hzbInfos = new HZBInfo[HZBInfoCount];

    private string generateDepthMipTag = "GenerateDepthMip";
    private string hiZCullingTag = "HiZCullingTag";
    
    public Shader GenDepthRTShader;    //Hiz的Shader

    public ComputeShader GenerateMipmapCS;     //生成Mip的CS
    public ComputeShader GPUCullingCS;         //GPU剔除CS
    
    public ComputeBuffer StaticMeshBuffer;

    public ComputeBuffer DynamicMeshBuffer;

    public bool Enable = false;
    const int IntBits = 32;
    int kernalInitialize = -1;
    int kernalOcclusionCulling = -1;
    public bool enableDpth = false;

    public Stopwatch stopwatch = new Stopwatch();

    // 存储未完成的请求及其时间戳
    private Queue<long> pendingRequestTimestamps = new Queue<long>();

    // 存储延迟结果（毫秒）
    public List<double> latencyResults = new List<double>();

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
        public static readonly int CameraFrustumPlanes = Shader.PropertyToID("CameraFrustumPlanes");
        public static readonly int CameraMatrixVP = Shader.PropertyToID("CameraMatrixVP");
        public static readonly int HizDepthMap = Shader.PropertyToID("HizDepthMap");
        public static readonly int HizDepthMapSize = Shader.PropertyToID("HizDepthMapSize");
        public static readonly int NumIntMasksID = Shader.PropertyToID("NumIntMasks");
    }

    /// <summary>
    /// 生成深度图的Mip，每帧调用
    /// </summary>
    /// <param name="renderingData"></param>
    public void GenerateDepthMip(ScriptableRenderContext context,ref RenderingData renderingData)
    {
        // scene view下不生成深度图
        if (!Enable || renderingData.cameraData.camera.name == "SceneCamera" || renderingData.cameraData.camera.name == "Preview Camera")
            return;

        var hizIndex = Time.frameCount % HZBInfoCount;
        var hzbInfo = hzbInfos[hizIndex];

        var proj = GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix, false);
        hzbInfo.VpMatrix = proj * renderingData.cameraData.camera.worldToCameraMatrix;

        hzbInfo.EnsureResourceReady(renderingData);
        
        CommandBuffer cmd = CommandBufferPool.Get(generateDepthMipTag);
        cmd.Blit(Texture2D.blackTexture, hzbInfo.hzbDepthRT, hzbInfo.genDepthRTMat);
        
        int mipLevel = 0, w = hzbInfo.hzbDepthRT.width, h = hzbInfo.hzbDepthRT.height;
        do
        {
            mipLevel++;
            w = Mathf.Max(1, w / 2);    //要生成的Mip的宽
            h = Mathf.Max(1, h / 2);    //要生成的Mip的高
            cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HzbSourceTexID, hzbInfo.hzbDepthRT, mipLevel - 1);
            cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HzbDestTexID, hzbInfo.hzbDepthRT, mipLevel);
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
        if (!Enable ||  renderingData.cameraData.camera.name == "SceneCamera" ||
            renderingData.cameraData.camera.name == "Preview Camera")
            return;

        var hizIndex = Time.frameCount % HZBInfoCount;
        var hzbInfo = hzbInfos[hizIndex];
        hzbInfo.readBackSuccess = false;

        var lastHzbInfo = hzbInfos[(hizIndex + HZBInfoCount - 1) % HZBInfoCount];

        hzbInfo.EnsureResourceReady(renderingData);

        var camera = renderingData.cameraData.camera;
        CommandBuffer cmd = CommandBufferPool.Get(hiZCullingTag);
        
        if (kernalOcclusionCulling == -1)
        {
            kernalInitialize = GPUCullingCS.FindKernel("IntializeResultBuffer");
        }

        int numIntMasks = 0;

        if (DynamicMeshBuffer != null)
            numIntMasks = Mathf.CeilToInt((float)StaticMeshBuffer.count / (float)IntBits) +
                          Mathf.CeilToInt((float)DynamicMeshBuffer.count / (float)IntBits);
        else
            numIntMasks = Mathf.CeilToInt((float)StaticMeshBuffer.count / (float)IntBits);

        numIntMasks = Mathf.Max(numIntMasks, 1);
        cmd.SetComputeIntParam(GPUCullingCS, ShaderConstants.NumIntMasksID, numIntMasks);
        int igx = Mathf.CeilToInt((float)numIntMasks / 64.0f);
        igx = Mathf.Max(igx, 1);
        cmd.SetComputeBufferParam(GPUCullingCS, kernalInitialize, ShaderConstants.CullingResultBufferID, hzbInfo.CullingResultBuffer);
        cmd.DispatchCompute(GPUCullingCS, kernalInitialize, igx, 1, 1);
        
        
        cmd.SetComputeIntParam(GPUCullingCS, ShaderConstants.MaxCountID, StaticMeshBuffer.count);
        cmd.SetComputeBufferParam(GPUCullingCS, 0, ShaderConstants.StaticMeshBufferID, StaticMeshBuffer);
        cmd.SetComputeBufferParam(GPUCullingCS, 0, ShaderConstants.CullingResultBufferID, hzbInfo.CullingResultBuffer);

        UpdateCameraFrustumPlanes(hzbInfo, camera);
        cmd.SetComputeMatrixParam(GPUCullingCS, ShaderConstants.CameraMatrixVP, lastHzbInfo.VpMatrix);
        cmd.SetComputeTextureParam(GPUCullingCS, 0, ShaderConstants.HizDepthMap, hzbInfo.hzbDepthRT);
        cmd.SetComputeVectorParam(GPUCullingCS, ShaderConstants.HizDepthMapSize, new Vector3(hzbInfo.hzbDepthRT.width, hzbInfo.hzbDepthRT.height, hzbInfo.hzbDepthRT.mipmapCount));
        
        cmd.DispatchCompute(GPUCullingCS, 0, Mathf.CeilToInt(StaticMeshBuffer.count / 64f), 1, 1);

        // 记录发起请求的时间戳（以Stopwatch的Ticks为单位）
        long requestTimestamp = stopwatch.ElapsedTicks;
        pendingRequestTimestamps.Enqueue(requestTimestamp);

        cmd.RequestAsyncReadback(hzbInfo.CullingResultBuffer, (req) => OnGPUCullingReadBack(req, hzbInfo));
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void UpdateCameraFrustumPlanes(HZBInfo hzbInfo, Camera camera)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, hzbInfo.cameraFrustumPlanes);
        for (var i = 0; i < hzbInfo.cameraFrustumPlanes.Length; i++)
        {
            Vector4 v4 = (Vector4)hzbInfo.cameraFrustumPlanes[i].normal;
            v4.w = hzbInfo.cameraFrustumPlanes[i].distance;
            hzbInfo.cameraFrustumPlanesV4[i] = v4;
        }
        GPUCullingCS.SetVectorArray(ShaderConstants.CameraFrustumPlanes, hzbInfo.cameraFrustumPlanesV4);
    }

    private void OnGPUCullingReadBack(AsyncGPUReadbackRequest request,HZBInfo hZBInfo)
    {
        if (request.done && !request.hasError)
        {
            request.GetData<int>().CopyTo(hZBInfo.cullResults);
            hZBInfo.readBackSuccess = true;

            long callbackTimestamp = stopwatch.ElapsedTicks;
            long latencyTicks = callbackTimestamp - pendingRequestTimestamps.Dequeue();

            // 转换为毫秒（Stopwatch.Frequency单位为Hz，1秒=1e7 ticks）
            double latencyMs = (latencyTicks * 1000.0) / Stopwatch.Frequency;
            latencyResults.Add(latencyMs);

        }
        else
        {
            UnityEngine.Debug.LogError("ReadBackFailed");
            hZBInfo.readBackSuccess = false;
        }
    }
    
    public void ShowDepth(ScriptableRenderContext context, ref RenderingData renderingData,Material _depthMaterial)
    {
        if (enableDpth)
        {
            var hizIndex = Time.frameCount % HZBInfoCount;
            var hzbInfo = hzbInfos[hizIndex];

            var cmd = CommandBufferPool.Get("DepthMapPass");
            cmd.Blit(hzbInfo.hzbDepthRT, renderingData.cameraData.renderer.cameraColorTarget, _depthMaterial);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

//存储每帧HZB需要的一些信息
public class HZBInfo   
{
    public RenderTexture hzbDepthRT;   //深度图RT
    public Material genDepthRTMat;   //生成深度图的材质
    public Matrix4x4 VpMatrix;       //存储当前帧的VP矩阵，供下一帧进行重投影

    public Plane[] cameraFrustumPlanes = new Plane[6];
    public Vector4[] cameraFrustumPlanesV4 = new Vector4[6];

    public ComputeBuffer CullingResultBuffer;
    public int[] cullResults;    //存储剔除结果 静态+动态，对应ComputeShader中的CullResult

    public bool readBackSuccess = false;    //当前帧的剔除结果是否读取成功

    public void EnsureResourceReady(RenderingData renderingData)
    {
        if (hzbDepthRT == null)
            CreateHzbRT(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
        if (genDepthRTMat == null)
            genDepthRTMat = new Material(MgrHiz.Instance.GenDepthRTShader);
    }

    private void CreateHzbRT(int width, int height)
    {
        int resizeX = Mathf.IsPowerOfTwo(width) ? width : Mathf.NextPowerOfTwo(width);
        int resizeY = Mathf.IsPowerOfTwo(height) ? height : Mathf.NextPowerOfTwo(height);

        RenderTextureFormat r16RTF = GraphicsFormatUtility.GetRenderTextureFormat(GraphicsFormat.R16_SFloat);
        if (SystemInfo.SupportsRenderTextureFormat(r16RTF) &&
            SystemInfo.SupportsRandomWriteOnRenderTextureFormat(r16RTF))
            hzbDepthRT = new RenderTexture(resizeX, resizeY, 0, GraphicsFormat.R16_SFloat);
        else
            hzbDepthRT = new RenderTexture(resizeX, resizeY, 0, GraphicsFormat.R32_SFloat);

        hzbDepthRT.name = "HzbDepthRT";
        hzbDepthRT.useMipMap = true;
        hzbDepthRT.autoGenerateMips = false;
        hzbDepthRT.enableRandomWrite = true;
        hzbDepthRT.filterMode = FilterMode.Point;
        hzbDepthRT.wrapMode = TextureWrapMode.Clamp;
        hzbDepthRT.Create();
    }

}
