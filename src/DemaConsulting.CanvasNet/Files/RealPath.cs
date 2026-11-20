using System.IO.Enumeration;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The facts of an open handle that a file-access decision needs.
/// </summary>
/// <param name="IsRegularFile">
///     <see langword="true"/> for a regular file, <see langword="false"/> for anything else (a directory, a device, a
///     named pipe), or <see langword="null"/> when the platform cannot tell.
/// </param>
/// <param name="FinalPath">The final path of the open file, or <see langword="null"/> when the platform cannot tell.</param>
internal readonly record struct HandleFacts(bool? IsRegularFile, string? FinalPath);

/// <summary>
///     Opens files and reports the facts of an open handle per operating system: whether it is a regular file, its
///     real (final) path, the letter case that path has on disk and whether that spelling names the open file, plus the
///     path comparison rules shared by the resolvers and the policies.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why it exists.</b> A decision about a file is only free of races when it is made about the file that will
///         be read: the real path must come from the <b>open handle</b>, not from the name, which another process can
///         change between a check and the open. .NET offers no API for this (its link APIs work by name), so this unit
///         asks the operating system: Windows <c>GetFinalPathNameByHandleW</c>, Linux <c>/proc/self/fd</c>, macOS
///         <c>proc_pidfdinfo</c>. On any other platform the facts are unknown (<see langword="null"/>), and the
///         resolvers fail closed: a file of unknown type is never read, under any policy.
///     </para>
///     <para>
///         <b>Exact comparison.</b> Real paths are compared exactly, letter case included, on every platform
///         (<see cref="IsUnder(string, string)"/>, <see cref="PathsEqual"/>), because a folder may be case-sensitive
///         even on Windows and macOS. A real path is compared in the letter case stored on disk
///         (<see cref="GetExactPathName"/>), confirmed to name the open file by its identity
///         (<see cref="TryConfirmExactPath(SafeFileHandle, string, string)"/>). Only paths as written are compared
///         under the platform rule (<see cref="PathComparison"/>).
///     </para>
///     <para>
///         <b>Non-blocking open.</b> On Linux and macOS a file is opened with <c>O_NONBLOCK</c>, so opening a named pipe
///         that has no writer returns at once (the regular-file check then refuses it) instead of hanging, and with
///         <c>O_CLOEXEC</c>, so the descriptor never leaks into a child process. On Windows a file is opened read-only
///         with read, write and delete sharing, so another process can keep writing, renaming or deleting it.
///     </para>
///     <para>
///         All members are thread-safe. The only state is a benign cache in <see cref="NativeMethods"/> of which Linux
///         <c>stat</c> entry point the C library exports; a race merely repeats the probe.
///     </para>
/// </remarks>
internal static partial class RealPath
{
    /// <summary>The <c>S_IFMT</c> mask of the file type bits of a Unix mode.</summary>
    private const int UnixTypeMask = 0xF000;

    /// <summary>The <c>S_IFREG</c> file type of a regular file.</summary>
    private const int UnixRegularFile = 0x8000;

    /// <summary>The directory separators accepted when a path is split into components.</summary>
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    ///     Gets the comparison under which two paths <b>as written</b> are taken to name the same place on this platform:
    ///     case-insensitive on Windows and macOS (their default file systems), ordinal elsewhere.
    /// </summary>
    /// <remarks>
    ///     Used only for written paths (the lexical pre-check of a policy, the lexical confinement of the sandbox, Windows
    ///     drive roots) and for cache keys; never for a decision about a real path, which is compared exactly (see
    ///     <see cref="IsUnder"/>), because a folder may be case-sensitive even on Windows and macOS.
    /// </remarks>
    internal static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    ///     Gets the comparer that matches <see cref="PathComparison"/>, for cache keys of written paths only.
    /// </summary>
    internal static StringComparer PathComparer { get; } =
        PathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    ///     Determines whether two real paths name the same location, comparing them exactly (letter case included); on
    ///     macOS both are compared in the composed Unicode form. A trailing directory separator does not matter.
    /// </summary>
    /// <remarks>
    ///     The comparison is exact because a folder may be case-sensitive on every platform (Windows per-directory case
    ///     sensitivity, case-sensitive APFS): two spellings that differ in letter case can name two different folders.
    ///     A real path in another letter case than the one stored on disk is therefore not equal; callers compare real
    ///     paths in their on-disk letter case (<see cref="GetExactPathName"/>).
    /// </remarks>
    /// <param name="left">A full path.</param>
    /// <param name="right">Another full path.</param>
    /// <returns><see langword="true"/> when they are the same.</returns>
    internal static bool PathsEqual(string left, string right) =>
        string.Equals(
            Normalize(Path.TrimEndingDirectorySeparator(left)),
            Normalize(Path.TrimEndingDirectorySeparator(right)),
            StringComparison.Ordinal);

