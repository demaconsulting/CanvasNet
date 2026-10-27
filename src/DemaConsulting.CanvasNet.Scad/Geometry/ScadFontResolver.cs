// cspell:ignore Liberation
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Where a resolved font came from.
/// </summary>
internal enum ScadFontSource
{
    /// <summary>One of the Liberation fonts bundled with the core package.</summary>
    Bundled,

    /// <summary>A font file found in one of <see cref="ScadFontResolutionOptions.AdditionalDirectories"/>.</summary>
    AdditionalDirectory,

    /// <summary>A font installed on the host.</summary>
    System,
}

/// <summary>
///     The outcome of resolving a <c>font</c> argument.
/// </summary>
/// <param name="Font">The font to lay the text out with; never <see langword="null"/>.</param>
/// <param name="Source">Where the font came from.</param>
/// <param name="Warning">What the model author should be told (the font was not found or could not be loaded), or <see langword="null"/> when the request was met.</param>
internal sealed record ScadResolvedFont(TrueTypeFont Font, ScadFontSource Source, string? Warning);

/// <summary>
///     A parsed <c>font</c> argument: a family name and the requested bold and italic flags.
/// </summary>
/// <param name="Family">The family name; empty when the argument names no family (the default font is then used).</param>
/// <param name="Bold">Whether a bold face is requested.</param>
/// <param name="Italic">Whether an italic (or oblique) face is requested.</param>
internal readonly record struct ScadFontSpec(string Family, bool Bold, bool Italic);

/// <summary>
///     One face of a font file found in an additional font directory.
/// </summary>
/// <param name="Family">The family name of the face (the file name without its extension when the font has none).</param>
/// <param name="Bold">Whether the face is bold.</param>
/// <param name="Italic">Whether the face is italic.</param>
/// <param name="FilePath">The path of the font file.</param>
/// <param name="FaceIndex">The index of the face within the file (zero for a file that is not a collection).</param>
internal readonly record struct ScadFontFace(string Family, bool Bold, bool Italic, string FilePath, int FaceIndex);

/// <summary>
///     Resolves the <c>font</c> argument of <c>text()</c> to a font, deterministically and without ever failing.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a resolver.</b> Font discovery scans directories and loading a font parses a file, so both are
///         done at most once per build: the resolver is owned by the build context, remembers the faces found in the
///         additional directories and every font it has loaded, and answers repeated requests from memory. It uses
///         only the public font API of the core package: <see cref="TrueTypeFont"/> to read the files of the
///         additional directories (so that a file that does not parse is skipped), and
///         <see cref="SystemFontCatalog"/> for the installed fonts and the bundled fonts.
///     </para>
///     <para>
///         <b>Order.</b> No font argument (or one with no family) is the bundled Liberation Sans Regular (or the
///         requested style of it) whatever the host has, which keeps default text reproducible. A named family is
///         looked up in the additional directories, then (when enabled) the installed fonts, and then among the
///         bundled families (<c>Liberation Sans</c>, <c>Liberation Serif</c>, <c>Liberation Mono</c> and the generic
///         names <c>sans-serif</c>, <c>serif</c> and <c>monospace</c>). Only an exact family match counts (ignoring
///         case): the generic substitution of the catalog is deliberately not used, because it would make the result
///         depend on what the host has installed. Within a family the face whose bold and italic flags differ least
///         from the request wins, and the first one found wins a tie. A family that is found nowhere resolves to the
///         bundled sans font with a warning; so does a face that was found but cannot be loaded.
///     </para>
///     <para>
///         <b>Additional directories.</b> Each directory is searched recursively (a link to another directory is not
///         followed) for <c>.ttf</c>, <c>.otf</c>, <c>.ttc</c> and <c>.otc</c> files, in the order of the directories
///         and, within a directory, in ordinal order of the full path, so the result never depends on the order the
///         file system lists files in. A missing or unreadable directory, and a file or face that does not parse, are
///         skipped without a diagnostic.
///     </para>
///     <para>
///         The resolver is used by one build on one thread and holds no state that outlives it.
///     </para>
/// </remarks>
internal sealed class ScadFontResolver
{
    /// <summary>The family name of the bundled sans-serif font, the default.</summary>
    private const string SansFamily = "Liberation Sans";

