using System.Security.Cryptography;
using System.Text;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The Lua shim of the Pandoc filter, embedded in the tool, and the protocol it speaks with the <c>pandoc-filter</c>
///     command.
/// </summary>
/// <remarks>
///     <para>
///         Each repository keeps only the shim (<c>docs/filters/canvasnet.lua</c>): it sends the whole document to
///         <c>canvasnet pandoc-filter</c> once and applies what comes back, so a fix of the filter ships with the tool.
///         The shim passes <c>--protocol</c> with <see cref="Protocol"/>; the tool accepts
///         <see cref="MinProtocol"/> to <see cref="MaxProtocol"/> and names the command that writes a matching shim when
///         the shim is too old or too new (<see cref="CheckProtocol"/>). The protocol changes only when the arguments or
///         the output contract change, never for a change of the filter's logic.
///     </para>
///     <para>
///         The embedded file is a template: its fast path, which decides whether a document needs the tool at all, holds
///         the placeholders <see cref="ClassesPlaceholder"/>, <see cref="DrawClassesPlaceholder"/>, and
///         <see cref="SuffixesPlaceholder"/>, which <see cref="Generate"/> replaces with Lua lists of the code-block classes
///         the filter draws, the classes it draws only with the class <c>draw</c>, and the picture suffixes it draws
///         (<see cref="PictureAttributes.BlockClasses"/>, <see cref="PictureAttributes.DrawBlockClasses"/>,
///         <see cref="PictureAttributes.PictureSuffixes"/>). The shim that a repository keeps therefore starts the tool for
///         exactly what this tool draws (a plain <c>openscad</c> block, which stays code, does not start it), and is written
///         again after the tool is updated. A shim written by an older tool keeps working, but does not start the tool for
///         a document whose only pictures are block classes that tool did not know.
///     </para>
///     <para>Stateless and thread-safe.</para>
/// </remarks>
internal static class PandocFilterShim
{
    /// <summary>
    ///     The protocol the embedded shim speaks.
    /// </summary>
    internal const int Protocol = 1;

    /// <summary>
    ///     The oldest protocol the tool accepts.
    /// </summary>
    internal const int MinProtocol = 1;

    /// <summary>
    ///     The newest protocol the tool accepts.
    /// </summary>
    internal const int MaxProtocol = 1;

    /// <summary>
    ///     The logical name of the embedded template of the shim.
    /// </summary>
    internal const string ResourceName = "DemaConsulting.CanvasNet.Tool.PandocFilter.canvasnet.lua";

    /// <summary>
    ///     The usual path of the shim in a repository, used in a message when the shim's path is unknown.
    /// </summary>
    internal const string DefaultPath = "docs/filters/canvasnet.lua";

    /// <summary>
    ///     The placeholder of the template that <see cref="Generate"/> replaces with the code-block classes the filter draws.
    /// </summary>
    internal const string ClassesPlaceholder = "@CLASSES@";

    /// <summary>
    ///     The placeholder of the template that <see cref="Generate"/> replaces with the code-block classes the filter draws
    ///     only when the block also has the class <c>draw</c>.
    /// </summary>
    internal const string DrawClassesPlaceholder = "@DRAW_CLASSES@";

    /// <summary>
    ///     The placeholder of the template that <see cref="Generate"/> replaces with the picture suffixes the filter may change.
    /// </summary>
    internal const string SuffixesPlaceholder = "@SUFFIXES@";

    /// <summary>
    ///     The bytes of the shim, generated from the embedded template on first use.
    /// </summary>
    private static readonly Lazy<byte[]> Content = new(() => Encoding.UTF8.GetBytes(Generate(Encoding.UTF8.GetString(Load()))));

    /// <summary>
    ///     Gets the bytes of the shim: the embedded template with the filter's tables filled in (<see cref="Generate"/>),
    ///     as UTF-8 without a byte order mark.
    /// </summary>
    internal static byte[] Bytes => Content.Value;

