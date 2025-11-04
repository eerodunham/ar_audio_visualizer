using Unity.Collections;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AudioReader : MonoBehaviour
{
    [SerializeField]
    private ComputeShader    visShader;
    private MicrophoneDevice microphone;

    private RenderTexture  texture;
    private GraphicsBuffer gpuBuffer;

    private Texture2D spriteTexture;
    private Sprite    sprite;

    private Image imageUI;

    private int gridDim = 1024 / 128;

    void Start()
    {
        imageUI     = GetComponent<Image>();
        int samples = 44100;

        string[] deviceNames = Microphone.devices;
        Debug.Assert(deviceNames.Length > 0);

        texture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, 0)
        {
            enableRandomWrite = true
        };
        spriteTexture = new Texture2D(1024, 1024, TextureFormat.ARGB32, false);

        sprite = Sprite.Create(spriteTexture, new Rect(0, 0, 1024, 1024), Vector2.zero);
        Debug.Assert(sprite != null);
        imageUI.sprite = sprite;

        string defaultDevice = deviceNames[0];
        microphone = new MicrophoneDevice(defaultDevice, samples);
        gpuBuffer  = new GraphicsBuffer(GraphicsBuffer.Target.Structured, samples, sizeof(float));
        microphone.Start();

        using CommandBuffer commandBuffer = new();
        commandBuffer.SetComputeIntParam(visShader,        "GridDim", gridDim);
        commandBuffer.SetComputeBufferParam(visShader,  0, "Sample",  gpuBuffer);
        commandBuffer.SetComputeTextureParam(visShader, 0, "Result",  texture);
        Graphics.ExecuteCommandBuffer(commandBuffer);
    }

    void Update()
    {
        microphone.Write();

        using CommandBuffer commandBuffer = new();
        commandBuffer.SetBufferData(gpuBuffer, microphone.PCM);
        commandBuffer.DispatchCompute(visShader, 0, gridDim, 1, 1);
        commandBuffer.CopyTexture(texture, spriteTexture);
        Graphics.ExecuteCommandBuffer(commandBuffer);
    }

    void OnDestroy()
    {
        microphone.Dispose();
        gpuBuffer.Dispose();

        Destroy(spriteTexture);
        Destroy(sprite);
        Destroy(texture);
    }
}