using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Pointly.App.Voice;

internal static class DefaultMicrophone
{
    // Ask WinMM for its current preferred capture device, rather than treating index 0 as default.
    private const uint MapperPreferredGet = 0x2015;

    public static (int Index, string Name) Resolve()
    {
        uint result = QueryPreferredDevice(new IntPtr(-1), MapperPreferredGet,
            out uint index, out _);
        if (result != 0 || index == uint.MaxValue || index >= WaveInEvent.DeviceCount)
            throw new VoiceException("Microphone", "DefaultMicrophoneUnavailable");
        return ((int)index, SafeName(WaveInEvent.GetCapabilities((int)index).ProductName));
    }

    public static string DescribeWindowsDefault()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            return $"WindowsDefaultCapture={SafeName(device.FriendlyName)} Role=Multimedia " +
                $"Muted={device.AudioEndpointVolume.Mute} State={device.State}";
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return "WindowsDefaultCapture=Unavailable MuteState=Unknown";
        }
    }

    private static string SafeName(string name) =>
        new(name.Where(character => !char.IsControl(character)).Take(128).ToArray());

    [DllImport("winmm.dll", EntryPoint = "waveInMessage")]
    private static extern uint QueryPreferredDevice(IntPtr device, uint message,
        out uint preferred, out uint flags);
}
