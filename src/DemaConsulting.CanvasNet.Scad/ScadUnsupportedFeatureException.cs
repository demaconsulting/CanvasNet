namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The exception thrown, when the caller asked for it, as soon as evaluation meets a feature that is
///     recognized but not supported.
/// </summary>
/// <remarks>
///     By default unsupported features are reported (see <see cref="ScadUnsupportedFeature"/>) and evaluation
///     continues; callers that must never accept an approximate result opt in to this exception instead. It derives
///     from <see cref="IOException"/> so that it can be handled like other "the content cannot be processed"
///     failures of the CanvasNet libraries.
/// </remarks>
public sealed class ScadUnsupportedFeatureException : IOException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadUnsupportedFeatureException"/> class with an empty
    ///     feature and an unknown location.
    /// </summary>
    public ScadUnsupportedFeatureException()
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadUnsupportedFeatureException"/> class with a message, an
    ///     empty feature and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ScadUnsupportedFeatureException(string message)
        : base(message)
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadUnsupportedFeatureException"/> class with a message, an
    ///     inner exception, an empty feature and an unknown location.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ScadUnsupportedFeatureException(string message, Exception innerException)
        : base(message, innerException)
    {
        Feature = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadUnsupportedFeatureException"/> class describing an
    ///     unsupported feature at a source location.
    /// </summary>
    /// <param name="feature">The stable feature identifier.</param>
    /// <param name="description">The description of what is not supported, without the location prefix.</param>
    /// <param name="location">The location where the feature is used.</param>
    public ScadUnsupportedFeatureException(string feature, string description, ScadSourceLocation location)
        : base(location.Line == 0 ? description : $"{location}: {description}")
    {
        Feature = feature;
        Location = location;
    }

    /// <summary>
    ///     Gets the stable identifier of the unsupported feature; empty when not specified.
    /// </summary>
    public string Feature { get; }

    /// <summary>
    ///     Gets the location where the feature is used; <c>default</c> (line zero) when unknown.
    /// </summary>
    public ScadSourceLocation Location { get; }
}
