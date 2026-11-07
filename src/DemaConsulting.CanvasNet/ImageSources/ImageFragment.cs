// cspell:ignore href hrefs
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     One parameter of the fragment of a link: a decoded name and value.
/// </summary>
/// <param name="Name">The name, percent-decoded, exactly as written (names are compared ASCII case-insensitively).</param>
/// <param name="Value">The value, percent-decoded; empty when the parameter had no <c>=</c> or nothing after it.</param>
public readonly record struct ImageFragmentParameter(string Name, string Value);

/// <summary>
///     A note about the parameters of the fragment of a link, from <see cref="ImageFragment.ReviewParameters(string, string, IImageSource)"/>.
/// </summary>
/// <param name="Code">
///     The stable code: <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> or
///     <see cref="FolderImageResolver.FragmentDuplicateParameterCode"/>.
/// </param>
/// <param name="Message">The description, which names the link and the parameter.</param>
public readonly record struct ImageFragmentNote(string Code, string Message);

/// <summary>
///     The fragment of a link (the text after the first <c>#</c>), parsed into ordered parameters in the style of the
///     <c>#page=3</c> fragments of RFC 8118 (the PDF media type), so a link can choose which page of a document to draw.
/// </summary>
/// <remarks>
///     <para>
///         <b>Grammar.</b> The fragment is a list of parameters separated by <c>&amp;</c>; a parameter is a name and an
///         optional value separated by the <b>first</b> <c>=</c> of the parameter (no <c>=</c> means the value is
///         empty). Empty segments (<c>&amp;&amp;</c>, a leading or trailing <c>&amp;</c>) are skipped. The text is split
///         first and percent-decoded afterwards (<see cref="Uri.UnescapeDataString(string)"/>), so <c>a=%26b</c> is the
///         value <c>&amp;b</c> and <c>%3D</c> in a name is a literal <c>=</c> of the name; a <c>+</c> stays a <c>+</c>,
///         and an invalid <c>%</c> sequence stays as written. Names are matched ASCII case-insensitively, values are
///         compared as written, and the order is kept.
///     </para>
///     <para>
///         <b>Limits.</b> A link is untrusted text, so a fragment longer than <see cref="MaxLength"/> characters, or with
///         more than <see cref="MaxParameters"/> parameters, is not parsed: <see cref="Problem"/> says why, and
///         <see cref="FolderImageResolver"/> refuses the link with <see cref="FolderImageResolver.FragmentInvalidCode"/>.
///     </para>
///     <para>
///         <b>Duplicates.</b> When a name occurs more than once, <see cref="GetValue(string[])"/> returns the last
///         occurrence (last wins), and when several names are given (aliases such as <c>page</c> and <c>slide</c>) the
///         last occurrence of any of them wins.
///     </para>
///     <para>
///         The class is immutable and thread-safe. It never throws from <see cref="Parse(string?)"/>.
///     </para>
///     <example>
///         <code language="csharp">
///         var fragment = ImageFragment.Parse("page=3&amp;view=front");
///         int? page = fragment.GetPositiveInt32("page");     // 3
///         string? view = fragment.GetValue("VIEW");          // "front" (names are case-insensitive)
///         </code>
///     </example>
/// </remarks>
public sealed class ImageFragment
{
    /// <summary>
    ///     The longest fragment, in characters as written (before decoding), that is parsed: 2048.
    /// </summary>
    public const int MaxLength = 2048;

    /// <summary>
    ///     The largest number of parameters a fragment may have: 32.
    /// </summary>
    public const int MaxParameters = 32;

    /// <summary>
    ///     The longest piece of untrusted text that is quoted in a message, in characters.
    /// </summary>
    private const int MaxQuoted = 40;

    /// <summary>
    ///     The longest link that is quoted in a message, in characters.
    /// </summary>
    private const int MaxQuotedLink = 120;