    /// <summary>
    ///     Determines whether a real path is a folder or lies below it, comparing exactly (letter case included); the
    ///     boundary is a directory separator, so a sibling whose name merely starts with the folder's name is not below
    ///     it. This is the test every file-access decision about a real path uses.
    /// </summary>
    /// <remarks>
    ///     The comparison is exact on every platform, for the reason given in <see cref="PathsEqual"/>: a folder whose
    ///     name differs from <paramref name="root"/> only in letter case is outside it. On macOS both paths are compared
    ///     in the composed Unicode form. The decision never uses <see cref="Path.GetRelativePath(string, string)"/>, which
    ///     applies the platform's case rule.
    /// </remarks>
    /// <param name="path">The full path.</param>
    /// <param name="root">The full path of the folder.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is <paramref name="root"/> or below it.</returns>
    internal static bool IsUnder(string path, string root) => IsSameOrBelow(path, root, StringComparison.Ordinal);

    /// <summary>
    ///     Determines whether a path as written is a folder or lies below it under the platform comparison
    ///     (<see cref="PathComparison"/>), with the same separator boundary as <see cref="IsUnder(string, string)"/>.
    /// </summary>
    /// <remarks>
    ///     Only for the lexical pre-check of a written path, which refuses a path outside every allowed folder before
    ///     anything is opened; it never allows a file on its own, because the real path is always checked exactly
    ///     after the open.
    /// </remarks>
    /// <param name="path">The full path as written.</param>
    /// <param name="root">The full path of the folder.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is <paramref name="root"/> or below it.</returns>
    internal static bool IsUnderAsWritten(string path, string root) => IsSameOrBelow(path, root, PathComparison);

    /// <summary>
    ///     Determines whether any component of a path is a name Windows reserves for a device, so that opening the path
    ///     could reach the device instead of a file.
    /// </summary>
    /// <remarks>
    ///     A pure text check, so it gives the same answer on every platform; the resolvers apply it on Windows only. The
    ///     root of the path is not a component.
    /// </remarks>
    /// <param name="path">The path, with forward or backward slashes.</param>
    /// <returns><see langword="true"/> when a component is a reserved device name.</returns>
    internal static bool HasWindowsDeviceName(string path)
    {
        var root = Path.IsPathRooted(path) ? Path.GetPathRoot(path) ?? string.Empty : string.Empty;
        return path[root.Length..]
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Any(IsWindowsDeviceName);
    }

