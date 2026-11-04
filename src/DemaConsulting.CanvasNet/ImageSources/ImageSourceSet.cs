// cspell:ignore hrefs
using System.Collections;
using System.Collections.ObjectModel;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     An ordered, immutable list of <see cref="IImageSource"/> instances that knows which source claims which file name
///     extension or URI scheme.
/// </summary>
/// <remarks>
///     <para>
///         <b>Precedence: the later source wins.</b> When two sources claim the same extension (or the same scheme),
///         the one that comes later in the sequence handles it. Put the built-in sources first and your own after them
///         to override a built-in format or to add a new one:
///         <c>new ImageSourceSet(builtIns).Add(mySource)</c>. The same rule applies to
///         <see cref="FolderImageResolver"/>, which builds an <see cref="ImageSourceSet"/> from the sequence it is given.
///     </para>
///     <para>
///         The set validates what every source claims when it is built, so a bad claim fails at once, at the place that
///         composed the set, with the name of the source in the message. The set is immutable: <see cref="Add"/> returns
///         a new set, and a set can be shared by any number of resolvers and threads. There is no global registry and no
///         discovery: a source is in a set only if the caller put it there.
///     </para>
/// </remarks>
public sealed class ImageSourceSet : IReadOnlyList<IImageSource>
{
    /// <summary>
    ///     The scheme names that sources may not claim: <c>file</c> is always a path inside the folder (handled by the
    ///     resolver) and <c>data</c> is decoded by the SVG codec itself.
    /// </summary>
    private static readonly string[] ReservedSchemes = ["file", "data"];

    /// <summary>
    ///     The sources in the order they were given.
    /// </summary>
    private readonly IImageSource[] _sources;

    /// <summary>
    ///     The winning source of every claimed extension (lower case, with the dot).
    /// </summary>
    private readonly Dictionary<string, IImageSource> _byExtension = new(StringComparer.Ordinal);

    /// <summary>
    ///     The winning source of every claimed scheme (lower case).
    /// </summary>
    private readonly Dictionary<string, IImageSource> _byScheme = new(StringComparer.Ordinal);