    /// <summary>
    ///     The parameters, decoded, in the order written.
    /// </summary>
    private readonly IReadOnlyList<ImageFragmentParameter> _parameters;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageFragment"/> class.
    /// </summary>
    /// <param name="raw">The fragment as written.</param>
    /// <param name="parameters">The decoded parameters, in order.</param>
    /// <param name="problem">The reason the fragment cannot be used, or <see langword="null"/>.</param>
    private ImageFragment(string raw, ImageFragmentParameter[] parameters, string? problem)
    {
        Raw = raw;
        _parameters = Array.AsReadOnly(parameters);
        Problem = problem;
    }

    /// <summary>
    ///     Gets the fragment that a link without a <c>#</c> has: no text and no parameters.
    /// </summary>
    public static ImageFragment Empty { get; } = new(string.Empty, [], null);

    /// <summary>
    ///     Gets the fragment as written, after the first <c>#</c>, still percent-encoded; empty when there is none.
    /// </summary>
    public string Raw { get; }

    /// <summary>
    ///     Gets the parameters, decoded, in the order they were written; empty when <see cref="Problem"/> is set.
    /// </summary>
    public IReadOnlyList<ImageFragmentParameter> Parameters => _parameters;

    /// <summary>
    ///     Gets the reason the fragment was not parsed (it is longer than <see cref="MaxLength"/> characters or has more
    ///     than <see cref="MaxParameters"/> parameters), or <see langword="null"/> when it was.
    /// </summary>
    public string? Problem { get; }

