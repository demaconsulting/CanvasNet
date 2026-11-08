namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The exception thrown, when the caller asked for it, as soon as measuring or rendering meets a feature that is
///     recognized but not supported, such as a diagram type that this version does not draw.
/// </summary>
/// <remarks>
///     By default unsupported features are reported (see <see cref="MermaidUnsupportedFeature"/>) and rendering
///     continues with the documented fallback; callers that must never accept an approximate result opt in to this
///     exception instead by setting <see cref="MermaidRenderOptions.ThrowOnUnsupportedFeature"/>. The report that causes
///     the exception is delivered to <see cref="MermaidRenderOptions.UnsupportedFeatureHandler"/> first. It derives from
///     <see cref="IOException"/> so that it can be handled like other "the content cannot be processed" failures of the
///     CanvasNet libraries.
/// </remarks>
public sealed class MermaidUnsupportedFeatureException : IOException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidUnsupportedFeatureException"/> class with an empty
    ///     feature and an unknown location.
    /// </summary>
    public MermaidUnsupportedFeatureException()
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidUnsupportedFeatureException"/> class with a message, an
    ///     empty feature and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MermaidUnsupportedFeatureException(string message)
        : base(message)
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidUnsupportedFeatureException"/> class with a message, an
    ///     inner exception, an empty feature and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public MermaidUnsupportedFeatureException(string message, Exception innerException)
        : base(message, innerException)
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidUnsupportedFeatureException"/> class describing an
    ///     unsupported feature at a location.
    /// </summary>
    /// <param name="feature">The stable <c>mermaid-*</c> feature identifier.</param>
    /// <param name="description">The description of what is not supported, without the location and the identifier.</param>
    /// <param name="location">The location where the feature is used.</param>
    /// <param name="hint">The suggested way forward, or <see langword="null"/>.</param>
    public MermaidUnsupportedFeatureException(string feature, string description, MermaidLocation location, string? hint = null)
        : base(MermaidSyntaxException.FormatMessage(feature, description, location))
    {
        Feature = feature;
        Location = location;
        Hint = hint;
    }

    /// <summary>
    ///     Gets the stable identifier of the unsupported feature; empty when not specified.
    /// </summary>
    public string Feature { get; }

    /// <summary>
    ///     Gets the location where the feature is used; <c>default</c> (line zero) when unknown.
    /// </summary>
    public MermaidLocation Location { get; }

    /// <summary>
    ///     Gets the suggested way forward, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Hint { get; }
}