    /// <summary>The family name of the bundled serif font.</summary>
    private const string SerifFamily = "Liberation Serif";

    /// <summary>The family name of the bundled monospace font.</summary>
    private const string MonoFamily = "Liberation Mono";

    /// <summary>The options of the build.</summary>
    private readonly ScadFontResolutionOptions _options;

    /// <summary>The resolutions already made, by request.</summary>
    private readonly Dictionary<ScadFontSpec, ScadResolvedFont> _resolved = [];

    /// <summary>The fonts loaded from files, by path and face index.</summary>
    private readonly Dictionary<(string Path, int Face), TrueTypeFont?> _loaded = [];

    /// <summary>The faces of the additional directories; <see langword="null"/> until the first lookup.</summary>
    private IReadOnlyList<ScadFontFace>? _additionalFaces;

    /// <summary>
    ///     Initializes a resolver.
    /// </summary>
    /// <param name="options">The font resolution options, or <see langword="null"/> for the defaults.</param>
    public ScadFontResolver(ScadFontResolutionOptions? options)
    {
        _options = options ?? new ScadFontResolutionOptions();
    }

    /// <summary>
    ///     Parses a <c>font</c> argument of the form <c>Family</c> or <c>Family:style=Bold Italic</c>.
    /// </summary>
    /// <remarks>
    ///     The text before the first colon is the family (trimmed). Each following colon-separated field of the form
    ///     <c>key=value</c> is examined; for the key <c>style</c> (any case) the words <c>Bold</c> and <c>Italic</c>
    ///     (or <c>Oblique</c>) set the flags, in any case and separated by white space or commas, and the other words
    ///     (<c>Regular</c>, <c>Normal</c>, ...) are ignored. Every other field is ignored.
    /// </remarks>
    /// <param name="font">The argument.</param>
    /// <returns>The parsed request.</returns>
    public static ScadFontSpec ParseSpec(string font)
    {
        var fields = font.Split(':');
        var bold = false;
        var italic = false;
        for (var i = 1; i < fields.Length; i++)
        {
            var separator = fields[i].IndexOf('=');
            if (separator < 0 || !fields[i][..separator].Trim().Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var word in fields[i][(separator + 1)..].Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                bold |= word.Equals("bold", StringComparison.OrdinalIgnoreCase);
                italic |= word.Equals("italic", StringComparison.OrdinalIgnoreCase) || word.Equals("oblique", StringComparison.OrdinalIgnoreCase);
            }
        }

        return new ScadFontSpec(fields[0].Trim(), bold, italic);
    }

    /// <summary>
    ///     Resolves a request to a font.
    /// </summary>
    /// <remarks>
    ///     Never throws for a font that is missing or damaged: that resolves to the bundled sans font and a warning
    ///     for the caller to report. The result is the same every time for the same request within one build.
    /// </remarks>
    /// <param name="spec">The request; an empty family means the default font.</param>
    /// <returns>The font, where it came from, and the warning to report if any.</returns>
    public ScadResolvedFont Resolve(ScadFontSpec spec)
    {
        if (_resolved.TryGetValue(spec, out var known))
        {
            return known;
        }

        var result = ResolveUncached(spec);
        _resolved[spec] = result;
        return result;
    }

