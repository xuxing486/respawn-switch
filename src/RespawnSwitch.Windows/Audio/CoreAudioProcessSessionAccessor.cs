// Author: Stress Monster
using System.Runtime.InteropServices;

namespace RespawnSwitch.Windows.Audio;

internal sealed class CoreAudioProcessSessionAccessor : IProcessAudioSessionAccessor
{
    public IReadOnlyList<ProcessAudioSessionSnapshot> Snapshot(int processId)
    {
        var result = new List<ProcessAudioSessionSnapshot>();
        VisitSessions((endpointId, instanceId, pid, volume) =>
        {
            if (pid != processId) return false;
            Marshal.ThrowExceptionForHR(volume.GetMute(out var muted));
            result.Add(new(new(endpointId, instanceId), pid, muted));
            return false;
        });
        return result;
    }

    public bool TryChangeMute(ProcessAudioSessionKey key, int processId, bool expectedCurrentMute, bool targetMute)
    {
        var changed = false;
        try
        {
            VisitSessions((endpointId, instanceId, pid, volume) =>
            {
                if (changed || pid != processId || endpointId != key.EndpointId || instanceId != key.SessionInstanceId) return false;
                Marshal.ThrowExceptionForHR(volume.GetMute(out var current));
                if (current != expectedCurrentMute) return false;
                var eventContext = Guid.Empty;
                Marshal.ThrowExceptionForHR(volume.SetMute(targetMute, ref eventContext));
                changed = true;
                return true;
            });
        }
        catch (COMException) { }
        catch (InvalidCastException) { }
        return changed;
    }

    private static void VisitSessions(Func<string, string, int, ISimpleAudioVolume, bool> visitor)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? devices = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceState.Active, out devices));
            Marshal.ThrowExceptionForHR(devices.GetCount(out var deviceCount));
            for (uint deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                IMMDevice? device = null;
                IAudioSessionManager2? manager = null;
                IAudioSessionEnumerator? sessionEnumerator = null;
                try
                {
                    Marshal.ThrowExceptionForHR(devices.Item(deviceIndex, out device));
                    Marshal.ThrowExceptionForHR(device.GetId(out var endpointId));
                    var managerId = typeof(IAudioSessionManager2).GUID;
                    Marshal.ThrowExceptionForHR(device.Activate(ref managerId, ClsCtx.All, IntPtr.Zero, out var activated));
                    manager = (IAudioSessionManager2)activated;
                    Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out sessionEnumerator));
                    Marshal.ThrowExceptionForHR(sessionEnumerator.GetCount(out var sessionCount));
                    for (var sessionIndex = 0; sessionIndex < sessionCount; sessionIndex++)
                    {
                        IAudioSessionControl? control = null;
                        try
                        {
                            Marshal.ThrowExceptionForHR(sessionEnumerator.GetSession(sessionIndex, out control));
                            var control2 = (IAudioSessionControl2)control;
                            var volume = (ISimpleAudioVolume)control;
                            Marshal.ThrowExceptionForHR(control2.GetProcessId(out var rawPid));
                            Marshal.ThrowExceptionForHR(control2.GetSessionInstanceIdentifier(out var instanceId));
                            if (visitor(endpointId, instanceId, checked((int)rawPid), volume)) return;
                        }
                        finally { Release(control); }
                    }
                }
                finally
                {
                    Release(sessionEnumerator);
                    Release(manager);
                    Release(device);
                }
            }
        }
        finally
        {
            Release(devices);
            Release(enumerator);
        }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }

    private enum EDataFlow { Render, Capture, All }
    [Flags] private enum DeviceState : uint { Active = 1 }
    [Flags] private enum ClsCtx : uint { InprocServer = 1, InprocHandler = 2, LocalServer = 4, RemoteServer = 16, All = 23 }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumeratorComObject { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, ClsCtx clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out DeviceState state);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid sessionGuid, uint streamFlags, out IntPtr sessionControl);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid sessionGuid, uint streamFlags, out IntPtr audioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
        [PreserveSig] int RegisterSessionNotification(IntPtr sessionNotification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr sessionNotification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    private interface IAudioSessionControl { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, Guid eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingId);
        [PreserveSig] int SetGroupingParam(Guid groupingId, Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionIdentifier);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceIdentifier);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
