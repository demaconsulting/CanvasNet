using System.Security.Cryptography;
using System.Text;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The picture cache of the Pandoc filter: the cache key, the names of the pictures, copies, and markers, and the
///     atomic writes that let two builds share the folder.
/// </summary>
/// <remarks>
///     <para>
///         Every name is content-addressed: the key is the SHA-1 of everything that decides the picture, so an unchanged
///         source is never drawn again and a changed one always is. The composition, the separators, and the names are those
///         of the Lua filter (version 16); only <see cref="KeyVersion"/> differs.
///     </para>
///     <para>
///         A file is written under a temporary name in the cache folder and then renamed without overwriting, so a build
///         running at the same time never reads a half-written file; when the rename fails because another build wrote
///         the same file first, its copy (identical, as the name is a hash of it) is kept. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class PictureCache
{
    /// <summary>
    ///     The version that is part of every picture's key, so a change to what the filter draws or how it treats a
    ///     diagram draws the pictures again.
    /// </summary>
    /// <remarks>
    ///     12: a diagram type the tool does not draw is left as written with a warning, no longer for another filter.
    ///     13: packet diagrams are drawn, so a marker that left one as written is no longer used.
    ///     14: git graphs are drawn, so a marker that left one as written is no longer used.
    ///     15: CANVASNET_FILE_ACCESS and CANVASNET_ROOTS pass --file-access and --allow-root (absolute folders; for roots
    ///     without CANVASNET_ROOTS, the working directory and the picture's folder) and are part of every name, and an
    ///     absolute path or a file: URI without a host is hashed, as the tool reads it.
    ///     16: CANVASNET_ROOTS without CANVASNET_FILE_ACCESS passes --file-access roots explicitly, a restrictive build also
    ///     stops on the tool's file-access-denied diagnostic, and a chart linked back from a picture it draws is hashed once.
    ///     17: the filter runs in the tool (the pandoc-filter command) and the Lua file is a shim; the key is composed as
    ///     in 16, and the new version keeps the two implementations from trusting each other's markers.
    /// </remarks>
    internal const string KeyVersion = "17";

    /// <summary>
    ///     The text that separates the fields of the key and the parts of the dependencies.
    /// </summary>
    private const string FieldSeparator = "\n\0\n";

    /// <summary>
    ///     Computes the cache key of a picture.
    /// </summary>
    /// <param name="kind">The picture's kind, or <c>svg</c> for a flattened drawing.</param>
    /// <param name="toolIdentity">The tool identity (<see cref="FilterSettings.ToolIdentity"/>).</param>
    /// <param name="fileAccessKey">The file-access key (<see cref="FileAccessArguments.Key"/>).</param>
    /// <param name="options">The tool options, sorted.</param>
    /// <param name="folder">
    ///     For a drawing, its folder as seen from the cache folder (the links the tool rebases lead there); otherwise
    ///     <see langword="null"/>.
    /// </param>
    /// <param name="parts">The parts of the dependencies (<see cref="DependencyHasher"/>).</param>
    /// <returns>The lowercase hexadecimal SHA-1 of the fields joined with <c>\n\0\n</c>.</returns>
    internal static string ComputeKey(
        string kind, string toolIdentity, string fileAccessKey, IReadOnlyList<string> options, string? folder, IReadOnlyList<byte[]> parts)
    {
        // The fields in the order of the Lua filter; the parts are streamed, not concatenated
        var fields = new List<string> { "canvasnet.lua " + KeyVersion, kind, toolIdentity, fileAccessKey, string.Join(';', options) };
        if (folder != null)
        {
            fields.Add("folder " + folder);
        }

#pragma warning disable CA5350, S4790 // SHA-1 names cached content (the Lua filter's key) and is not a security control
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
#pragma warning restore CA5350, S4790
        foreach (var field in fields)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(field + FieldSeparator));
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(FieldSeparator));
            }

            hash.AppendData(parts[i]);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>
    ///     Gets the stem of a cache file name (<c>stem_of</c>): the source's file name without its extension (and
    ///     <c>.vl</c>), with every byte that is not an ASCII letter, a digit, <c>_</c>, or <c>-</c> made a <c>-</c>.
    /// </summary>
    /// <param name="source">The source file.</param>
    /// <returns>The stem; a character of several UTF-8 bytes becomes several dashes.</returns>
    internal static string StemOf(string source)
    {
        var stem = FilterPaths.Stem(FilterPaths.FileName(source));
        if (stem.EndsWith(".vl", StringComparison.Ordinal))
        {
            stem = stem[..^3];
        }

        var builder = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(stem))
        {
            builder.Append(char.IsAsciiLetterOrDigit((char)b) || b is (byte)'_' or (byte)'-' ? (char)b : '-');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the file of a drawn picture: <c>&lt;stem&gt;-&lt;key&gt;.png</c>.
    /// </summary>
    /// <param name="folder">The cache folder.</param>
    /// <param name="stem">The stem.</param>
    /// <param name="key">The key.</param>
    /// <returns>The path.</returns>
    internal static string PictureFile(string folder, string stem, string key)
    {
        return FilterPaths.Join([folder, $"{stem}-{key}.png"]);
    }

    /// <summary>
    ///     Gets the file of a flattened drawing: <c>&lt;stem&gt;-&lt;key&gt;.svg</c>.
    /// </summary>
    /// <param name="folder">The cache folder.</param>
    /// <param name="stem">The stem.</param>
    /// <param name="key">The key.</param>
    /// <returns>The path.</returns>
    internal static string FlattenedFile(string folder, string stem, string key)
    {
        return FilterPaths.Join([folder, $"{stem}-{key}.svg"]);
    }

    /// <summary>
    ///     Gets the marker of a diagram the tool does not draw: <c>&lt;key&gt;.unsupported</c>, holding the tool's message.
    /// </summary>
    /// <param name="folder">The cache folder.</param>
    /// <param name="key">The key.</param>
    /// <returns>The path.</returns>
    internal static string UnsupportedMarker(string folder, string key)
    {
        return FilterPaths.Join([folder, key + ".unsupported"]);
    }

    /// <summary>
    ///     Gets the marker of a drawing the tool found nothing to flatten in: <c>&lt;key&gt;.plain</c>, holding the tool's
    ///     warnings, one per line.
    /// </summary>
    /// <param name="folder">The cache folder.</param>
    /// <param name="key">The key.</param>
    /// <returns>The path.</returns>
    internal static string PlainMarker(string folder, string key)
    {
        return FilterPaths.Join([folder, key + ".plain"]);
    }

    /// <summary>
    ///     Gets the file the text of a code block is written to: the SHA-1 of the text and a fixed prefix that names the kind,
    ///     with the extension of the kind (<c>.mmd</c> for a Mermaid block, <c>.puml</c> for a PlantUML block, <c>.mml</c> for
    ///     a MathML block).
    /// </summary>
    /// <param name="folder">The cache folder.</param>
    /// <param name="text">The text as written to the file (the block's text and a line feed).</param>
    /// <param name="kind">The kind the block is drawn as (<see cref="PictureAttributes.Mermaid"/>, <see cref="PictureAttributes.PlantUml"/>, or <see cref="PictureAttributes.MathMl"/>).</param>
    /// <returns>The path.</returns>
    /// <remarks>
    ///     The name of a Mermaid block is the one of earlier versions (the prefix <c>canvasnet.lua mermaid block</c> and
    ///     <c>.mmd</c>), so the pictures already in a cache keep their keys.
    /// </remarks>
    internal static string BlockSourceFile(string folder, string text, string kind = PictureAttributes.Mermaid)
    {
        var extension = kind switch
        {
            PictureAttributes.PlantUml => ".puml",
            PictureAttributes.MathMl => ".mml",
            _ => ".mmd"
        };
        return FilterPaths.Join([folder, FilterSettings.Sha1Hex(Encoding.UTF8.GetBytes($"canvasnet.lua {kind} block\n" + text)) + extension]);
    }

    /// <summary>
    ///     Gets a temporary file to draw into: <c>&lt;key&gt;.&lt;9 random digits&gt;.partial.&lt;extension&gt;</c> in the
    ///     cache folder (the links a flattened copy rebases are right after the rename).
    /// </summary>
    /// <param name="folder">The cache folder.</param>
    /// <param name="key">The key.</param>
    /// <param name="extension">The extension without the dot: <c>png</c> or <c>svg</c>.</param>
    /// <returns>The path.</returns>
    internal static string PartialFile(string folder, string key, string extension)
    {
        return FilterPaths.Join([folder, $"{key}.{RandomDigits()}.partial.{extension}"]);
    }

    /// <summary>
    ///     Writes a text file atomically unless it exists (<c>write_atomically</c>).
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="content">The text, written as UTF-8 without a byte order mark.</param>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the temporary file cannot be written, or the file is
    ///     not there after the rename failed.
    /// </exception>
    /// <remarks>A file that is there already holds the same text: its name is a hash of it.</remarks>
    internal static void WriteTextAtomically(string path, string content)
    {
        WriteTextAtomically(path, content, File.WriteAllBytes);
    }

    /// <summary>
    ///     Writes a text file atomically unless it exists, writing the temporary file with the given writer.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="content">The text, written as UTF-8 without a byte order mark.</param>
    /// <param name="write">Writes bytes to a new file (<see cref="File.WriteAllBytes(string, byte[])"/>, or a test's writer).</param>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the temporary file cannot be written (it is removed
    ///     first), or the file is not there after the rename failed.
    /// </exception>
    internal static void WriteTextAtomically(string path, string content, Action<string, byte[]> write)
    {
        if (FilterPaths.FileExists(path))
        {
            return;
        }

        var partial = $"{path}.{RandomDigits()}.partial";
        try
        {
            write(partial, Encoding.UTF8.GetBytes(content));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A write that failed after the file was created (a full disk, say) must not leave the temporary file behind
            Delete(partial);
            throw new ToolException(ErrorCodes.PandocFilterError, $"could not write {partial}", null, ex);
        }

        Commit(partial, path, $"could not write {path}");
    }

    /// <summary>
    ///     Renames a temporary file to its cache name without overwriting; when another build wrote the file first, the
    ///     temporary file is removed and the other copy is kept.
    /// </summary>
    /// <param name="partial">The temporary file.</param>
    /// <param name="target">The cache file.</param>
    /// <param name="failure">The message when neither the rename worked nor the file is there.</param>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> and <paramref name="failure"/>.</exception>
    internal static void Commit(string partial, string target, string failure)
    {
        try
        {
            File.Move(partial, target, overwrite: false);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Another build wrote the same file first, or the rename failed: either way the partial file goes
            Delete(partial);
        }

        if (!FilterPaths.FileExists(target))
        {
            throw new ToolException(ErrorCodes.PandocFilterError, failure);
        }
    }

    /// <summary>
    ///     Reads a marker or another text file of the cache.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The text, or <see langword="null"/> when the file cannot be read.</returns>
    internal static string? ReadText(string path)
    {
        return FilterPaths.ReadFile(path) is { } bytes ? Encoding.UTF8.GetString(bytes) : null;
    }

    /// <summary>
    ///     Deletes a file when it exists, ignoring a failure (<c>os.remove</c>).
    /// </summary>
    /// <param name="path">The file.</param>
    internal static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing to do: a partial file left behind is harmless, as no name of the cache ends in .partial.*
        }
    }

    /// <summary>
    ///     Gets nine random decimal digits for a temporary name.
    /// </summary>
    /// <returns>A number from 100000000 to 999999999 as text.</returns>
    private static string RandomDigits()
    {
        return RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
