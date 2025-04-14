using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HizInit : MonoBehaviour
{
    struct  BoundStruct
    {
        public Vector3 center;
        public Vector3 size;
    }
    
    private OCMesh[] renderers;
    
    private List<double> cullingRate=new List<double>();
    
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
        Log("共有{0}个静态物体 {1}个动态物体 循环缓冲大小:{2}", staticMeshBounds.Length, dynamicMeshBounds.Length,CommonData.HZBInfoCount);
        
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
        if (EnableDynamicCull)
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

            MgrHiz.Instance.MeshBoundBuffer.SetData(dynamicMeshBounds, 0, staticMeshBounds.Length,
                dynamicMeshBounds.Length);
            // MgrHiz.Instance.DynamicMeshBuffer.SetData(dynamicMeshBounds);
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
        if (EnableDynamicCull)
            MgrHiz.Instance.MeshBoundBuffer =
                new ComputeBuffer(staticMeshRenders.Count + dynamicMeshRenders.Count, sizeof(float) * 6);
        else
            MgrHiz.Instance.MeshBoundBuffer = new ComputeBuffer(staticMeshRenders.Count, sizeof(float) * 6);
        MgrHiz.Instance.MeshBoundBuffer.SetData(staticMeshBounds, 0, 0, staticMeshBounds.Length);

        MgrHiz.Instance.staticMeshRenders = staticMeshRenders;
        MgrHiz.Instance.EnableLog = EnableLog;
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
                int StaticCount = 0,DynamicCount = 0;
                for (int i = 0; i < staticMeshRenders.Count; i++)
                {
                    int intIndex = i / IntBits;
                    int integer = hzbInfo.cullResults[intIndex];
                    int bit = i - intIndex * IntBits;
                    int mask = 1 << bit;
                    bool visible = (integer & mask) != 0;
                    if (!visible)
                        StaticCount++;
                    staticMeshRenders[i].enabled = visible;
                }
                for(int i=staticMeshRenders.Count;i<staticMeshRenders.Count+dynamicMeshRenders.Count;i++)
                {
                    int intIndex = i / IntBits;
                    int integer = hzbInfo.cullResults[intIndex];
                    int bit = i - intIndex * IntBits;
                    int mask = 1 << bit;
                    bool visible = (integer & mask) != 0;
                    if (!visible)
                        DynamicCount++;
                    dynamicMeshRenders[i - staticMeshRenders.Count].enabled = visible;
                }
                success = true;
                cullingRate.Add((StaticCount + DynamicCount) /
                                (float)(staticMeshRenders.Count + dynamicMeshRenders.Count));
                Log("剔除结果读取成功,剔除静态物体:{0}个，动态物体:{1}个", StaticCount, DynamicCount);
                break;
            }
        }
    
        if(!success)
        {
            FailFrameCount++;
            Error("剔除结果读取失败 frameCount:{0}", Time.frameCount);
            cullingRate.Add(0);
            for (int i = 0; i < staticMeshRenders.Count; i++)
                staticMeshRenders[i].enabled= true;
            for(int i=0;i<dynamicMeshRenders.Count;i++)
                dynamicMeshRenders[i].enabled = true;
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
        MgrHiz.Instance.OnDestroy();
        Log("回读失败帧数:{0}，总帧数:{1}，回读失败率：{2}", FailFrameCount, TotalFrameCount, (double)FailFrameCount / TotalFrameCount);
        Log("剔除率：{0}", cullingRate.Average());
    }
}