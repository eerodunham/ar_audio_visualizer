using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AudioReader : MonoBehaviour
{
    [SerializeField] private ComputeShader visShader;
    [SerializeField] private Image imageUI;

    private MicrophoneDevice microphone;
    private SignalVisualizer signalVis;

    void Start()
    {
        string[] deviceNames = Microphone.devices;
        Debug.Assert(deviceNames.Length > 0);

        string defaultDevice = deviceNames[0];
        microphone = new MicrophoneDevice(defaultDevice, 44100);
        microphone.Start();
        signalVis  = new SignalVisualizer(microphone, visShader, imageUI);
    }

    void Update()
    {
        microphone.Write();
        signalVis.Dispatch();
    }

    void OnDestroy()
    {
        microphone.Dispose();
        signalVis.Dispose();
    }
}