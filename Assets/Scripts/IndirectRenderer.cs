using UnityEngine;
using UnityEngine.Rendering;
public class IndirectRenderer : MonoBehaviour
{
    [System.Serializable]
    public class IndirectInstanceData
    {
        public GameObject prefab;
        public int count;

        //每个实例的位置、旋转和缩放
        public Vector3[] positions;
        public Vector3[] rotations;
        public Vector3[] scales;
    }
    
    private class ShaderConstants
    {
        public static readonly int CulledResultBuffer = Shader.PropertyToID("CulledResultBuffer");
        public static readonly int InputInstanceDataBuffer = Shader.PropertyToID("InputInstanceBuffer");
        public static readonly int InstanceDataList = Shader.PropertyToID("InstanceDataList");
    }
    public ComputeShader ComputeShader;
    public IndirectInstanceData[] Instances;    //需要GPU Instancing的物体

    private int numberOfInstanceTypes;  //实例的种类的数量
    private int numberOfInstances = 0;      //实例的总数量
    private ComputeBuffer indirectArgs;
    private ComputeBuffer culledResultBuffer;   //保存剔除后的结果
    private ComputeBuffer inpuInstanceDataBuffer;   //保存实例数据
    
    private int maxInstanceSize = 3000;  //剔除后的最大绘制的实例数量
    private CommandBuffer commandBuffer;
    private uint[] args;
    private int kernel;

    // Start is called before the first frame update
    void Start()
    {
        if(Instances == null || Instances.Length == 0)
            Debug.LogWarning("没有Instance数据");
        Init();
    }
    private void Init()
    {
        numberOfInstanceTypes = Instances.Length;
        for (int i = 0; i < numberOfInstanceTypes; i++)
            numberOfInstances += Instances[i].count;
        //一共多少个元素，每个元素几个字节，类型
        indirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);

        var meshFilter = Instances[0].prefab.GetComponent<MeshFilter>();
        var meshRenderer = Instances[0].prefab.GetComponent<MeshRenderer>();
        var mesh = meshFilter.sharedMesh;
        indirectArgs.SetData(new uint[] { mesh.GetIndexCount(0), 3000, 0, 0, 0 });
        commandBuffer = new CommandBuffer();
        culledResultBuffer = new ComputeBuffer(maxInstanceSize, 24, ComputeBufferType.Append);
        InitKernels();
        InitInputBuffer();
    }
    
    private void InitInputBuffer()
    {
        inpuInstanceDataBuffer = new ComputeBuffer(numberOfInstances, sizeof(float) * 16);
        ComputeShader.SetBuffer(kernel, ShaderConstants.InputInstanceDataBuffer, inpuInstanceDataBuffer);
        
        int cnt = 0;
        Matrix4x4[] instanceMatrix= new Matrix4x4[numberOfInstances];
        for (int i = 0; i < numberOfInstanceTypes; i++)
        {
            var instanceData = Instances[i];
            for(int j=0; j<instanceData.count; j++)
            {
                instanceMatrix[cnt++] = Matrix4x4.TRS(instanceData.positions[j],
                    Quaternion.Euler(instanceData.rotations[j]), instanceData.scales[j]);
            }
        }
        inpuInstanceDataBuffer.SetData(instanceMatrix);
    }

    private void InitKernels()
    {
        kernel = ComputeShader.FindKernel("Culling");
        ComputeShader.SetBuffer(kernel, ShaderConstants.CulledResultBuffer, culledResultBuffer);
    }
    
    // Update is called once per frame
    void Update()
    {
        Dispatch();
        
        for (int i=0; i<numberOfInstanceTypes; i++)
        {
            var meshFilter = Instances[i].prefab.GetComponent<MeshFilter>();
            var meshRenderer = Instances[i].prefab.GetComponent<MeshRenderer>();
            var mesh = meshFilter.sharedMesh;
            meshRenderer.sharedMaterial.SetBuffer(ShaderConstants.InstanceDataList, culledResultBuffer);
            Graphics.DrawMeshInstancedIndirect(mesh, 0, meshRenderer.sharedMaterial, new Bounds(Vector3.zero, new Vector3(500, 100, 500)), indirectArgs);
        }
    }
    private void Dispatch()
    {
        ClearBufferCounter();
        commandBuffer.DispatchCompute(ComputeShader, kernel, 1 + numberOfInstances / 64, 1, 1);
        commandBuffer.CopyCounterValue(culledResultBuffer, indirectArgs, 4);    //获得结果的实例数量
        Graphics.ExecuteCommandBuffer(commandBuffer);
        // LogInstanceArgs();
    }
    private void LogInstanceArgs(){
        var data = new uint[5];
        indirectArgs.GetData(data);
        Debug.Log(data[1]);
    }
    private void ClearBufferCounter()
    {
        commandBuffer.Clear();
        commandBuffer.SetBufferCounterValue(inpuInstanceDataBuffer, (uint)numberOfInstances);
        commandBuffer.SetBufferCounterValue(culledResultBuffer, 0);
    }

    private void OnDestroy()
    {
        indirectArgs.Dispose();
    }

    [ContextMenu("生成集Instance数据")]
    public void CollectInstanceData()
    {
        Vector2 xRange = new Vector2(-250f, 250f);
        Vector2 yRange = new Vector2(0f, 30f);
        Vector2 zRange = new Vector2(-250f, 250f);
        Vector2 scaleRange = new Vector2(0.5f, 3f);

        for (int i=0; i<Instances.Length; i++)
        {
            var instanceData = Instances[i];
            instanceData.rotations = new Vector3[instanceData.count];
            instanceData.positions = new Vector3[instanceData.count];
            instanceData.scales = new Vector3[instanceData.count];
            for(int j=0; j<instanceData.count; j++)
            {
                // 随机生成位置
                Vector3 pos = new Vector3(
                    Random.Range(xRange.x, xRange.y),
                    Random.Range(yRange.x, yRange.y),
                    Random.Range(zRange.x, zRange.y)
                );
                // 随机生成均匀缩放
                float scale = Random.Range(scaleRange.x, scaleRange.y);
                instanceData.positions[j] = pos;
                instanceData.scales[j] = new Vector3(scale, scale, scale);
                instanceData.rotations[j] = Vector3.zero;
            }
        }
    }
}
