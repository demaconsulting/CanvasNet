using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The Linux facts of an open handle (the macOS facts come from one native call, see
///     <see cref="NativeMethods.TryGetMacVnodeInfo"/>), and the Linux and macOS identity of a file.
/// </summary>
internal static partial class RealPath
{
    /// <summary>
    ///     Determines on Linux whether a path names the same file as an open handle, through no link: the device and the
    ///     inode of both must be equal, and the <c>/proc/self/fd</c> link of the descriptor opened by the path must be
    ///     the path itself (a link in one of its folders would make it another path). The path is opened with
    ///     <c>O_PATH | O_NOFOLLOW</c>, so a link at its last component is compared itself (its own inode), not its
    ///     target, and no read permission is needed.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <param name="path">The full path.</param>
    /// <param name="reason">Why the answer is not <see langword="true"/>; <see langword="null"/> when it is.</param>
    /// <returns>The answer, or <see langword="null"/> when an identity cannot be read.</returns>
    /// <exception cref="IOException">Thrown when the path cannot be opened (the caller treats it as unknown).</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the path cannot be searched (treated as unknown).</exception>
    [SupportedOSPlatform("linux")]
    private static bool? IsSameFileLinux(SafeFileHandle handle, string path, out string? reason)
    {
        using var other = NativeMethods.OpenNonBlocking(path, pathOnly: true, noFollow: true);
        var opened = NativeMethods.TryGetLinuxStat(handle);
        var named = NativeMethods.TryGetLinuxStat(other);
        if (opened is null || named is null)
        {
            reason = opened is null
                ? "fstat cannot describe the open file"
                : "fstat cannot describe the file the path names";
            return null;
        }

        if (opened.Value.Device != named.Value.Device || opened.Value.Inode != named.Value.Inode)
        {
            reason = "the path names another file (device and inode differ)";
            return false;
        }

        var reached = NativeMethods.WithDescriptor(
            other,
            descriptor => new FileInfo("/proc/self/fd/" + descriptor.ToString(CultureInfo.InvariantCulture)).LinkTarget);
        var throughNoLink = string.Equals(reached, path, StringComparison.Ordinal);
        reason = throughNoLink ? null : "the path reaches the file through a link (its /proc/self/fd link differs)";
        return throughNoLink;
    }

    /// <summary>
    ///     Determines on macOS whether a path names the same file as an open handle, through no link: the device and the
    ///     inode reported by <c>proc_pidfdinfo</c> for both must be equal. The path is opened with <c>O_NOFOLLOW_ANY</c>
    ///     alone (the kernel refuses it together with <c>O_NOFOLLOW</c>, <c>EINVAL</c>), so a link at any of its
    ///     components, the last one included, is refused (<c>ELOOP</c>) instead of being followed.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <param name="path">The full path.</param>
    /// <param name="reason">Why the answer is not <see langword="true"/>; <see langword="null"/> when it is.</param>
    /// <returns>The answer, or <see langword="null"/> when an identity cannot be read.</returns>
    /// <exception cref="IOException">Thrown when the path cannot be opened, a link included (the caller treats it as unknown).</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the path cannot be read (treated as unknown).</exception>
    [SupportedOSPlatform("macos")]
    private static bool? IsSameFileMac(SafeFileHandle handle, string path, out string? reason)
    {
        using var other = NativeMethods.OpenNonBlocking(path, pathOnly: false, noFollow: true);
        var opened = NativeMethods.TryGetMacVnodeInfo(handle).Identity;
        var named = NativeMethods.TryGetMacVnodeInfo(other).Identity;
        if (opened is null || named is null)
        {
            reason = opened is null
                ? "proc_pidfdinfo cannot describe the open file"
                : "proc_pidfdinfo cannot describe the file the path names";
            return null;
        }

        reason = opened == named ? null : "the path names another file (device and inode differ)";
        return opened == named;
    }

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
