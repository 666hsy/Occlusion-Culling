using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

//存放一些公共数据
public class MgrHiz
{
    private static MgrHiz instance;
    
    public HZBInfo[] hzbInfos = new HZBInfo[CommonData.HZBInfoCount];

    private string generateDepthMipTag = "GenerateDepthMip";
    private string hiZCullingTag = "HiZCullingTag";
    
    public Shader GenDepthRTShader;    //Hiz的Shader

    public ComputeShader GenerateMipmapCS;     //生成Mip的CS
    public ComputeShader GPUCullingCS;         //GPU剔除CS

    public ComputeBuffer MeshBoundBuffer;

    // public ComputeBuffer DynamicMeshBuffer;

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
        public static readonly string[] HizMapMip = { "HIZ_MAP_Mip0", "HIZ_MAP_Mip1", "HIZ_MAP_Mip2", "HIZ_MAP_Mip3" };
        
        public static readonly int InputDepthMap = Shader.PropertyToID("inputDepthMap");
        public static readonly int InputDepthMapSizeID = Shader.PropertyToID("inputDepthMapSize");
        public static readonly int SourceMipLevel = Shader.PropertyToID("SourceMipLevel");
        
        public static readonly GlobalKeyword DIM_MIP_LEVEL_COUNT_1 = GlobalKeyword.Create("DIM_MIP_LEVEL_COUNT_1");
        public static readonly GlobalKeyword DIM_MIP_LEVEL_COUNT_2 = GlobalKeyword.Create("DIM_MIP_LEVEL_COUNT_2");
        public static readonly GlobalKeyword DIM_MIP_LEVEL_COUNT_4 = GlobalKeyword.Create("DIM_MIP_LEVEL_COUNT_4");
        public static readonly GlobalKeyword SOURCE_SCENE_DEPTH = GlobalKeyword.Create("SOURCE_SCENE_DEPTH");
        
        
        public static readonly int MaxCountID = Shader.PropertyToID("MaxCount");
        public static readonly int MeshBoundBufferID = Shader.PropertyToID("MeshBoundBuffer");
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
    public void GenerateDepthMip(ScriptableRenderContext context,ref RenderingData renderingData,int m_NumMips,RenderTargetIdentifier SourceZTexture,RenderTextureDescriptor SourceZDescriptor)
    {
        // scene view下不生成深度图
        if (!Enable || renderingData.cameraData.camera.name == "SceneCamera" || renderingData.cameraData.camera.name == "Preview Camera")
            return;
        

        var hizIndex = Time.frameCount % CommonData.HZBInfoCount;
        var hzbInfo = hzbInfos[hizIndex];

        var proj = GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix, false);
        hzbInfo.VpMatrix = proj * renderingData.cameraData.camera.worldToCameraMatrix;

        hzbInfo.EnsureResourceReady(renderingData);
        
        int leftMipCount = m_NumMips;
        
        int sourceMipLevel = 0;
        int destMipLevel = 0;

        Vector2 sourceSize = new Vector2(SourceZDescriptor.width, SourceZDescriptor.height);
        
        RenderTargetIdentifier sourceTexture = SourceZTexture;  //非2^n形式
        
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
            