    /// <summary>
    ///     Resolves a request that is not in the cache.
    /// </summary>
    /// <param name="spec">The request.</param>
    /// <returns>The resolution.</returns>
    private ScadResolvedFont ResolveUncached(ScadFontSpec spec)
    {
        // No family: the default font, in the requested style, from the bundle only
        if (spec.Family.Length == 0)
        {
            return new ScadResolvedFont(SystemFontCatalog.LoadBundledFallback(false, false, spec.Bold, spec.Italic), ScadFontSource.Bundled, null);
        }

        // The additional directories come first, then the installed fonts; a face that cannot be loaded is reported
        string? failure = null;
        var fromDirectories = FindInDirectories(AdditionalFaces(), spec);
        if (fromDirectories is not null)
        {
            var font = Load(fromDirectories.Value.FilePath, fromDirectories.Value.FaceIndex);
            if (font is not null)
            {
                return new ScadResolvedFont(font, ScadFontSource.AdditionalDirectory, null);
            }

            failure = $"the font file '{fromDirectories.Value.FilePath}' could not be loaded";
        }

        if (failure is null && _options.UseSystemFonts)
        {
            var installed = FindInstalled(spec);
            if (installed is not null)
            {
                var font = Load(installed.Value.FilePath, installed.Value.FaceIndex);
                if (font is not null)
                {
                    return new ScadResolvedFont(font, ScadFontSource.System, null);
                }

                failure = $"the font file '{installed.Value.FilePath}' could not be loaded";
            }
        }

        // The bundled families
        if (failure is null && TryBundled(spec, out var bundled))
        {
            return new ScadResolvedFont(bundled!, ScadFontSource.Bundled, null);
        }

        var reason = failure ?? "no such font family was found";
        return new ScadResolvedFont(
            SystemFontCatalog.LoadBundledFallback(false, false, spec.Bold, spec.Italic),
            ScadFontSource.Bundled,
            $"The font '{spec.Family}' is not available ({reason}); using the bundled {SansFamily} font");
    }

    /// <summary>
    ///     Finds the face of the additional directories that has exactly the requested family, in the closest style.
    /// </summary>
    /// <param name="faces">The faces of the additional directories, in search order.</param>
    /// <param name="spec">The request.</param>
    /// <returns>The face, or <see langword="null"/> when no face has the family.</returns>
    private static ScadFontFace? FindInDirectories(IReadOnlyList<ScadFontFace> faces, ScadFontSpec spec)
    {
        // The score is the number of style flags that differ; the first face found wins a tie
        ScadFontFace? best = null;
        var bestScore = int.MaxValue;
        foreach (var face in faces)
        {
            if (!Is(face.Family, spec.Family))
            {
                continue;
            }

            var score = (face.Bold != spec.Bold ? 1 : 0) + (face.Italic != spec.Italic ? 1 : 0);
            if (score < bestScore)
            {
                best = face;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    ///     Finds the installed face that has exactly the requested family, in the closest style.
    /// </summary>
    /// <param name="spec">The request.</param>
    /// <returns>The face, or <see langword="null"/> when no installed font has the family.</returns>
    private static SystemFontInfo? FindInstalled(ScadFontSpec spec)
    {
        // The catalog may answer with a generic substitute when the family is absent; only the family itself counts
        var match = SystemFontCatalog.FindBestMatch(spec.Family, spec.Bold, spec.Italic, serif: false, fixedPitch: false);
        return match is not null && Is(match.Value.FamilyName, spec.Family) ? match : null;
    }

    /// <summary>
    ///     Gets the faces of the additional directories, scanning them on the first call.
    /// </summary>
    /// <returns>The faces; empty when there are no directories.</returns>
    private IReadOnlyList<ScadFontFace> AdditionalFaces()
    {
        _additionalFaces ??= ScanDirectories(_options.AdditionalDirectories);
        return _additionalFaces;
    }

    /// <summary>
    ///     Reads the faces of the font files of a list of directories.
    /// </summary>
    /// <param name="directories">The directories, in search order.</param>
    /// <returns>The faces, in the order of the directories and then of the ordinal order of the file paths.</returns>
    private static List<ScadFontFace> ScanDirectories(IReadOnlyList<string> directories)
    {
        var faces = new List<ScadFontFace>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            // Sorted so that the result does not depend on the order the file system lists the files in
            var files = new List<string>();
            CollectFontFiles(directory, files);
            files.Sort(StringComparer.Ordinal);
            foreach (var file in files.Where(seen.Add))
            {
                AddFaces(file, faces);
            }
        }

        return faces;
    }

    /// <summary>
    ///     Collects the font files of a directory and of its subdirectories (except links), skipping what cannot be read.
    /// </summary>
    /// <param name="root">The directory; nothing is collected when it does not exist.</param>
    /// <param name="files">The list to add the paths to.</param>
    private static void CollectFontFiles(string root, List<string> files)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                files.AddRange(Directory.GetFiles(directory).Where(IsFontFile));
                // A link can point back at an ancestor, so links are not followed
                foreach (var subdirectory in Directory.GetDirectories(directory).Where(d => !new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.ReparsePoint)))
                {
                    pending.Push(subdirectory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A directory that cannot be listed contributes nothing
            }
        }
    }

