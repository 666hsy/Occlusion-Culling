using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public class TestReadBack : MonoBehaviour
{
    private int intCount = 1000000;
    private int[] testInts;

    private ComputeBuffer testComputerBuffer;

    public ComputeShader testComputeShader;
    
    // 存储未完成的请求及其时间戳
    private Queue<float> pendingRequestTimestamps = new Queue<float>();
    private Stopwatch stopwatch = new Stopwatch();
    // 存储延迟结果（毫秒）
    private List<float> latencyResults = new List<float>();
    
    private class ShaderConstants
    {
        public static int testInts = Shader.PropertyToID("intArray");
        public static int intCount = Shader.PropertyToID("count");
    }
    void Start()
    {
        testInts = new int[intCount];
        testComputerBuffer = new ComputeBuffer(intCount, sizeof(int));
        testComputerBuffer.SetData(testInts);
    }

    // Update is called once per frame
    void Update()
    {
        testComputeShader.SetInt(ShaderConstants.intCount, intCount);
        testComputeShader.SetBuffer(0, ShaderConstants.testInts, testComputerBuffer);
        int groupCountX = (int)Mathf.CeilToInt((1.0f * intCount) / 64);
        testComputeShader.Dispatch(0, groupCountX, 1, 1);
        
        testComputeShader.SetBuffer(1, ShaderConstants.testInts, testComputerBuffer);
        // long requestTimestamp = stopwatch.ElapsedTicks;
        pendingRequestTimestamps.Enqueue(Time.time);
        
        testComputeShader.Dispatch(1, groupCountX, 1, 1);
        Debug.Log("start compute  " + Time.time);
        AsyncGPUReadback.Request(testComputerBuffer, (req) => TestAsyncReadBack(req));
    }

    void TestAsyncReadBack(AsyncGPUReadbackRequest request)
    {
        if (request.done && !request.hasError)
        {
            request.GetData<int>().CopyTo(testInts);

            // long callbackTimestamp = stopwatch.ElapsedTicks;
            float latencyTicks = Time.time - pendingRequestTimestamps.Dequeue();
            // 转换为毫秒（Stopwatch.Frequency单位为Hz，1秒=1e7 ticks）
            // double latencyMs = (latencyTicks * 1000.0) / Stopwatch.Frequency;
            Debug.Log("endcompute " + latencyTicks + "   " + testInts[0]);
            latencyResults.Add(latencyTicks);
        }
        else
        {
            Debug.LogError("ReadBackFailed");
        }
    }
    
    private void OnDestroy()
    {
        if (testComputerBuffer != null)
        {
            testComputerBuffer.Release();
            testComputerBuffer = null;
        }

        if (latencyResults.Count > 0)
            Debug.Log("平均延迟" + latencyResults.Average() * 1000 + "ms");
    }
}
