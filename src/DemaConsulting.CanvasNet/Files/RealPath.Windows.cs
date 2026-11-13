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
}
