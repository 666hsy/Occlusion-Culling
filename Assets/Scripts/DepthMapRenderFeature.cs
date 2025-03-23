using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthMapRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material depthMaterial;
    }

    public Settings settings = new Settings();
    private DepthMapRenderPass _renderPass;

    public override void Create()
    {
        _renderPass = new DepthMapRenderPass(settings.depthMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_renderPass);
    }
}

public class DepthMapRenderPass : ScriptableRenderPass
{
    private Material _depthMaterial;
    private RenderTargetHandle _depthTexture;

    public DepthMapRenderPass(Material depthMaterial)
    {
        _depthMaterial = depthMaterial;
        _depthTexture.Init("_DepthTexture");
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // 配置深度纹理（需与URP的深度格式匹配）
        cmd.GetTemporaryRT(_depthTexture.id, cameraTextureDescriptor);
        ConfigureTarget(_depthTexture.Identifier());
        ConfigureClear(ClearFlag.All, Color.black);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_depthMaterial == null) return;
        MgrHiz.Instance.ShowDepth(context, ref renderingData, _depthMaterial);

    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(_depthTexture.id);
    }
}