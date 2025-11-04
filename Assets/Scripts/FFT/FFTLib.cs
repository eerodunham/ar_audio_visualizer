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
    [DllImport("AudioVisFFT.dll")]
    private static unsafe extern bool CreateFFTProps(float* signalPtr, float2* complexPtr, ulong length, char* fileName, ref FFTProperties props);

    private unsafe float*  signalPtr;
    private unsafe float2* complexPtr;
    private ulong length;

    public static unsafe bool Create<T>(in ReadOnlySpan<float> signal, in ReadOnlySpan<float2> complex, T str, out FFTProperties props) where T : unmanaged, IUTF8Bytes
    {
        props = default;
        if (Hint.Unlikely(signal.Length == 0 || signal.Length != complex.Length))
        {
            return false;
        }
        fixed (float* signalPtr = signal)
        {
            fixed (float2* complexPtr = complex)
            {
                return CreateFFTProps(signalPtr, complexPtr, unchecked((ulong) signal.Length), (char*) str.GetUnsafePtr(), ref props);
            }
        }
    }
}

public struct FFTDispatch
{

}