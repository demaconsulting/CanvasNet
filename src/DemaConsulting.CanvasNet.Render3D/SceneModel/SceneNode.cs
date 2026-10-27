namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     The base class of every node in a scene graph: a local double-precision transform and an
///     optional appearance.
/// </summary>
/// <remarks>
///     The hierarchy is deliberately closed (the constructor is <see langword="private"/>
///     <see langword="protected"/>): the renderer enumerates the concrete node types it knows
///     (<see cref="MeshNode"/>, <see cref="GroupNode"/>, <see cref="CsgNode"/>, <see cref="OverlayNode"/>), and further node
///     kinds are added inside this assembly. Nodes are immutable after construction and may be shared
///     between several parents. The world transform of a node is
///     <c>node.Transform * parent.Transform * ...</c> (child first, row-vector convention).
/// </remarks>
public abstract class SceneNode
{
    /// <summary>Backing store for <see cref="Transform"/>, defaulting to the identity (not <c>default</c>, which is the zero matrix).</summary>
    private readonly Matrix4x4D _transform = Matrix4x4D.Identity;

    /// <summary>Initializes a new instance; restricted to this assembly to keep the hierarchy closed.</summary>
    private protected SceneNode()
    {
    }

    /// <summary>
    ///     Gets the transform from this node's local space to its parent's space. Defaults to the
    ///     identity.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown (from the <see langword="init"/> accessor) when the matrix contains a non-finite element.</exception>
    public Matrix4x4D Transform
    {
        get => _transform;
        init
        {
            // A NaN or infinite element would silently corrupt every descendant vertex
            if (!value.IsFinite)
            {
                throw new ArgumentException("The node transform must be finite.", nameof(value));
            }

            _transform = value;
        }
    }

    /// <summary>
    ///     Gets the optional appearance applied to this node and, unless overridden, to its
    ///     descendants. <see langword="null"/> (default) inherits the nearest ancestor's appearance.
    /// </summary>
    public NodeAppearance? Appearance { get; init; }
}