    /// <summary>
    ///     Parses the text after the first <c>#</c> of a link.
    /// </summary>
    /// <param name="raw">The fragment as written, still percent-encoded, without the <c>#</c>; null counts as empty.</param>
    /// <returns>
    ///     The parsed fragment; <see cref="Problem"/> is set (and there are no parameters) when a limit is exceeded.
    ///     Parsing never throws.
    /// </returns>
    public static ImageFragment Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return Empty;
        }

        // The limits come first: a hostile link must cost nothing to refuse
        if (raw.Length > MaxLength)
        {
            return new ImageFragment(raw, [], $"the fragment is longer than {MaxLength.ToString(CultureInfo.InvariantCulture)} characters");
        }

        // Split on '&' in the raw text, then decode each part: splitting first keeps an encoded %26 inside its value
        var parameters = new List<ImageFragmentParameter>();
        foreach (var segment in raw.Split('&'))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            if (parameters.Count == MaxParameters)
            {
                return new ImageFragment(raw, [], $"the fragment has more than {MaxParameters.ToString(CultureInfo.InvariantCulture)} parameters");
            }

            var equals = segment.IndexOf('=');
            var name = equals < 0 ? segment : segment[..equals];
            var value = equals < 0 ? string.Empty : segment[(equals + 1)..];
            parameters.Add(new ImageFragmentParameter(Decode(name), Decode(value)));
        }

        return new ImageFragment(raw, [.. parameters], null);
    }

    /// <summary>
    ///     Gets the value of a parameter, the last occurrence of any of the given names.
    /// </summary>
    /// <param name="names">The names to look for, ASCII case-insensitively; they are aliases of each other.</param>
    /// <returns>The decoded value of the last matching parameter, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="names"/> is null.</exception>
    public string? GetValue(params string[] names)
    {
        return FindLast(names)?.Value;
    }

    /// <summary>
    ///     Gets the value of a parameter as a whole number of 1 or more, the last occurrence of any of the given names.
    /// </summary>
    /// <param name="names">The names to look for, ASCII case-insensitively; they are aliases of each other.</param>
    /// <returns>The number, or <see langword="null"/> when no parameter has one of the names.</returns>
    /// <remarks>
    ///     Only ASCII digits are accepted (leading zeros are allowed: <c>003</c> is 3); a sign, a space, a decimal
    ///     point, an empty value, zero and a value above <see cref="int.MaxValue"/> are all invalid.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="names"/> is null.</exception>
    /// <exception cref="ImageSourceRefusedException">
    ///     Thrown, with the code <see cref="FolderImageResolver.FragmentInvalidCode"/>, when the value is not a whole
    ///     number from 1 to <see cref="int.MaxValue"/>.
    /// </exception>
    public int? GetPositiveInt32(params string[] names)
    {
        var found = FindLast(names);
        if (found is not { } parameter)
        {
            return null;
        }

        // Only ASCII digits are a number here (int.TryParse with NumberStyles.None would also reject a sign or a space)
        var value = parameter.Value;
        if (value.Length > 0
            && value.All(static c => c is >= '0' and <= '9')
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number >= 1)
        {
            return number;
        }

        throw new ImageSourceRefusedException(
            FolderImageResolver.FragmentInvalidCode,
            $"the fragment parameter {Quote(parameter.Name)} must be a whole number of 1 or more, not {Quote(parameter.Value)}");
    }

    /// <summary>
    ///     Gets the parameters whose name starts with a prefix, with the prefix removed from the name.
    /// </summary>
    /// <param name="prefix">The prefix, for example <c>def.</c>, matched ASCII case-insensitively; not empty.</param>
    /// <returns>
    ///     The parameters in the order their names first appear. A name is what follows the prefix, as written (it is not
    ///     case-folded), and a parameter that has nothing after the prefix is not included. When a name occurs more than
    ///     once the last value is returned (last wins), in the position of its first occurrence.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is null or empty.</exception>
    /// <example>
    ///     <code language="csharp">
    ///     var fragment = ImageFragment.Parse("view=top&amp;def.width=20&amp;def.label=%22A%22");
    ///     foreach (var definition in fragment.GetPrefixed("def."))
    ///     {
    ///         Console.WriteLine($"{definition.Name} = {definition.Value}");   // width = 20, then label = "A"
    ///     }
    ///     </code>
    /// </example>
    public IReadOnlyList<ImageFragmentParameter> GetPrefixed(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        var order = new List<string>();
        var last = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in _parameters)
        {
            if (!HasPrefix(parameter.Name, prefix))
            {
                continue;
            }

            var name = parameter.Name[prefix.Length..];
            if (!last.ContainsKey(name))
            {
                order.Add(name);
            }

            last[name] = parameter.Value;
        }

        return [.. order.Select(name => new ImageFragmentParameter(name, last[name]))];
    }

    /// <summary>
    ///     Tells whether a parameter name starts with a prefix and has at least one more character.
    /// </summary>
    /// <param name="name">The decoded name of a parameter.</param>
    /// <param name="prefix">The prefix, matched ASCII case-insensitively.</param>
    /// <returns><see langword="true"/> when the name is the prefix followed by something.</returns>
    internal static bool HasPrefix(string name, string prefix)
    {
        return name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Reviews the parameters of the fragment of a link against what its source declares: a parameter the source does not
    ///     use, and a parameter that is given more than once with different values.
    /// </summary>
    /// <param name="subject">What the link is called in a message, for example <c>image</c> or <c>input</c>.</param>
    /// <param name="href">The link as written; it is quoted in the messages (control characters replaced, cut at 120 characters).</param>
    /// <param name="source">The source that draws the link; its <see cref="IFragmentImageSource"/> declaration (if any) says which names it uses.</param>
    /// <returns>
    ///     The notes, each with the code <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> or
    ///     <see cref="FolderImageResolver.FragmentDuplicateParameterCode"/>, in the order of the first appearance of each
    ///     name; empty when the fragment has no parameter or every parameter is understood once.
    /// </returns>
    /// <remarks>
    ///     This is the check <see cref="FolderImageResolver"/> makes for a link in an SVG document, so a command line and a
    ///     link report the same mistakes with the same codes. A name is understood when it is declared
    ///     (<see cref="IFragmentImageSource.FragmentParameters"/>, ASCII case-insensitively) or starts with a declared
    ///     prefix. Each name is reported once; a name that is unknown is not also reported as a duplicate; aliases of one
    ///     source (<c>page</c> and <c>slide</c>) are never duplicates of each other; what follows a prefix is compared as written.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public IReadOnlyList<ImageFragmentNote> ReviewParameters(string subject, string href, IImageSource source)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(href);
        ArgumentNullException.ThrowIfNull(source);

        var notes = new List<ImageFragmentNote>();
        var declared = source as IFragmentImageSource;
        var names = declared?.FragmentParameters ?? [];
        var prefixes = (declared?.FragmentParameterPrefixes ?? []).Where(p => !string.IsNullOrEmpty(p)).ToArray();
        foreach (var group in _parameters.GroupBy(p => GroupKey(p.Name, prefixes), StringComparer.Ordinal))
        {
            // The name as first written; a prefixed name is understood whatever follows its prefix
            var name = group.First().Name;
            var understood = prefixes.Any(prefix => HasPrefix(name, prefix))
                             || names.Any(n => n is not null && string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (!understood)
            {
                notes.Add(
                    new ImageFragmentNote(
                        FolderImageResolver.FragmentUnknownParameterCode,
                        $"{subject} {QuoteLink(href)}: the fragment parameter {Quote(name)} is not used by the '{source.Name}' source and is ignored"));
            }
            else if (group.Select(p => p.Value).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                notes.Add(
                    new ImageFragmentNote(
                        FolderImageResolver.FragmentDuplicateParameterCode,
                        $"{subject} {QuoteLink(href)}: the fragment parameter {Quote(name)} is given more than once with different values; the last one, {Quote(group.Last().Value)}, is used"));
            }
        }

        return notes;
    }

    /// <summary>
    ///     Gets the key that makes two parameter names the same parameter: the whole name compared ASCII
    ///     case-insensitively, except that what follows a declared prefix is compared as written.
    /// </summary>
    /// <param name="name">The decoded name of a parameter.</param>
    /// <param name="prefixes">The prefixes the source declares.</param>
    /// <returns>The key.</returns>
    private static string GroupKey(string name, string[] prefixes)
    {
        var prefix = prefixes.FirstOrDefault(p => HasPrefix(name, p));
        return prefix is null
            ? "n:" + name.ToUpperInvariant()
            : "p:" + prefix.ToUpperInvariant() + name[prefix.Length..];
    }

    /// <summary>
    ///     Quotes a piece of untrusted text for a message: control characters are replaced and a long text is cut.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text in single quotes, at most 40 characters long inside the quotes, free of control characters.</returns>
    internal static string Quote(string text)
    {
        return Quote(text, MaxQuoted);
    }

    /// <summary>
    ///     Quotes a link for a message the way <see cref="Quote(string)"/> does, but allows a longer text, because a
    ///     legitimate link can be long.
    /// </summary>
    /// <param name="href">The link as written.</param>
    /// <returns>The link in single quotes, at most 120 characters long inside the quotes, free of control characters.</returns>
    internal static string QuoteLink(string href)
    {
        return Quote(href, MaxQuotedLink);
    }

    /// <summary>
    ///     Quotes a piece of untrusted text for a message.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The most characters of the text to show.</param>
    /// <returns>The quoted text.</returns>
    private static string Quote(string text, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(text.Length, maxLength) + 5);
        builder.Append('\'');
        foreach (var c in text.Take(maxLength))
        {
            builder.Append(char.IsControl(c) ? '?' : c);
        }

        if (text.Length > maxLength)
        {
            builder.Append("...");
        }

        return builder.Append('\'').ToString();
    }

    /// <summary>
    ///     Finds the last parameter that has one of the names.
    /// </summary>
    /// <param name="names">The names, matched ASCII case-insensitively.</param>
    /// <returns>The parameter, or <see langword="null"/>.</returns>
    private ImageFragmentParameter? FindLast(string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        for (var i = _parameters.Count - 1; i >= 0; i--)
        {
            if (names.Any(name => string.Equals(name, _parameters[i].Name, StringComparison.OrdinalIgnoreCase)))
            {
                return _parameters[i];
            }
        }

        return null;
    }

    /// <summary>
    ///     Percent-decodes a name or value.
    /// </summary>
    /// <param name="text">The text as written.</param>
    /// <returns>The decoded text; text without a <c>%</c> is returned as it is.</returns>
    private static string Decode(string text)
    {
        return text.Contains('%') ? Uri.UnescapeDataString(text) : text;
    }
}
