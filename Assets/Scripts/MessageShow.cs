using TMPro;
using UnityEngine;

public class MessageShow : MonoBehaviour
{
    public TextMeshProUGUI fpsText;
    private float deltaTime;
    private string computeShaderSupportMessage;

    private void Start()
    {
        // 在 Start 方法中检查是否支持 Compute Shader 并输出信息
        bool supportsComputeShader = SystemInfo.supportsComputeShaders;
        computeShaderSupportMessage = supportsComputeShader ? "Support Compute Shader" : "not Support Compute Shader";
    }
    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        fpsText.text = computeShaderSupportMessage + "\n" + $"{fps:0.} FPS";
    }
}