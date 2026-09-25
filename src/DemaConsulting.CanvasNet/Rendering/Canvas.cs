using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;
using Rgba32 = DemaConsulting.CanvasNet.Canvas.Rgba32;
using Surface = DemaConsulting.CanvasNet.Canvas.Surface;

namespace DemaConsulting.CanvasNet.Rendering;

/// <summary>
///     Wraps a <see cref="Surface"/> with a per-instance affine transform stack, and offers
///     transform-aware <see cref="FillPath(Path, Rgba32, FillRule)"/> /
///     <see cref="FillPath(Path, Gradient, FillRule)"/> /
///     <see cref="StrokePath(Path, StrokeStyle, Rgba32)"/> entry points that delegate to the
///     underlying <see cref="Drawing.PathFiller"/> and <see cref="Drawing.PathStroker"/> after
///     baking the current transform into a fresh <see cref="Path"/> instance via
///     <see cref="Path.Transform(Matrix3x2)"/>.
/// </summary>
/// <remarks>
///     <para>
///     Composition order is row-vector prepend (<c>_current = newOp * _current</c>): a subsequent
///     transform is applied to points <em>before</em> earlier transforms, matching HTML5 canvas,
///     Skia, and Cairo. So <c>Translate(a); RotateDegrees(d);</c> applied to a point <c>p</c>
///     evaluates as "rotate first, then translate" — the rotation happens in the (still
///     untranslated) local frame, then the result is translated.
///     </para>
///     <para>
///     When the current transform equals <see cref="Matrix3x2.Identity"/>, this class produces
///     byte-identical output to calling <see cref="Drawing.PathFiller.Fill(Surface, Path, Rgba32, FillRule, float)"/>
///     or <see cref="Drawing.PathStroker.Stroke(Path, StrokeStyle, float)"/>+<see cref="Drawing.PathFiller.Fill(Surface, Path, Rgba32, FillRule, float)"/>
///     directly. The identity check short-circuits <see cref="Path.Transform(Matrix3x2)"/> so
///     no work is done when there is no transformation to apply.
///     </para>
/// </remarks>
public sealed class Canvas
{
    private readonly Stack<Matrix3x2> _stack = new();
    private Matrix3x2 _current = Matrix3x2.Identity;

    /// <summary>The underlying pixel surface every fill/stroke on this Canvas writes to.</summary>
    /// <remarks>
    ///     <c>Canvas</c> holds a non-owning reference to this <see cref="Surface"/>: it never
    ///     calls <see cref="Surface.Dispose"/> and does not itself implement
    ///     <see cref="IDisposable"/>. The caller that constructed the <see cref="Surface"/> passed
    ///     to the constructor remains solely responsible for disposing it once both this
    ///     <c>Canvas</c> and the <see cref="Surface"/> are no longer needed.
    /// </remarks>
    public Surface Surface { get; }

    /// <summary>The current composed affine transform.</summary>
    public Matrix3x2 CurrentTransform => _current;

    /// <summary>
    ///     Initializes a new <see cref="Canvas"/> wrapping <paramref name="surface"/>. Starts
    ///     with <see cref="Matrix3x2.Identity"/> as the current transform and an empty stack.
    /// </summary>
    /// <param name="surface">
    ///     The pixel surface to render into. Must not be <see langword="null"/>. Ownership of
    ///     <paramref name="surface"/> is not transferred to this <c>Canvas</c>: this constructor
    ///     stores only a reference to it, and the caller remains responsible for calling
    ///     <see cref="Surface.Dispose"/> on it once both this <c>Canvas</c> and the
    ///     <see cref="Surface"/> are no longer needed.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is <see langword="null"/>.</exception>
    public Canvas(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Surface = surface;
    }

    /// <summary>Pushes the current transform onto an internal LIFO stack.</summary>
    public void Save() => _stack.Push(_current);

    /// <summary>Pops the most-recently-saved transform, restoring it as the current transform.</summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the save-stack is empty (no matching <see cref="Save"/> to restore from).
    /// </exception>
    public void Restore()
    {
        if (_stack.Count == 0)
        {
            throw new InvalidOperationException("Canvas.Restore called on an empty transform stack.");
        }

        _current = _stack.Pop();
    }