    /// <summary>
    ///     The claimed extensions, longest first, so the first match on a file name is the longest.
    /// </summary>
    private readonly string[] _extensionsLongestFirst;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageSourceSet"/> class.
    /// </summary>
    /// <param name="sources">
    ///     The sources, in order of increasing precedence: for an extension or scheme claimed by more than one source, the
    ///     later one wins. May be empty. Must not contain null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sources"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a source is null, has no name, returns a null list, claims an extension that is not a dot followed by
    ///     a name without path separators, or claims a scheme that is not valid (see <see cref="IImageSource.Schemes"/>)
    ///     or is <c>file</c> or <c>data</c>.
    /// </exception>
    public ImageSourceSet(IEnumerable<IImageSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = [.. sources];

        // Later sources overwrite earlier ones, which is the precedence rule
        foreach (var source in _sources)
        {
            if (source is null)
            {
                throw new ArgumentException("An image source must not be null.", nameof(sources));
            }

            if (string.IsNullOrWhiteSpace(source.Name))
            {
                throw new ArgumentException("An image source must have a name.", nameof(sources));
            }

            foreach (var extension in source.Extensions ?? throw NullList(source, nameof(source.Extensions)))
            {
                _byExtension[NormalizeExtension(source, extension)] = source;
            }

            foreach (var scheme in source.Schemes ?? throw NullList(source, nameof(source.Schemes)))
            {
                _byScheme[NormalizeScheme(source, scheme)] = source;
            }
        }

        _extensionsLongestFirst = [.. _byExtension.Keys.OrderByDescending(e => e.Length).ThenBy(e => e, StringComparer.Ordinal)];
        Extensions = new ReadOnlyCollection<string>([.. _byExtension.Keys.Order(StringComparer.Ordinal)]);
        Schemes = new ReadOnlyCollection<string>([.. _byScheme.Keys.Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    ///     Gets every extension claimed by any source (lower case, with the dot), sorted in ordinal order and without duplicates.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>
    ///     Gets every scheme claimed by any source (lower case, without the colon), sorted in ordinal order and without
    ///     duplicates.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; }

    /// <summary>
    ///     Gets the number of sources in the set.
    /// </summary>
    public int Count => _sources.Length;

    /// <summary>
    ///     Gets the source at a position; the later a source is, the higher its precedence.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public IImageSource this[int index] => _sources[index];

    /// <summary>
    ///     Creates a new set with one more source at the end, so it takes precedence over every source already in the set.
    /// </summary>
    /// <param name="source">The source to add. Must satisfy the rules of the constructor.</param>
    /// <returns>A new set; this set is not changed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the source makes an invalid claim (see the constructor).</exception>
    public ImageSourceSet Add(IImageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ImageSourceSet([.. _sources, source]);
    }

    /// <summary>
    ///     Finds the source that handles a file name: the one that claims the longest extension the name ends with.
    /// </summary>
    /// <param name="fileName">A file name or a path; only the part after the last slash or backslash is looked at.</param>
    /// <returns>The source, or <see langword="null"/> when no claimed extension matches (case-insensitively).</returns>
    public IImageSource? FindByExtension(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = fileName[(fileName.AsSpan().LastIndexOfAny('/', '\\') + 1)..];
        var claimed = _extensionsLongestFirst.FirstOrDefault(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        return claimed is null ? null : _byExtension[claimed];
    }

    /// <summary>
    ///     Finds the source that claims a URI scheme.
    /// </summary>
    /// <param name="scheme">The scheme, without the colon; compared case-insensitively.</param>
    /// <returns>The source, or <see langword="null"/> when no source claims the scheme.</returns>
    public IImageSource? FindByScheme(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return _byScheme.GetValueOrDefault(scheme.ToLowerInvariant());
    }

    /// <inheritdoc />
    public IEnumerator<IImageSource> GetEnumerator()
    {
        return ((IEnumerable<IImageSource>)_sources).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Finds the scheme of a link: the letters, digits, <c>+</c>, <c>-</c> and <c>.</c> before the first colon, when
    ///     they start with a letter (RFC 3986).
    /// </summary>
    /// <param name="href">The link.</param>
    /// <returns>
    ///     The scheme in lower case, or <see langword="null"/> when the link has none. A single letter followed by a colon is
    ///     a Windows drive, not a scheme, so it also gives <see langword="null"/> (the path is then refused as an absolute
    ///     path).
    /// </returns>
    internal static string? GetScheme(string href)
    {
        if (href.Length < 2 || !char.IsAsciiLetter(href[0]))
        {
            return null;
        }

        for (var i = 1; i < href.Length; i++)
        {
            var c = href[i];
            if (c == ':')
            {
                return i == 1 ? null : href[..i].ToLowerInvariant();
            }

            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     Validates a claimed extension and puts it in its canonical form.
    /// </summary>
    /// <param name="source">The source that claims it, for the message.</param>
    /// <param name="extension">The claimed extension.</param>
    /// <returns>The extension in lower case.</returns>
    /// <exception cref="ArgumentException">Thrown when the extension is not a dot followed by a name without separators.</exception>
    private static string NormalizeExtension(IImageSource source, string? extension)
    {
        if (extension is null
            || extension.Length < 2
            || extension[0] != '.'
            || extension.AsSpan().IndexOfAny('/', '\\', ':') >= 0
            || extension.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                $"The image source '{source.Name}' claims the extension '{extension}', which is not a dot followed by a name (for example '.png' or '.vl.json').",
                nameof(source));
        }

        return extension.ToLowerInvariant();
    }

    /// <summary>
    ///     Validates a claimed scheme and puts it in its canonical form.
    /// </summary>
    /// <param name="source">The source that claims it, for the message.</param>
    /// <param name="scheme">The claimed scheme.</param>
    /// <returns>The scheme in lower case.</returns>
    /// <exception cref="ArgumentException">Thrown when the scheme is not valid, is a single letter, or is reserved.</exception>
    private static string NormalizeScheme(IImageSource source, string? scheme)
    {
        // Appending the colon lets GetScheme do the syntax check, so a claim and a link follow one definition
        var valid = scheme is not null && GetScheme(scheme + ":") == scheme.ToLowerInvariant();
        if (!valid)
        {
            throw new ArgumentException(
                $"The image source '{source.Name}' claims the scheme '{scheme}', which is not a URI scheme of two or more characters (letters, digits, '+', '-' and '.', starting with a letter, without the colon).",
                nameof(source));
        }

        var lower = scheme!.ToLowerInvariant();
        if (Array.IndexOf(ReservedSchemes, lower) >= 0)
        {
            throw new ArgumentException(
                $"The image source '{source.Name}' claims the scheme '{scheme}', which is reserved: 'file' is always a path inside the folder and 'data' is decoded by the SVG codec.",
                nameof(source));
        }

        return lower;
    }

    /// <summary>
    ///     Creates the exception for a source that returned a null list.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="property">The name of the property.</param>
    /// <returns>The exception to throw.</returns>
    private static ArgumentException NullList(IImageSource source, string property)
    {
        return new ArgumentException($"The image source '{source.Name}' returned null for {property}; it must return a list (empty if it claims nothing).", nameof(source));
    }
}
