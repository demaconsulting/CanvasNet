namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     The exception thrown, when the caller asked for it, as soon as rendering meets a feature that is recognized
///     but not supported.
/// </summary>
/// <remarks>
///     By default unsupported features are reported (see <see cref="VegaLiteUnsupportedFeature"/>) and rendering
///     continues; callers that must never accept an approximate result opt in to this exception instead by setting
///     <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/>. It derives from <see cref="IOException"/> so
///     that it can be handled like other "the content cannot be processed" failures of the CanvasNet libraries.
/// </remarks>
public sealed class VegaLiteUnsupportedFeatureException : IOException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteUnsupportedFeatureException"/> class with an empty
    ///     feature and an unknown location.
    /// </summary>
    public VegaLiteUnsupportedFeatureException()
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteUnsupportedFeatureException"/> class with a message,
    ///     an empty feature and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public VegaLiteUnsupportedFeatureException(string message)
        : base(message)
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteUnsupportedFeatureException"/> class with a message,
    ///     an inner exception, an empty feature and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public VegaLiteUnsupportedFeatureException(string message, Exception innerException)
        : base(message, innerException)
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteUnsupportedFeatureException"/> class describing an
    ///     unsupported feature at a location.
    /// </summary>
    /// <param name="feature">The stable <c>vl-*</c> feature identifier.</param>
    /// <param name="description">The description of what is not supported, without the location prefix.</param>
    /// <param name="location">The location where the feature is used.</param>
    public VegaLiteUnsupportedFeatureException(string feature, string description, VegaLiteLocation location)
        : base(location.IsKnown ? $"{location}: {description}" : description)
    {
        Feature = feature;
        Location = location;
    }

    /// <summary>
    ///     Gets the stable identifier of the unsupported feature; empty when not specified.
    /// </summary>
    public string Feature { get; }

    /// <summary>
    ///     Gets the location where the feature is used; <c>default</c> (line zero, empty pointer) when unknown.
    /// </summary>
    public VegaLiteLocation Location { get; }
}
