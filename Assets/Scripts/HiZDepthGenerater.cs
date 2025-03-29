using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HiZDepthGenerater : ScriptableRendererFeature
{
    class HiZDepthGeneraterPass : ScriptableRenderPass
    {

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public HiZDepthGeneraterPass()
        {
            isComputePass = true;
            base.profilingSampler = new ProfilingSampler(nameof(HiZDepthGeneraterPass));
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            MgrHiz.Instance.GenerateDepthMip(context,ref renderingData);
        }
    }

    HiZDepthGeneraterPass hiZDepthGeneraterPass;

    /// <inheritdoc/>
    public override void Create()
    {
        hiZDepthGeneraterPass = new HiZDepthGeneraterPass();
        hiZDepthGeneraterPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox - 1;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(hiZDepthGeneraterPass);
    }
}


