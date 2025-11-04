using System;
using System.Runtime.InteropServices;

using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEngine;

using Impl;

namespace Impl
{
    [BurstCompile]
    public struct ProcessSignalJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias]
        public unsafe float* outPtr;
        public int length;

        public unsafe void Execute()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < length; i++)
            {
                outPtr[i] = math.abs(outPtr[i]);
                min = math.min(min, outPtr[i]);
                max = math.max(max, outPtr[i]);
            }
            float recipExt = 1.0f / (max - min);
            for (int i = 0; i < length; i++)
            {
                outPtr[i] = (outPtr[i] - min) * recipExt;
            }
        }
    }
}

[BurstCompile]
public struct FFTProperties
{
    [DllImport("AudioVisFFT")]
    private static unsafe extern bool CreateFFTProps(float* signalPtr, float* outPtr, ulong length, char* fileName, ref FFTProperties props);

    [DllImport("AudioVisFFT")]
    private static unsafe extern void ComputeFFT(ref FFTProperties props);

    private unsafe float* signalPtr;
    private unsafe float* outPtr;
    private ulong length;

    public unsafe readonly bool IsValid => signalPtr != null && outPtr != null && length > 0;

    public static unsafe bool TryCreate<T>(in NativeArray<float> signal, in NativeArray<float> outData, T str, out FFTProperties props) where T : unmanaged, IUTF8Bytes
    {
        props = default;
        if (Hint.Unlikely(signal.Length == 0 || signal.Length != outData.Length))
        {
            return false;
        }

        float* signalPtr = (float*) signal.GetUnsafePtr();
        float* outPtr    = (float*) outData.GetUnsafePtr();
        return CreateFFTProps(signalPtr, outPtr, unchecked((ulong) signal.Length), (char*) str.GetUnsafePtr(), ref props);
    }

    public unsafe bool ComputeFFT()
    {
        if (Hint.Unlikely(!IsValid))
        {
            return false;
        }
        ComputeFFT(ref this);
        return true;
    }

    public unsafe bool ProcessSignal(out JobHandle handleOut) => ProcessSignal(default, out handleOut);
    public unsafe bool ProcessSignal(JobHandle handleIn, out JobHandle handleOut)
    {
        if (Hint.Unlikely(!IsValid))
        {
            handleOut = default;
            return false;
        }
        ProcessSignalJob processJob = new()
        {
            outPtr = outPtr,
            length = unchecked((int) length)
        };
        handleOut = processJob.Schedule(handleIn);
        handleOut.Complete();
        return true;
    }
}