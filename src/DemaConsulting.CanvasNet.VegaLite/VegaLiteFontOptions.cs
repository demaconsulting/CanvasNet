namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     Controls where text in a chart finds its fonts.
/// </summary>
/// <remarks>
///     <para>
///         Without any options a chart uses the Liberation Sans, Serif and Mono fonts bundled with the core package
///         (the generic and Arial-like families of a specification map to them), so a chart renders identically on
///         every machine, whatever fonts are installed. These options only matter for a specification that names a
///         font family explicitly: the family is searched in <see cref="AdditionalDirectories"/> first, then, when
///         <see cref="UseSystemFonts"/> is <see langword="true"/>, in the fonts installed on the host, and finally
///         among the bundled families.
///     </para>
///     <para>
///         The instance is immutable after initialization (the directory list is copied when it is set, so later
///         changes to the caller's list have no effect) and can be shared between threads and calls.
///     </para>
/// </remarks>
public sealed class VegaLiteFontOptions
{
    /// <summary>The backing field of <see cref="AdditionalDirectories"/>; a read-only wrapper over a private copy, never <see langword="null"/>.</summary>
    private readonly IReadOnlyList<string> _additionalDirectories = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>
    ///     Gets a value indicating whether the fonts installed on the host are searched for a font family that a
    ///     specification names explicitly; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    ///     The default is <see langword="false"/> so that output is reproducible: the bundled fonts and the
    ///     <see cref="AdditionalDirectories"/> are then the only candidates.
    /// </remarks>
    public bool UseSystemFonts { get; init; }

    /// <summary>
    ///     Gets extra directories that are scanned for fonts before the installed fonts; empty by default.
    /// </summary>
    /// <remarks>
    ///     A directory that does not exist or cannot be read is skipped silently. The list is copied when it is set,
    ///     so the instance does not change when the caller's list does.
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
