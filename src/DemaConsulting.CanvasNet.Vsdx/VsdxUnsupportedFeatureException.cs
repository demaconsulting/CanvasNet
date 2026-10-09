namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     The exception thrown when a <see cref="VsdxDocument"/> shape resolver refuses to resolve
///     an otherwise well-formed VisioML construct because it declares a feature not implemented
///     (a recognized-but-deferred construct, distinct from a tolerant, silent skip, which is
///     instead used for an unrecognized geometry row type or an unresolved "Themed" color - see
///     <c>VsdxColorPalette.ThemedFallback</c>'s own remarks for the
///     evidence-based justification of that choice). Not yet thrown by any resolver in this
///     package - reserved for a future recognized-but-deferred VisioML construct requiring a hard
///     failure, exactly mirroring <c>PptxUnsupportedFeatureException</c>'s own established
///     precedent and contract.
/// </summary>
/// <remarks>
///     Mirrors the sibling <c>DemaConsulting.CanvasNet.Pptx.PptxUnsupportedFeatureException</c>'s
///     own rationale and shape exactly, applied to this package instead of the sibling PPTX
///     package (not referenced via <c>cref</c>: this package has no project reference to
///     <c>DemaConsulting.CanvasNet.Pptx</c>):
///     a distinct type from <see cref="InvalidDataException"/> specifically so a caller can
///     distinguish "well-formed but unsupported" from "malformed" without string-matching
///     <see cref="Exception.Message"/>. <see cref="InvalidDataException"/> itself is
///     <see langword="sealed"/> in .NET and therefore cannot be a base type here; this type
///     instead derives from <see cref="IOException"/> - a sibling of
///     <see cref="InvalidDataException"/>, not its base type. <see cref="Feature"/> is a short,
///     stable, machine-matchable token identifying which unsupported feature was encountered
///     (for example <c>"vsdx-group-recursion"</c>), distinct from the free-text, human-readable
///     <see cref="Exception.Message"/>. Declared <see langword="public"/> - like its
///     <c>PptxUnsupportedFeatureException</c> sibling - rather than <see langword="internal"/>
///     (PR #42 review Finding #4): this type's own documented purpose is for an external caller
///     of <see cref="VsdxDocument"/>'s public API to distinguish "well-formed but unsupported"
///     from "malformed" input, which an <see langword="internal"/> accessibility would make
///     impossible regardless of whether any resolver throws it yet - and declaring it public now
///     (rather than only once a future resolver first throws it) avoids a later breaking
///     accessibility change to this already-named, already-shaped type.
/// </remarks>
public sealed class VsdxUnsupportedFeatureException : IOException
{
    /// <summary>
    ///     A short, stable, machine-matchable token identifying which unsupported feature caused
    ///     this exception to be thrown (for example <c>"vsdx-group-recursion"</c>), or
    ///     <see cref="string.Empty"/> when this exception was constructed via one of the standard
    ///     parameterless/message-only/inner-exception constructors rather than the two
    ///     feature-carrying constructors below.
    /// </summary>
    public string Feature { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VsdxUnsupportedFeatureException"/> class
    ///     with a default message and an empty <see cref="Feature"/>.
    /// </summary>
    public VsdxUnsupportedFeatureException()
        : this(string.Empty, "An unsupported VSDX feature was encountered.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VsdxUnsupportedFeatureException"/> class
    ///     with the specified message and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public VsdxUnsupportedFeatureException(string message)
        : this(string.Empty, message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VsdxUnsupportedFeatureException"/> class
    ///     with the specified message, inner exception, and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public VsdxUnsupportedFeatureException(string message, Exception innerException)
        : this(string.Empty, message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VsdxUnsupportedFeatureException"/> class
    ///     with the specified feature token and message.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"vsdx-group-recursion"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    public VsdxUnsupportedFeatureException(string feature, string message)
        : base(message)
        => Feature = feature;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VsdxUnsupportedFeatureException"/> class
    ///     with the specified feature token, message, and inner exception.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"vsdx-group-recursion"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public VsdxUnsupportedFeatureException(string feature, string message, Exception innerException)
        : base(message, innerException)
        => Feature = feature;
}
