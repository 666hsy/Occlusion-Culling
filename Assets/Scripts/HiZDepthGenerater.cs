using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HiZDepthGenerater : ScriptableRendererFeature
{
    class HiZDepthGeneraterPass : ScriptableRenderPass
    {
        RenderTargetIdentifier SourceZTexture;
        RenderTextureDescriptor SourceZDescriptor;
        private int m_NumMips = 0;
        public HiZDepthGeneraterPass()
        {
            isComputePass = true;
            base.profilingSampler = new ProfilingSampler(nameof(HiZDepthGeneraterPass));
        }

        public void Init(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var universalRenderer = renderer as UniversalRenderer;
            if (universalRenderer != null)
            {
                SourceZTexture = universalRenderer.m_DepthTexture;
                SourceZDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                int numMipsX = Math.Max(Mathf.CeilToInt(Mathf.Log((float)SourceZDescriptor.width) / Mathf.Log(2.0f)) - 1, 1);
                int numMipsY = Math.Max(Mathf.CeilToInt(Mathf.Log((float)SourceZDescriptor.height) / Mathf.Log(2.0f)) - 1, 1);
                m_NumMips = Math.Max(numMipsX, numMipsY);
            }
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            MgrHiz.Instance.GenerateDepthMip(context,ref renderingData,m_NumMips,SourceZTexture,SourceZDescriptor);
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
        hiZDepthGeneraterPass.Init(renderer, ref renderingData);
        renderer.EnqueuePass(hiZDepthGeneraterPass);
    }
}


