namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The exception thrown by <see cref="SceneRenderer"/> when a scene that needs per-pixel
///     fragment resolution (constructive solid geometry with a difference or an intersection)
///     would need more than <see cref="SceneRenderOptions.MaxFragmentsPerTile"/> fragments for a
///     single render tile.
/// </summary>
/// <remarks>
///     The exception carries only data that does not depend on which tile or thread detected the
///     overflow, so the failure is identical for sequential and parallel rendering. The usual
///     remedies are to raise <see cref="SceneRenderOptions.MaxFragmentsPerTile"/>, simplify the
///     scene, or reduce the image size or supersampling. Scenes that only combine geometry by
///     union never need fragment resolution and are never subject to the budget.
/// </remarks>
public sealed class RenderBudgetExceededException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance with a default message and the default
    ///     <see cref="MaxFragmentsPerTile"/> (4 Mi fragments).
    /// </summary>
    public RenderBudgetExceededException()
        : this(SceneRenderOptions.DefaultMaxFragmentsPerTile)
    {
    }

    /// <summary>Initializes a new instance with the specified message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public RenderBudgetExceededException(string message)
        : base(message)
    {
        MaxFragmentsPerTile = SceneRenderOptions.DefaultMaxFragmentsPerTile;
    }

    /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public RenderBudgetExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
        MaxFragmentsPerTile = SceneRenderOptions.DefaultMaxFragmentsPerTile;
    }

    /// <summary>Initializes a new instance for the exceeded budget, with the standard message.</summary>
    /// <param name="maxFragmentsPerTile">The per-tile fragment budget that was exceeded.</param>
    public RenderBudgetExceededException(long maxFragmentsPerTile)
        : this(maxFragmentsPerTile, BuildMessage(maxFragmentsPerTile))
    {
    }

    /// <summary>Initializes a new instance for the exceeded budget with the specified message.</summary>
    /// <param name="maxFragmentsPerTile">The per-tile fragment budget that was exceeded.</param>
    /// <param name="message">The message that describes the error.</param>
    public RenderBudgetExceededException(long maxFragmentsPerTile, string message)
        : base(message)
    {
        MaxFragmentsPerTile = maxFragmentsPerTile;
    }

    /// <summary>Gets the per-tile fragment budget (<see cref="SceneRenderOptions.MaxFragmentsPerTile"/>) that was exceeded.</summary>
    public long MaxFragmentsPerTile { get; }

    /// <summary>Builds the standard, tile-independent message.</summary>
    /// <param name="maxFragmentsPerTile">The exceeded budget.</param>
    /// <returns>The message text.</returns>
    private static string BuildMessage(long maxFragmentsPerTile) =>
        $"A render tile needs more than {maxFragmentsPerTile} fragments. "
        + "Raise MaxFragmentsPerTile, simplify the scene, or reduce the image size or supersampling.";
}
