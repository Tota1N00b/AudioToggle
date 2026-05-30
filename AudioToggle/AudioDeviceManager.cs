using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AudioToggle;

internal sealed class AudioDeviceManager
{
    private const string RenderDevicesRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
    private const string DeviceDescriptionRegistryValueName = "{a45c254e-df1c-4efd-8020-67d146a850e0},2";
    private const string EndpointNameRegistryValueName = "{b3f8fa53-0004-438e-9003-51a46e139bfc},6";

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
                        var friendlyName = GetFriendlyName(id);

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

    private static string GetFriendlyName(string deviceId)
    {
        return ReadRegistryFriendlyName(deviceId) ?? "Unknown device";
    }

    private static string? ReadRegistryFriendlyName(string deviceId)
    {
        var endpointKeyName = GetEndpointKeyName(deviceId);
        if (endpointKeyName is null)
        {
            return null;
        }

        using var propertiesKey = Registry.LocalMachine.OpenSubKey($@"{RenderDevicesRegistryPath}\{endpointKeyName}\Properties", writable: false);
        if (propertiesKey is null)
        {
            return null;
        }

        return BuildFallbackFriendlyName(
            ReadRegistryString(propertiesKey, DeviceDescriptionRegistryValueName),
            ReadRegistryString(propertiesKey, EndpointNameRegistryValueName));
    }

    private static string? GetEndpointKeyName(string deviceId)
    {
        var endpointStart = deviceId.LastIndexOf(".{", StringComparison.Ordinal);
        return endpointStart >= 0 && endpointStart + 1 < deviceId.Length
            ? deviceId[(endpointStart + 1)..]
            : null;
    }

    private static string? ReadRegistryString(RegistryKey key, string valueName)
    {
        return key.GetValue(valueName) is string text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
    }

    private static string BuildFallbackFriendlyName(string? deviceDescription, string? endpointName)
    {
        if (deviceDescription is null)
        {
            return endpointName ?? "Unknown device";
        }

        if (endpointName is null || string.Equals(deviceDescription, endpointName, StringComparison.OrdinalIgnoreCase))
        {
            return deviceDescription;
        }

        return $"{deviceDescription} ({endpointName})";
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
