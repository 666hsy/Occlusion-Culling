using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1)]
public class HizInit : MonoBehaviour
{
    private MeshRenderer[] renderers;
    private bool[] cullResults;
    public Texture2D centerTex;
    public Texture2D sizeTex;
    private Color[] centers;
    private Color[] sizes;
    public int cullCount;

    private List<MeshRenderer> staticMeshRenders = new List<MeshRenderer>();

    // 获取当前场景中静态物体的AABB包围盒数据并保存到贴图中
    private void Start()
    {
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        renderers = FindObjectsOfType<MeshRenderer>();
        foreach (var meshRenderer in renderers)
            staticMeshRenders.Add(meshRenderer);

        cullResults = new bool[staticMeshRenders.Count];
        int texSize = HizCullingFeature.HizCullingPass.cullTextureSize;
        centerTex = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, 0, true);
        sizeTex = new Texture2D(texSize, texSize, TextureFormat.RGBAFloat, 0, true);
        centerTex.filterMode = FilterMode.Point;
        sizeTex.filterMode = FilterMode.Point;
        centers = new Color[texSize * texSize];
        sizes = new Color[texSize * texSize];
        UpdateAABB();
    }

    private void UpdateAABB()
    {
        for (int i = 0; i < staticMeshRenders.Count; i++)
        {
            if (i >= centers.Length)
            {
                Debug.LogError("need bigger size Texture");
                break;
            }

            if (staticMeshRenders[i] != null)
            {
                Bounds aabb = staticMeshRenders[i].bounds;
                var center = aabb.center;
                var size = aabb.size;
                centers[i] = new Color(center.x, center.y, center.z, 1f);
                sizes[i] = new Color(size.x, size.y, size.z, 1f);
            }
            else
            {
                centers[i] = Color.black;
                sizes[i] = Color.black;
            }
        }

        for (int i = staticMeshRenders.Count; i < centers.Length; i++)
        {
            centers[i] = Color.clear;
            sizes[i] = Color.clear;
        }

        centerTex.SetPixelData(centers, 0);
        centerTex.Apply();
        sizeTex.SetPixelData(sizes, 0);
        sizeTex.Apply();
    }

    // 读取剔除结果
    private void LateUpdate()
    {
        HizCullingFeature.HizCullingPass.objectCenterTex = centerTex;
        HizCullingFeature.HizCullingPass.objectSizeTex = sizeTex;
        var mipinfo = HizCullingFeature.HizCullingPass.GetHizInfo();

        cullCount = 0;

        if (!mipinfo.readBackSuccess)
        {
            for (int i = 0; i < staticMeshRenders.Count; i++)
            {
                if (cullResults[i])
                {
                    staticMeshRenders[i].gameObject.SetActive(true);
                    cullResults[i] = false;
                }
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
                    
                    if (cullResults[i] != needCull)
                    {
                        HasChanged = true;
                        if (!needCull)
                            staticMeshRenders[i].gameObject.SetActive((true));
                        else
                        {
                            staticMeshRenders[i].gameObject.SetActive((false));
                        }
                        cullResults[i] = needCull;
                    }
                }
            }

            Debug.Log("frameCount: " + Time.frameCount + " Buffer index " + Time.frameCount % 3 + " : cullcount = " +
                      cullCount);
            // Debug.Log("frameCount: " + Time.frameCount + "HasChanged " + HasChanged);
        }
    }
}