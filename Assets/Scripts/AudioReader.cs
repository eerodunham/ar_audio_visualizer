using System;
using System.Runtime.CompilerServices;
using System.Reflection;

using Unity.Collections;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

public static class MicrophoneQueryMethods
{
    private delegate int GetMicrophoneDeviceIDLambda(string name);
    private delegate AudioClip StartRecordLambda(int deviceID, bool loop, float lengthSec, int frequency);
    private delegate void StopRecordLambda(int deviceID);
    private delegate bool IsRecordingLambda(int deviceID);
    private delegate int  GetRecordPositionLambda(int deviceID);

    private static GetMicrophoneDeviceIDLambda microphoneDeviceIDLambda;
    private static StartRecordLambda startRecordLambda;
    private static StopRecordLambda  stopRecordLambda;
    private static IsRecordingLambda isRecordingLambda;
    private static GetRecordPositionLambda getRecordPositionLambda;

    private static T GetPrivateStaticFunc<T>(string name) where T : Delegate
    {
        MethodInfo microphoneIDInfo = typeof(Microphone).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        return (T) Delegate.CreateDelegate(typeof(T), microphoneIDInfo);
    }

    static MicrophoneQueryMethods()
    {
        microphoneDeviceIDLambda = GetPrivateStaticFunc<GetMicrophoneDeviceIDLambda>("GetMicrophoneDeviceIDFromName");
        startRecordLambda        = GetPrivateStaticFunc<StartRecordLambda>("StartRecord");
        stopRecordLambda         = GetPrivateStaticFunc<StopRecordLambda>("EndRecord");
        isRecordingLambda        = GetPrivateStaticFunc<IsRecordingLambda>("IsRecording");
        getRecordPositionLambda  = GetPrivateStaticFunc<GetRecordPositionLambda>("GetRecordPosition");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMicrophoneDeviceID(string name) => microphoneDeviceIDLambda.Invoke(name);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AudioClip StartRecording(int deviceID, bool loop, float lengthSec, int frequency) => startRecordLambda.Invoke(deviceID, loop, lengthSec, frequency);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StopRecording(int deviceID)    => stopRecordLambda.Invoke(deviceID);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRecording(int deviceID)      => isRecordingLambda.Invoke(deviceID);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRecordPosition(int deviceID) => getRecordPositionLambda.Invoke(deviceID);
}

public sealed class MicrophoneDevice : IDisposable
{
    private readonly string deviceName;
    private readonly int    cachedID;
    private readonly int    samples;
    private AudioClip       audioClip;

    private int currentSample;

    private NativeArray<float>  sampledData;
    private NativeArray<float2> complexData;

    private FFTProperties props;

    public string DeviceName       => deviceName;
    public int Samples             => samples;
    public NativeArray<float> Data => sampledData;

    private MicrophoneDevice()
    {
        deviceName  = null;
        audioClip   = null;
        cachedID    = -1;
        samples     = 0;

        sampledData = default;
        complexData = default;
    }

    public MicrophoneDevice(string deviceName, int samples = 44100)
    {
        Debug.Assert(deviceName != null && deviceName.Length > 0);
        Debug.Assert(samples > 0);
        this.deviceName = deviceName;
        this.samples    = samples;

        cachedID = MicrophoneQueryMethods.GetMicrophoneDeviceID(deviceName);
        Debug.Assert(cachedID != -1);

        sampledData = new NativeArray<float>(samples,  Allocator.Persistent);
        complexData = new NativeArray<float2>(samples, Allocator.Persistent);

        UnsafeText text = new(deviceName.Length, Allocator.Temp);
        text.Append($"{samples}Wisdom");
        bool isSuccessful = FFTProperties.Create(sampledData.AsReadOnlySpan(), complexData.AsReadOnlySpan(), text, out props);
        Debug.Assert(isSuccessful);
    }

    public void Start()
    {
        audioClip = MicrophoneQueryMethods.StartRecording(cachedID, true, 1, samples);
    }

    public void End() => MicrophoneQueryMethods.StopRecording(cachedID);

    public void Write()
    {
        audioClip.GetData(sampledData.AsSpan(), offsetSamples: currentSample);
        currentSample = MicrophoneQueryMethods.GetRecordPosition(cachedID);
    }

    public void Dispose()
    {
        audioClip = null;

        sampledData.Dispose();
        sampledData = default;

        complexData.Dispose();
        complexData = default;
    }
}

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

    private NativeArray<float2> complex;

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
        commandBuffer.SetBufferData(gpuBuffer, microphone.Data);
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