using System;

using Unity.Collections;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public enum BufferType : uint
{
    PCM,
    Result
}

public sealed class SignalVisualizer : IDisposable
{
    private ComputeShader    visShader;
    private MicrophoneDevice microphone;

    private RenderTexture  texture;
    private GraphicsBuffer gpuBuffer;

    private Texture2D spriteTexture;
    private Sprite    sprite;

    private const int ImageDimensions = 1024;
    private const int ThreadsPerDim   = 256;
    private const int GridDimensions  = ImageDimensions / ThreadsPerDim;

    private Image imageUI;

    private NativeArray<float> output;

    public SignalVisualizer(MicrophoneDevice microphone, ComputeShader visShader, Image imageUI, BufferType bufferType)
    {
        Debug.Assert(microphone != null);
        Debug.Assert(imageUI    != null);
        Debug.Assert(visShader  != null);

        this.visShader  = visShader;
        this.microphone = microphone;
        gpuBuffer       = new GraphicsBuffer(GraphicsBuffer.Target.Structured, microphone.Samples, sizeof(float));
        texture         = new RenderTexture(ImageDimensions, ImageDimensions, 0, RenderTextureFormat.ARGB32, 0)
        {
            enableRandomWrite = true
        };
        spriteTexture = new Texture2D(ImageDimensions, ImageDimensions, TextureFormat.ARGB32, false);
        sprite        = Sprite.Create(spriteTexture, new Rect(0, 0, ImageDimensions, ImageDimensions), Vector2.zero);
        Debug.Assert(sprite != null);
        this.imageUI        = imageUI;
        this.imageUI.sprite = sprite;

        using CommandBuffer commandBuffer = new();
        commandBuffer.SetComputeIntParam(visShader, "GridDim", GridDimensions);
        commandBuffer.SetComputeBufferParam(visShader,  0, "Sample", gpuBuffer);
        commandBuffer.SetComputeTextureParam(visShader, 0, "Result", texture);
        Graphics.ExecuteCommandBuffer(commandBuffer);

        output = bufferType switch
        {
            BufferType.PCM    => microphone.PCM,
            BufferType.Result => microphone.Result,
            _ => throw new NotImplementedException()
        };
    }

    public void Dispatch(CommandBuffer commandBuffer)
    {
        commandBuffer.SetBufferData(gpuBuffer, output);
        commandBuffer.DispatchCompute(visShader, 0, GridDimensions, 1, 1);
        commandBuffer.CopyTexture(texture, spriteTexture);
    }

    public void Dispose()
    {
        microphone.Dispose();
        gpuBuffer.Dispose();

        UnityEngine.Object.Destroy(spriteTexture);
        UnityEngine.Object.Destroy(sprite);
        UnityEngine.Object.Destroy(texture);
    }
}
