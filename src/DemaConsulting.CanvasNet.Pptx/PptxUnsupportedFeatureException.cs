namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     The exception thrown when a <see cref="PptxDocument"/> geometry/paint resolver refuses to
///     resolve an otherwise well-formed DrawingML construct because it declares a feature this
///     phase does not implement (for example a preset geometry name not in the supported ~24
///     subset, a radial/path gradient fill, or a pattern/picture fill).
/// </summary>
/// <remarks>
///     This mirrors <see cref="Codecs.UnsupportedImageFeatureException"/>'s own rationale and
///     shape exactly, applied to this package instead of the core codec package: it is a distinct
///     type from <see cref="InvalidDataException"/> specifically so a caller can distinguish
///     "well-formed but unsupported" from "malformed" without string-matching
///     <see cref="Exception.Message"/>. <see cref="InvalidDataException"/> itself is
///     <see langword="sealed"/> in .NET and therefore cannot be a base type here; this type
///     instead derives from <see cref="IOException"/> - a sibling of
///     <see cref="InvalidDataException"/>, not its base type (<see cref="InvalidDataException"/>
///     derives directly from <see cref="SystemException"/>, not <see cref="IOException"/>). A
///     caller that wants to handle both the malformed and the well-formed-but-unsupported cases
///     together cannot do so with a single <c>catch (IOException)</c> clause and must add two
///     explicit clauses. <see cref="Feature"/> is a short, stable, machine-matchable token
///     identifying which unsupported feature was encountered (for example
///     <c>"pptx-preset-geometry"</c>, <c>"pptx-gradient-path"</c>, <c>"pptx-pattern-fill"</c>,
///     <c>"pptx-picture-fill"</c>), distinct from the free-text, human-readable
///     <see cref="Exception.Message"/>.
/// </remarks>
public sealed class PptxUnsupportedFeatureException : IOException
{
    /// <summary>
    ///     A short, stable, machine-matchable token identifying which unsupported feature caused
    ///     this exception to be thrown (for example <c>"pptx-preset-geometry"</c>), or
    ///     <see cref="string.Empty"/> when this exception was constructed via one of the standard
    ///     parameterless/message-only/inner-exception constructors rather than the two
    ///     feature-carrying constructors below.
    /// </summary>
    public string Feature { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxUnsupportedFeatureException"/> class
    ///     with a default message and an empty <see cref="Feature"/>.
    /// </summary>
    public PptxUnsupportedFeatureException()
        : this(string.Empty, "An unsupported PPTX feature was encountered.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxUnsupportedFeatureException"/> class
    ///     with the specified message and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PptxUnsupportedFeatureException(string message)
        : this(string.Empty, message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxUnsupportedFeatureException"/> class
    ///     with the specified message, inner exception, and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public PptxUnsupportedFeatureException(string message, Exception innerException)
        : this(string.Empty, message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxUnsupportedFeatureException"/> class
    ///     with the specified feature token and message.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"pptx-preset-geometry"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    public PptxUnsupportedFeatureException(string feature, string message)
        : base(message)
        => Feature = feature;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxUnsupportedFeatureException"/> class
    ///     with the specified feature token, message, and inner exception.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"pptx-preset-geometry"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public PptxUnsupportedFeatureException(string feature, string message, Exception innerException)
        : base(message, innerException)
        => Feature = feature;
}
