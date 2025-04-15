using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

//存放一些公共数据
public class MgrHiz
{
    private static MgrHiz instance;

    public List<MeshRenderer> staticMeshRenders;
    public long TotalFrameCount = 0;
    public long FailFrameCount = 0;
    
    public HZBInfo[] hzbInfos = new HZBInfo[CommonData.HZBInfoCount];

    private string generateDepthMipTag = "GenerateDepthMip";
    private string hiZCullingTag = "HiZCullingTag";
    
    public Shader GenDepthRTShader;    //Hiz的Shader

    public ComputeShader GenerateMipmapCS;     //生成Mip的CS
    public ComputeShader GPUCullingCS;         //GPU剔除CS

    public ComputeBuffer MeshBoundBuffer;

    public bool Enable = false;
    const int IntBits = 32;
    int kernalInitialize = -1;
    int kernalOcclusionCulling = -1;
    public bool enableDpth = false;
    public bool EnableLog = true;

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
        public static readonly string[] HizMapMip = { "HIZ_MAP_Mip0", "HIZ_MAP_Mip1", "HIZ_MAP_Mip2", "HIZ_MAP_Mip3" };
        
        public static readonly int InputDepthMap = Shader.PropertyToID("InputDepthMap");
        public static readonly int CameraDepthMapID = Shader.PropertyToID("CameraDepthMap");
        public static readonly int InputDepthMapSizeID = Shader.PropertyToID("inputDepthMapSize");
        public static readonly int DestTetSizeID = Shader.PropertyToID("DepthRTSize");
        
