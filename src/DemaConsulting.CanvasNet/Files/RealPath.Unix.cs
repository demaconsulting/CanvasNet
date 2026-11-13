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
    /// <summary>
    ///     Reports the facts of an open Linux descriptor: the file type and link count from <c>fstat</c>, and the final
    ///     path from the <c>/proc/self/fd</c> link of the descriptor.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>
    ///     The facts; the path is <see langword="null"/> when <c>/proc</c> is not mounted, when the file was deleted
    ///     (its link count is zero), or when the type and link count cannot be read.
    /// </returns>
    [SupportedOSPlatform("linux")]
    private static HandleFacts InspectLinux(SafeFileHandle handle)
    {
        // The link of the descriptor names the file the descriptor refers to now, whatever happened to the name used to
        // open it. It is read before fstat, so a file deleted between the two calls is seen as deleted
        var target = NativeMethods.WithDescriptor(
            handle,
            descriptor => new FileInfo("/proc/self/fd/" + descriptor.ToString(CultureInfo.InvariantCulture)).LinkTarget);
        var stat = NativeMethods.TryGetLinuxStat(handle);
        var isRegular = IsRegularMode(stat?.Mode);

        // Deletion is decided from the open file (no name links to it any more), never from the " (deleted)" text Linux
        // appends to the link, which a live file may carry in its own name; the link text is kept verbatim
        var path = stat is { LinkCount: > 0 } && target is not null && target.StartsWith('/') ? target : null;
        return new HandleFacts(isRegular, path);
    }
}
