using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The Linux facts of an open handle (the macOS facts come from one native call, see
///     <see cref="NativeMethods.TryGetMacVnodeInfo"/>).
/// </summary>
internal static partial class RealPath
{
    /// <summary>The suffix Linux appends to the <c>/proc/self/fd</c> link of a file that was deleted.</summary>
    private const string LinuxDeletedSuffix = " (deleted)";

    /// <summary>
    ///     Reports the facts of an open Linux descriptor: the file type from <c>fstat</c>, and the final path from the
    ///     <c>/proc/self/fd</c> link of the descriptor.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>The facts; the path is <see langword="null"/> when <c>/proc</c> is not mounted or the file was deleted.</returns>
    [SupportedOSPlatform("linux")]
    private static HandleFacts InspectLinux(SafeFileHandle handle)
    {
        var isRegular = IsRegularMode(NativeMethods.TryGetLinuxMode(handle));

        // The link of the descriptor names the file the descriptor refers to now, whatever happened to the name used to
        // open it; a deleted file is reported with a suffix and has no path any more
        var target = NativeMethods.WithDescriptor(
            handle,
            descriptor => new FileInfo("/proc/self/fd/" + descriptor.ToString(CultureInfo.InvariantCulture)).LinkTarget);
        var path = target is not null && target.StartsWith('/') && !target.EndsWith(LinuxDeletedSuffix, StringComparison.Ordinal)
            ? target
            : null;
        return new HandleFacts(isRegular, path);
    }
}