    /// <summary>
    ///     Determines whether one path component is a name Windows reserves for a device: <c>CON</c>, <c>PRN</c>,
    ///     <c>AUX</c>, <c>NUL</c>, <c>COM0</c>-<c>COM9</c>, <c>LPT0</c>-<c>LPT9</c>, <c>COM</c> or <c>LPT</c> with a
    ///     superscript 1, 2 or 3, <c>CONIN$</c> and <c>CONOUT$</c>, in any letter case. Windows ignores what follows the
    ///     name, so an extension (<c>NUL.txt</c>), trailing spaces or dots (<c>NUL .</c>) and a stream suffix
    ///     (<c>NUL:</c>) still name the device.
    /// </summary>
    /// <param name="component">The path component.</param>
    /// <returns><see langword="true"/> when it is a reserved device name.</returns>
    internal static bool IsWindowsDeviceName(string component)
    {
        // The name is what precedes a stream suffix and an extension, without trailing spaces
        var name = component;
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        name = colon >= 0 ? name[..colon] : name;
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        name = (dot >= 0 ? name[..dot] : name).TrimEnd(' ');
        return name.Length switch
        {
            3 => name.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || name.Equals("NUL", StringComparison.OrdinalIgnoreCase),
            4 => (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && (char.IsAsciiDigit(name[3]) || name[3] is '\u00B9' or '\u00B2' or '\u00B3'),
            _ => name.Equals("CONIN$", StringComparison.OrdinalIgnoreCase)
                || name.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    ///     Determines whether a full path is in a Windows device namespace: the Win32 device namespace (<c>\\.\</c>), an
    ///     extended-length path (<c>\\?\</c>), or the NT object namespace (<c>\??\</c>), with either slash.
    /// </summary>
    /// <remarks>A pure text check; the resolvers apply it on Windows only.</remarks>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsWindowsDeviceNamespace(string path) =>
        path.Length >= 4
        && path[0] is '\\' or '/'
        && path[1] is '\\' or '/' or '?'
        && path[2] is '.' or '?'
        && path[3] is '\\' or '/';

    /// <summary>
    ///     Determines whether a full path is an ordinary extended-length path: <c>\\?\</c> followed by a drive
    ///     (<c>\\?\C:\docs</c>) or a volume GUID (<c>\\?\Volume{GUID}\docs</c>). These name a file on a local volume with
    ///     no device names mapped; every other extended-length or device-namespace root (<c>\\?\GLOBALROOT\</c>,
    ///     <c>\\?\UNC\</c>, <c>\\?\pipe\</c>, <c>\\.\</c>, <c>\??\</c>) is not ordinary.
    /// </summary>
    /// <remarks>A pure text check, the one rule both resolvers use to exempt an extended-length directory.</remarks>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> when it is an ordinary extended-length path.</returns>
    internal static bool IsOrdinaryExtendedPath(string path)
    {
        if (!path.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return false;
        }

        // A drive: \\?\X: alone or followed by a backslash
        var rest = path.AsSpan(4);
        if (rest.Length >= 2 && char.IsAsciiLetter(rest[0]) && rest[1] == ':')
        {
            return rest.Length == 2 || rest[2] == '\\';
        }

        // A volume: \\?\Volume{GUID} alone or followed by a backslash
        const string VolumePrefix = "Volume{";
        const int GuidLength = 36;
        var close = VolumePrefix.Length + GuidLength;
        return rest.StartsWith(VolumePrefix, StringComparison.OrdinalIgnoreCase)
            && rest.Length > close
            && rest[close] == '}'
            && Guid.TryParseExact(rest.Slice(VolumePrefix.Length, GuidLength), "D", out _)
            && (rest.Length == close + 1 || rest[close + 1] == '\\');
    }

    /// <summary>
    ///     Gets the deepest folder that two full paths share, comparing whole components exactly (letter case included);
    ///     only the roots are compared under the platform rule (a Windows drive letter names the same drive in either
    ///     letter case).
    /// </summary>
    /// <remarks>
    ///     The components are compared exactly because the result is a trusted folder (the anchor of the no-symlinks
    ///     rule): on a case-sensitive folder <c>ALLOWED</c> and <c>allowed</c> are two folders, and only a folder that
    ///     really is an ancestor of both paths may be trusted. Two spellings that differ only in letter case therefore
    ///     share a shallower folder, which trusts less (fail closed).
    /// </remarks>
    /// <param name="first">A full path.</param>
    /// <param name="second">Another full path.</param>
    /// <returns>The shared folder, or <see langword="null"/> when the paths are on different roots (Windows drives).</returns>
    internal static string? CommonAncestor(string first, string second)
    {
        var root = Path.GetPathRoot(first);
        if (string.IsNullOrEmpty(root) || !string.Equals(root, Path.GetPathRoot(second), PathComparison))
        {
            return null;
        }

        var firstParts = first[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var secondParts = second[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var count = 0;
        while (count < firstParts.Length && count < secondParts.Length
            && string.Equals(firstParts[count], secondParts[count], StringComparison.Ordinal))
        {
            count++;
        }

        return count == 0 ? root : Path.Join(root, string.Join(Path.DirectorySeparatorChar, firstParts, 0, count));
    }

    /// <summary>
    ///     Opens a file for reading: read-only and shared on Windows, non-blocking and not inherited on Linux and macOS.
    /// </summary>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>The open handle; the caller disposes it.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a directory of the path does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied (on Windows also for a directory).</exception>
    /// <exception cref="IOException">Thrown for any other failure.</exception>
    internal static SafeFileHandle OpenForRead(string fullPath)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return NativeMethods.OpenNonBlocking(fullPath, pathOnly: false);
        }

        // Windows (and any other platform): the framework's open, read-only with the most permissive sharing
        return File.OpenHandle(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    /// <summary>
    ///     Reports whether an open handle is a regular file and what its final path is. Never throws.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <returns>The facts; each is <see langword="null"/> where the platform cannot tell.</returns>
    internal static HandleFacts Inspect(SafeFileHandle handle)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return InspectWindows(handle);
            }

            if (OperatingSystem.IsLinux())
            {
                return InspectLinux(handle);
            }

            if (OperatingSystem.IsMacOS())
            {
                var (mode, path, _) = NativeMethods.TryGetMacVnodeInfo(handle);
                return new HandleFacts(IsRegularMode(mode), path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException
            or DllNotFoundException or EntryPointNotFoundException or ObjectDisposedException)
        {
            // A platform call that is missing or fails leaves the facts unknown, and the caller decides (fail closed)
        }

        return default;
    }

    /// <summary>
    ///     Gets the real location of a folder (or file) by name: its final path once links, junctions and short names
    ///     are resolved. Used for folders the host chose (roots and anchors), which are trusted. A folder that may be
    ///     searched but not read (execute-only) has a real location too: Linux opens it with <c>O_PATH</c>, and macOS
    ///     asks <c>realpath(3)</c>, which resolves by name and needs only search permission.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>The final path, or <see langword="null"/> when it does not exist, cannot be opened, or the platform cannot tell.</returns>
    internal static string? TryGetCanonicalDirectory(string fullPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return NativeMethods.TryGetFinalPathOfPath(fullPath);
            }

            if (OperatingSystem.IsLinux())
            {
                using var handle = NativeMethods.OpenNonBlocking(fullPath, pathOnly: true);
                return Inspect(handle).FinalPath;
            }

            if (OperatingSystem.IsMacOS())
            {
                return NativeMethods.TryRealPath(fullPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException
            or DllNotFoundException or EntryPointNotFoundException or ArgumentException)
        {
            // A folder that cannot be opened has no known real location
        }

        return null;
    }

    /// <summary>
    ///     Gets the real location of a folder (as <see cref="TryGetCanonicalDirectory"/>) in the letter case its folders
    ///     have on disk (as <see cref="GetExactPathName"/>): the spelling a trusted folder (a root or an anchor) is
    ///     compared in, exactly, with the real path of a file.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>The real location, or <see langword="null"/> when it does not exist, cannot be opened, or the platform cannot tell.</returns>
    internal static string? TryGetExactCanonicalDirectory(string fullPath) =>
        TryGetCanonicalDirectory(fullPath) is { } canonical ? GetExactPathName(canonical) : null;

    /// <summary>
    ///     Gets a full path in the letter case its folders and its name have on disk, component by component. Never
    ///     throws.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Why it exists.</b> Real paths are compared exactly (see <see cref="IsUnder(string, string)"/>), so a
    ///         folder that differs from an allowed one only in letter case is outside it even where the file system
    ///         ignores the case. A real path must therefore be compared in the spelling stored on disk: on a
    ///         case-insensitive volume a folder written as <c>docs</c> is stored as <c>Docs</c>, and some file systems
    ///         report the spelling used to open a file rather than the stored one.
    ///     </para>
    ///     <para>
    ///         <b>Rules.</b> The path is made full; in its root only a drive letter is upper-cased (a <c>/</c>, a UNC
    ///         root and a <c>\\?\</c> volume root are kept as they are, see <see cref="ExactRoot"/>). Each component is
    ///         then looked up in its corrected parent folder, whose entries are listed and compared with the component
    ///         ignoring letter case (on macOS both in the composed Unicode form): an entry spelled exactly as written
    ///         wins; otherwise the one entry that matches gives the stored spelling; when no entry or several entries
    ///         match (a case-sensitive folder holding <c>Docs</c> and <c>docs</c>), the component is kept as written.
    ///         Once a folder does not exist, the rest of the path is kept as written; a folder that cannot be listed
    ///         keeps its component as written. Entries are compared as plain names, so a <c>*</c> or <c>?</c> in a
    ///         name never matches another entry. A <c>\\?\</c> path keeps its root and has the components below it
    ///         corrected; a real path from <see cref="Inspect"/> never carries the prefix.
    ///     </para>
    ///     <para>
    ///         <b>Not a decision.</b> The spelling is found by name, so it says nothing about the opened file on its
    ///         own: a caller that uses it for the real path of an open handle confirms it first
    ///         (<see cref="TryConfirmExactPath(SafeFileHandle, string, string)"/>). Reads the file system (one listing
    ///         per folder of the path); safe to call from any thread.
    ///     </para>
    /// </remarks>
    /// <param name="path">The path; a relative path is made full against the current directory.</param>
    /// <returns>The path in its on-disk letter case, or <paramref name="path"/> when it is not a valid path.</returns>
    internal static string GetExactPathName(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or System.Security.SecurityException)
        {
            return path;
        }

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
        {
            return full;
        }

        // Each component is corrected in the folder corrected so far; once a folder is missing, nothing below it can be
        // listed, so the rest is kept as written
        var parts = full[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var current = ExactRoot(root);
        var folderExists = true;
        foreach (var part in parts)
        {
            var name = part;
            if (folderExists)
            {
                name = StoredName(current, part, out folderExists);
            }

            current = Path.Join(current, name);
        }

        // A trailing separator of the input is kept, so the result names the same kind of location
        return parts.Length > 0 && Path.EndsInDirectorySeparator(full) ? current + Path.DirectorySeparatorChar : current;
    }

    /// <summary>
    ///     Gets the on-disk spelling of the root of a full path: a drive letter (<c>c:\</c>, <c>\\?\c:\</c>) is
    ///     upper-cased, as Windows reports it in a final path; every other root (<c>/</c>, a UNC root, a volume GUID
    ///     root) is kept as it is. A pure text rule, the same on every platform.
    /// </summary>
    /// <param name="root">The root, as <see cref="Path.GetPathRoot(string)"/> returns it.</param>
    /// <returns>The root in its on-disk spelling.</returns>
    internal static string ExactRoot(string root)
    {
        // A drive is X: at the start, or after the \\?\ prefix of an ordinary extended-length path
        var drive = root.StartsWith(@"\\?\", StringComparison.Ordinal) ? 4 : 0;
        if (root.Length >= drive + 2 && char.IsAsciiLetterLower(root[drive]) && root[drive + 1] == ':')
        {
            return string.Concat(root.AsSpan(0, drive), char.ToUpperInvariant(root[drive]).ToString(), root.AsSpan(drive + 1));
        }

        return root;
    }

    /// <summary>
    ///     Confirms that the on-disk spelling of the real path of an open file names that same file, so a decision about
    ///     it is a decision about the file that is read.
    /// </summary>
    /// <remarks>
    ///     When the spelling equals the reported real path exactly, it needs no confirmation. Otherwise the spelling must
    ///     name the open file <b>through no link</b> (<see cref="IsSameFile(SafeFileHandle, string)"/>): the same volume
    ///     serial number and file identifier on Windows, device and inode on Linux and macOS, with no link at its last
    ///     component and no junction or link in one of its folders. A spelling that names another file, a link, a file
    ///     reached through a linked folder (which would place the file where it is not), or a file whose identity cannot
    ///     be read is not confirmed (fail closed). Never throws.
    /// </remarks>
    /// <param name="handle">The open handle of the file.</param>
    /// <param name="realPath">The real path reported for the handle.</param>
    /// <param name="exactPath">The real path in its on-disk letter case (<see cref="GetExactPathName"/>).</param>
    /// <returns>
    ///     The confirmed spelling (<paramref name="realPath"/> when the two are equal, otherwise
    ///     <paramref name="exactPath"/>), or <see langword="null"/> when it is not confirmed.
    /// </returns>
    internal static string? TryConfirmExactPath(SafeFileHandle handle, string realPath, string exactPath) =>
        TryConfirmExactPath(handle, realPath, exactPath, out _);

    /// <summary>
    ///     Confirms the on-disk spelling of the real path of an open file, as
    ///     <see cref="TryConfirmExactPath(SafeFileHandle, string, string)"/>, and tells why a spelling is not confirmed.
    /// </summary>
    /// <param name="handle">The open handle of the file.</param>
    /// <param name="realPath">The real path reported for the handle.</param>
    /// <param name="exactPath">The real path in its on-disk letter case (<see cref="GetExactPathName"/>).</param>
    /// <param name="reason">
    ///     Why the spelling is not confirmed (the step that failed, with the <c>errno</c> where one is known), for
    ///     diagnostics; <see langword="null"/> when it is confirmed. It never names the path.
    /// </param>
    /// <returns>The confirmed spelling, or <see langword="null"/> when it is not confirmed.</returns>
    internal static string? TryConfirmExactPath(SafeFileHandle handle, string realPath, string exactPath, out string? reason)
    {
        if (string.Equals(realPath, exactPath, StringComparison.Ordinal))
        {
            reason = null;
            return realPath;
        }

        return IsSameFile(handle, exactPath, out reason) == true ? exactPath : null;
    }

    /// <summary>
    ///     Determines whether a path names the same file as an open handle through no link, by identity: the volume
    ///     serial number and the file identifier on Windows (128-bit where the volume reports it), the device and the
    ///     inode on Linux and macOS. The last component
    ///     of the path is not followed when it is a link, so a link to the file is a different file; and the path must
    ///     reach the file through no linked folder (Windows and Linux: the final path of the file opened by the path is
    ///     the path itself; macOS: the path is opened with <c>O_NOFOLLOW_ANY</c>). Never throws.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <param name="path">The full path.</param>
    /// <returns>
    ///     <see langword="true"/> when both are the same file reached through no link, <see langword="false"/> when they
    ///     are not, and
    ///     <see langword="null"/> when the path cannot be opened or an identity cannot be read (including on a platform
    ///     whose identity is not known here).
    /// </returns>
    internal static bool? IsSameFile(SafeFileHandle handle, string path) => IsSameFile(handle, path, out _);

    /// <summary>
    ///     Determines whether a path names the same file as an open handle through no link, as
    ///     <see cref="IsSameFile(SafeFileHandle, string)"/>, and tells why the answer is not <see langword="true"/>.
    /// </summary>
    /// <param name="handle">The open handle.</param>
    /// <param name="path">The full path.</param>
    /// <param name="reason">
    ///     Why the answer is not <see langword="true"/> (the step that failed, with the <c>errno</c> where one is
    ///     known), for diagnostics; <see langword="null"/> when it is. It never names the path.
    /// </param>
    /// <returns>The answer, as <see cref="IsSameFile(SafeFileHandle, string)"/>.</returns>
    internal static bool? IsSameFile(SafeFileHandle handle, string path, out string? reason)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return IsSameFileWindows(handle, path, out reason);
            }

            if (OperatingSystem.IsLinux())
            {
                return IsSameFileLinux(handle, path, out reason);
            }

            if (OperatingSystem.IsMacOS())
            {
                return IsSameFileMac(handle, path, out reason);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException
            or DllNotFoundException or EntryPointNotFoundException or ObjectDisposedException or ArgumentException
            or NotSupportedException)
        {
            // A path that cannot be opened, or an identity that cannot be read, leaves the answer unknown (fail closed).
            // The messages raised here carry the errno, never the path
            reason = $"the path cannot be opened or an identity cannot be read ({ex.GetType().Name}: {ex.Message})";
            return null;
        }

        reason = "the identity of a file is not known on this platform";
        return null;
    }

    /// <summary>
    ///     Expands the short (8.3) names of an existing path into their long spelling (Windows); elsewhere, or when the
    ///     path cannot be expanded, returns it unchanged. Links are not resolved.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>The long spelling, or <paramref name="fullPath"/>.</returns>
    internal static string ExpandShortNames(string fullPath)
    {
        try
        {
            return OperatingSystem.IsWindows() ? NativeMethods.TryGetLongPathName(fullPath) ?? fullPath : fullPath;
        }
        catch (Exception ex) when (ex is ExternalException or DllNotFoundException or EntryPointNotFoundException)
        {
            return fullPath;
        }
    }

    /// <summary>
    ///     Determines from a Unix mode whether it describes a regular file.
    /// </summary>
    /// <param name="mode">The mode, or <see langword="null"/> when unknown.</param>
    /// <returns>The answer, or <see langword="null"/> when the mode is unknown.</returns>
    private static bool? IsRegularMode(int? mode) => mode is { } value ? (value & UnixTypeMask) == UnixRegularFile : null;

    /// <summary>
    ///     Determines whether a full path is a folder or lies below it under a given comparison: equal once a trailing
    ///     separator is removed, or starting with the folder followed by a directory separator.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <param name="root">The full path of the folder.</param>
    /// <param name="comparison">The comparison: ordinal for real paths, the platform's for written paths.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is <paramref name="root"/> or below it.</returns>
    private static bool IsSameOrBelow(string path, string root, StringComparison comparison)
    {
        // A root keeps its separator (C:\ and / stay whole), so the boundary is the root itself or the root and a separator
        var normalizedPath = Normalize(path);
        var normalizedRoot = Normalize(root);
        if (string.Equals(
                Path.TrimEndingDirectorySeparator(normalizedPath), Path.TrimEndingDirectorySeparator(normalizedRoot), comparison))
        {
            return true;
        }

        var boundary = Path.EndsInDirectorySeparator(normalizedRoot) ? normalizedRoot : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(boundary, comparison);
    }

    /// <summary>
    ///     Gets the on-disk spelling of one name in a folder, for <see cref="GetExactPathName"/>: the entry spelled
    ///     exactly as written, else the single entry that matches it ignoring letter case, else the name as written.
    /// </summary>
    /// <remarks>
    ///     The folder is listed without a search pattern and every entry is compared as a plain name, so wildcard
    ///     characters in a name (<c>*</c>, <c>?</c>, legal on Linux and macOS) match only themselves. Hidden and system
    ///     entries are included. Never throws: a folder that cannot be listed keeps the name as written.
    /// </remarks>
    /// <param name="folder">The full path of the folder, in its on-disk spelling.</param>
    /// <param name="name">The name as written.</param>
    /// <param name="folderExists">Set to <see langword="false"/> when the folder does not exist (or is not a folder).</param>
    /// <returns>The stored spelling of the name, or <paramref name="name"/>.</returns>
    private static string StoredName(string folder, string name, out bool folderExists)
    {
        folderExists = true;
        var options = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
            RecurseSubdirectories = false,
        };

        try
        {
            // The composed form of a written name can fail (an invalid code point), which keeps the name as written
            var wanted = Normalize(name);

            // No search pattern is given (the options state the intended matching: whole names, any letter case, hidden
            // entries included); only the entries equal to the name ignoring letter case are materialized
            var entries = new FileSystemEnumerable<string>(folder, (ref FileSystemEntry entry) => entry.FileName.ToString(), options)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => MatchesIgnoringCase(entry.FileName, wanted),
            };

            string? single = null;
            var count = 0;
            foreach (var entry in entries)
            {
                // The name as written wins when it is stored exactly so; several other matches leave it as written
                if (string.Equals(Normalize(entry), wanted, StringComparison.Ordinal))
                {
                    return name;
                }

                single = entry;
                count++;
            }

            return count == 1 ? single! : name;
        }
        catch (DirectoryNotFoundException)
        {
            // The folder is missing (or is a file): nothing below it can be corrected
            folderExists = false;
            return name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or ArgumentException or NotSupportedException)
        {
            // A folder that cannot be listed keeps the name as written; a folder below it may still be listed
            return name;
        }
    }

    /// <summary>
    ///     Determines whether a directory entry has a name equal to a wanted name ignoring letter case; on macOS the entry
    ///     is compared in the composed Unicode form (the wanted name is already composed).
    /// </summary>
    /// <param name="entry">The name of the entry.</param>
    /// <param name="wanted">The wanted name, normalized with <see cref="Normalize"/>.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool MatchesIgnoringCase(ReadOnlySpan<char> entry, string wanted) =>
        OperatingSystem.IsMacOS()
            ? string.Equals(Normalize(entry.ToString()), wanted, StringComparison.OrdinalIgnoreCase)
            : entry.Equals(wanted, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Brings a path into the composed Unicode form on macOS, whose file systems may hand back decomposed names;
    ///     elsewhere returns it unchanged.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The normalized path.</returns>
    private static string Normalize(string path) =>
        OperatingSystem.IsMacOS() && !path.IsNormalized(NormalizationForm.FormC) ? path.Normalize(NormalizationForm.FormC) : path;
}
