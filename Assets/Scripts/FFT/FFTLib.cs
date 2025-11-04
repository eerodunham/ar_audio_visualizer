using System;
using System.Runtime.InteropServices;

using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;

[BurstCompile]
public struct FFTProperties
{
    [DllImport("AudioVisFFT")]
    private static unsafe extern bool CreateFFTProps(float* signalPtr, float* complexPtr, ulong length, char* fileName, ref FFTProperties props);

    [DllImport("AudioVisFFT")]
    private static unsafe extern void ComputeFFT(ref FFTProperties props);

    private unsafe float*  signalPtr;
    private unsafe float2* complexPtr;
    private ulong length;

    public unsafe readonly bool IsValid => signalPtr != null && complexPtr != null && length > 0;

    public static unsafe bool TryCreate<T>(in ReadOnlySpan<float> signal, in ReadOnlySpan<float> complex, T str, out FFTProperties props) where T : unmanaged, IUTF8Bytes
    {
        props = default;
        if (Hint.Unlikely(signal.Length == 0 || signal.Length != complex.Length))
        {
            return false;
        }
        fixed (float* signalPtr = signal)
        {
            fixed (float* complexPtr = complex)
            {
                return CreateFFTProps(signalPtr, complexPtr, unchecked((ulong) signal.Length), (char*) str.GetUnsafePtr(), ref props);
            }
        }
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
}