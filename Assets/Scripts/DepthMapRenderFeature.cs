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
        _renderPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera ||
            renderingData.cameraData.camera.name != "Main Camera")
            return;
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

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_depthMaterial == null) return;
        MgrHiz.Instance.ShowDepth(context, ref renderingData, _depthMaterial);
    }
}