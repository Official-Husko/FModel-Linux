using System;

namespace Microsoft.Win32;

// .NET ships Microsoft.Win32.Registry on every platform, but outside Windows each call throws
// PlatformNotSupportedException. Upstream reads the registry to auto-detect installed games (Steam, Epic,
// Rockstar, ...), sometimes outside a try/catch, which would crash FModel during startup.
//
// These source-defined types take precedence over the framework ones inside FModel (compiler warning
// CS0436, silenced in the project file) and behave like a Windows registry where nothing is installed:
// detection simply finds no games and FModel asks for a folder, as it does on a fresh Windows machine.

public static class Registry
{
    public static readonly RegistryKey CurrentUser = RegistryKey.Empty;
    public static readonly RegistryKey LocalMachine = RegistryKey.Empty;
    public static readonly RegistryKey ClassesRoot = RegistryKey.Empty;
    public static readonly RegistryKey Users = RegistryKey.Empty;

    public static object GetValue(string keyName, string valueName, object defaultValue) => defaultValue;
}

public sealed class RegistryKey : IDisposable
{
    internal static readonly RegistryKey Empty = new();

    private RegistryKey() { }

    public string Name => string.Empty;
    public int SubKeyCount => 0;
    public int ValueCount => 0;

    public static RegistryKey OpenBaseKey(RegistryHive hKey, RegistryView view) => Empty;

    public RegistryKey OpenSubKey(string name) => null;
    public RegistryKey OpenSubKey(string name, bool writable) => null;
    public object GetValue(string name) => null;
    public object GetValue(string name, object defaultValue) => defaultValue;
    public string[] GetSubKeyNames() => [];
    public string[] GetValueNames() => [];
    public void Dispose() { }
}

public enum RegistryHive
{
    ClassesRoot = int.MinValue,
    CurrentUser = -2147483647,
    LocalMachine = -2147483646,
    Users = -2147483645,
    PerformanceData = -2147483644,
    CurrentConfig = -2147483643
}

public enum RegistryView
{
    Default = 0,
    Registry64 = 256,
    Registry32 = 512
}
