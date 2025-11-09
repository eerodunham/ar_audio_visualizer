using System;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[Serializable]
public struct VisEntry
{
    public ComputeShader visShader;
    public Image         imageUI;
}

[Serializable]
public struct MicrophoneVis
{
    public VisEntry pcmVis;
    public VisEntry outVis;
}

[DisallowMultipleComponent]
public sealed class AudioReader : MonoBehaviour
{
    [SerializeField] private MicrophoneVis vis;

    private MicrophoneDevice microphone;
    private SignalVisualizer pcmVis, outVis;

    void Start()
    {
        string[] deviceNames = Microphone.devices;
        Debug.Assert(deviceNames.Length > 0);

        string defaultDevice = deviceNames[0];
        microphone = new MicrophoneDevice(defaultDevice, 44100);
        microphone.Start();

        pcmVis = new SignalVisualizer(microphone, vis.pcmVis.visShader, vis.pcmVis.imageUI, BufferType.PCM);
        outVis = new SignalVisualizer(microphone, vis.outVis.visShader, vis.outVis.imageUI, BufferType.Result);
    }

    void Update()
    {
        microphone.Write();

        using CommandBuffer commandBuffer = new();
        pcmVis.Dispatch(commandBuffer);
        outVis.Dispatch(commandBuffer);
        Graphics.ExecuteCommandBuffer(commandBuffer);
    }

    void OnDestroy()
    {
        microphone.Dispose();
        pcmVis.Dispose();
    }
}