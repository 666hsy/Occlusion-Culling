using System.Collections.Generic;
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
    private List<MeshRenderer> dynamicMeshRenders = new List<MeshRenderer>();
    private List<Vector3> targetPositions=new List<Vector3>();
        
    private BoundStruct[] staticMeshBounds;
    private BoundStruct[] dynamicMeshBounds;
    
    const int IntBits = 32;
    public float moveSpeed = 2f; 
    public bool EnableLog = true;
    public bool EnableDynamicCull = true;
    public ComputeShader GenerateHzbCS;
    public Shader GenDepthRTShader;
    
    public ComputeShader CullingCS;

    private bool _enableHZB = true;
    public Material depthMaterial;

    public long TotalFrameCount = 0;
    public long FailFrameCount = 0;

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
    
    private Vector3 GetRandomPosition()
    {
        float x = Random.Range(-500, 500);
        float y = Random.Range(0, 30);
        float z = Random.Range(-500, 500);
        return new Vector3(x, y, z);
    }
    

    // 获取当前场景中静态物体的AABB包围盒数据并保存到贴图中
    private void Start()
    {
        Application.targetFrameRate = 60;
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        renderers = FindObjectsOfType<OCMesh>();
        Log("一共有{0}个OCMesh物体", renderers.Length);
        foreach (var meshRenderer in renderers)
        {
            if (meshRenderer.isStaticMesh)
                staticMeshRenders.Add(meshRenderer.gameObject.GetComponent<MeshRenderer>());
            else
            {
                dynamicMeshRenders.Add(meshRenderer.gameObject.GetComponent<MeshRenderer>());
                targetPositions.Add(GetRandomPosition());
            }
        }
        
        staticMeshBounds = new BoundStruct[staticMeshRenders.Count];
        dynamicMeshBounds = new BoundStruct[dynamicMeshRenders.Count];
        Log("共有{0}个静态物体 {0}个动态物体", staticMeshBounds.Length, dynamicMeshBounds.Length);
        
        enableHZB = true;
        InitStaticAABB();
        InitMgrHzb();
    }
    
    void Update()
    {
        // 移动立方体
        for (int i = 0; i < dynamicMeshRenders.Count; i++)
        {
            Vector3 currentPosition = dynamicMeshRenders[i].transform.position;
            Vector3 target = targetPositions[i];

            // 移动立方体到目标位置
            dynamicMeshRenders[i].transform.position = Vector3.MoveTowards(currentPosition, target, moveSpeed * Time.deltaTime);

            // 如果到达目标位置，设置新的目标位置
            if (currentPosition == target)
                targetPositions[i] = GetRandomPosition();
        }
        if(EnableDynamicCull) 
            UpdateDynamicAABB();

        MgrHiz.Instance.stopwatch.Start();
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
    
    private void UpdateDynamicAABB()
    {
        for (int i = 0; i < dynamicMeshRenders.Count; i++)
        {
            if (dynamicMeshRenders[i] != null)
            {
                Bounds aabb = dynamicMeshRenders[i].bounds;
                var center = aabb.center;
                var size = aabb.size;
                dynamicMeshBounds[i].center=center;
                dynamicMeshBounds[i].size=size;
            }
        }

        if (EnableDynamicCull)
        {
            MgrHiz.Instance.DynamicMeshBuffer ??= new ComputeBuffer(dynamicMeshRenders.Count, sizeof(float) * 6);
            MgrHiz.Instance.DynamicMeshBuffer.SetData(dynamicMeshBounds);
        }
    }

    private void InitMgrHzb()
    {
        MgrHiz.Instance.Enable = true;
        MgrHiz.Instance.GenerateMipmapCS = GenerateHzbCS;
        MgrHiz.Instance.GenDepthRTShader = GenDepthRTShader;
        MgrHiz.Instance.GPUCullingCS = CullingCS;
        Log("Result StaticMeshBuffer Count:{0}", staticMeshRenders.Count);

        int numInts = Mathf.CeilToInt((float)staticMeshRenders.Count / (float)IntBits) +
                      Mathf.CeilToInt((float)dynamicMeshRenders.Count / (float)IntBits);

        for (int i = 0; i < CommonData.HZBInfoCount; i++)
        {
            MgrHiz.Instance.hzbInfos[i] = new HZBInfo();
            var hzbInfo = MgrHiz.Instance.hzbInfos[i];
            hzbInfo.cullResults = new int[numInts];
            hzbInfo.CullingResultBuffer = new ComputeBuffer(numInts, 4);
            hzbInfo.CullingResultBuffer.SetData(hzbInfo.cullResults);
        }
           
        Log("StaticMeshBuffer Count:{0}", staticMeshRenders.Count);
        MgrHiz.Instance.StaticMeshBuffer = new ComputeBuffer(staticMeshRenders.Count, sizeof(float) * 6);
        MgrHiz.Instance.StaticMeshBuffer.SetData(staticMeshBounds);
    }

    // 读取剔除结果
    private void LateUpdate()
    {
        TotalFrameCount++;
        bool success = false;
        var frame = Time.frameCount;
        for (int j= 1;j <= CommonData.HZBInfoCount; j++)
        {
            var hzbInfo = MgrHiz.Instance.hzbInfos[(frame - j + CommonData.HZBInfoCount) % CommonData.HZBInfoCount];
            if(hzbInfo.readBackSuccess)
            {
                int count = 0;
                for (int i = 0; i < staticMeshRenders.Count; i++)
                {
                    int intIndex = i / IntBits;
                    int integer = hzbInfo.cullResults[intIndex];
                    int bit = i - intIndex * IntBits;
                    int mask = 1 << bit;
                    bool visible = (integer & mask) != 0;
                    if (!visible)
                        count++;
                    staticMeshRenders[i].gameObject.SetActive(visible);
                }
                success = true;
                // Log("剔除结果读取成功,剔除数量:{0}", count);
                break;
            }
        }

        if(!success)
        {
            FailFrameCount++;
            Error("剔除结果读取失败 frameCount:{0}", Time.frameCount);
            for (int i = 0; i < staticMeshRenders.Count; i++)
                staticMeshRenders[i].gameObject.SetActive(true);
        }
    }

    private void Log(string format, params object[] args)
    {
        if (EnableLog)
            Debug.LogFormat(format, args);
    }
    private void Error(string format, params object[] args)
    {
        if (EnableLog)
            Debug.LogErrorFormat(format, args);
    }

    public void SwitchHZB()
    {
        _enableHZB = !_enableHZB;
        enableHZB = _enableHZB;
    }

    public void SwitchDepth()
    {
        MgrHiz.Instance.enableDpth=!MgrHiz.Instance.enableDpth;
    }

    public void Quit()
    {
        Application.Quit();
    }
    public void OnDestroy()
    {
        MgrHiz.Instance.Enable = false;
        MgrHiz.Instance.StaticMeshBuffer?.Dispose();
        MgrHiz.Instance.DynamicMeshBuffer?.Dispose();

        foreach (var hzbInfo in MgrHiz.Instance.hzbInfos)
        {
            hzbInfo.CullingResultBuffer?.Dispose();
        }

        if (MgrHiz.Instance.latencyResults.Count > 0)
        {
            double sum = 0;
            foreach (var latency in MgrHiz.Instance.latencyResults) sum += latency;
            double avg = sum / MgrHiz.Instance.latencyResults.Count;
            Log("回读延迟平均值:{0}ms", avg);
        }
        Log("回读失败帧数:{0}，总帧数:{1}，回读失败率：{2}", FailFrameCount, TotalFrameCount, (double)FailFrameCount / TotalFrameCount);
    }
}