    /// <summary>
    ///     Fills the tables of the shim's fast path into the template.
    /// </summary>
    /// <param name="template">The text of the template.</param>
    /// <returns>
    ///     The template with <see cref="ClassesPlaceholder"/> replaced by a Lua list of
    ///     <see cref="PictureAttributes.BlockClasses"/>, <see cref="DrawClassesPlaceholder"/> by a Lua list of
    ///     <see cref="PictureAttributes.DrawBlockClasses"/>, and <see cref="SuffixesPlaceholder"/> by a Lua list of
    ///     <see cref="PictureAttributes.PictureSuffixes"/>, for example <c>{ "mermaid" }</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when a placeholder is not in the template exactly once (a defect of the build).</exception>
    internal static string Generate(string template)
    {
        var classes = Fill(template, ClassesPlaceholder, PictureAttributes.BlockClasses);
        var drawClasses = Fill(classes, DrawClassesPlaceholder, PictureAttributes.DrawBlockClasses);
        return Fill(drawClasses, SuffixesPlaceholder, PictureAttributes.PictureSuffixes);
    }

    /// <summary>
    ///     Checks the protocol a shim speaks.
    /// </summary>
    /// <param name="protocol">The protocol the shim passed with <c>--protocol</c>.</param>
    /// <param name="filterPath">The shim's path (<c>--filter-path</c>), or <see langword="null"/> when not given.</param>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.UsageError"/> when the protocol is older than <see cref="MinProtocol"/> (the
    ///     hint names the <c>--write-shim</c> command for the shim's path) or newer than <see cref="MaxProtocol"/> (the hint
    ///     says to update the tool).
    /// </exception>
    internal static void CheckProtocol(int protocol, string? filterPath)
    {
        var path = string.IsNullOrEmpty(filterPath) ? DefaultPath : filterPath;
        if (protocol < MinProtocol)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"{path} speaks protocol {protocol}; this tool needs protocol {MinProtocol}",
                $"Write the shim again: dotnet canvasnet pandoc-filter --write-shim {path}");
        }

        if (protocol > MaxProtocol)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"{path} speaks protocol {protocol}; this tool speaks protocol {MaxProtocol} at most",
                "Update DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json (dotnet tool update DemaConsulting.CanvasNet.Tool)");
        }
    }

    /// <summary>
    ///     Writes the shim (<see cref="Bytes"/>) to a file, byte for byte.
    /// </summary>
    /// <param name="path">The file; its folder is created, and an existing file is replaced.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.OutputWriteFailed"/> when the file cannot be written.</exception>
    /// <remarks>
    ///     The shim is written under a temporary name in the same folder and then moved into place, so a Pandoc run that
    ///     starts meanwhile reads either the old shim or the new one, never half of it.
    /// </remarks>
    internal static int Write(string path)
    {
        var bytes = Bytes;
        var partial = $"{path}.{RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000)}.partial";
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllBytes(partial, bytes);
            File.Move(partial, path, overwrite: true);
            return bytes.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            PictureCache.Delete(partial);
            throw new ToolException(ErrorCodes.OutputWriteFailed, $"Could not write the shim to '{path}': {ex.Message}", "Check the path and its permissions.", ex);
        }
    }

    /// <summary>
    ///     Replaces the one placeholder of a template with a Lua list of strings.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="placeholder">The placeholder.</param>
    /// <param name="items">The strings; a backslash and a double quote are escaped.</param>
    /// <returns>The template with the placeholder replaced by <c>{ "a", "b" }</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the placeholder is not in the template exactly once.</exception>
    private static string Fill(string template, string placeholder, IEnumerable<string> items)
    {
        var at = template.IndexOf(placeholder, StringComparison.Ordinal);
        if (at < 0 || template.IndexOf(placeholder, at + placeholder.Length, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException($"The shim template must hold the placeholder '{placeholder}' exactly once.");
        }

        var list = "{ " + string.Join(", ", items.Select(item => "\"" + item.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"")) + " }";
        return string.Concat(template.AsSpan(0, at), list, template.AsSpan(at + placeholder.Length));
    }

    /// <summary>
    ///     Loads the embedded template of the shim.
    /// </summary>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the shim is not embedded (a defect of the build).</exception>
    private static byte[] Load()
    {
        using var stream = typeof(PandocFilterShim).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The resource '{ResourceName}' is not embedded in the tool.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