    /// <summary>Composes a translation onto the current transform (prepend order).</summary>
    /// <param name="x">The translation's x component.</param>
    /// <param name="y">The translation's y component.</param>
    public void Translate(float x, float y)
    {
        _current = Matrix3x2.CreateTranslation(x, y) * _current;
    }

    /// <summary>Composes a rotation (in degrees) onto the current transform (prepend order).</summary>
    /// <param name="angleDegrees">The rotation angle, in degrees.</param>
    public void RotateDegrees(float angleDegrees)
    {
        var radians = angleDegrees * (MathF.PI / 180f);
        _current = Matrix3x2.CreateRotation(radians) * _current;
    }

    /// <summary>
    ///     Fills every pixel of the underlying <see cref="Surface"/> with the constant
    ///     <paramref name="color"/>, overwriting any existing pixel data.
    /// </summary>
    /// <param name="color">The color to fill the entire surface with.</param>
    /// <remarks>
    ///     A direct passthrough to <see cref="DemaConsulting.CanvasNet.Canvas.Surface.Clear(Rgba32)"/>. Unlike
    ///     <see cref="FillPath(Path, Rgba32, FillRule)"/> and <see cref="StrokePath"/>, no
    ///     geometry is involved, so <see cref="CurrentTransform"/> has no effect on the result -
    ///     the whole surface is filled unconditionally, regardless of the current transform.
    /// </remarks>
    public void Clear(Rgba32 color) => Surface.Clear(color);

    /// <summary>
    ///     Fills <paramref name="path"/> with a solid color, honoring the current transform.
    /// </summary>
    /// <param name="path">The path to fill. Must not be <see langword="null"/>.</param>
    /// <param name="color">The solid color to paint.</param>
    /// <param name="fillRule">Fill rule to resolve overlaps. Defaults to <see cref="FillRule.NonZero"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="fillRule"/> is not a defined <see cref="FillRule"/> value.</exception>
    public void FillPath(Path path, Rgba32 color, FillRule fillRule = FillRule.NonZero)
    {
        ArgumentNullException.ThrowIfNull(path);
        PathFiller.Fill(Surface, TransformIfNeeded(path), color, fillRule);
    }

    /// <summary>
    ///     Fills <paramref name="path"/> with a gradient paint, honoring the current transform.
    /// </summary>
    /// <param name="path">The path to fill. Must not be <see langword="null"/>.</param>
    /// <param name="paint">The gradient to paint. Must not be <see langword="null"/>.</param>
    /// <param name="fillRule">Fill rule to resolve overlaps. Defaults to <see cref="FillRule.NonZero"/>.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="path"/> or <paramref name="paint"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="fillRule"/> is not a defined <see cref="FillRule"/> value.</exception>
    public void FillPath(Path path, Gradient paint, FillRule fillRule = FillRule.NonZero)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(paint);
        var transformedPaint = _current.IsIdentity ? paint : paint.WithTransform(_current);
        PathFiller.Fill(Surface, TransformIfNeeded(path), transformedPaint, fillRule);
    }

    /// <summary>
    ///     Strokes <paramref name="path"/> with <paramref name="style"/> and fills the resulting
    ///     outline with <paramref name="color"/>, honoring the current transform.
    /// </summary>
    /// <param name="path">The path to stroke. Must not be <see langword="null"/>.</param>
    /// <param name="style">The stroke style. Must not be <see langword="null"/>.</param>
    /// <param name="color">The color to fill the stroke outline with.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="path"/> or <paramref name="style"/> is <see langword="null"/>.
    /// </exception>
    public void StrokePath(Path path, StrokeStyle style, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(style);
        var outline = PathStroker.Stroke(TransformIfNeeded(path), style);
        PathFiller.Fill(Surface, outline, color);
    }

    /// <summary>
    ///     Applies <see cref="CurrentTransform"/> to <paramref name="path"/> only when the
    ///     current transform is not the identity — the identity fast-path preserves the
    ///     byte-identical no-transform regression against direct
    ///     <see cref="Drawing.PathFiller"/> / <see cref="Drawing.PathStroker"/> calls.
    /// </summary>
    private Path TransformIfNeeded(Path path)
    {
        return _current.IsIdentity ? path : path.Transform(_current);
    }
}
