using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FModel.Linux;

/// <summary>
/// Routes P/Invokes of Windows system libraries to libwin32compat.so (linux/native/win32compat.c).
///
/// Upstream FModel and Snooper call a few Win32 functions directly (console attach, monitor refresh rate,
/// memory status, OS branding). Rather than patching those call sites, every assembly that gets loaded is
/// given a resolver that answers for these library names; everything else resolves the usual way.
/// </summary>
public static class NativeCompat
{
    private static readonly string[] _windowsLibraries = ["kernel32", "user32", "winbrand"];
    private static IntPtr _handle;

    public static void Install()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) => Register(args.LoadedAssembly);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            Register(assembly);
    }

    private static void Register(Assembly assembly)
    {
        if (assembly.IsDynamic) return;

        // System and third party assemblies never import these under Linux, and some set their own resolver
        var name = assembly.GetName().Name ?? string.Empty;
        if (name.StartsWith("System.", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
            name.StartsWith("Avalonia", StringComparison.Ordinal) || name.StartsWith("OpenTK", StringComparison.Ordinal))
            return;

        try
        {
            NativeLibrary.SetDllImportResolver(assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // the assembly already has a resolver of its own
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var name = Path.GetFileNameWithoutExtension(libraryName).ToLowerInvariant();
        if (Array.IndexOf(_windowsLibraries, name) < 0)
            return IntPtr.Zero;

        if (_handle == IntPtr.Zero)
            NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, "libwin32compat.so"), out _handle);
        return _handle;
    }
}
