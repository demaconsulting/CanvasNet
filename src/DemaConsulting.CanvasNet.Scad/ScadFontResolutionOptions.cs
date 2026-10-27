namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     Controls where the <c>text()</c> module finds the font named by its <c>font</c> argument.
/// </summary>
/// <remarks>
///     <para>
///         Without a <c>font</c> argument <c>text()</c> always uses the Liberation Sans Regular font bundled with
///         the core package, whatever these options say, so a model that does not name a font renders identically on
///         every machine. These options only matter for an explicit <c>font = "Family"</c> or
///         <c>font = "Family:style=Bold Italic"</c>: the family is searched in <see cref="AdditionalDirectories"/>
///         first, then (when <see cref="UseSystemFonts"/> is <see langword="true"/>) in the fonts installed on the host,
///         and finally among the bundled Liberation Sans, Serif and Mono families. Set
///         <see cref="UseSystemFonts"/> to <see langword="false"/> to make explicit font requests independent of the
///         host (the bundled fonts and the additional directories are then the only candidates), which is what a
///         build that must be reproducible should do.
///     </para>
///     <para>
///         The instance is immutable after initialization (the directory list is copied when it is set, so later
///         changes to the caller's list have no effect) and can be shared between threads and calls.
///     </para>
/// </remarks>
public sealed class ScadFontResolutionOptions
{
    /// <summary>The backing field of <see cref="AdditionalDirectories"/>; a read-only wrapper over a private copy, never <see langword="null"/>.</summary>
    private readonly IReadOnlyList<string> _additionalDirectories = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>
    ///     Gets a value indicating whether the fonts installed on the host are searched for an explicit
    ///     <c>font</c> argument; <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    ///     The installed fonts are found by a directory scan of the well-known font folders of the operating system
    ///     and are scanned at most once per process, the first time a model asks for a font that is not found in
    ///     <see cref="AdditionalDirectories"/>. A model without a <c>font</c> argument never triggers the scan.
    /// </remarks>
    public bool UseSystemFonts { get; init; } = true;

    /// <summary>
    ///     Gets extra directories that are scanned (recursively) for fonts before the installed fonts; empty by
    ///     default.
    /// </summary>
    /// <remarks>
    ///     A directory that does not exist or cannot be read, and a file in it that is not a usable font, is skipped
    ///     silently. The list is copied when it is set, so the instance does not change when the caller's list does.
    ///     The directories are scanned once per render call, the first time an explicit font is requested.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown on initialization when an entry is <see langword="null"/>, empty or white space.</exception>
    public IReadOnlyList<string> AdditionalDirectories
    {
        get => _additionalDirectories;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            var copy = new string[value.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(value[i]))
                {
                    throw new ArgumentException($"The directory at index {i} must not be null, empty or white space.", nameof(AdditionalDirectories));
                }

                copy[i] = value[i];
            }

            _additionalDirectories = Array.AsReadOnly(copy);
        }
    }
}
