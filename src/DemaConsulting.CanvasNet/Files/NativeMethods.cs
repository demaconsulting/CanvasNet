using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The operating-system calls the <see cref="RealPath"/> unit needs and .NET does not offer: the final path and the
///     type of an <b>open handle</b>, a non-blocking open on Unix, the real location of a macOS path by name, and the long
///     spelling of a short (8.3) Windows path.
/// </summary>
/// <remarks>
///     <para>
///         Every import is private and wrapped by an internal method that adds the logic the raw call needs (buffer
///         growth, prefix stripping, error translation, structure offsets), so no caller handles a raw native contract.
///         Each wrapper is marked with the platforms it runs on and is only called from <see cref="RealPath"/> after an
///         <see cref="OperatingSystem"/> check. No unsafe code is used: buffers are managed arrays.
///     </para>
///     <para>
///         <b>Windows</b> (<c>kernel32.dll</c>, loaded from System32 only): <c>GetFinalPathNameByHandleW</c>,
///         <c>GetFileType</c>, <c>CreateFileW</c> and <c>GetLongPathNameW</c>. <b>Linux</b> (<c>libc</c>):
///         <c>open</c> and <c>fstat</c> (with <c>__fxstat</c> for C libraries that predate an exported <c>fstat</c>).
///         <b>macOS</b> (<c>libc</c>, which re-exports <c>libSystem</c>): <c>open</c>, <c>proc_pidfdinfo</c> (with
///         <c>fstat</c> as a fallback for the file type) and <c>realpath</c>.
///     </para>
/// </remarks>
internal static class NativeMethods
{
    /// <summary>The <c>GetFileType</c> result of a file on a disk (as opposed to a character device or a pipe).</summary>
    private const uint WindowsFileTypeDisk = 1;

    /// <summary>The <c>GetFinalPathNameByHandleW</c> flags <c>FILE_NAME_NORMALIZED | VOLUME_NAME_DOS</c> (both zero).</summary>
    private const uint WindowsFinalPathNormalizedDos = 0;

    /// <summary>The <c>CreateFileW</c> share mode <c>FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE</c>.</summary>
    private const uint WindowsShareAll = 7;

    /// <summary>The <c>CreateFileW</c> disposition <c>OPEN_EXISTING</c>.</summary>
    private const uint WindowsOpenExisting = 3;

    /// <summary>The <c>CreateFileW</c> flag <c>FILE_FLAG_BACKUP_SEMANTICS</c>, needed to open a directory.</summary>
    private const uint WindowsBackupSemantics = 0x02000000;

    /// <summary>The first size of a path buffer, in characters; it grows when Windows asks for more.</summary>
    private const int WindowsInitialPathBuffer = 512;

    /// <summary>The largest path Windows can report, in characters (including the terminating NUL).</summary>
    private const int WindowsMaxPathBuffer = 32768;

    /// <summary>The prefix Windows puts in front of a final path (<c>\\?\</c>).</summary>
    private const string WindowsExtendedPrefix = @"\\?\";

    /// <summary>The prefix Windows puts in front of the final path of a network file (<c>\\?\UNC\</c>).</summary>
    private const string WindowsExtendedUncPrefix = @"\\?\UNC\";

    /// <summary>The <c>errno</c> value <c>EPERM</c> (Linux and macOS).</summary>
    private const int ErrorNotPermitted = 1;

    /// <summary>The <c>errno</c> value <c>ENOENT</c> (Linux and macOS).</summary>
    private const int ErrorNoEntry = 2;

    /// <summary>The <c>errno</c> value <c>EINTR</c> (Linux and macOS).</summary>
    private const int ErrorInterrupted = 4;

    /// <summary>The <c>errno</c> value <c>EACCES</c> (Linux and macOS).</summary>
    private const int ErrorAccess = 13;

    /// <summary>The <c>errno</c> value <c>ENOTDIR</c> (Linux and macOS).</summary>
    private const int ErrorNotDirectory = 20;

