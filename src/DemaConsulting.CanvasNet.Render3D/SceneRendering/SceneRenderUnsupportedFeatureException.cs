using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The exception thrown by <see cref="SceneRenderer"/> when a scene contains a feature or input
///     it cannot render faithfully and the caller opted into strict behavior via
///     <see cref="SceneRenderOptions.ThrowOnUnsupportedFeature"/>; by default the scene is rendered
///     tolerantly and the problem is reported through
///     <see cref="SceneRenderOptions.UnsupportedFeatureHandler"/> instead.
/// </summary>
/// <remarks>
///     Mirrors the unsupported-feature exceptions of the sibling CanvasNet packages: a distinct
///     type so a caller can tell "well-formed but unsupported" from a programming error without
///     string-matching <see cref="Exception.Message"/>, with a short, stable, machine-matchable
///     <see cref="Feature"/> token. It derives from <see cref="InvalidOperationException"/>
///     because the problem lies in the scene handed to the renderer, not in an I/O operation.
/// </remarks>
public sealed class SceneRenderUnsupportedFeatureException : InvalidOperationException
{
    /// <summary>Initializes a new instance with a default message and an empty <see cref="Feature"/>.</summary>
    public SceneRenderUnsupportedFeatureException()
        : this(string.Empty, "An unsupported scene feature was encountered.")
    {
    }

    /// <summary>Initializes a new instance with the specified message and an empty <see cref="Feature"/>.</summary>
    /// <param name="message">The message that describes the error.</param>
    public SceneRenderUnsupportedFeatureException(string message)
        : this(string.Empty, message)
    {
    }

    /// <summary>Initializes a new instance with the specified message, inner exception and an empty <see cref="Feature"/>.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public SceneRenderUnsupportedFeatureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance with the specified feature token and message.</summary>
    /// <param name="feature">A short, stable, machine-matchable token identifying the unsupported feature.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="node">The mesh node the problem concerns, or <see langword="null"/>.</param>
    public SceneRenderUnsupportedFeatureException(string feature, string message, MeshNode? node = null)
        : base(message)
    {
        Feature = feature;
        Node = node;
    }

    /// <summary>
    ///     Gets the short, stable, machine-matchable token identifying which feature caused this
    ///     exception (for example <c>"render3d-csg-open-mesh"</c>), or <see cref="string.Empty"/>
    ///     when it was constructed without one.
    /// </summary>
    public string Feature { get; } = string.Empty;

    /// <summary>Gets the mesh node the problem concerns, or <see langword="null"/> when it concerns the whole render.</summary>
    public MeshNode? Node { get; }
}
