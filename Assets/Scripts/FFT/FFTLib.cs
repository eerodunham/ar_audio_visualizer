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
                outPtr[i] = math.mul(outPtr[i], outPtr[i]);
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

    [BurstCompile]
    public struct HammingWindowJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias]
        public unsafe float* outPtr;
        public int length;

        public unsafe void Execute()
        {
            float recipLength = 1.0f / length;
            for (int i = 0; i < length; i++)
            {
                float cosTerm = (math.PI2 * i) * recipLength;
                outPtr[i]     = 0.54f - (0.46f * math.cos(cosTerm));
            }
        }
    }
}

[BurstCompile]
public struct FFTProperties
{
    [DllImport("AudioVisFFT")]
    private static unsafe extern bool CreateFFTProps(float* signalPtr, float* outPtr, float* windowPtr, ulong length, ulong interval, char* fileName, ref FFTProperties props);

    [DllImport("AudioVisFFT")]
    private static unsafe extern void ComputeFFT(ref FFTProperties props, ulong intervalOffset);

    private unsafe float* signalPtr;
    private unsafe float* windowPtr;
    private unsafe float* outPtr;
    private ulong length;
    private ulong interval;

    public unsafe readonly bool IsValid => signalPtr != null && outPtr != null && length > 0 && interval > 0;

    public static unsafe bool TryCreate<T>(in NativeArray<float> signal, in NativeArray<float> outData, in NativeArray<float>.ReadOnly windowData, T str, out FFTProperties props) where T : unmanaged, IUTF8Bytes
    {
        props = default;
        if (Hint.Unlikely(signal.Length == 0 || signal.Length != outData.Length || windowData.Length == 0))
        {
            return false;
        }

        float* signalPtr = (float*) signal.GetUnsafePtr();
        float* outPtr    = (float*) outData.GetUnsafePtr();
        float* windowPtr = (float*) windowData.GetUnsafeReadOnlyPtr();
        return CreateFFTProps(signalPtr, outPtr, windowPtr, unchecked((ulong) signal.Length), unchecked((ulong) windowData.Length), (char*) str.GetUnsafePtr(), ref props);
    }

    public unsafe bool ComputeFFT(int intervalOffset)
    {
        if (Hint.Unlikely(!IsValid))
        {
            return false;
        }
        ComputeFFT(ref this, unchecked((ulong) intervalOffset));
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
        return true;
    }
}