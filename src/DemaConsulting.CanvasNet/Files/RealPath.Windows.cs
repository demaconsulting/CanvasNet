using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The Windows facts of an open handle.
/// </summary>
internal static partial class RealPath
{
    /// <summary>
    ///     Reports the facts of an open Windows handle: a regular file is a disk file that is not a directory, and its
    ///     final path comes from <c>GetFinalPathNameByHandleW</c>.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>The facts.</returns>
    [SupportedOSPlatform("windows")]
    private static HandleFacts InspectWindows(SafeFileHandle handle)
    {
        // A character device (NUL, a console) or a pipe is not a disk file; a directory handle is a disk file with the
        // directory attribute. Neither is read, so its final path is not needed
        if (!NativeMethods.IsDiskFile(handle) || File.GetAttributes(handle).HasFlag(FileAttributes.Directory))
        {
            return new HandleFacts(false, null);
        }

        return new HandleFacts(true, NativeMethods.TryGetFinalPathName(handle));
    }

    /// <summary>
    ///     Determines whether a full path is a network location on Windows: a UNC path (<c>\\server\share</c> or
    ///     <c>\\?\UNC\server\share</c>), or a path on a mapped network drive (also as <c>\\?\Z:\</c>). A mapped network
    ///     drive is a network location, so a host working on <c>Z:\</c> keeps working when its files are compared with
    ///     their real paths (which name the share). Any other device-namespace path (<c>\\.\</c>, a volume GUID,
    ///     <c>\\?\GLOBALROOT\</c>) and a local drive are not. Always <see langword="false"/> on other systems.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns><see langword="true"/> when the path is a network location.</returns>
    internal static bool IsNetworkLocation(string fullPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // An ordinary extended-length path on a drive is classified by its drive, and \\?\UNC\ is a share
        if (fullPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsOrdinaryExtendedPath(fullPath))
        {
            fullPath = fullPath[4..];
        }

        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return fullPath.Length > 2 && fullPath[2] != '.' && fullPath[2] != '?';
        }

        // A drive is asked for its type; a drive that cannot be asked is treated as local (the real path then decides)
        var root = Path.GetPathRoot(fullPath);
        if (root is not { Length: 3 } || root[1] != ':')
        {
            return false;
        }

        try
        {
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
