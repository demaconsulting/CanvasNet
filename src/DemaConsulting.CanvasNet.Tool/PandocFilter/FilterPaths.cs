using System.Text;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The path rules of the Pandoc filter: a literal port of the path helpers of the Lua filter (version 16) and of the
///     <c>pandoc.path</c> functions it used, so that a picture is found, and its files are named in the cache key, exactly
///     as the Lua filter did.
/// </summary>
/// <remarks>
///     <para>
///         <c>pandoc.path</c> is Haskell's <c>System.FilePath</c> for the platform Pandoc runs on: on Windows both
///         <c>/</c> and <c>\</c> separate, a drive is <c>C:</c>, <c>C:\</c>, or a share, and the drive letter is
///         upper-cased by <see cref="Normalize"/>; elsewhere only <c>/</c> separates. <see cref="Split"/>,
///         <see cref="Join"/>, <see cref="Normalize"/>, <see cref="IsAbsolute"/>, <see cref="Directory"/>,
///         <see cref="FileName"/>, <see cref="Stem"/>, and <see cref="SplitSearchPath"/> follow that library.
///         <see cref="System.IO.Path.GetFullPath(string)"/> is never used: it trims trailing dots and spaces on Windows,
///         which would change the names a key is made of.
///     </para>
///     <para>
///         The character classes of Lua patterns are those of the C locale (ASCII only): <see cref="IsLuaSpace"/> is
///         <c>%s</c>, and <see cref="AsciiLower"/> is <c>string.lower</c>. Percent decoding works on the UTF-8 bytes.
///         Every member is a pure function of its arguments except <see cref="FileExists"/> and <see cref="ReadFile"/>,
///         which read the file system; all are safe to call from several threads at once.
///     </para>
/// </remarks>
internal static class FilterPaths
{
    /// <summary>
    ///     The characters of Lua's <c>%s</c> class in the C locale.
    /// </summary>
    private static readonly char[] LuaSpaces = [' ', '\t', '\n', '\v', '\f', '\r'];

    /// <summary>
    ///     Gets a value indicating whether the Windows path rules apply (the platform the tool runs on).
    /// </summary>
    internal static bool Windows { get; } = OperatingSystem.IsWindows();

    /// <summary>
    ///     Gets the separator <c>pandoc.path.separator</c> joins with: <c>\</c> on Windows, <c>/</c> elsewhere.
    /// </summary>
    internal static char Separator => Windows ? '\\' : '/';

    /// <summary>
    ///     Determines whether a character is white space for a Lua pattern (<c>%s</c> in the C locale).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for space, tab, line feed, vertical tab, form feed, and carriage return.</returns>
    internal static bool IsLuaSpace(char c)
    {
        return c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';
    }

    /// <summary>
    ///     Determines whether a text has a character that is not white space (<c>text:match("%S")</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when some character is not <see cref="IsLuaSpace"/>.</returns>
    internal static bool HasNonSpace(string text)
    {
        return text.Any(c => !IsLuaSpace(c));
    }

    /// <summary>
    ///     Removes Lua white space from both ends of a text (<c>text:match("^%s*(.-)%s*$")</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    internal static string TrimLuaSpace(string text)
    {
        return text.Trim(LuaSpaces);
    }

    /// <summary>
    ///     Removes Lua white space from the start of a text (<c>text:gsub("^%s+", "")</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without leading white space.</returns>
    internal static string TrimLuaSpaceStart(string text)
    {
        return text.TrimStart(LuaSpaces);
    }

    /// <summary>
    ///     Removes Lua white space from the end of a text (<c>text:gsub("%s+$", "")</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without trailing white space.</returns>
    internal static string TrimLuaSpaceEnd(string text)
    {
        return text.TrimEnd(LuaSpaces);
    }

