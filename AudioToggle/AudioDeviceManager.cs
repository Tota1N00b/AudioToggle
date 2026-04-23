using System.Runtime.InteropServices;

namespace AudioToggle;

internal sealed class AudioDeviceManager
{
    public IReadOnlyList<AudioDeviceInfo> GetActiveRenderDevices()
    {
        var enumerator = CreateEnumerator();
        try
        {
            var defaultConsoleId = TryGetDefaultDeviceId(enumerator, ERole.eConsole);
            var defaultMultimediaId = TryGetDefaultDeviceId(enumerator, ERole.eMultimedia);
            var defaultCommunicationsId = TryGetDefaultDeviceId(enumerator, ERole.eCommunications);

            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceState.Active, out var collection));
            if (collection is null)
            {
                throw new InvalidOperationException("Windows returned a null device collection.");
            }

            try
            {
                Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
                var devices = new List<AudioDeviceInfo>(count);

                for (var index = 0; index < count; index++)
                {
                    Marshal.ThrowExceptionForHR(collection.Item(index, out var device));
                    if (device is null)
                    {
                        throw new InvalidOperationException($"Windows returned a null device at index {index}.");
                    }

                    try
                    {
                        var id = GetDeviceId(device);
                        var friendlyName = GetFriendlyName(device);

                        devices.Add(new AudioDeviceInfo
                        {
                            Id = id,
                            FriendlyName = friendlyName,
                            IsDefaultConsole = string.Equals(id, defaultConsoleId, StringComparison.Ordinal),
                            IsDefaultMultimedia = string.Equals(id, defaultMultimediaId, StringComparison.Ordinal),
                            IsDefaultCommunications = string.Equals(id, defaultCommunicationsId, StringComparison.Ordinal)
                        });
                    }
                    finally
                    {
                        ReleaseComObject(device);
                    }
                }

                return devices
                    .OrderBy(device => device.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            finally
            {
                ReleaseComObject(collection);
            }
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    public string? GetCurrentDefaultDeviceId()
    {
        var enumerator = CreateEnumerator();
        try
        {
            return TryGetDefaultDeviceId(enumerator, ERole.eMultimedia)
                ?? TryGetDefaultDeviceId(enumerator, ERole.eConsole)
                ?? TryGetDefaultDeviceId(enumerator, ERole.eCommunications);
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    public void SetDefaultOutputDevice(string deviceId)
    {
        var policyConfig = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications));
        }
        finally
        {
            ReleaseComObject(policyConfig);
        }
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        return (IMMDeviceEnumerator)new MMDeviceEnumerator();
    }

    private static string? TryGetDefaultDeviceId(IMMDeviceEnumerator enumerator, ERole role)
    {
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, role, out var device));
            if (device is null)
            {
                return null;
            }

            try
            {
                return GetDeviceId(device);
            }
            finally
            {
                ReleaseComObject(device);
            }
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string GetDeviceId(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.GetId(out var id));
        return string.IsNullOrWhiteSpace(id)
            ? throw new InvalidOperationException("Windows returned an empty audio device ID.")
            : id;
    }

    private static string GetFriendlyName(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(StorageAccessMode.Read, out var propertyStore));
        if (propertyStore is null)
        {
            return "Unknown device";
        }

        try
        {
            Marshal.ThrowExceptionForHR(propertyStore.GetValue(PropertyKeys.PKEY_Device_FriendlyName, out var value));
            try
            {
                return value.GetString() ?? "Unknown device";
            }
            finally
            {
                value.Dispose();
            }
        }
        finally
        {
            ReleaseComObject(propertyStore);
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
