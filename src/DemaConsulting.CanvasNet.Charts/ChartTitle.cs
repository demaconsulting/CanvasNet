namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Represents an immutable title description for a <see cref="Chart"/>: the title text and
///     an optional font-size hint.
/// </summary>
/// <remarks>
///     <see cref="ChartTitle"/> is immutable and thread-safe after construction.
/// </remarks>
public sealed class ChartTitle
{
    /// <summary>
    ///     Initializes a new, validated <see cref="ChartTitle"/>.
    /// </summary>
    /// <param name="text">
    ///     The title text. Must not be <see langword="null"/>, empty, or consist only of
    ///     whitespace.
    /// </param>
    /// <param name="fontSize">
    ///     An optional font-size hint, in the same units a renderer otherwise uses for text size,
    ///     or <see langword="null"/> to let a renderer choose a default. Must be finite and
    ///     strictly greater than zero when supplied.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="text"/> is empty or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="fontSize"/> is not finite or is less than or equal to zero.
    /// </exception>
    public ChartTitle(string text, float? fontSize = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text must not be empty or consist only of whitespace.", nameof(text));
        }

        if (fontSize.HasValue && (!float.IsFinite(fontSize.Value) || fontSize.Value <= 0f))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fontSize), fontSize, "Font size must be a finite value greater than zero.");
        }

        Text = text;
        FontSize = fontSize;
    }

    /// <summary>
    ///     Gets the title text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the font-size hint, or <see langword="null"/> when unspecified.
    /// </summary>
    public float? FontSize { get; }
}
