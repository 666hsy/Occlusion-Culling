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
    
    private OCMesh[] renderers;

    
    private List<MeshRenderer> staticMeshRenders = new List<MeshRenderer>();
    private BoundStruct[] staticMeshBounds;
    const int IntBits = 32;

    public bool EnableLog = true;
    public ComputeShader GenerateHzbCS;
    public Shader GenDepthRTShader;
    
    public ComputeShader CullingCS;

    private bool _enableHZB = true;
    private bool enableHZB
    {
        get
        {
            return _enableHZB;
        }
        set
        {
            if (value)
            {
                Log("开启HZB");
                CullingCS.EnableKeyword("ENABLE_HIZ_CULL");
            }

            else
            {
                Log("关闭HZB");
                CullingCS.DisableKeyword("ENABLE_HIZ_CULL");
            }
        }
    }
    

    // 获取当前场景中静态物体的AABB包围盒数据并保存到贴图中
    private void Start()
    {
        Application.targetFrameRate = 60;
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        renderers = FindObjectsOfType<OCMesh>();
        foreach (var meshRenderer in renderers)
        {
            if (meshRenderer.isStaticMesh)
                staticMeshRenders.Add(meshRenderer.gameObject.GetComponent<MeshRenderer>());
        }
        
        staticMeshBounds = new BoundStruct[staticMeshRenders.Count];
        Log("共有{0}个静态物体", staticMeshBounds.Length);
        enableHZB = true;
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
        MgrHiz.Instance.Enable = true;
        MgrHiz.Instance.GenerateMipmapCS = GenerateHzbCS;
        MgrHiz.Instance.GenDepthRTShader = GenDepthRTShader;
        MgrHiz.Instance.GPUCullingCS = CullingCS;
        Log("Result StaticMeshBuffer Count:{0}", staticMeshRenders.Count);
        
        int numInts = Mathf.CeilToInt((float)staticMeshRenders.Count / (float)IntBits);
        MgrHiz.Instance.staticCullResults = new int[numInts];
        MgrHiz.Instance.CullingResultBuffer = new ComputeBuffer(numInts, 4);     
        MgrHiz.Instance.CullingResultBuffer.SetData(MgrHiz.Instance.staticCullResults);
        
        Log("StaticMeshBuffer Count:{0}", staticMeshRenders.Count);
        MgrHiz.Instance.StaticMeshBuffer = new ComputeBuffer(staticMeshRenders.Count, sizeof(float) * 6);
        MgrHiz.Instance.StaticMeshBuffer.SetData(staticMeshBounds);
    }

    // 读取剔除结果
    private void LateUpdate()
    {
        if (MgrHiz.Instance.readBackSuccess)
        {
            int count = 0;
            for (int i = 0; i < staticMeshRenders.Count; i++)
            {
                int intIndex = i / IntBits;
                int integer = MgrHiz.Instance.staticCullResults[intIndex];
                int bit = i - intIndex * IntBits;
                int mask = 1 << bit;
                bool visible = (integer & mask) != 0;
                if (!visible)
                    count++;
                staticMeshRenders[i].gameObject.SetActive(visible);
            }

            MgrHiz.Instance.readBackSuccess = false;
            Log("共剔除了{0}个静态物体", count);
        }
        else
        {
            Log("剔除结果读取失败 frameCount:{0}", Time.frameCount);
            for (int i = 0; i < staticMeshRenders.Count; i++)
                staticMeshRenders[i].gameObject.SetActive(true);
        }
    }

    private void Log(string format, params object[] args)
    {
        if (EnableLog)
            Debug.LogFormat(format, args);
    }

    public void SwitchHZB()
    {
        _enableHZB = !_enableHZB;
        enableHZB = _enableHZB;
    }
}