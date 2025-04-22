using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HiZDepthGenerater : ScriptableRendererFeature
{
    class HiZDepthGeneraterPass : ScriptableRenderPass
    {
        private int m_NumMips = 0;
        public HiZDepthGeneraterPass()
        {
            // isComputePass = true;
            base.profilingSampler = new ProfilingSampler(nameof(HiZDepthGeneraterPass));
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            MgrHiz.Instance.GenerateDepthMip(context, ref renderingData);
        }
    }

    HiZDepthGeneraterPass hiZDepthGeneraterPass;

    /// <inheritdoc/>
    public override void Create()
    {
        hiZDepthGeneraterPass = new HiZDepthGeneraterPass();
        hiZDepthGeneraterPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera ||
            renderingData.cameraData.camera.name != "Main Camera")
            return;
        
        renderer.EnqueuePass(hiZDepthGeneraterPass);
    }
}