    /// <summary>The <c>errno</c> value <c>ENAMETOOLONG</c> on Linux.</summary>
    private const int LinuxErrorNameTooLong = 36;

    /// <summary>The <c>errno</c> value <c>ENAMETOOLONG</c> on macOS.</summary>
    private const int MacErrorNameTooLong = 63;

    /// <summary>The Linux <c>open</c> flags <c>O_RDONLY | O_NONBLOCK | O_NOCTTY | O_CLOEXEC</c> (x64 and Arm64).</summary>
    private const int LinuxOpenReadNonBlocking = 0x800 | 0x100 | 0x80000;

    /// <summary>The Linux <c>open</c> flags <c>O_PATH | O_CLOEXEC</c>, which open a directory without read permission.</summary>
    private const int LinuxOpenPathOnly = 0x200000 | 0x80000;

    /// <summary>The macOS <c>open</c> flags <c>O_RDONLY | O_NONBLOCK | O_NOCTTY | O_CLOEXEC</c>.</summary>
    private const int MacOpenReadNonBlocking = 0x4 | 0x20000 | 0x01000000;

    /// <summary>The size of the buffer handed to <c>fstat</c>; larger than <c>struct stat</c> on every Linux architecture.</summary>
    private const int LinuxStatBufferSize = 256;

    /// <summary>The macOS <c>proc_pidfdinfo</c> flavor <c>PROC_PIDFDVNODEPATHINFO</c>.</summary>
    private const int MacVnodePathInfo = 2;

    /// <summary>
    ///     The size of <c>struct vnode_fdinfowithpath</c> (<c>PROC_PIDFDVNODEPATHINFO_SIZE</c>): <c>proc_fileinfo</c>
    ///     (24 bytes) and <c>vnode_info_path</c> (152 bytes of <c>vnode_info</c> and a 1024-byte path). The layout has
    ///     no pointer- or <c>long</c>-sized field, so it is the same on x64 and Arm64.
    /// </summary>
    private const int MacVnodePathInfoSize = 1200;

    /// <summary>The offset of <c>vst_mode</c> (16 bits) in <c>vnode_fdinfowithpath</c>: 24 + 4.</summary>
    private const int MacModeOffset = 28;

    /// <summary>The offset of <c>vip_path</c> (a NUL-terminated UTF-8 path) in <c>vnode_fdinfowithpath</c>: 24 + 152.</summary>
    private const int MacPathOffset = 176;

    /// <summary>The offset of the 16-bit <c>st_mode</c> in the macOS Arm64 <c>struct stat</c>, after the 32-bit <c>st_dev</c>.</summary>
    private const int MacStatModeOffset = 4;

    /// <summary>The macOS <c>PATH_MAX</c>: the size of the buffer <c>realpath</c> fills, including the terminating NUL.</summary>
    private const int MacPathMax = 1024;

    /// <summary>Whether <c>fstat</c> is missing from the C library (so <c>__fxstat</c> is used); null until first needed.</summary>
    private static bool? _linuxUsesFxStat;