    /// <summary>
    ///     Lower-cases the ASCII letters of a text, as <c>string.lower</c> does in the C locale; other characters stay.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with A to Z made a to z.</returns>
    internal static string AsciiLower(string text)
    {
        return string.Create(text.Length, text, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;
            }
        });
    }

    /// <summary>
    ///     Compares two texts by their UTF-8 bytes, the order of Lua's <c>table.sort</c> on strings.
    /// </summary>
    /// <param name="a">The first text.</param>
    /// <param name="b">The second text.</param>
    /// <returns>A negative number, zero, or a positive number as <paramref name="a"/> sorts before, with, or after <paramref name="b"/>.</returns>
    internal static int CompareBytes(string? a, string? b)
    {
        return Encoding.UTF8.GetBytes(a ?? string.Empty).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(b ?? string.Empty));
    }

    /// <summary>
    ///     Sorts texts in place by their UTF-8 bytes (Lua's <c>table.sort</c>).
    /// </summary>
    /// <param name="texts">The texts.</param>
    internal static void SortBytes(List<string> texts)
    {
        texts.Sort(CompareBytes);
    }

    /// <summary>
    ///     Decodes every <c>%XX</c> escape of a text to its byte, reading the result as UTF-8.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The decoded text; the text itself when it has no escape.</returns>
    /// <remarks>
    ///     The Lua filter replaced <c>%%(%x%x)</c> by the byte on the raw bytes, so a run of escapes such as
    ///     <c>%C3%A9</c> is one character. Bytes that are not valid UTF-8 become U+FFFD.
    /// </remarks>
    internal static string DecodePercent(string text)
    {
        if (!text.Contains('%', StringComparison.Ordinal))
        {
            return text;
        }

        // Work on the bytes, so that the escapes of one multi-byte character join again
        var bytes = Encoding.UTF8.GetBytes(text);
        var decoded = new List<byte>(bytes.Length);
        var i = 0;
        while (i < bytes.Length)
        {
            if (bytes[i] == '%' && i + 2 < bytes.Length && char.IsAsciiHexDigit((char)bytes[i + 1]) && char.IsAsciiHexDigit((char)bytes[i + 2]))
            {
                decoded.Add(Convert.FromHexString([(char)bytes[i + 1], (char)bytes[i + 2]])[0]);
                i += 3;
            }
            else
            {
                decoded.Add(bytes[i]);
                i++;
            }
        }

        return Encoding.UTF8.GetString([.. decoded]);
    }

    /// <summary>
    ///     Collapses the <c>.</c> and <c>..</c> segments of a path, which <c>pandoc.path.normalize</c> leaves in place.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>
    ///     The normalized path without <c>.</c> segments and with each <c>..</c> removing the segment before it; a
    ///     <c>..</c> above the root of a rooted path stays at the root, and a leading <c>..</c> of a relative path is
    ///     kept.
    /// </returns>
    internal static string Collapse(string path)
    {
        var parts = new List<string>();
        var split = Split(Normalize(path));

        // A rooted path's first part ("/", "C:\", a share) is its root, which a ".." above it leaves in place
        var root = split.Count > 0 && split[0].Length > 0 && split[0][^1] is '/' or '\\' ? 1 : 0;
        foreach (var part in split)
        {
            if (part == ".." && parts.Count == root && root == 1)
            {
                // Climbing above the root stays at the root
                continue;
            }

            if (part == ".." && parts.Count > root && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return Join(parts);
    }

    /// <summary>
    ///     Makes a path absolute and collapses it: a relative path is taken from the working directory.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="workingDirectory">The working directory (absolute), an explicit input so that tests need no process state.</param>
    /// <returns>The absolute, collapsed path.</returns>
    internal static string Absolute(string path, string workingDirectory)
    {
        return IsAbsolute(path) ? Collapse(path) : Collapse(Join([workingDirectory, path]));
    }

    /// <summary>
    ///     Gets the path of a target seen from a folder, both absolute and collapsed, with forward slashes.
    /// </summary>
    /// <param name="from">The folder.</param>
    /// <param name="target">The target.</param>
    /// <returns>The relative path, or <see langword="null"/> when the two share no first segment (another drive).</returns>
    internal static string? RelativePath(string from, string target)
    {
        var fromParts = Split(from);
        var targetParts = Split(target);
        var common = 0;
        while (common < fromParts.Count && common < targetParts.Count && fromParts[common] == targetParts[common])
        {
            common++;
        }

        if (common == 0)
        {
            return null;
        }

        var parts = new List<string>();
        for (var i = common; i < fromParts.Count; i++)
        {
            parts.Add("..");
        }

        parts.AddRange(targetParts.Skip(common));
        return string.Join('/', parts).Replace('\\', '/');
    }

    /// <summary>
    ///     Gets the text a dependency is hashed under: its path from the source's folder, so that the cache key does not
    ///     depend on where the repository is checked out.
    /// </summary>
    /// <param name="root">The folder of the picture's source.</param>
    /// <param name="path">The dependency (absolute and collapsed).</param>
    /// <returns>The relative path, or the full path when there is no relative one.</returns>
    internal static string Label(string root, string path)
    {
        return RelativePath(root, path) ?? path;
    }

    /// <summary>
    ///     Gets the local path of a <c>file:</c> URI without a host (<c>file:///x</c>, <c>file:/x</c>,
    ///     <c>file:///C:/x</c>), percent-decoded: the tool's own rule (<c>FileReferenceResolver.ParseFileUri</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The path, or <see langword="null"/> for a URI with a host (<c>localhost</c> included) or another form.</returns>
    internal static string? FileUriPath(string text)
    {
        if (text.Length < 5 || AsciiLower(text[..5]) != "file:")
        {
            return null;
        }

        var rest = text[5..];
        string path;
        if (rest.StartsWith("///", StringComparison.Ordinal))
        {
            path = rest[2..];
        }
        else if (rest.StartsWith('/') && (rest.Length < 2 || rest[1] != '/'))
        {
            path = rest;
        }
        else
        {
            return null;
        }

        path = DecodePercent(path);
        if (path.Length >= 2 && path[0] is '/' or '\\' && path[1] is '/' or '\\')
        {
            return null;
        }

        // "/C:/x" is the drive path C:/x
        if (path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':')
        {
            path = path[1..];
        }

        return path;
    }

    /// <summary>
    ///     Determines whether a URL or link is remote, which the tool never reads: the tool's own rule
    ///     (<c>FeatureCatalog.IsRemoteDataUrl</c>).
    /// </summary>
    /// <param name="url">The URL or link.</param>
    /// <returns>
    ///     <see langword="true"/> for two leading slashes or backslashes, a scheme of two or more characters other than
    ///     <c>file</c>, or a <c>file:</c> URI with a host; <see langword="false"/> for every local path.
    /// </returns>
    internal static bool IsRemoteUrl(string url)
    {
        var text = TrimLuaSpaceStart(url);
        if (text.Length >= 2 && text[0] is '/' or '\\' && text[1] is '/' or '\\')
        {
            return true;
        }

        var scheme = SchemeOf(text);
        if (scheme == null || scheme.Length <= 1)
        {
            return false;
        }

        return AsciiLower(scheme) != "file" || FileUriPath(text) == null;
    }

    /// <summary>
    ///     Gets the file a data URL names, relative to a folder.
    /// </summary>
    /// <param name="folder">The folder of the file that holds the URL (absolute and collapsed).</param>
    /// <param name="url">The URL as written.</param>
    /// <returns>
    ///     The absolute, collapsed path: a <c>file:</c> URI without a host by its path, an absolute path as it is, a path
    ///     rooted without a drive on the drive of the folder, otherwise taken from the folder (a backslash is a slash); or
    ///     <see langword="null"/> when the URL is blank or remote.
    /// </returns>
    internal static string? LocalDataFile(string folder, string url)
    {
        if (!HasNonSpace(url) || IsRemoteUrl(url))
        {
            return null;
        }

        var path = (FileUriPath(TrimLuaSpaceStart(url)) ?? url).Replace('\\', '/');
        if (IsAbsolute(path))
        {
            return Collapse(path);
        }

        if (path.StartsWith('/'))
        {
            var drive = folder.Length >= 2 && char.IsAsciiLetter(folder[0]) && folder[1] == ':' ? folder[..2] : string.Empty;
            return Collapse(drive + path);
        }

        return Collapse(Join([folder, path]));
    }

    /// <summary>
    ///     Gets the file a picture link names: the link without its fragment or query, as <see cref="LocalDataFile"/>.
    /// </summary>
    /// <param name="folder">The folder of the file that holds the link.</param>
    /// <param name="link">The link as written.</param>
    /// <returns>The absolute, collapsed path, or <see langword="null"/> when the link is blank or remote.</returns>
    internal static string? LocalImageFile(string folder, string link)
    {
        return LocalDataFile(folder, CutFragment(link));
    }

    /// <summary>
    ///     Removes everything from the first <c>#</c> or <c>?</c> of a link (<c>link:gsub("[#?].*$", "")</c>).
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>The link without its fragment and query.</returns>
    internal static string CutFragment(string link)
    {
        var cut = link.IndexOfAny(['#', '?']);
        return cut < 0 ? link : link[..cut];
    }

    /// <summary>
    ///     Determines whether an absolute, collapsed path is a folder or inside it, comparing whole segments, with the
    ///     letter case ignored on Windows.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="folder">The folder.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is <paramref name="folder"/> or below it.</returns>
    internal static bool IsSameOrUnder(string path, string folder)
    {
        static string Plain(string text)
        {
            text = text.TrimEnd('/', '\\');
            return Windows ? AsciiLower(text.Replace('/', '\\')) : text;
        }

        var inner = Plain(path);
        var outer = Plain(folder);
        return inner == outer || inner.StartsWith(outer + Separator, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Finds the source file of a picture the way Pandoc finds a picture.
    /// </summary>
    /// <param name="src">The picture's <c>src</c> as written.</param>
    /// <param name="resourceDirectories">The folders of the document's resource path, absolute and in order.</param>
    /// <returns>The absolute, collapsed path of the first file that opens, or <see langword="null"/> when none does.</returns>
    /// <remarks>
    ///     The name as written is tried first, then its percent-decoded form when that differs. An absolute name is used
    ///     when the file opens; a relative name is joined to each resource folder in turn. There is no containment check:
    ///     the filter is not a security boundary.
    /// </remarks>
    internal static string? LocateSource(string src, IReadOnlyList<string> resourceDirectories)
    {
        var names = new List<string> { src };
        var decoded = DecodePercent(src);
        if (decoded != src)
        {
            names.Add(decoded);
        }

        foreach (var name in names)
        {
            IEnumerable<string> candidates = IsAbsolute(name)
                ? [Collapse(name)]
                : resourceDirectories.Select(directory => Collapse(Join([directory, name])));
            var found = candidates.FirstOrDefault(FileExists);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    ///     Determines whether a file opens for reading (Lua <c>io.open(path, "rb")</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when the file can be opened; <see langword="false"/> for a folder, a missing or unreadable file, or a malformed path.</returns>
    internal static bool FileExists(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Reads a file's bytes.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The content, or <see langword="null"/> when the file cannot be read.</returns>
    internal static byte[]? ReadFile(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Gets the URL scheme a text starts with (<c>^(%a[%w+.-]*):</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The scheme without the colon, or <see langword="null"/> when the text does not start with one.</returns>
    internal static string? SchemeOf(string text)
    {
        if (text.Length == 0 || !char.IsAsciiLetter(text[0]))
        {
            return null;
        }

        var end = 1;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '+' or '.' or '-'))
        {
            end++;
        }

        return end < text.Length && text[end] == ':' ? text[..end] : null;
    }

    /// <summary>
    ///     Splits a path into its segments (<c>pandoc.path.split</c>, <c>splitDirectories</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The drive (when there is one) followed by each segment, trailing separators removed.</returns>
    internal static List<string> Split(string path)
    {
        var (drive, rest) = SplitDrive(path);
        var parts = new List<string>();
        if (drive.Length > 0)
        {
            parts.Add(drive);
        }

        // Each segment is a name and the separators after it, without them unless it is a drive
        var i = 0;
        while (i < rest.Length)
        {
            var start = i;
            while (i < rest.Length && !IsSeparator(rest[i]))
            {
                i++;
            }

            while (i < rest.Length && IsSeparator(rest[i]))
            {
                i++;
            }

            parts.Add(DropTrailingSeparator(rest[start..i]));
        }

        return parts;
    }

    /// <summary>
    ///     Joins path segments (<c>pandoc.path.join</c>, <c>joinPath</c>): a segment that starts with a separator or has a
    ///     drive replaces everything before it.
    /// </summary>
    /// <param name="parts">The segments.</param>
    /// <returns>The joined path; empty for no segments.</returns>
    internal static string Join(IReadOnlyList<string> parts)
    {
        // foldr combine "": combine the segments from the right
        var result = string.Empty;
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            result = Combine(parts[i], result);
        }

        return result;
    }

    /// <summary>
    ///     Determines whether a path is absolute (<c>pandoc.path.is_absolute</c>): on Windows it has a drive with a
    ///     separator or a share (<c>\x</c> and <c>C:x</c> are relative); elsewhere it starts with <c>/</c>.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when the path is absolute.</returns>
    internal static bool IsAbsolute(string path)
    {
        var drive = SplitDrive(path).Drive;
        return drive.Length > 0 && !IsRelativeDrive(drive);
    }

    /// <summary>
    ///     Gets the folder of a path (<c>pandoc.path.directory</c>, <c>takeDirectory</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The folder without a trailing separator (a drive keeps its own), or <c>.</c> for a bare name.</returns>
    internal static string Directory(string path)
    {
        var (drive, rest) = SplitDrive(path);
        var end = rest.Length;
        while (end > 0 && !IsSeparator(rest[end - 1]))
        {
            end--;
        }

        var folder = drive + rest[..end];
        return DropTrailingSeparator(folder.Length == 0 ? "./" : folder);
    }

    /// <summary>
    ///     Gets the file name of a path (<c>pandoc.path.filename</c>, <c>takeFileName</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The text after the last separator (and after the drive).</returns>
    internal static string FileName(string path)
    {
        var rest = SplitDrive(path).Path;
        var end = rest.Length;
        while (end > 0 && !IsSeparator(rest[end - 1]))
        {
            end--;
        }

        return rest[end..];
    }

    /// <summary>
    ///     Gets a path without the last extension of its file name (the first value of
    ///     <c>pandoc.path.split_extension</c>).
    /// </summary>
    /// <param name="path">The path or file name.</param>
    /// <returns>The path up to the last dot of its file name, or the path itself when the file name has no dot.</returns>
    internal static string Stem(string path)
    {
        var fileStart = path.Length - FileName(path).Length;
        var dot = path.LastIndexOf('.');
        return dot < fileStart ? path : path[..dot];
    }

    /// <summary>
    ///     Splits a list of folders separated like <c>PATH</c> (<c>pandoc.path.split_search_path</c>).
    /// </summary>
    /// <param name="text">The list: <c>;</c> separates on Windows, <c>:</c> elsewhere.</param>
    /// <returns>
    ///     The entries: on Windows an empty entry is dropped and a double-quoted entry is unquoted; elsewhere an empty entry
    ///     is <c>.</c>.
    /// </returns>
    internal static List<string> SplitSearchPath(string text)
    {
        var entries = new List<string>();
        foreach (var entry in text.Split(Windows ? ';' : ':'))
        {
            if (entry.Length == 0)
            {
                if (!Windows)
                {
                    entries.Add(".");
                }
            }
            else if (Windows && entry.Length >= 2 && entry[0] == '"' && entry[^1] == '"')
            {
                entries.Add(entry[1..^1]);
            }
            else
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    ///     Normalizes a path (<c>pandoc.path.normalize</c>, <c>normalise</c>): separators become the platform's, repeated
    ///     separators and <c>.</c> segments are removed, a Windows drive letter is upper-cased, and <c>..</c> is kept.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The normalized path; <c>.</c> for an empty one.</returns>
    internal static string Normalize(string path)
    {
        var (drive, rest) = SplitDrive(path);

        // joinPath . dropDots . propSep . splitDirectories: a leading root becomes one separator, "." segments go
        var segments = Split(rest);
        if (segments.Count > 0 && segments[0].All(IsSeparator))
        {
            segments[0] = Separator.ToString();
        }

        segments.RemoveAll(s => s == ".");
        var body = Join(segments);
        var normalizedDrive = NormalizeDrive(drive);
        var result = normalizedDrive.Length == 0 && body.Length == 0 ? "." : CombineAlways(normalizedDrive, body);

        // A path that names a folder keeps one trailing separator
        var isDirectoryPath = HasTrailingSeparator(rest)
                              || (rest.Length > 0 && rest[^1] == '.' && HasTrailingSeparator(rest[..^1]));
        return isDirectoryPath && !HasTrailingSeparator(result) && !IsRelativeDrive(drive) ? result + Separator : result;
    }

    /// <summary>
    ///     Splits a path into its drive and the rest (<c>splitDrive</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The drive (empty when there is none) and the rest.</returns>
    private static (string Drive, string Path) SplitDrive(string path)
    {
        if (!Windows)
        {
            var count = 0;
            while (count < path.Length && path[count] == '/')
            {
                count++;
            }

            return (path[..count], path[count..]);
        }

        return ReadDriveLetter(path) ?? ReadDriveUnc(path) ?? ReadDriveShare(path) ?? (string.Empty, path);
    }

    /// <summary>
    ///     Reads a Windows drive letter (<c>C:</c>, with the separators after it).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The drive and the rest, or <see langword="null"/>.</returns>
    private static (string Drive, string Path)? ReadDriveLetter(string path)
    {
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        {
            return AddSeparators(path[..2], path[2..]);
        }

        return null;
    }

    /// <summary>
    ///     Reads a Windows device drive (<c>\\?\UNC\server\</c> or <c>\\?\C:\</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The drive and the rest, or <see langword="null"/>.</returns>
    private static (string Drive, string Path)? ReadDriveUnc(string path)
    {
        if (path.Length < 4 || !IsSeparator(path[0]) || !IsSeparator(path[1]) || path[2] != '?' || !IsSeparator(path[3]))
        {
            return null;
        }

        var tail = path[4..];
        if (tail.Length >= 4 && AsciiLower(tail[..3]) == "unc" && IsSeparator(tail[3]))
        {
            var (share, rest) = ReadShareName(tail[4..]);
            return (path[..8] + share, rest);
        }

        return ReadDriveLetter(tail) is var (drive, after) ? (path[..4] + drive, after) : null;
    }

    /// <summary>
    ///     Reads a Windows share drive (<c>\\server\</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The drive and the rest, or <see langword="null"/>.</returns>
    private static (string Drive, string Path)? ReadDriveShare(string path)
    {
        if (path.Length < 2 || !IsSeparator(path[0]) || !IsSeparator(path[1]))
        {
            return null;
        }

        var (name, rest) = ReadShareName(path[2..]);
        return (path[..2] + name, rest);
    }

    /// <summary>
    ///     Reads a share or server name with the separators after it.
    /// </summary>
    /// <param name="text">The text after the leading separators.</param>
    /// <returns>The name with its separators, and the rest.</returns>
    private static (string Name, string Path) ReadShareName(string text)
    {
        var end = 0;
        while (end < text.Length && !IsSeparator(text[end]))
        {
            end++;
        }

        return AddSeparators(text[..end], text[end..]);
    }

    /// <summary>
    ///     Moves the separators at the start of a rest onto the end of a drive (<c>addSlash</c>).
    /// </summary>
    /// <param name="drive">The drive.</param>
    /// <param name="rest">The rest.</param>
    /// <returns>The drive with the separators, and the rest after them.</returns>
    private static (string Drive, string Path) AddSeparators(string drive, string rest)
    {
        var count = 0;
        while (count < rest.Length && IsSeparator(rest[count]))
        {
            count++;
        }

        return (drive + rest[..count], rest[count..]);
    }

    /// <summary>
    ///     Determines whether a drive is a relative drive letter (<c>C:</c> without a separator).
    /// </summary>
    /// <param name="drive">The drive.</param>
    /// <returns><see langword="true"/> for a Windows drive letter without a separator after it.</returns>
    private static bool IsRelativeDrive(string drive)
    {
        return Windows && ReadDriveLetter(drive) is var (letter, _) && !HasTrailingSeparator(letter);
    }

    /// <summary>
    ///     Normalizes a drive (<c>normaliseDrive</c>): any drive is <c>/</c> elsewhere; on Windows the separators become
    ///     <c>\</c> and a drive letter is upper-cased.
    /// </summary>
    /// <param name="drive">The drive.</param>
    /// <returns>The normalized drive.</returns>
    private static string NormalizeDrive(string drive)
    {
        if (drive.Length == 0)
        {
            return drive;
        }

        if (!Windows)
        {
            return "/";
        }

        var replaced = drive.Replace('/', '\\');
        return ReadDriveLetter(replaced) != null ? replaced.ToUpperInvariant() : replaced;
    }

    /// <summary>
    ///     Combines two paths (<c>combine</c>): the second wins when it starts with a separator or has a drive.
    /// </summary>
    /// <param name="a">The first path.</param>
    /// <param name="b">The second path.</param>
    /// <returns>The combined path.</returns>
    private static string Combine(string a, string b)
    {
        if ((b.Length > 0 && IsSeparator(b[0])) || SplitDrive(b).Drive.Length > 0)
        {
            return b;
        }

        return CombineAlways(a, b);
    }

    /// <summary>
    ///     Combines two paths with a separator between them (<c>combineAlways</c>).
    /// </summary>
    /// <param name="a">The first path.</param>
    /// <param name="b">The second path.</param>
    /// <returns>The combined path.</returns>
    private static string CombineAlways(string a, string b)
    {
        if (a.Length == 0)
        {
            return b;
        }

        if (b.Length == 0 || HasTrailingSeparator(a))
        {
            return a + b;
        }

        return Windows && a.Length == 2 && char.IsLetter(a[0]) && a[1] == ':' ? a + b : a + Separator + b;
    }

    /// <summary>
    ///     Removes the trailing separators of a path unless it is a drive (<c>dropTrailingPathSeparator</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The path without trailing separators; a path of separators only keeps its last one.</returns>
    private static string DropTrailingSeparator(string path)
    {
        if (!HasTrailingSeparator(path) || IsDrive(path))
        {
            return path;
        }

        var end = path.Length;
        while (end > 0 && IsSeparator(path[end - 1]))
        {
            end--;
        }

        return end == 0 ? path[^1..] : path[..end];
    }

    /// <summary>
    ///     Determines whether a path is a drive and nothing else (<c>isDrive</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when the path is not empty and has nothing after its drive.</returns>
    private static bool IsDrive(string path)
    {
        return path.Length > 0 && SplitDrive(path).Path.Length == 0;
    }

    /// <summary>
    ///     Determines whether a path ends with a separator.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when the last character is a separator.</returns>
    private static bool HasTrailingSeparator(string path)
    {
        return path.Length > 0 && IsSeparator(path[^1]);
    }

    /// <summary>
    ///     Determines whether a character separates path segments: <c>/</c>, and on Windows also <c>\</c>.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a separator.</returns>
    private static bool IsSeparator(char c)
    {
        return c == '/' || (Windows && c == '\\');
    }
}