        public static readonly GlobalKeyword DIM_MIP_LEVEL_COUNT_1 = GlobalKeyword.Create("DIM_MIP_LEVEL_COUNT_1");
        public static readonly GlobalKeyword DIM_MIP_LEVEL_COUNT_2 = GlobalKeyword.Create("DIM_MIP_LEVEL_COUNT_2");
        public static readonly GlobalKeyword DIM_MIP_LEVEL_COUNT_4 = GlobalKeyword.Create("DIM_MIP_LEVEL_COUNT_4");
        public static readonly int MipCountID = Shader.PropertyToID("mipCount");
        
        
        public static readonly int MaxCountID = Shader.PropertyToID("MaxCount");
        public static readonly int MeshBoundBufferID = Shader.PropertyToID("MeshBoundBuffer");
        public static readonly int CullingResultBufferID = Shader.PropertyToID("CullResult");
        public static readonly int CameraFrustumPlanes = Shader.PropertyToID("CameraFrustumPlanes");
        public static readonly int CameraMatrixVP = Shader.PropertyToID("CameraMatrixVP");
        public static readonly int HizDepthMap = Shader.PropertyToID("HizDepthMap");
        public static readonly int HizDepthMapSize = Shader.PropertyToID("HizDepthMapSize");
        public static readonly int NumIntMasksID = Shader.PropertyToID("NumIntMasks");
    }

    public void InitGenerateDepthMip()
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            if (GenerateMipmapCS != null)
            {
                Debug.Log("GenerateMipmap设置UNITY_REVERSED_Z");
                GenerateMipmapCS.EnableKeyword("UNITY_REVERSED_Z");
            }
            else
                GenerateMipmapCS.DisableKeyword("UNITY_REVERSED_Z");
        }
    }

    public void InitHizCulling()
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            if (GPUCullingCS != null)
            {
                Debug.Log("GPUCullingCS设置UNITY_REVERSED_Z");
                GPUCullingCS.EnableKeyword("UNITY_REVERSED_Z");
            }
            else
                GPUCullingCS.DisableKeyword("UNITY_REVERSED_Z");
        }
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
        var hizIndex = Time.frameCount % CommonData.HZBInfoCount;
        var hzbInfo = hzbInfos[hizIndex];

        var proj = GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix, false);
        hzbInfo.VpMatrix = proj * renderingData.cameraData.camera.worldToCameraMatrix;

        hzbInfo.EnsureResourceReady(renderingData);
        
        int leftMipCount = hzbInfo.m_NumMips;
        
        int sourceMipLevel = 0;
        int destMipLevel = 0;
        
        Vector2 sourceSize = new Vector2(renderingData.cameraData.camera.pixelWidth,
            renderingData.cameraData.camera.pixelHeight);
        
        RenderTexture dstTexture = hzbInfo.hzbDepthRT;          //2^n形式
        
        CommandBuffer cmd = CommandBufferPool.Get(generateDepthMipTag);

        while (leftMipCount > 0)
        {
            int mipCount;
            if (leftMipCount >= 4)
            {
                cmd.DisableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_2);
                cmd.DisableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_1);
                cmd.EnableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_4);
                mipCount = 4;
            }
            else if (leftMipCount >= 2)
            {
                cmd.DisableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_4);
                cmd.DisableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_1);
                cmd.EnableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_2);
                mipCount = 2;
            }
            else if (leftMipCount >= 1)
            {
                cmd.DisableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_2);
                cmd.DisableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_4);
                cmd.EnableKeyword(ShaderConstants.DIM_MIP_LEVEL_COUNT_1);
                mipCount = 1;
            }
            else
            {
                break;
            }

            cmd.SetComputeIntParam(GenerateMipmapCS, ShaderConstants.MipCountID, mipCount);
            if (leftMipCount == hzbInfo.m_NumMips)  //第一次调用
            {
                for (int i = 0; i < mipCount; ++i)
                {
                    cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HizMapMip[i], dstTexture,
                        destMipLevel + i);
                }

                cmd.SetComputeVectorParam(GenerateMipmapCS, ShaderConstants.InputDepthMapSizeID,
                    new Vector4(sourceSize.x, sourceSize.y, 2 * dstTexture.width, 2 * dstTexture.height));

                cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.CameraDepthMapID, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            }
            else
            {
                for (int i = 0; i < mipCount; ++i)
                {
                    cmd.SetComputeTextureParam(GenerateMipmapCS, 1, ShaderConstants.HizMapMip[i], dstTexture,
                        destMipLevel + i);
                }
                cmd.SetComputeVectorParam(GenerateMipmapCS, ShaderConstants.InputDepthMapSizeID,
                    new Vector4(sourceSize.x, sourceSize.y, sourceSize.x, sourceSize.y));

                //cmd.CopyTexture(dstTexture, 0, sourceMipLevel, tmpTextures[(int)sourceSize.x], 0, 0);
                //cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.InputDepthMap, tmpTextures[(int)sourceSize.x]);

                cmd.SetComputeTextureParam(GenerateMipmapCS, 1, ShaderConstants.InputDepthMap, dstTexture, sourceMipLevel);
            }

            Vector2 dstSize = (hzbInfo.ExpandHZBSize / (1 << destMipLevel));
            
            int groupCountX = (int)Mathf.CeilToInt(dstSize.x / 32);
            int groupCountY = (int)Mathf.CeilToInt(dstSize.y / 16);
            groupCountX = Mathf.Max(groupCountX, 1);
            groupCountY = Mathf.Max(groupCountY, 1);

            if (leftMipCount == hzbInfo.m_NumMips)
                cmd.DispatchCompute(GenerateMipmapCS, 0, groupCountX, groupCountY, 1);
            else
                cmd.DispatchCompute(GenerateMipmapCS, 1, groupCountX, groupCountY, 1);

            destMipLevel += mipCount;   //4
            sourceMipLevel = destMipLevel - 1;
            sourceSize = hzbInfo.ExpandHZBSize / (1 << destMipLevel);
            leftMipCount -= mipCount;
        }
        
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
        var hizIndex = Time.frameCount % CommonData.HZBInfoCount;
        var hzbInfo = hzbInfos[hizIndex];
        hzbInfo.readBackSuccess = false;

        var lastHzbInfo = hzbInfos[(hizIndex + CommonData.HZBInfoCount - 1) % CommonData.HZBInfoCount];

        hzbInfo.EnsureResourceReady(renderingData);

        var camera = renderingData.cameraData.camera;
        CommandBuffer cmd = CommandBufferPool.Get(hiZCullingTag);
        
        if (kernalOcclusionCulling == -1)
        {
            kernalInitialize = GPUCullingCS.FindKernel("IntializeResultBuffer");
        }

        int numIntMasks = 0;
        
        numIntMasks = Mathf.CeilToInt((float)MeshBoundBuffer.count / (float)IntBits);

        numIntMasks = Mathf.Max(numIntMasks, 1);
        cmd.SetComputeIntParam(GPUCullingCS, ShaderConstants.NumIntMasksID, numIntMasks);
        int igx = Mathf.CeilToInt((float)numIntMasks / 64.0f);
        igx = Mathf.Max(igx, 1);
        if (hzbInfo.CullingResultBuffer == null)
            UnityEngine.Debug.LogError("CullingResultBuffer is null");
        cmd.SetComputeBufferParam(GPUCullingCS, kernalInitialize, ShaderConstants.CullingResultBufferID, hzbInfo.CullingResultBuffer);

        cmd.DispatchCompute(GPUCullingCS, kernalInitialize, igx, 1, 1);
        
        cmd.SetComputeIntParam(GPUCullingCS, ShaderConstants.MaxCountID, MeshBoundBuffer.count);
        cmd.SetComputeBufferParam(GPUCullingCS, 0, ShaderConstants.MeshBoundBufferID, MeshBoundBuffer);
        cmd.SetComputeBufferParam(GPUCullingCS, 0, ShaderConstants.CullingResultBufferID, hzbInfo.CullingResultBuffer);

        UpdateCameraFrustumPlanes(hzbInfo, camera);
        cmd.SetComputeMatrixParam(GPUCullingCS, ShaderConstants.CameraMatrixVP, lastHzbInfo.VpMatrix);
        cmd.SetComputeTextureParam(GPUCullingCS, 0, ShaderConstants.HizDepthMap, hzbInfo.hzbDepthRT);
        cmd.SetComputeVectorParam(GPUCullingCS, ShaderConstants.HizDepthMapSize, new Vector3(hzbInfo.hzbDepthRT.width, hzbInfo.hzbDepthRT.height, hzbInfo.hzbDepthRT.mipmapCount));
        cmd.DispatchCompute(GPUCullingCS, 0, Mathf.CeilToInt(MeshBoundBuffer.count / 64f), 1, 1);
        cmd.RequestAsyncReadback(hzbInfo.CullingResultBuffer, (req) => OnGPUCullingReadBack(req, hzbInfo));
        // 记录发起请求的时间戳（以Stopwatch的Ticks为单位）
        long requestTimestamp = stopwatch.ElapsedTicks;
        pendingRequestTimestamps.Enqueue(requestTimestamp);
        
        context.ExecuteCommandBuffer(cmd);
        
        CommandBufferPool.Release(cmd);
        
       
        // AsyncGPUReadback.Request(hzbInfo.CullingResultBuffer).WaitForCompletion();
        // hzbInfo.CullingResultBuffer.GetData(hzbInfo.cullResults);
        // hzbInfo.readBackSuccess = true;
        // SyncCullResult(hzbInfo); 
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
    
    private void SyncCullResult(HZBInfo hZBInfo)
    {
        long callbackTimestamp = stopwatch.ElapsedTicks;
        long latencyTicks = callbackTimestamp - pendingRequestTimestamps.Dequeue();

        // 转换为毫秒（Stopwatch.Frequency单位为Hz，1秒=1e7 ticks）
        double latencyMs = (latencyTicks * 1000.0) / Stopwatch.Frequency;
        latencyResults.Add(latencyMs);
    }

    //异步回读
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
            Debug.LogError("ReadBackFailed");
            hZBInfo.readBackSuccess = false;
        }
    }
    
    public void ShowDepth(ScriptableRenderContext context, ref RenderingData renderingData,Material _depthMaterial)
    {
        if (enableDpth)
        {
            var hizIndex = Time.frameCount % CommonData.HZBInfoCount;
            var hzbInfo = hzbInfos[hizIndex];

            var cmd = CommandBufferPool.Get("DepthMapPass");
            
            cmd.Blit(hzbInfo.hzbDepthRT, renderingData.cameraData.renderer.cameraColorTarget, _depthMaterial);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public void OnDestroy()
    {
        Enable = false;
        MeshBoundBuffer?.Dispose();

        foreach (var hzbInfo in hzbInfos)
        {
            hzbInfo.CullingResultBuffer?.Dispose();
        }

        if (latencyResults.Count > 0)
        {
            double sum = 0;
            foreach (var latency in latencyResults) sum += latency;
            double avg = sum / latencyResults.Count;
            Log("回读延迟平均值:{0}ms", avg);
        }
    }
    
    private void Log(string format, params object[] args)
    {
        if (EnableLog)
            Debug.LogFormat(format, args);
    }
}

//存储每帧HZB需要的一些信息
public class HZBInfo   
{
    public RenderTexture hzbDepthRT;   //深度图RT
    public int m_NumMips = 0;          //深度图的Mip数量
    public Material genDepthRTMat;   //生成深度图的材质
    public Matrix4x4 VpMatrix;       //存储当前帧的VP矩阵，供下一帧进行重投影

    public RenderTexture tmpDepth;

    public Plane[] cameraFrustumPlanes = new Plane[6];
    public Vector4[] cameraFrustumPlanesV4 = new Vector4[6];

    public ComputeBuffer CullingResultBuffer;
    public int[] cullResults;    //存储剔除结果 静态+动态，对应ComputeShader中的CullResult

    public bool readBackSuccess = false;    //当前帧的剔除结果是否读取成功
    public Vector2Int ExpandHZBSize;    //扩充后的大小，2^n形式的
    
    public RenderTexture tempInputDepthMap;
    

    public void EnsureResourceReady(RenderingData renderingData)
    {
        if (hzbDepthRT == null)
            CreateHzbRT(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
        //if (genDepthRTMat == null)
        //    genDepthRTMat = new Material(MgrHiz.Instance.GenDepthRTShader);
    }

    private void CreateHzbRT(int width, int height)
    {
        if (hzbDepthRT)
            hzbDepthRT.Release();
        
        int resizeX = Mathf.IsPowerOfTwo(width) ? width : Mathf.NextPowerOfTwo(width);
        int resizeY = Mathf.IsPowerOfTwo(height) ? height : Mathf.NextPowerOfTwo(height);
        
        ExpandHZBSize = new Vector2Int(resizeX, resizeY);
        
        resizeX /= 2;
        resizeY /= 2;

        m_NumMips = Mathf.Min(Mathf.CeilToInt(Mathf.Log(resizeX) / Mathf.Log(2.0f)),
            Mathf.CeilToInt(Mathf.Log(resizeY) / Mathf.Log(2.0f))) + 1;

        Debug.Log("HIZMapSize:" + resizeX + "x" + resizeY + "  m_NumMips:" + m_NumMips);
        
        RenderTextureFormat r16RTF = GraphicsFormatUtility.GetRenderTextureFormat(GraphicsFormat.R16_SFloat);
        if (SystemInfo.SupportsRenderTextureFormat(r16RTF) &&
            SystemInfo.SupportsRandomWriteOnRenderTextureFormat(r16RTF))
            hzbDepthRT = new RenderTexture(resizeX, resizeY, 0, GraphicsFormat.R16_SFloat, m_NumMips);
        else
            hzbDepthRT = new RenderTexture(resizeX, resizeY, 0, GraphicsFormat.R32_SFloat, m_NumMips);

        hzbDepthRT.name = "HzbDepthRT";
        hzbDepthRT.useMipMap = true;
        hzbDepthRT.autoGenerateMips = false;
        hzbDepthRT.enableRandomWrite = true;
        hzbDepthRT.filterMode = FilterMode.Point;
        hzbDepthRT.wrapMode = TextureWrapMode.Clamp;
        hzbDepthRT.Create();
        
        if (SystemInfo.SupportsRenderTextureFormat(r16RTF) &&
            SystemInfo.SupportsRandomWriteOnRenderTextureFormat(r16RTF))
            tmpDepth = new RenderTexture(width, height, 0, GraphicsFormat.R16_SFloat);
        else
            tmpDepth = new RenderTexture(width, height, 0, GraphicsFormat.R32_SFloat);

        tmpDepth.name = "tmpDepth";
        tmpDepth.useMipMap = false;
        tmpDepth.enableRandomWrite = true;
        tmpDepth.filterMode = FilterMode.Point;
        tmpDepth.wrapMode = TextureWrapMode.Clamp;
        tmpDepth.Create();


    }
}