    /// <summary>
    ///     Gets the final path of an open file or directory, with links, junctions, substituted and mapped drives, and
    ///     short names resolved.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>The path (without the <c>\\?\</c> prefix; a network path starts with <c>\\</c>), or <see langword="null"/> when Windows cannot tell.</returns>
    [SupportedOSPlatform("windows")]
    internal static string? TryGetFinalPathName(SafeFileHandle handle)
    {
        // The call returns the length without the NUL when the buffer is large enough, and the size it needs otherwise
        var buffer = new char[WindowsInitialPathBuffer];
        while (true)
        {
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, WindowsFinalPathNormalizedDos);
            if (length == 0 || length > WindowsMaxPathBuffer)
            {
                return null;
            }

            if (length < buffer.Length)
            {
                return StripExtendedPrefix(new string(buffer, 0, (int)length));
            }

            buffer = new char[length + 1];
        }
    }

    /// <summary>
    ///     Determines whether an open handle is a file on a disk, rather than a character device (such as <c>NUL</c> or a
    ///     console) or a pipe.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns><see langword="true"/> for a disk file.</returns>
    [SupportedOSPlatform("windows")]
    internal static bool IsDiskFile(SafeFileHandle handle)
    {
        // A closed or invalid handle has no type; GetFileType would report "unknown" for it
        if (handle.IsInvalid || handle.IsClosed)
        {
            return false;
        }

        return GetFileType(handle) == WindowsFileTypeDisk;
    }

    /// <summary>
    ///     Gets the final path of a directory (or file) by name, by opening it without access rights and asking its handle.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns>The final path, or <see langword="null"/> when it does not exist or cannot be opened.</returns>
    [SupportedOSPlatform("windows")]
    internal static string? TryGetFinalPathOfPath(string path)
    {
        using var handle = CreateFile(path, 0, WindowsShareAll, 0, WindowsOpenExisting, WindowsBackupSemantics, 0);
        return handle.IsInvalid ? null : TryGetFinalPathName(handle);
    }

    /// <summary>
    ///     Gets the long spelling of a path whose components may be short (8.3) names.
    /// </summary>
    /// <param name="path">The full path of an existing file or directory.</param>
    /// <returns>The long spelling, or <see langword="null"/> when it cannot be determined (for example the path does not exist).</returns>
    [SupportedOSPlatform("windows")]
    internal static string? TryGetLongPathName(string path)
    {
        // Same protocol as the final path: grow the buffer to the size the call asks for
        var buffer = new char[WindowsInitialPathBuffer];
        while (true)
        {
            var length = GetLongPathName(path, buffer, (uint)buffer.Length);
            if (length == 0 || length > WindowsMaxPathBuffer)
            {
                return null;
            }

            if (length < buffer.Length)
            {
                return new string(buffer, 0, (int)length);
            }

            buffer = new char[length + 1];
        }
    }

    /// <summary>
    ///     Opens a path for reading without blocking, so a named pipe (FIFO) or a device cannot hang the open, and so
    ///     the descriptor is not inherited by child processes.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <param name="pathOnly">
    ///     <see langword="true"/> to open only a reference to the path (Linux <c>O_PATH</c>, which needs no read
    ///     permission; used for directories), <see langword="false"/> to open it for reading. Ignored on macOS, which
    ///     has no <c>O_PATH</c>; macOS directories are canonicalized with <c>realpath</c> instead.
    /// </param>
    /// <returns>The handle, which owns the descriptor.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a directory of the path does not exist or is not a directory.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    /// <exception cref="PathTooLongException">Thrown when the path is too long.</exception>
    /// <exception cref="IOException">Thrown for any other failure.</exception>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static SafeFileHandle OpenNonBlocking(string path, bool pathOnly)
    {
        // The variadic mode argument of open is only read with O_CREAT, which is never passed, so the two fixed
        // arguments are all the call needs on every ABI (Arm64 macOS included)
        int flags;
        if (OperatingSystem.IsMacOS())
        {
            flags = MacOpenReadNonBlocking;
        }
        else
        {
            flags = pathOnly ? LinuxOpenPathOnly : LinuxOpenReadNonBlocking;
        }

        var bytes = Encoding.UTF8.GetBytes(path + "\0");
        int descriptor;
        int error;
        do
        {
            descriptor = Open(bytes, flags);
            error = descriptor < 0 ? Marshal.GetLastPInvokeError() : 0;
        }
        while (descriptor < 0 && error == ErrorInterrupted);

        if (descriptor >= 0)
        {
            return new SafeFileHandle(descriptor, ownsHandle: true);
        }

        // The errno values are translated into the exceptions .NET itself raises for them, so the caller can tell a
        // missing file from an unreadable one without knowing errno
        var message = $"The file cannot be opened (errno {error}).";
        throw error switch
        {
            ErrorNoEntry => new FileNotFoundException(message),
            ErrorNotDirectory => new DirectoryNotFoundException(message),
            ErrorAccess or ErrorNotPermitted => new UnauthorizedAccessException(message),
            LinuxErrorNameTooLong when OperatingSystem.IsLinux() => new PathTooLongException(message),
            MacErrorNameTooLong when OperatingSystem.IsMacOS() => new PathTooLongException(message),
            _ => new IOException(message, error),
        };
    }

    /// <summary>
    ///     Gets the mode (type and permission bits) and the hard-link count of an open descriptor on Linux.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>
    ///     The mode and the link count (zero for a file that was deleted while open), or <see langword="null"/> when they
    ///     cannot be read (for example on an architecture whose <c>struct stat</c> layout is not known here).
    /// </returns>
    [SupportedOSPlatform("linux")]
    internal static (int Mode, long LinkCount)? TryGetLinuxStat(SafeFileHandle handle)
    {
        // The two layouts of struct stat known here (glibc and musl agree on both):
        // - x64 (the kernel's x86_64 stat): st_dev u64 @0, st_ino u64 @8, st_nlink u64 @16, st_mode u32 @24
        // - Arm64 (the generic stat): st_dev u64 @0, st_ino u64 @8, st_mode u32 @16, st_nlink u32 @20
        // Other architectures are not known here
        var architecture = RuntimeInformation.ProcessArchitecture;
        int modeOffset;
        int linkOffset;
        bool wideLinkCount;
        int statVersion;
        if (architecture == Architecture.X64)
        {
            modeOffset = 24;
            linkOffset = 16;
            wideLinkCount = true;
            statVersion = 1;
        }
        else if (architecture == Architecture.Arm64)
        {
            modeOffset = 16;
            linkOffset = 20;
            wideLinkCount = false;
            statVersion = 0;
        }
        else
        {
            return null;
        }

        var buffer = new byte[LinuxStatBufferSize];
        var result = WithDescriptor(handle, descriptor => CallFStat(descriptor, buffer, statVersion));
        if (result != 0)
        {
            return null;
        }

        var linkCount = wideLinkCount ? BitConverter.ToInt64(buffer, linkOffset) : BitConverter.ToUInt32(buffer, linkOffset);
        return (BitConverter.ToInt32(buffer, modeOffset), linkCount);
    }

    /// <summary>
    ///     Gets the mode of an open descriptor on macOS through <c>fstat</c>, the fallback when <c>proc_pidfdinfo</c>
    ///     cannot describe the descriptor.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>The mode, or <see langword="null"/> when it cannot be read.</returns>
    [SupportedOSPlatform("macos")]
    private static int? TryGetMacStatMode(SafeFileHandle handle)
    {
        // On Arm64 fstat fills the struct stat with 64-bit inode numbers, where the 16-bit st_mode follows the 32-bit
        // st_dev at offset 4; the x64 fstat symbol fills the older layout, which is not read here
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            return null;
        }

        var buffer = new byte[LinuxStatBufferSize];
        var result = WithDescriptor(handle, descriptor => FStat(descriptor, buffer));
        return result == 0 ? BitConverter.ToUInt16(buffer, MacStatModeOffset) : null;
    }

    /// <summary>
    ///     Gets the mode and the path of an open descriptor on macOS, from one <c>proc_pidfdinfo</c> call.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>The mode and the path; each is <see langword="null"/> when it cannot be read.</returns>
    [SupportedOSPlatform("macos")]
    internal static (int? Mode, string? Path) TryGetMacVnodeInfo(SafeFileHandle handle)
    {
        // The kernel refuses a buffer smaller than the structure, and answers with the number of bytes it filled in
        var buffer = new byte[MacVnodePathInfoSize];
        var filled = WithDescriptor(
            handle,
            descriptor => ProcPidFdInfo(Environment.ProcessId, descriptor, MacVnodePathInfo, buffer, buffer.Length));
        if (filled != MacVnodePathInfoSize)
        {
            // Without the vnode information there is no path, but the type can still come from fstat, so a device or a
            // named pipe is never mistaken for a file of unknown type
            return (TryGetMacStatMode(handle), null);
        }

        var mode = BitConverter.ToUInt16(buffer, MacModeOffset);
        var end = Array.IndexOf(buffer, (byte)0, MacPathOffset);
        var length = (end < 0 ? buffer.Length : end) - MacPathOffset;
        var path = length > 0 ? Encoding.UTF8.GetString(buffer, MacPathOffset, length) : null;
        return (mode, path);
    }

    /// <summary>
    ///     Gets the real location of a path on macOS through <c>realpath(3)</c>, which resolves links by name and needs
    ///     only search permission on the components of the path, never read permission.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns>The resolved path, or <see langword="null"/> when it does not exist or cannot be resolved.</returns>
    [SupportedOSPlatform("macos")]
    internal static string? TryRealPath(string path)
    {
        // The buffer is owned by the caller (PATH_MAX bytes), so nothing is allocated by the C library or freed here
        var resolved = new byte[MacPathMax];
        if (RealPathNative(Encoding.UTF8.GetBytes(path + "\0"), resolved) == IntPtr.Zero)
        {
            return null;
        }

        var length = Array.IndexOf(resolved, (byte)0);
        return length > 0 ? Encoding.UTF8.GetString(resolved, 0, length) : null;
    }

    /// <summary>
    ///     Runs a call with the descriptor number of a handle, keeping the handle from being closed (and its number
    ///     reused by another open) for the duration of the call.
    /// </summary>
    /// <remarks>
    ///     This is the one place that reads the raw descriptor of a handle: the C functions take an <c>int</c>
    ///     descriptor, and the <c>/proc/self/fd</c> link is named by it. The reference count taken around the call is
    ///     what makes reading the raw value safe.
    /// </remarks>
    /// <typeparam name="T">The result type of the call.</typeparam>
    /// <param name="handle">The open handle.</param>
    /// <param name="call">The call that uses the descriptor.</param>
    /// <returns>What the call returned.</returns>
    internal static T WithDescriptor<T>(SafeFileHandle handle, Func<int, T> call)
    {
        var added = false;
        try
        {
            handle.DangerousAddRef(ref added);
#pragma warning disable S3869 // The raw descriptor is needed by the C call; the reference count above keeps it valid
            return call((int)handle.DangerousGetHandle());
#pragma warning restore S3869
        }
        finally
        {
            if (added)
            {
                handle.DangerousRelease();
            }
        }
    }

    /// <summary>
    ///     Calls <c>fstat</c>, or <c>__fxstat</c> where the C library does not export <c>fstat</c> (glibc before 2.33).
    /// </summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="buffer">The buffer for <c>struct stat</c>.</param>
    /// <param name="statVersion">The <c>_STAT_VER</c> value of the architecture, for <c>__fxstat</c>.</param>
    /// <returns>Zero on success.</returns>
    [SupportedOSPlatform("linux")]
    private static int CallFStat(int descriptor, byte[] buffer, int statVersion)
    {
        if (_linuxUsesFxStat != true)
        {
            try
            {
                var result = FStat(descriptor, buffer);
                _linuxUsesFxStat = false;
                return result;
            }
            catch (EntryPointNotFoundException)
            {
                _linuxUsesFxStat = true;
            }
        }

        return FXStat(statVersion, descriptor, buffer);
    }

    /// <summary>
    ///     Removes the <c>\\?\</c> prefix of a final path, mapping <c>\\?\UNC\server\share</c> to <c>\\server\share</c>
    ///     and <c>\\?\C:\x</c> to <c>C:\x</c>. Any other spelling (a volume GUID or a device-namespace root, which the
    ///     flags asked for should never yield) is no usable path, so it is reported as unknown (fail closed).
    /// </summary>
    /// <param name="path">The path as Windows reported it.</param>
    /// <returns>The path in its usual spelling, or <see langword="null"/>.</returns>
    private static string? StripExtendedPrefix(string path)
    {
        if (path.StartsWith(WindowsExtendedUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[WindowsExtendedUncPrefix.Length..];
        }

        var stripped = path.StartsWith(WindowsExtendedPrefix, StringComparison.Ordinal) ? path[WindowsExtendedPrefix.Length..] : path;
        return stripped.Length >= 3 && char.IsAsciiLetter(stripped[0]) && stripped[1] == ':' && stripped[2] == '\\'
            ? stripped
            : null;
    }

    /// <summary>The Windows <c>GetFinalPathNameByHandleW</c> function.</summary>
    /// <param name="file">The open handle.</param>
    /// <param name="buffer">The buffer for the path.</param>
    /// <param name="bufferLength">The size of the buffer, in characters.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The length of the path, the size needed, or zero on failure.</returns>
    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, [Out] char[] buffer, uint bufferLength, uint flags);

    /// <summary>The Windows <c>GetFileType</c> function.</summary>
    /// <param name="file">The open handle.</param>
    /// <returns>The type of the file.</returns>
    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", EntryPoint = "GetFileType", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetFileType(SafeFileHandle file);

    /// <summary>The Windows <c>CreateFileW</c> function.</summary>
    /// <param name="fileName">The path.</param>
    /// <param name="desiredAccess">The access rights.</param>
    /// <param name="shareMode">The share mode.</param>
    /// <param name="securityAttributes">The security attributes (zero).</param>
    /// <param name="creationDisposition">The disposition.</param>
    /// <param name="flagsAndAttributes">The flags.</param>
    /// <param name="templateFile">The template file (zero).</param>
    /// <returns>The handle; invalid on failure.</returns>
    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    /// <summary>The Windows <c>GetLongPathNameW</c> function.</summary>
    /// <param name="shortPath">The path.</param>
    /// <param name="buffer">The buffer for the long path.</param>
    /// <param name="bufferLength">The size of the buffer, in characters.</param>
    /// <returns>The length of the long path, the size needed, or zero on failure.</returns>
    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", EntryPoint = "GetLongPathNameW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetLongPathName(string shortPath, [Out] char[] buffer, uint bufferLength);

    /// <summary>The C library <c>open</c> function, without the optional mode argument.</summary>
    /// <param name="path">The path as NUL-terminated UTF-8.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The descriptor, or -1 with <c>errno</c> set.</returns>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [DllImport("libc", EntryPoint = "open", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Open(byte[] path, int flags);

    /// <summary>The C library <c>fstat</c> function (Linux, and macOS as a fallback).</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="statBuffer">The buffer for <c>struct stat</c>.</param>
    /// <returns>Zero on success.</returns>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [DllImport("libc", EntryPoint = "fstat", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int FStat(int descriptor, [Out] byte[] statBuffer);

    /// <summary>The glibc <c>__fxstat</c> function, for glibc before 2.33.</summary>
    /// <param name="version">The <c>_STAT_VER</c> value.</param>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="statBuffer">The buffer for <c>struct stat</c>.</param>
    /// <returns>Zero on success.</returns>
    [SupportedOSPlatform("linux")]
    [DllImport("libc", EntryPoint = "__fxstat", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int FXStat(int version, int descriptor, [Out] byte[] statBuffer);

    /// <summary>The macOS <c>proc_pidfdinfo</c> function.</summary>
    /// <param name="pid">The process.</param>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="flavor">The kind of information.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="bufferSize">The size of the buffer.</param>
    /// <returns>The number of bytes filled in, or zero on failure.</returns>
    [SupportedOSPlatform("macos")]
    [DllImport("libc", EntryPoint = "proc_pidfdinfo", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int ProcPidFdInfo(int pid, int descriptor, int flavor, [Out] byte[] buffer, int bufferSize);

    /// <summary>The C library <c>realpath</c> function, with a caller-owned buffer of <c>PATH_MAX</c> bytes.</summary>
    /// <param name="path">The path as NUL-terminated UTF-8.</param>
    /// <param name="resolvedPath">The buffer that receives the NUL-terminated resolved path.</param>
    /// <returns>The buffer's address on success, or zero (<c>NULL</c>) with <c>errno</c> set.</returns>
    [SupportedOSPlatform("macos")]
    [DllImport("libc", EntryPoint = "realpath", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern IntPtr RealPathNative(byte[] path, [Out] byte[] resolvedPath);
}
