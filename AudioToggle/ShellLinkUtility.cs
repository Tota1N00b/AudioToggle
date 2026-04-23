using System.Runtime.InteropServices;

namespace AudioToggle;

internal sealed class ShellShortcutOptions
{
    public required string ShortcutPath { get; init; }
    public required string TargetPath { get; init; }
    public required string WorkingDirectory { get; init; }
    public required string Description { get; init; }
    public string? IconPath { get; init; }
    public string? Arguments { get; init; }
    public string? AppUserModelId { get; init; }
}

internal static class ShellLinkUtility
{
    private static readonly PropertyKey AppUserModelIdKey = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    public static void CreateOrUpdateShortcut(ShellShortcutOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.ShortcutPath)
            ?? throw new InvalidOperationException("Shortcut target directory is unavailable."));

        var shellLink = (IShellLinkW)new ShellLink();
        try
        {
            shellLink.SetPath(options.TargetPath);
            shellLink.SetWorkingDirectory(options.WorkingDirectory);
            shellLink.SetDescription(options.Description);
            shellLink.SetArguments(options.Arguments ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(options.IconPath) && File.Exists(options.IconPath))
            {
                shellLink.SetIconLocation(options.IconPath, 0);
            }

            if (!string.IsNullOrWhiteSpace(options.AppUserModelId))
            {
                var propertyStore = (IPropertyStore)shellLink;
                var key = AppUserModelIdKey;
                var value = PropVariant.FromString(options.AppUserModelId);
                try
                {
                    propertyStore.SetValue(ref key, ref value);
                    propertyStore.Commit();
                }
                finally
                {
                    value.Dispose();
                }
            }

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(options.ShortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] string pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] string pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] string pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] string pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        uint GetCount(out uint cProps);
        uint GetAt(uint iProp, out PropertyKey pkey);
        uint GetValue(ref PropertyKey key, out PropVariant pv);
        uint SetValue(ref PropertyKey key, ref PropVariant pv);
        uint Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PropertyKey(Guid formatId, uint propertyId)
    {
        public Guid FormatId { get; } = formatId;
        public uint PropertyId { get; } = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant : IDisposable
    {
        private ushort _valueType;
        private ushort _reserved1;
        private ushort _reserved2;
        private ushort _reserved3;
        private IntPtr _value;
        private int _valueInt32;

        public static PropVariant FromString(string value)
        {
            return new PropVariant
            {
                _valueType = (ushort)VarEnum.VT_LPWSTR,
                _value = Marshal.StringToCoTaskMemUni(value)
            };
        }

        public void Dispose()
        {
            PropVariantClear(ref this);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);
    }
}
