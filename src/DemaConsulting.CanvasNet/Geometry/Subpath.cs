using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Represents one contiguous, independently drawn contour of a <see cref="Path"/>: a starting
///     point plus an ordered sequence of drawing commands, and whether the contour is closed.
/// </summary>
/// <remarks>
///     A <see cref="Path"/> may contain any number of subpaths (for example, a letter "O" rendered
///     as glyph outlines needs two: an outer and an inner contour). Command order is preserved
///     exactly as built, and subpaths are never merged or reordered, because a future rasterizer
///     needs both subpath boundaries and command order (which implies winding direction) to
///     evaluate a fill rule (nonzero or even-odd) - this type deliberately performs no winding-
///     rule computation itself, since that evaluation is out of scope for this namespace.
/// </remarks>
public readonly struct Subpath
{
    /// <summary>
    ///     The point this subpath starts at, from the <c>MoveTo</c> call that opened it.
    /// </summary>
    public Vector2 Start { get; }

    /// <summary>
    ///     The ordered sequence of drawing commands following <see cref="Start"/>. Never contains
    ///     a "MoveTo" entry - see <see cref="PathCommandType"/> for why that case is
    ///     unrepresentable by construction.
    /// </summary>
    public IReadOnlyList<PathCommand> Commands { get; }

    /// <summary>
    ///     <see langword="true"/> if this subpath ends with an explicit
    ///     <see cref="PathCommandType.Close"/> command; otherwise, <see langword="false"/> for an
    ///     open subpath.
    /// </summary>
    public bool IsClosed { get; }

    /// <summary>
    ///     Initializes a new subpath snapshot. Used only by <see cref="PathBuilder"/>, which is
    ///     the sole component responsible for guaranteeing <paramref name="commands"/> never
    ///     contains a "MoveTo" entry and that <paramref name="isClosed"/> accurately reflects
    ///     whether the last command is a <see cref="PathCommandType.Close"/>.
    /// </summary>
    /// <param name="start">The point this subpath starts at.</param>
    /// <param name="commands">The ordered sequence of drawing commands following <paramref name="start"/>.</param>
    /// <param name="isClosed">Whether this subpath ends with an explicit close command.</param>
    internal Subpath(Vector2 start, IReadOnlyList<PathCommand> commands, bool isClosed)
    {
        Start = start;
        Commands = commands;
        IsClosed = isClosed;
    }
}
