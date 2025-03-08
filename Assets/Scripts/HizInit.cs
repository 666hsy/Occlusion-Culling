using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class HizInit : MonoBehaviour
{
    struct  BoundStruct
    {
        public Vector3 center;
        public Vector3 size;
    }
    
    private MeshRenderer[] renderers;

    private int[] staticCullResults;
    private List<MeshRenderer> staticMeshRenders = new List<MeshRenderer>();
    private BoundStruct[] staticMeshBounds;

    public ComputeShader GenerateHzbCS;
    public Shader GenDepthRTShader;
    public ComputeShader CullingCS;

    // 获取当前场景中静态物体的AABB包围盒数据并保存到贴图中
    private void Start()
    {
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        renderers = FindObjectsOfType<MeshRenderer>();
        foreach (var meshRenderer in renderers)
        {
            if (meshRenderer.gameObject.isStatic)
                staticMeshRenders.Add(meshRenderer);
        }
        
        staticCullResults = new int[staticMeshRenders.Count];
        staticMeshBounds = new BoundStruct[staticMeshRenders.Count];
        int texSize = HizCullingFeature.HizCullingPass.cullTextureSize;

        InitStaticAABB();
        InitMgrHzb();
    }

    private void InitStaticAABB()
    {
        for (int i = 0; i < staticMeshRenders.Count; i++)
        {
            if (staticMeshRenders[i] != null)
            {
                Bounds aabb = staticMeshRenders[i].bounds;
                var center = aabb.center;
                var size = aabb.size;
                staticMeshBounds[i] = new BoundStruct()
                {
                    center = center,
                    size = size
                };
            }
        }
    }

    private void InitMgrHzb()
    {
        MgrHiz.Instance.cullResultBackArray = new NativeArray<int>(staticMeshRenders.Count, Allocator.Persistent);
        MgrHiz.Instance.Enable = true;
        MgrHiz.Instance.GenerateMipmapCS = GenerateHzbCS;
        MgrHiz.Instance.GenDepthRTShader = GenDepthRTShader;
        MgrHiz.Instance.GPUCullingCS = CullingCS;

        MgrHiz.Instance.CullingResultBuffer = new ComputeBuffer(staticMeshRenders.Count, 4);
        MgrHiz.Instance.CullingResultBuffer.SetData(staticCullResults);

        MgrHiz.Instance.StaticMeshBuffer = new ComputeBuffer(staticMeshRenders.Count, sizeof(float) * 6);
        MgrHiz.Instance.StaticMeshBuffer.SetData(staticMeshBounds);
    }

    // 读取剔除结果
    private void LateUpdate()
    {
        if (MgrHiz.Instance.readBackSuccess)
        {
            for (int i = 0; i < staticMeshRenders.Count; i++)
            {
                if(MgrHiz.Instance.cullResultBackArray[i]<=0)
                {
                    staticMeshRenders[i].gameObject.SetActive(false);
                }
                else
                {
                    staticMeshRenders[i].gameObject.SetActive(true);
                }
            }
        }
    }
}