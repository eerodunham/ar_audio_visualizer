using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine;

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
    private readonly int    sampleRate;

    private AudioClip audioClip;

    private int currentSample;
    private int currentWindow;

    private NativeArray<float> pcmData;
    private NativeArray<float> result;

    private FFTProperties fftProps;

    public string DeviceName         => deviceName;
    public int Samples               => samples;
    public int SampleRate            => sampleRate;

    public NativeArray<float> PCM    => pcmData;
    public NativeArray<float> Result => result;

    private MicrophoneDevice()
    {
        deviceName  = null;
        audioClip   = null;

        cachedID    = -1;
        samples     = 0;

        pcmData = default;
        result  = default;
    }

    public MicrophoneDevice(string deviceName, int samples = 44100, int sampleRate = 441)
    {
        Debug.Assert(deviceName != null && deviceName.Length > 0);
        Debug.Assert(samples    > 0);
        Debug.Assert(sampleRate > 0 && math.frac(samples / (float) sampleRate) == 0.0f, "Sample rate is not cleanly divisible.");
        this.deviceName = deviceName;
        this.samples    = samples;
        this.sampleRate = sampleRate;

        cachedID = MicrophoneQueryMethods.GetMicrophoneDeviceID(deviceName);
        Debug.Assert(cachedID != -1);

        pcmData = new NativeArray<float>(samples, Allocator.Persistent);
        result  = new NativeArray<float>(samples, Allocator.Persistent);

        UnsafeText text = new(deviceName.Length, Allocator.Temp);
        text.Append($"{sampleRate}Wisdom.dat");
        bool isSuccessful = FFTProperties.TryCreate(pcmData, result, text, samples / sampleRate, out fftProps);
        Debug.Assert(isSuccessful);
    }

    public void Write()
    {
        audioClip.GetData(pcmData.AsSpan(), offsetSamples: currentSample);
        currentSample = MicrophoneQueryMethods.GetRecordPosition(cachedID);

        Debug.Assert(fftProps.ComputeFFT(currentWindow));
        Debug.Assert(fftProps.ProcessSignal(out var handle));
        handle.Complete();

        currentWindow = math.clamp(++currentWindow, 0, sampleRate - 1);
    }


    public void Start()
    {
        audioClip = MicrophoneQueryMethods.StartRecording(cachedID, true, 1, samples);
    }

    public void End() => MicrophoneQueryMethods.StopRecording(cachedID);

    public void Dispose()
    {
        audioClip = null;

        pcmData.Dispose();
        pcmData = default;

        result.Dispose();
        result = default;
    }
}