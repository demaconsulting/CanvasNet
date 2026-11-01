namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     The bounds <see cref="FontDirectoryScanner.Scan"/> keeps one scan within, so a huge or hostile
///     directory tree cannot make it run for an unbounded time or build an unbounded result.
/// </summary>
/// <remarks>
///     <para>
///         The scan stops at the first bound it reaches and keeps what it found before that point: the
///         part of a tree it reads is deterministic, because directories are visited in ordinal order.
///         Reaching a bound is not an error.
///     </para>
///     <para>
///         <b>Thread safety</b>: instances are immutable once constructed (the properties are
///         <see langword="init"/>-only), so one instance may be shared freely between threads.
///     </para>
/// </remarks>
public sealed record FontScanLimits
{
    /// <summary>The default value of <see cref="MaxDirectories"/>.</summary>
    private const int DefaultMaxDirectories = 10_000;

    /// <summary>The default value of <see cref="MaxEntries"/>.</summary>
    private const int DefaultMaxEntries = 200_000;

    /// <summary>The default value of <see cref="MaxFontFiles"/>.</summary>
    private const int DefaultMaxFontFiles = 5_000;

    /// <summary>The default value of <see cref="MaxFaces"/>.</summary>
    private const int DefaultMaxFaces = 20_000;

    /// <summary>Backs <see cref="MaxDirectories"/>.</summary>
    private readonly int _maxDirectories = DefaultMaxDirectories;

    /// <summary>Backs <see cref="MaxEntries"/>.</summary>
    private readonly int _maxEntries = DefaultMaxEntries;

    /// <summary>Backs <see cref="MaxFontFiles"/>.</summary>
    private readonly int _maxFontFiles = DefaultMaxFontFiles;

    /// <summary>Backs <see cref="MaxFaces"/>.</summary>
    private readonly int _maxFaces = DefaultMaxFaces;

    /// <summary>
    ///     Gets the largest number of directories one scan lists, across all of its roots (default 10,000).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to less than one.</exception>
    public int MaxDirectories
    {
        get => _maxDirectories;
        init => _maxDirectories = RequireAtLeastOne(value, nameof(MaxDirectories));
    }

    /// <summary>
    ///     Gets the largest number of directory entries (files and subdirectories) one scan examines,
    ///     across all of its roots (default 200,000).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to less than one.</exception>
    public int MaxEntries
    {
        get => _maxEntries;
        init => _maxEntries = RequireAtLeastOne(value, nameof(MaxEntries));
    }

    /// <summary>
    ///     Gets the largest number of font files one scan accepts, across all of its roots (default 5,000).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to less than one.</exception>
    public int MaxFontFiles
    {
        get => _maxFontFiles;
        init => _maxFontFiles = RequireAtLeastOne(value, nameof(MaxFontFiles));
    }

    /// <summary>
    ///     Gets the largest number of faces (a collection file holds several) one scan examines, across
    ///     all of its roots (default 20,000).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to less than one.</exception>
    public int MaxFaces
    {
        get => _maxFaces;
        init => _maxFaces = RequireAtLeastOne(value, nameof(MaxFaces));
    }

    /// <summary>
    ///     Checks that a bound is at least one.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The name of the property.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is less than one.</exception>
    private static int RequireAtLeastOne(int value, string name) =>
        value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "The bound must be at least 1.");
}