            for (int i = 0; i < mipCount; ++i)
            {
                cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HizMapMip[i], dstTexture,
                    destMipLevel + i);
            }

            if (leftMipCount == m_NumMips)  //第一次调用
            {
                cmd.EnableKeyword(ShaderConstants.SOURCE_SCENE_DEPTH);
                cmd.SetComputeVectorParam(GenerateMipmapCS, ShaderConstants.InputDepthMapSizeID,
                    new Vector4(sourceSize.x, sourceSize.y, dstTexture.width, dstTexture.height));
                cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.InputDepthMap, sourceTexture, 0);
            }
            else
            {
                cmd.DisableKeyword(ShaderConstants.SOURCE_SCENE_DEPTH);
                cmd.SetComputeVectorParam(GenerateMipmapCS, ShaderConstants.InputDepthMapSizeID,
                    new Vector4(sourceSize.x, sourceSize.y, sourceSize.x, sourceSize.y));
                
                cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.InputDepthMap, dstTexture, sourceMipLevel);
            }
            
            cmd.SetComputeFloatParam(GenerateMipmapCS, ShaderConstants.SourceMipLevel, sourceMipLevel);
            
            Vector2 dstSize = (hzbInfo.m_HZBSize / (1 << destMipLevel));
            int groupCountX = (int)Mathf.CeilToInt(dstSize.x / 32);
            int groupCountY = (int)Mathf.CeilToInt(dstSize.y / 16);
            groupCountX = Mathf.Max(groupCountX, 1);
            groupCountY = Mathf.Max(groupCountY, 1);
            cmd.DispatchCompute(GenerateMipmapCS, 0, groupCountX, groupCountY, 1);
            
            destMipLevel += mipCount;   //4
            sourceMipLevel = destMipLevel - 1;
            sourceSize = hzbInfo.m_HZBSize / (1 << sourceMipLevel);

            leftMipCount -= mipCount;
        }
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
                

        // int sourceMipLevel = 0;
        // int destMipLevel = 0;
        //
        //
        
        //
        // int mipLevel = 0,
        //     w = hzbInfo.hzbDepthRT.width,
        //     h = hzbInfo.hzbDepthRT.height,
        //     width = renderingData.cameraData.camera.pixelWidth,
        //     height = renderingData.cameraData.camera.pixelHeight;
        // do
        // {
        //     if (mipLevel == 0)
        //     {
        //         cmd.SetComputeVectorParam(GenerateMipmapCS, ShaderConstants.InputDepthMapSizeID,
        //             new Vector4(width, height, w, h));
        //     }
        //     else
        //     {
        //         cmd.SetComputeVectorParam(GenerateMipmapCS, ShaderConstants.InputDepthMapSizeID,
        //             new Vector4(w, h, w, h));
        //         cmd.SetComputeTextureParam(GenerateMipmapCS, 1, ShaderConstants.InputDepthMap,
        //             hzbInfo.hzbDepthRT, mipLevel - 1);
        //     }
        //     cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HizMapMip0, hzbInfo.hzbDepthRT,
        //         mipLevel);
        //     cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HizMapMip1, hzbInfo.hzbDepthRT,
        //         mipLevel + 1);
        //     cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HizMapMip2, hzbInfo.hzbDepthRT,
        //         mipLevel + 2);
        //     cmd.SetComputeTextureParam(GenerateMipmapCS, 0, ShaderConstants.HizMapMip3, hzbInfo.hzbDepthRT,
        //         mipLevel + 3);
        //     mipLevel += 4;
        //     w /= 16;
        //     h /= 16;
        //     if (mipLevel == 0)
        //         cmd.DispatchCompute(GenerateMipmapCS, 0, Mathf.Max(1, width / 32), Mathf.Max(1, height / 16), 1);
        //     else
        //         cmd.DispatchCompute(GenerateMipmapCS, 1, Mathf.Max(1, width / 32), Mathf.Max(1, height / 16), 1);
        // } while (w > 1 || h > 1);
        //

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
        // int cnt1 = 0, cnt2 = 0;
        // for(int i=0;i<hzbInfo.cullResults.Length;i++)
        // {
        //     if (hzbInfo.cullResults[i] != 0)
        //     {
        //         cnt1++;
        //     }
        // }
        
        // 记录发起请求的时间戳（以Stopwatch的Ticks为单位）
        long requestTimestamp = stopwatch.ElapsedTicks;
        pendingRequestTimestamps.Enqueue(requestTimestamp);
        context.ExecuteCommandBuffer(cmd);
        
        
        // hzbInfo.CullingResultBuffer.GetData(hzbInfo.cullResults);
        // hzbInfo.readBackSuccess = true;
        // for(int i=0;i<hzbInfo.cullResults.Length;i++)
        // {
        //     if (hzbInfo.cullResults[i] != 0)
        //     {
        //         cnt2++;
        //     }
        // }
        // long callbackTimestamp = stopwatch.ElapsedTicks;
        // long latencyTicks = callbackTimestamp - pendingRequestTimestamps.Dequeue();
        // Debug.Log("拿到结果：cnt1:" + cnt1 + "  cnt2:" + cnt2);
        // // 转换为毫秒（Stopwatch.Frequency单位为Hz，1秒=1e7 ticks）
        // double latencyMs = (latencyTicks * 1000.0) / Stopwatch.Frequency;
        // latencyResults.Add(latencyMs);

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
            var hizIndex = Time.frameCount % CommonData.HZBInfoCount;
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
    public Vector2Int m_HZBSize;    //2^n形式的

    public void EnsureResourceReady(RenderingData renderingData)
    {
        if (hzbDepthRT == null)
            CreateHzbRT(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
        if (genDepthRTMat == null)
            genDepthRTMat = new Material(MgrHiz.Instance.GenDepthRTShader);
    }

    private void CreateHzbRT(int width, int height)
    {
        if (hzbDepthRT)
            hzbDepthRT.Release();

        // int numMipsX = Math.Max(Mathf.CeilToInt(Mathf.Log((float)width) / Mathf.Log(2.0f)) - 1, 1);
        // int numMipsY = Math.Max(Mathf.CeilToInt(Mathf.Log((float)height) / Mathf.Log(2.0f)) - 1, 1);
        
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
        
        m_HZBSize = new Vector2Int(resizeX, resizeY);
    }
}
