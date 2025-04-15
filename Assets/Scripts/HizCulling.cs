using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HizCulling : ScriptableRendererFeature
{
    class HizCullingPass : ScriptableRenderPass
    {
        public HizCullingPass()
        {
            //isComputePass = true;
            base.profilingSampler = new ProfilingSampler(nameof(HizCullingPass));
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            MgrHiz.Instance.ExecuteCull(context,ref renderingData);
        }
    }

    HizCullingPass hizCullingPass;

    /// <inheritdoc/>
    public override void Create()
    {
        hizCullingPass = new HizCullingPass();
        hizCullingPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(hizCullingPass);
    }
}


