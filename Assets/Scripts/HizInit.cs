using System.Collections.Generic;
using UnityEngine;

public class HizInit : MonoBehaviour
{
    struct  BoundStruct
    {
        public Vector3 center;
        public Vector3 size;
    }
    
    private MeshRenderer[] renderers;

    private bool[] staticCullResults;
    private ComputeBuffer staticMeshBuffer;
    
    public int cullCount;

    private List<MeshRenderer> staticMeshRenders = new List<MeshRenderer>();
    private BoundStruct[] staticMeshBounds;

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
        
        staticCullResults = new bool[staticMeshRenders.Count];
        staticMeshBounds = new BoundStruct[staticMeshRenders.Count];
        int texSize = HizCullingFeature.HizCullingPass.cullTextureSize;
        InitStaticAABB();
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
        staticMeshBuffer = new ComputeBuffer(staticMeshRenders.Count, 24);
        
        staticMeshBuffer.SetData(staticMeshBounds);
    }

    // 读取剔除结果
    private void LateUpdate()
    {
        // HizCullingFeature.HizCullingPass.objectCenterTex = centerTex;
        // HizCullingFeature.HizCullingPass.objectSizeTex = sizeTex;
        var mipinfo = HizCullingFeature.HizCullingPass.GetHizInfo();

        cullCount = 0;

        if (!mipinfo.readBackSuccess)
        {
            for (int i = 0; i < staticMeshRenders.Count; i++)
            {
                // if (cullResults[i])
                // {
                //     staticMeshRenders[i].gameObject.SetActive(true);
                //     cullResults[i] = false;
                // }
            }
        }
        else
        {
            bool HasChanged = false;
            var cullResult = mipinfo.cullResultBackArray;
            for (int i = 0; i < staticMeshRenders.Count; i++)
            {
                if (staticMeshRenders[i])
                {
                    bool needCull = cullResult[i] > 0f;
                    if (needCull)
                    {
                        cullCount++;
                    }
                    
                    // if (cullResults[i] != needCull)
                    // {
                    //     HasChanged = true;
                    //     if (!needCull)
                    //         staticMeshRenders[i].gameObject.SetActive((true));
                    //     else
                    //     {
                    //         staticMeshRenders[i].gameObject.SetActive((false));
                    //     }
                    //     cullResults[i] = needCull;
                    // }
                }
            }

            Debug.Log("frameCount: " + Time.frameCount + " Buffer index " + Time.frameCount % 3 + " : cullcount = " +
                      cullCount);
            // Debug.Log("frameCount: " + Time.frameCount + "HasChanged " + HasChanged);
        }
    }
}