    /// <summary>
    ///     Reports whether a file name has the extension of a font file the font loader supports.
    /// </summary>
    /// <param name="file">The file path.</param>
    /// <returns><see langword="true"/> for <c>.ttf</c>, <c>.otf</c>, <c>.ttc</c> and <c>.otc</c> in any case.</returns>
    private static bool IsFontFile(string file)
    {
        var extension = Path.GetExtension(file);
        return Is(extension, ".ttf") || Is(extension, ".otf") || Is(extension, ".ttc") || Is(extension, ".otc");
    }

    /// <summary>
    ///     Reads the faces of one font file, skipping the file or the face when it does not parse.
    /// </summary>
    /// <param name="file">The font file.</param>
    /// <param name="faces">The list to add the faces to.</param>
    private static void AddFaces(string file, List<ScadFontFace> faces)
    {
        int count;
        try
        {
            count = TrueTypeFont.GetFaceCount(file);
        }
        catch (Exception ex) when (IsSkippable(ex))
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            try
            {
                var font = TrueTypeFont.Load(file, index);
                var family = font.GetNameInfo().FamilyName;
                faces.Add(new ScadFontFace(
                    string.IsNullOrEmpty(family) ? Path.GetFileNameWithoutExtension(file) : family,
                    font.IsBold,
                    font.IsItalic,
                    file,
                    index));
            }
            catch (Exception ex) when (IsSkippable(ex))
            {
                // A face that does not parse is not offered
            }
        }
    }

    /// <summary>
    ///     Reports whether an exception means that a font file is missing, unreadable or damaged.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns><see langword="true"/> when the file should be skipped (or reported) rather than the build failed.</returns>
    private static bool IsSkippable(Exception ex) =>
        ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException;

    /// <summary>
    ///     Loads a face, remembering the outcome (a face that failed to load is not tried again).
    /// </summary>
    /// <param name="path">The font file.</param>
    /// <param name="faceIndex">The index of the face in the file.</param>
    /// <returns>The font, or <see langword="null"/> when it cannot be loaded.</returns>
    private TrueTypeFont? Load(string path, int faceIndex)
    {
        var key = (path, faceIndex);
        if (_loaded.TryGetValue(key, out var cached))
        {
            return cached;
        }

        TrueTypeFont? font;
        try
        {
            font = TrueTypeFont.Load(path, faceIndex);
        }
        catch (Exception ex) when (IsSkippable(ex))
        {
            // A damaged, vanished or unreadable file is the model author's problem to hear about, not a crash
            font = null;
        }

        _loaded[key] = font;
        return font;
    }

    /// <summary>
    ///     Maps a family name to one of the bundled fonts.
    /// </summary>
    /// <param name="spec">The request.</param>
    /// <param name="font">The bundled font when the family is one of the bundled ones.</param>
    /// <returns><see langword="true"/> when the family names a bundled family or a generic family.</returns>
    private static bool TryBundled(ScadFontSpec spec, out TrueTypeFont? font)
    {
        font = null;
        var family = spec.Family;
        bool serif;
        bool mono;
        if (Is(family, SansFamily) || Is(family, "sans-serif") || Is(family, "sans"))
        {
            serif = false;
            mono = false;
        }
        else if (Is(family, SerifFamily) || Is(family, "serif"))
        {
            serif = true;
            mono = false;
        }
        else if (Is(family, MonoFamily) || Is(family, "monospace") || Is(family, "mono"))
        {
            serif = false;
            mono = true;
        }
        else
        {
            return false;
        }

        font = SystemFontCatalog.LoadBundledFallback(serif, mono, spec.Bold, spec.Italic);
        return true;
    }

    /// <summary>
    ///     Compares two names ignoring case.
    /// </summary>
    /// <param name="left">The first name.</param>
    /// <param name="right">The second name.</param>
    /// <returns><see langword="true"/> when they are equal ignoring case.</returns>
    private static bool Is(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
