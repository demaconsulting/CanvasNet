using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The identity of a file on Windows: the serial number of its volume and its identifier on that volume, which
///     together name one file whatever path reached it.
/// </summary>
/// <remarks>
///     Two identities compare only when read the same way (<paramref name="Extended"/> equal): a 128-bit identifier and
///     a 64-bit file index of one file differ in their volume serial number (64 against 32 bits).
///     <see cref="NativeMethods.TryGetWindowsFileIds"/> reads both handles of a comparison the same way.
/// </remarks>
/// <param name="Volume">The volume serial number: 64 bits from <c>FILE_ID_INFO</c>, 32 bits from <c>BY_HANDLE_FILE_INFORMATION</c>.</param>
/// <param name="Id">The file identifier: 128 bits from <c>FILE_ID_INFO</c>, the 64-bit file index from <c>BY_HANDLE_FILE_INFORMATION</c>.</param>
/// <param name="Extended"><see langword="true"/> when read from <c>FILE_ID_INFO</c>.</param>
internal readonly record struct WindowsFileId(ulong Volume, UInt128 Id, bool Extended)
{
    /// <summary>The size of <c>FILE_ID_INFO</c>, in bytes: a 64-bit volume serial number and a 16-byte <c>FILE_ID_128</c>.</summary>
    internal const int FileIdInfoSize = 24;

    /// <summary>The size of <c>BY_HANDLE_FILE_INFORMATION</c>, in bytes (seven 32-bit fields and three 64-bit file times).</summary>
    internal const int ByHandleInformationSize = 52;

    /// <summary>The offset of <c>FileId</c> in <c>FILE_ID_INFO</c>, after the 64-bit <c>VolumeSerialNumber</c>.</summary>
    private const int FileIdOffset = 8;

    /// <summary>The offset of <c>dwVolumeSerialNumber</c> in <c>BY_HANDLE_FILE_INFORMATION</c>: 4 + three file times of 8.</summary>
    private const int ByHandleVolumeSerialOffset = 28;

    /// <summary>The offset of <c>nFileIndexHigh</c> in <c>BY_HANDLE_FILE_INFORMATION</c>.</summary>
    private const int ByHandleFileIndexHighOffset = 44;

    /// <summary>The offset of <c>nFileIndexLow</c> in <c>BY_HANDLE_FILE_INFORMATION</c>.</summary>
    private const int ByHandleFileIndexLowOffset = 48;

    /// <summary>
    ///     Decodes a <c>FILE_ID_INFO</c> structure (little-endian, as Windows writes it). A pure decoder, the same on
    ///     every platform.
    /// </summary>
    /// <param name="information">The structure; at least <see cref="FileIdInfoSize"/> bytes.</param>
    /// <returns>The identity, with the 128-bit identifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the structure is too short.</exception>
    internal static WindowsFileId FromFileIdInfo(ReadOnlySpan<byte> information)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(information.Length, FileIdInfoSize);
        return new WindowsFileId(
            BinaryPrimitives.ReadUInt64LittleEndian(information),
            BinaryPrimitives.ReadUInt128LittleEndian(information[FileIdOffset..FileIdInfoSize]),
            true);
    }

    /// <summary>
    ///     Decodes a <c>BY_HANDLE_FILE_INFORMATION</c> structure (little-endian, as Windows writes it). A pure decoder,
    ///     the same on every platform.
    /// </summary>
    /// <param name="information">The structure; at least <see cref="ByHandleInformationSize"/> bytes.</param>
    /// <returns>The identity, with the 64-bit file index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the structure is too short.</exception>
    internal static WindowsFileId FromByHandleInformation(ReadOnlySpan<byte> information)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(information.Length, ByHandleInformationSize);
        var index = ((ulong)BinaryPrimitives.ReadUInt32LittleEndian(information[ByHandleFileIndexHighOffset..]) << 32)
            | BinaryPrimitives.ReadUInt32LittleEndian(information[ByHandleFileIndexLowOffset..]);
        return new WindowsFileId(BinaryPrimitives.ReadUInt32LittleEndian(information[ByHandleVolumeSerialOffset..]), index, false);
    }
}

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
    ///     Determines on Windows whether a path names the same file as an open handle, through no link: the volume serial
    ///     number and the file identifier of both must be equal (the 128-bit <c>FileIdInfo</c> identifier when both
    ///     volumes report it, else the 64-bit file index for both), and the final path of the handle opened by the path
    ///     must be the path itself. The path is opened with <c>FILE_FLAG_OPEN_REPARSE_POINT</c>, so a link at its last
    ///     component is compared itself, not its target; a junction or link in one of its folders changes the final path.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <param name="path">The full path.</param>
    /// <param name="reason">Why the answer is not <see langword="true"/>; <see langword="null"/> when it is.</param>
    /// <returns>The answer, or <see langword="null"/> when the path cannot be opened or an identity cannot be read.</returns>
    [SupportedOSPlatform("windows")]
    private static bool? IsSameFileWindows(SafeFileHandle handle, string path, out string? reason)
    {
        using var other = NativeMethods.OpenForIdentity(path);
        if (other.IsInvalid)
        {
            reason = $"the path cannot be opened (Win32 error {Marshal.GetLastPInvokeError()})";
            return null;
        }

        // The same file, reached through no link: the final path of the handle opened by the spelling is the spelling
        // itself (a junction or link in a folder of the path would make it another path)
        if (NativeMethods.TryGetWindowsFileIds(handle, other) is not { } identities)
        {
            reason = "the identity of the open file or of the file the path names cannot be read";
            return null;
        }

        if (identities.First != identities.Second)
        {
            reason = "the path names another file (volume serial number and file identifier differ)";
            return false;
        }

        var reached = string.Equals(NativeMethods.TryGetFinalPathName(other), path, StringComparison.Ordinal);
        reason = reached ? null : "the path reaches the file through a link or junction (its final path differs)";
        return reached;
    }

    /// <summary>
    ///     Determines on Windows whether two paths name the same directory entry: both are opened with
    ///     <c>FILE_FLAG_OPEN_REPARSE_POINT</c> (a link at the last component is opened itself, not followed) and their
    ///     volume serial numbers and file identifiers are compared.
    /// </summary>
    /// <param name="writtenPath">The full path as written.</param>
    /// <param name="storedPath">The full path of an entry as stored.</param>
    /// <returns>The answer, or <see langword="null"/> when a path cannot be opened or an identity cannot be read.</returns>
    [SupportedOSPlatform("windows")]
    private static bool? IsSameEntryWindows(string writtenPath, string storedPath)
    {
        using var written = NativeMethods.OpenForIdentity(writtenPath);
        using var stored = NativeMethods.OpenForIdentity(storedPath);
        if (written.IsInvalid || stored.IsInvalid)
        {
            return null;
        }

        return NativeMethods.TryGetWindowsFileIds(written, stored) is { } identities
            ? identities.First == identities.Second
            : null;
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
